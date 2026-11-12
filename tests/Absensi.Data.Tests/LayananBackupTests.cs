using Absensi.Data.Backup;
using Absensi.Data.Repositori;
using Absensi.Domain.Akun;
using Microsoft.Data.Sqlite;

namespace Absensi.Data.Tests;

public sealed class LayananBackupTests : IDisposable
{
    private static readonly AkunTersimpan Admin = new(1, "admin1", "x", PeranAkun.Admin, null, true);
    private static readonly AkunTersimpan WaliKelas = new(3, "wali_a", "x", PeranAkun.WaliKelas, 1, true);
    private readonly string _folderBackup = Path.Combine(Path.GetTempPath(), $"absensi-backup-{Guid.NewGuid():N}");
    private readonly string _folderCloudStub = Path.Combine(Path.GetTempPath(), $"absensi-cloud-{Guid.NewGuid():N}");

    private LayananBackup BuatLayanan() => new(_folderBackup, new PenyimpanCloudBackupLokal(_folderCloudStub), () => new byte[32]);

    // Keputusan pemilik 12 November 2026: backup lokal dienkripsi kunci lisensi.
    [Fact]
    public async Task JalankanAsync_LisensiAktif_BerkasLokalTerenkripsiDanBukanSqlitePolos()
    {
        using var database = new MigratedDatabase();
        database.SeedBaselineReferenceRows();

        await BuatLayanan().JalankanAsync(database.Connection, new DateTime(2026, 9, 18, 6, 0, 0), kunciLisensiAktif: "SEKOLAH-001-ABCD");

        var isi = File.ReadAllBytes(Assert.Single(Directory.GetFiles(_folderBackup, "*.db")));
        Assert.True(EnkripsiBackup.TerenkripsiLokal(isi));
        Assert.False(System.Text.Encoding.ASCII.GetString(isi, 0, 15).StartsWith("SQLite format", StringComparison.Ordinal));
        Assert.Empty(Directory.GetFiles(_folderBackup, "*.tmp"));
    }

    [Fact]
    public async Task SiapkanRestore_BackupTerenkripsiDenganLisensiSama_DidekripsiMenjadiSqlite()
    {
        using var databaseAsal = new MigratedDatabase();
        databaseAsal.SeedBaselineReferenceRows();
        var layanan = BuatLayanan();
        await layanan.JalankanAsync(databaseAsal.Connection, new DateTime(2026, 9, 1, 6, 0, 0), kunciLisensiAktif: "SEKOLAH-001-ABCD");

        using var databaseTujuan = new MigratedDatabase();
        databaseTujuan.SeedMasterDataUntukUji();
        TulisLisensi(databaseTujuan, "SEKOLAH-001-ABCD");

        var data = layanan.SiapkanRestore(databaseTujuan.Connection, Admin, new DateTime(2026, 9, 18, 7, 0, 0), layanan.DaftarBackupLokal().Single());

        Assert.Equal("SQLite format 3", System.Text.Encoding.ASCII.GetString(data, 0, 15));
    }

    [Fact]
    public async Task SiapkanRestore_BackupTerenkripsiLisensiLain_DitolakTanpaMenulisAudit()
    {
        using var databaseAsal = new MigratedDatabase();
        databaseAsal.SeedBaselineReferenceRows();
        var layanan = BuatLayanan();
        await layanan.JalankanAsync(databaseAsal.Connection, new DateTime(2026, 9, 1, 6, 0, 0), kunciLisensiAktif: "SEKOLAH-001-ABCD");
        var berkas = layanan.DaftarBackupLokal().Single();

        using var databaseTujuan = new MigratedDatabase();
        databaseTujuan.SeedMasterDataUntukUji();
        TulisLisensi(databaseTujuan, "SEKOLAH-LAIN-9999");

        Assert.Throws<InvalidOperationException>(() =>
            layanan.SiapkanRestore(databaseTujuan.Connection, Admin, new DateTime(2026, 9, 18, 7, 0, 0), berkas));

        using var command = databaseTujuan.Connection.CreateCommand();
        command.CommandText = "SELECT COUNT(*) FROM audit WHERE aksi = 'restore_backup';";
        Assert.Equal(0, Convert.ToInt32(command.ExecuteScalar()));
    }

    private static void TulisLisensi(MigratedDatabase database, string kunci)
    {
        using var transaction = database.Connection.BeginTransaction();
        new Absensi.Data.Lisensi.RepositoriLisensi().Tulis(database.Connection, transaction, kunci, new DateTime(2026, 9, 1, 6, 0, 0));
        transaction.Commit();
    }

    [Fact]
    public async Task JalankanAsync_MembuatSatuBerkasDb()
    {
        using var database = new MigratedDatabase();
        database.SeedBaselineReferenceRows();

        await BuatLayanan().JalankanAsync(database.Connection, new DateTime(2026, 9, 18, 6, 0, 0), kunciLisensiAktif: null);

        Assert.Single(Directory.GetFiles(_folderBackup, "*.db"));
    }

