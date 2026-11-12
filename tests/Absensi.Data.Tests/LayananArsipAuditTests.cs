using Absensi.Data.Audit;
using Absensi.Data.Backup;
using Absensi.Data.Repositori;
using Absensi.Domain.Akun;
using Microsoft.Data.Sqlite;

namespace Absensi.Data.Tests;

// Arsip riwayat audit pra-restore. Keputusan pemilik 12 November 2026
// (PRD §25.5): tanpa DPAPI; terenkripsi dengan kunci lisensi bila lisensi
// aktif, polos bila belum, sama seperti backup lokal.
public sealed class LayananArsipAuditTests : IDisposable
{
    private const string License = "SEKOLAH-001-ABCD";
    private readonly string _folder = Path.Combine(Path.GetTempPath(), $"absensi-arsip-{Guid.NewGuid():N}");
    private static readonly DateTime Time = new(2026, 9, 24, 8, 0, 0);
    private static readonly AkunTersimpan Admin = new(1, "admin", "x", PeranAkun.Admin, null, true);

    [Fact]
    public void Write_LisensiAktif_TerenkripsiTanpaHashSandi()
    {
        var path = WriteArchive(License);

        var file = File.ReadAllBytes(path);
        Assert.True(file.AsSpan().StartsWith("ABSENSI-AUDIT-2\n"u8));
        var json = System.Text.Encoding.UTF8.GetString(EnkripsiBackup.Dekripsi(file[16..], License));
        Assert.Contains("NamaPelaku", json);
        Assert.DoesNotContain("sandi", json, StringComparison.OrdinalIgnoreCase);
        Assert.Equal("uji", Assert.Single(LayananArsipAudit.Read(path, License)!).Aksi);
    }

    [Fact]
    public void Write_TanpaLisensi_ArsipPolosTetapTerbaca()
    {
        var path = WriteArchive(licenseKey: null);

        Assert.True(File.ReadAllBytes(path).AsSpan().StartsWith("ABSENSI-AUDIT-P\n"u8));
        Assert.Equal("uji", Assert.Single(LayananArsipAudit.Read(path, licenseKey: null)!).Aksi);
    }

    [Fact]
    public void Read_KunciLisensiLain_PesanJelasBukanCryptographicException()
    {
        var path = WriteArchive(License);

        var error = Assert.Throws<InvalidOperationException>(() => LayananArsipAudit.Read(path, "SEKOLAH-LAIN-9999"));
        Assert.Contains("kunci lisensi lain", error.Message);
    }

    [Fact]
    public void Read_ArsipTerenkripsiTanpaLisensiAktif_PesanJelas()
    {
        var path = WriteArchive(License);

        var error = Assert.Throws<InvalidOperationException>(() => LayananArsipAudit.Read(path, licenseKey: null));
        Assert.Contains("lisensi pada aplikasi ini belum aktif", error.Message);
    }

    [Fact]
    public void Baca_ArsipDpapiLama_TercatatTidakBisaDibaca()
    {
        using var active = new MigratedDatabase();
        active.SeedMasterDataUntukUji();
        Directory.CreateDirectory(_folder);
        File.WriteAllBytes(Path.Combine(_folder, $"arsip-audit-{Time:yyyyMMdd-HHmmss}.db"),
            [.. "ABSENSI-AUDIT-1\n"u8.ToArray(), .. new byte[64]]);

        var result = new LayananAudit().Baca(active.Connection, Admin, _folder, Time.AddDays(-1), Time.AddDays(1));

        Assert.Contains("DPAPI", Assert.Single(result.ArsipBermasalah).Alasan);
    }

    [Fact]
    public void Baca_ArsipTerenkripsi_DibukaDenganLisensiDatabaseAktif()
    {
        WriteArchive(License);
        using var active = new MigratedDatabase();
        active.SeedMasterDataUntukUji();
        TulisLisensi(active, License);

        var result = new LayananAudit().Baca(active.Connection, Admin, _folder, Time.AddDays(-1), Time.AddDays(1));

        Assert.Empty(result.ArsipBermasalah);
        Assert.Contains(result.Baris, row => row.Aksi == "uji");
    }

