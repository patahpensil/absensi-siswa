using System.IO;
using Absensi.App.Shell;
using Absensi.Data.Backup;
using Absensi.Data.Repositori;
using Absensi.Domain.Akun;

namespace Absensi.App.Tests;

// Bug UX pasca serah terima (windowing): MainWindow butuh Icon di title bar
// dan judul yang menyebut nama sekolah — keduanya properti murni yang bisa
// diuji tanpa membuka jendela WPF sungguhan (lihat catatan lain di berkas
// ini soal Window.ShowDialog() yang sengaja tidak diuji langsung).
public sealed class MainWindowViewModelTests
{
    private static readonly DateTime Waktu = new(2026, 9, 19, 8, 0, 0);
    private static readonly AkunTersimpan Admin = new(1, "admin1", "x", PeranAkun.Admin, null, true);

    private static LayananBackup BuatLayananBackup() => new(
        Path.Combine(Path.GetTempPath(), $"absensi-test-backup-{Guid.NewGuid():N}"),
        new PenyimpanCloudBackupLokal(Path.Combine(Path.GetTempPath(), $"absensi-test-cloud-{Guid.NewGuid():N}")));

    private static MainWindowViewModel BuatViewModel(MigratedDatabase database, string namaSekolah, string? logoPath) =>
        new(
            database.Connection,
            Admin,
            new PenyediaWaktuUji(Waktu),
            new StatusModeRegistrasi(),
            namaSekolah,
            logoPath,
            tahunAjaranAktif: "2026/2027",
            tahunAjaranId: 1,
            BuatLayananBackup());

    [Fact]
    public void TitleJendela_MenyebutNamaSekolah()
    {
        using var database = new MigratedDatabase();
        database.SeedKelasUntukRegistrasiKartu();
        var viewModel = BuatViewModel(database, "SMP Contoh", logoPath: null);

        Assert.Equal("Absensi - SMP Contoh", viewModel.TitleJendela);
    }

    [Fact]
    public void Logo_TanpaLogoPath_Null()
    {
        using var database = new MigratedDatabase();
        database.SeedKelasUntukRegistrasiKartu();
        var viewModel = BuatViewModel(database, "SMP Contoh", logoPath: null);

        Assert.Null(viewModel.Logo);
    }

    [Fact]
    public void Logo_BerkasTidakDitemukan_NullBukanCrash()
    {
        using var database = new MigratedDatabase();
        database.SeedKelasUntukRegistrasiKartu();
        var viewModel = BuatViewModel(database, "SMP Contoh", logoPath: "logo-tidak-ada.png");

        Assert.Null(viewModel.Logo);
    }

    // UAT Bug #11 (2026-09-23, putaran kelima): RegistrasiKartuWindow tidak
    // pernah terhubung ke SiswaViewModel — kalau panel Siswa sudah pernah
    // dibuka (ter-cache), kartu yang baru didaftarkan lewat dialog terpisah
    // itu tidak pernah muncul di kolom Kartu tanpa panggilan eksplisit ini
    // (dipanggil MainWindow.xaml.cs setiap RegistrasiKartuWindow tertutup).
    [Fact]
    public void MuatUlangSiswaSetelahRegistrasiKartu_PanelSiswaSudahDibuka_StatusKartuTerbaru()
    {
        using var database = new MigratedDatabase();
        database.SeedKelasUntukRegistrasiKartu();
        var viewModel = BuatViewModel(database, "SMP Contoh", logoPath: null);

        // Buka panel Siswa dulu (persis urutan operator: buka Siswa,
        // lalu buka Absensi -> Registrasi Kartu terpisah) supaya
        // SiswaViewModel ter-cache SEBELUM kartu baru didaftarkan.
        viewModel.MenuAktif = "Siswa";
        var siswaViewModel = Assert.IsType<Absensi.App.MasterData.SiswaViewModel>(viewModel.KontenAktif);
        var siswaBelumKartu = siswaViewModel.Daftar.Single(s => s.Nama == "Siswa Belum Satu");
        Assert.False(siswaBelumKartu.PunyaKartu);

        // Simulasi: RegistrasiKartuViewModel.Simpan() berhasil menulis
        // kartu baru lewat koneksi yang SAMA (persis skenario nyata —
        // App._connection dipakai bersama semua ViewModel).
        database.Execute("""
            INSERT INTO kartu (uid, pemilik_tipe, pemilik_id, aktif, didaftar_pada)
            VALUES ('UID-BARU-SIMULASI', 'siswa', 2, 1, '2026-09-23T00:00:00');
            """);

        viewModel.MuatUlangSiswaSetelahRegistrasiKartu();

        var siswaSetelahRefresh = siswaViewModel.Daftar.Single(s => s.Nama == "Siswa Belum Satu");
        Assert.True(siswaSetelahRefresh.PunyaKartu);
    }

    [Fact]
    public void MuatUlangSiswaSetelahRegistrasiKartu_PanelSiswaBelumPernahDibuka_TidakMelempar()
    {
        using var database = new MigratedDatabase();
        database.SeedKelasUntukRegistrasiKartu();
        var viewModel = BuatViewModel(database, "SMP Contoh", logoPath: null);

        var exception = Record.Exception(() => viewModel.MuatUlangSiswaSetelahRegistrasiKartu());

        Assert.Null(exception);
    }
}
