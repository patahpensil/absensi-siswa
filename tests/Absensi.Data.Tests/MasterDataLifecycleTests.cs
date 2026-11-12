using Absensi.Data.Akun;
using Absensi.Data.MasterData;
using Absensi.Data.Repositori;
using Absensi.Domain.Akun;
using Absensi.Domain.Kehadiran;

namespace Absensi.Data.Tests;

// PRD §5.3/§5.8 (mutasi siswa: tidak dihapus, kartu aktif dinonaktifkan)
// dan §3.1 (akun: username/sandi minimum, username unik).
public sealed class MasterDataLifecycleTests
{
    private static readonly DateTime Waktu = new(2026, 9, 17, 8, 0, 0);
    private static readonly AkunTersimpan Admin = new(1, "admin1", "x", PeranAkun.Admin, null, true);

    [Fact]
    public void MutasiSiswa_DenganKartu_KartuDinonaktifkanDanTercatatDiAudit()
    {
        using var database = new MigratedDatabase();
        // Siswa 1 = "Siswa Sudah Kartu" (UID-SISWA-LAMA).
        database.SeedKelasUntukRegistrasiKartu();

        new LayananSiswa().TandaiTidakAktif(database.Connection, Admin, Waktu, 1, "pindah");

        Assert.Null(new RepositoriKartu().CariUidAktifUntukPemilik(database.Connection, JenisPemilikKartu.Siswa, 1));
        Assert.Equal(1L, Scalar(database, "SELECT COUNT(*) FROM kartu WHERE uid = 'UID-SISWA-LAMA' AND aktif = 0 AND alasan = 'siswa nonaktif: pindah';"));
        Assert.Equal("status=aktif; kartu_aktif=UID-SISWA-LAMA", Scalar(database, "SELECT nilai_lama FROM audit WHERE aksi = 'nonaktifkan_siswa';"));
        Assert.Equal("status=nonaktif (pindah); kartu_aktif=(kosong)", Scalar(database, "SELECT nilai_baru FROM audit WHERE aksi = 'nonaktifkan_siswa';"));
    }

    [Fact]
    public void MutasiSiswa_KartuSetelahMutasi_TapDitolakSebagaiKartuTidakDikenal()
    {
        using var database = new MigratedDatabase();
        database.SeedKelasUntukRegistrasiKartu();

        new LayananSiswa().TandaiTidakAktif(database.Connection, Admin, Waktu, 1, "keluar");

        Assert.Null(new RepositoriKartu().CariAktifBerdasarkanUid(database.Connection, "UID-SISWA-LAMA"));
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void MutasiSiswa_TanpaAlasan_DitolakTanpaPerubahan(string alasan)
    {
        using var database = new MigratedDatabase();
        database.SeedKelasUntukRegistrasiKartu();

        Assert.Throws<InvalidOperationException>(() => new LayananSiswa().TandaiTidakAktif(database.Connection, Admin, Waktu, 1, alasan));

        Assert.Equal(1L, Scalar(database, "SELECT aktif FROM siswa WHERE id = 1;"));
        Assert.NotNull(new RepositoriKartu().CariUidAktifUntukPemilik(database.Connection, JenisPemilikKartu.Siswa, 1));
    }

    [Theory]
    [InlineData("ab")]
    [InlineData("Admin Baru")]
    [InlineData("admin-baru")]
    [InlineData("")]
    public void BuatAkun_UsernameTidakValid_Ditolak(string username)
    {
        using var database = new MigratedDatabase();
        database.SeedAkunUntukUji();
        var kepsek = new LayananAkun().Autentikasi(database.Connection, "kepsek", MigratedDatabase.SandiKepsek)!;

        var error = Assert.Throws<InvalidOperationException>(() =>
            new LayananAkun().BuatAkun(database.Connection, kepsek, Waktu, username, "SandiValid1!", PeranAkun.Admin, null));

        Assert.Contains("Username", error.Message);
    }

    [Fact]
    public void BuatAkun_SandiPendek_Ditolak()
    {
        using var database = new MigratedDatabase();
        database.SeedAkunUntukUji();
        var kepsek = new LayananAkun().Autentikasi(database.Connection, "kepsek", MigratedDatabase.SandiKepsek)!;

        var error = Assert.Throws<InvalidOperationException>(() =>
            new LayananAkun().BuatAkun(database.Connection, kepsek, Waktu, "admin_baru", "pendek", PeranAkun.Admin, null));

        Assert.Contains("minimal 8", error.Message);
    }

    [Fact]
    public void BuatAkun_UsernameSudahDipakai_DitolakDenganPesanJelas()
    {
        using var database = new MigratedDatabase();
        database.SeedAkunUntukUji();
        var layanan = new LayananAkun();
        var kepsek = layanan.Autentikasi(database.Connection, "kepsek", MigratedDatabase.SandiKepsek)!;
        layanan.BuatAkun(database.Connection, kepsek, Waktu, "admin_baru", "SandiAdmin1!", PeranAkun.Admin, null);

        var error = Assert.Throws<InvalidOperationException>(() =>
            layanan.BuatAkun(database.Connection, kepsek, Waktu, "admin_baru", "SandiAdmin1!", PeranAkun.Admin, null));

        Assert.Contains("sudah dipakai", error.Message);
    }

    [Fact]
    public void ResetSandi_SandiPendek_DitolakDanSandiLamaTetapBerlaku()
    {
        using var database = new MigratedDatabase();
        database.SeedAkunUntukUji();
        var layanan = new LayananAkun();
        var kepsek = layanan.Autentikasi(database.Connection, "kepsek", MigratedDatabase.SandiKepsek)!;
        var admin = layanan.BuatAkun(database.Connection, kepsek, Waktu, "admin_baru", "SandiAdmin1!", PeranAkun.Admin, null);

        Assert.Throws<InvalidOperationException>(() => layanan.ResetSandi(database.Connection, kepsek, Waktu, admin.Id, "1234567"));

        Assert.NotNull(layanan.Autentikasi(database.Connection, "admin_baru", "SandiAdmin1!"));
    }

    private static object? Scalar(MigratedDatabase database, string sql)
    {
        using var command = database.Connection.CreateCommand();
        command.CommandText = sql;
        return command.ExecuteScalar();
    }
}