    [Fact]
    public async Task JalankanAsync_LebihDariTujuhKali_HanyaMenyisakanTujuhTerbaru()
    {
        using var database = new MigratedDatabase();
        database.SeedBaselineReferenceRows();
        var layanan = BuatLayanan();

        for (var i = 0; i < 9; i++)
        {
            await layanan.JalankanAsync(database.Connection, new DateTime(2026, 9, 10, 6, 0, 0).AddDays(i), kunciLisensiAktif: null);
        }

        Assert.Equal(7, Directory.GetFiles(_folderBackup, "*.db").Length);
        var daftar = layanan.DaftarBackupLokal();
        Assert.Equal(7, daftar.Count);
        Assert.Equal(new DateTime(2026, 9, 18, 6, 0, 0), daftar[0].WaktuDibuat);
    }

    [Fact]
    public async Task JalankanAsync_LisensiAktif_TerunggahKeCloudStub()
    {
        using var database = new MigratedDatabase();
        database.SeedBaselineReferenceRows();
        var penyimpanCloud = new PenyimpanCloudBackupLokal(_folderCloudStub);
        var layanan = new LayananBackup(_folderBackup, penyimpanCloud);
        var waktu = new DateTime(2026, 9, 18, 6, 0, 0);

        await layanan.JalankanAsync(database.Connection, waktu, kunciLisensiAktif: "SEKOLAH-001-ABCD");

        var hasil = await penyimpanCloud.UnduhTerakhirAsync("SEKOLAH-001-ABCD");
        Assert.NotNull(hasil);
        Assert.Equal(waktu, hasil!.WaktuBackup);

        var dataAsli = EnkripsiBackup.Dekripsi(hasil.DataTerenkripsi, "SEKOLAH-001-ABCD");
        Assert.NotEmpty(dataAsli);
    }

    [Fact]
    public async Task JalankanAsync_TanpaLisensi_TidakMengunggahApaPun()
    {
        using var database = new MigratedDatabase();
        database.SeedBaselineReferenceRows();
        var penyimpanCloud = new PenyimpanCloudBackupLokal(_folderCloudStub);
        var layanan = new LayananBackup(_folderBackup, penyimpanCloud);

        await layanan.JalankanAsync(database.Connection, DateTime.UtcNow, kunciLisensiAktif: null);

        Assert.False(Directory.Exists(_folderCloudStub));
    }

    // PRD §18.4: cadangan kondisi saat ini dibuat dulu, aksinya tercatat di
    // audit, lalu berkas database benar-benar ditimpa isi berkas restore.
    [Fact]
    public async Task SiapkanRestoreLaluTerapkanRestore_DatabaseBerisiIsiBerkasSumber()
    {
        using var databaseAsal = new MigratedDatabase();
        databaseAsal.SeedBaselineReferenceRows();
        var layanan = BuatLayanan();
        await layanan.JalankanAsync(databaseAsal.Connection, new DateTime(2026, 9, 1, 6, 0, 0), kunciLisensiAktif: null);
        var berkasSumberRestore = layanan.DaftarBackupLokal().Single();

        using var databaseTujuan = new MigratedDatabase();
        databaseTujuan.SeedMasterDataUntukUji();
        databaseTujuan.Execute("INSERT INTO guru (id, nip, nama) VALUES (99, '999', 'Guru Yang Akan Hilang');");

        var dataRestore = layanan.SiapkanRestore(databaseTujuan.Connection, Admin, new DateTime(2026, 9, 18, 7, 0, 0), berkasSumberRestore);

        using var command = databaseTujuan.Connection.CreateCommand();
        command.CommandText = "SELECT COUNT(*) FROM audit WHERE aksi = 'restore_backup';";
        Assert.Equal(1, Convert.ToInt32(command.ExecuteScalar()));

        // Cadangan kondisi saat ini (sebelum restore) juga tersimpan sebagai
        // berkas ke-2 di folder backup/.
        Assert.Equal(2, Directory.GetFiles(_folderBackup, "*.db").Length);

        var pathDbTujuan = databaseTujuan.Connection.DataSource;
        databaseTujuan.Connection.Dispose();
        SqliteConnection.ClearAllPools();

        LayananBackup.TerapkanRestore(dataRestore, pathDbTujuan);

        using var connectionSetelahRestore = new SqliteConnection($"Data Source={pathDbTujuan}");
        connectionSetelahRestore.Open();
        using var commandGuru = connectionSetelahRestore.CreateCommand();
        commandGuru.CommandText = "SELECT COUNT(*) FROM guru WHERE id = 99;";
        Assert.Equal(0, Convert.ToInt32(commandGuru.ExecuteScalar()));
        connectionSetelahRestore.Dispose();
        SqliteConnection.ClearAllPools();
    }

