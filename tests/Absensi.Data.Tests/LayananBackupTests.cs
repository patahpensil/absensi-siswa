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

    private LayananBackup BuatLayanan() => new(_folderBackup, new PenyimpanCloudBackupLokal(_folderCloudStub));

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

    // CLAUDE.md §4.5 aturan 19: pemeriksaan peran di lapisan layanan, bukan
    // hanya menyembunyikan tombol Pulihkan di UI.
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