    [Fact]
    public void Write_JumlahBarisSamaDanSumberTidakBerubah()
    {
        using var database = new MigratedDatabase();
        database.SeedMasterDataUntukUji();
        using var transaction = database.Connection.BeginTransaction();
        var repository = new RepositoriAudit();
        repository.Catat(database.Connection, transaction, Time, 1, "uji-1", "akun", null, null, null, null);
        repository.Catat(database.Connection, transaction, Time, 1, "uji-2", "akun", null, null, null, null);
        var before = CountAudit(database.Connection, transaction);

        var path = new LayananArsipAudit(_folder).Write(database.Connection, transaction, Time, License);

        Assert.Equal(before, CountAudit(database.Connection, transaction));
        Assert.Equal(before, LayananArsipAudit.Read(path, License)!.Count);
        transaction.Commit();
    }

    [Fact]
    public void Write_FolderTidakBisaDibuat_PesanJelasTanpaBerkasSisa()
    {
        File.WriteAllText(_folder, "bukan folder");
        try
        {
            using var database = new MigratedDatabase();
            database.SeedMasterDataUntukUji();
            using var transaction = database.Connection.BeginTransaction();

            var error = Assert.Throws<InvalidOperationException>(() =>
                new LayananArsipAudit(_folder).Write(database.Connection, transaction, Time, License));

            Assert.Contains("Pemulihan dibatalkan", error.Message);
        }
        finally
        {
            File.Delete(_folder);
        }
    }

    [Fact]
    public void Baca_ArsipSqliteLamaMasihMenampilkanNamaPelaku()
    {
        using var active = new MigratedDatabase();
        active.SeedMasterDataUntukUji();
        Directory.CreateDirectory(_folder);
        var path = Path.Combine(_folder, $"arsip-audit-{Time:yyyyMMdd-HHmmss}.db");
        using (var legacy = new SqliteConnection(new SqliteConnectionStringBuilder { DataSource = path, Pooling = false }.ToString()))
        {
            legacy.Open();
            using var command = legacy.CreateCommand();
            command.CommandText = """
                CREATE TABLE akun(id INTEGER, username TEXT, sandi_hash TEXT);
                INSERT INTO akun VALUES (1, 'admin_lama', 'hash-lama');
                CREATE TABLE audit(id INTEGER, waktu TEXT, aksi TEXT, entitas TEXT, entitas_id INTEGER, nilai_lama TEXT,
                    nilai_baru TEXT, atas_permintaan TEXT, audit_uid TEXT, akun_id INTEGER);
                INSERT INTO audit VALUES (1, '2026-09-24T08:00:00', 'uji', 'akun', NULL, NULL, NULL, NULL, 'legacy-uid', 1);
                """;
            command.ExecuteNonQuery();
        }

        var result = new LayananAudit().Baca(active.Connection, Admin, _folder, Time.AddDays(-1), Time.AddDays(1));
        Assert.Empty(result.ArsipBermasalah);
        Assert.Equal("admin_lama", Assert.Single(result.Baris).NamaPelaku);
    }

    private string WriteArchive(string? licenseKey)
    {
        using var database = new MigratedDatabase();
        database.SeedMasterDataUntukUji();
        using var transaction = database.Connection.BeginTransaction();
        new RepositoriAudit().Catat(database.Connection, transaction, Time, 1, "uji", "akun", null, null, null, null);
        var path = new LayananArsipAudit(_folder).Write(database.Connection, transaction, Time, licenseKey);
        transaction.Commit();
        return path;
    }

    private static void TulisLisensi(MigratedDatabase database, string key)
    {
        using var transaction = database.Connection.BeginTransaction();
        new Absensi.Data.Lisensi.RepositoriLisensi().Tulis(database.Connection, transaction, key, Time);
        transaction.Commit();
    }

    private static long CountAudit(SqliteConnection connection, SqliteTransaction transaction)
    {
        using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = "SELECT COUNT(*) FROM audit;";
        return (long)command.ExecuteScalar()!;
    }

    public void Dispose()
    {
        if (Directory.Exists(_folder)) Directory.Delete(_folder, recursive: true);
        if (File.Exists(_folder)) File.Delete(_folder);
    }
}