    // A-39 Tahap 2 (ditemukan lewat tes gagal, LayananAuditTests): baris
    // restore_backup ditulis SETELAH arsip dibuat (sengaja -- kalau
    // pengarsipan gagal, database sumber tidak boleh punya jejak restore
    // sama sekali), jadi tanpa langkah tambahan baris itu sendiri tidak
    // pernah ter-snapshot ke arsipnya. Diuji di sini LANGSUNG di lapisan
    // arsip (bukan cuma lewat integrasi LayananAudit).
    [Fact]
    public async Task SiapkanRestore_ArsipnyaSendiriBerisiBarisRestoreBackupMarkerItu()
    {
        using var databaseAsal = new MigratedDatabase();
        databaseAsal.SeedBaselineReferenceRows();
        var layanan = BuatLayanan();
        await layanan.JalankanAsync(databaseAsal.Connection, new DateTime(2026, 9, 1, 6, 0, 0), kunciLisensiAktif: null);
        var berkasSumberRestore = layanan.DaftarBackupLokal().Single();

        using var databaseTujuan = new MigratedDatabase();
        databaseTujuan.SeedMasterDataUntukUji();
        var waktuRestore = new DateTime(2026, 9, 18, 7, 0, 0);

        layanan.SiapkanRestore(databaseTujuan.Connection, Admin, waktuRestore, berkasSumberRestore);

        var pathArsip = Path.Combine(_folderBackup, "arsip-audit", $"arsip-audit-{waktuRestore:yyyyMMdd-HHmmss}.db");
        Assert.True(File.Exists(pathArsip));

        var entry = Assert.Single(LayananArsipAudit.ReadProtected(pathArsip, () => new byte[32])!);
        Assert.Equal("restore_backup", entry.Aksi);
        Assert.Throws<InvalidOperationException>(() => LayananArsipAudit.ReadProtected(pathArsip, () => Enumerable.Repeat((byte)1, 32).ToArray()));
    }

    // PRD §24.3: pemeriksaan peran di lapisan layanan, bukan
    // hanya menyembunyikan tombol Pulihkan di UI.
    [Fact]
    public void TerapkanRestore_BackupRusak_MeninggalkanDatabaseAktif()
    {
        using var database = new MigratedDatabase();
        database.SeedMasterDataUntukUji();
        var damaged = "SQLite format 3\0"u8.ToArray();
        Assert.Throws<InvalidOperationException>(() => LayananBackup.TerapkanRestore(damaged, database.Connection));
        using var command = database.Connection.CreateCommand();
        command.CommandText = "SELECT username FROM akun WHERE id = 1;";
        Assert.NotNull(command.ExecuteScalar());
    }

    [Fact]
    public void TerapkanRestore_TujuanHanyaBaca_MeninggalkanDatabaseAktif()
    {
        using var source = new MigratedDatabase();
        source.SeedBaselineReferenceRows();
        using var target = new MigratedDatabase();
        target.SeedMasterDataUntukUji();
        using var readOnly = new SqliteConnection(new SqliteConnectionStringBuilder
        {
            DataSource = target.Connection.DataSource, Mode = SqliteOpenMode.ReadOnly, Pooling = false
        }.ToString());
        readOnly.Open();
        using var copy = new MigratedDatabase();
        source.Connection.BackupDatabase(copy.Connection);
        var copyPath = copy.Connection.DataSource;
        copy.Connection.Close();
        SqliteConnection.ClearAllPools();
        var bytes = File.ReadAllBytes(copyPath);
        Assert.Throws<InvalidOperationException>(() => LayananBackup.TerapkanRestore(bytes, readOnly));
        using var command = target.Connection.CreateCommand();
        command.CommandText = "SELECT username FROM akun WHERE id = 1;";
        Assert.NotNull(command.ExecuteScalar());
    }

    [Fact]
    public async Task SiapkanRestore_WaliKelas_Ditolak()
    {
        using var databaseAsal = new MigratedDatabase();
        databaseAsal.SeedBaselineReferenceRows();
        var layanan = BuatLayanan();
        await layanan.JalankanAsync(databaseAsal.Connection, new DateTime(2026, 9, 1, 6, 0, 0), kunciLisensiAktif: null);
        var berkasSumberRestore = layanan.DaftarBackupLokal().Single();

        using var databaseTujuan = new MigratedDatabase();
        databaseTujuan.SeedMasterDataUntukUji();

        Assert.Throws<InvalidOperationException>(() =>
            layanan.SiapkanRestore(databaseTujuan.Connection, WaliKelas, new DateTime(2026, 9, 18, 7, 0, 0), berkasSumberRestore));

        using var command = databaseTujuan.Connection.CreateCommand();
        command.CommandText = "SELECT COUNT(*) FROM audit WHERE aksi = 'restore_backup';";
        Assert.Equal(0, Convert.ToInt32(command.ExecuteScalar()));
    }

    public void Dispose()
    {
        if (Directory.Exists(_folderBackup))
        {
            Directory.Delete(_folderBackup, recursive: true);
        }

        if (Directory.Exists(_folderCloudStub))
        {
            Directory.Delete(_folderCloudStub, recursive: true);
        }
    }
}
