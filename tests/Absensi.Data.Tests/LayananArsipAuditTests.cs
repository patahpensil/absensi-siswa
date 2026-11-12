using Absensi.Data.Audit;
using Absensi.Data.Backup;
using Absensi.Data.Repositori;
using Absensi.Domain.Akun;
using Microsoft.Data.Sqlite;

namespace Absensi.Data.Tests;

public sealed class LayananArsipAuditTests : IDisposable
{
    private readonly string _folder = Path.Combine(Path.GetTempPath(), $"absensi-arsip-{Guid.NewGuid():N}");
    private static readonly DateTime Time = new(2026, 9, 24, 8, 0, 0);
    private static readonly byte[] Key = new byte[32];

    [Fact]
    public void Write_ArsipTerenkripsiMembawaNamaPelakuTanpaHashSandi()
    {
        using var database = new MigratedDatabase();
        database.SeedMasterDataUntukUji();
        using var transaction = database.Connection.BeginTransaction();
        new RepositoriAudit().Catat(database.Connection, transaction, Time, 1, "uji", "akun", null, null, null, null);
        var path = new LayananArsipAudit(_folder).Write(database.Connection, transaction, Time, Key);
        transaction.Commit();

        var file = File.ReadAllBytes(path);
        Assert.True(file.AsSpan().StartsWith("ABSENSI-AUDIT-1\n"u8));
        var json = System.Text.Encoding.UTF8.GetString(EnkripsiBackup.Dekripsi(file["ABSENSI-AUDIT-1\n"u8.Length..], Convert.ToBase64String(Key)));
        Assert.Contains("NamaPelaku", json);
        Assert.DoesNotContain("sandi", json, StringComparison.OrdinalIgnoreCase);
        var row = Assert.Single(LayananArsipAudit.ReadProtected(path, () => Key)!);
        Assert.Equal("uji", row.Aksi);
        Assert.Throws<InvalidOperationException>(() => LayananArsipAudit.ReadProtected(path, null));
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

        var admin = new AkunTersimpan(1, "admin", "x", PeranAkun.Admin, null, true);
        var result = new LayananAudit().Baca(active.Connection, admin, _folder, Time.AddDays(-1), Time.AddDays(1));
        Assert.Empty(result.ArsipBermasalah);
        Assert.Equal("admin_lama", Assert.Single(result.Baris).NamaPelaku);
    }

    public void Dispose()
    {
        if (Directory.Exists(_folder)) Directory.Delete(_folder, recursive: true);
    }
}
