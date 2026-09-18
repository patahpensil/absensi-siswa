using Absensi.App.Login;
using Absensi.Data.Lisensi;

namespace Absensi.App.Tests;

// RENCANA-TUGAS.md Tugas 13 — PANDUAN-UI-UX.md §9 (layar Login): "Logo,
// nama sekolah, ... status lisensi". Pemuatan gambar sungguhan (BitmapImage)
// tidak diuji di sini, murni pengolahan gambar — sama seperti PengolahFoto
// (lihat KoreksiTugas7Tests) — hanya jalur fallback placeholder yang diuji.
public sealed class LoginViewModelTests
{
    private static LoginViewModel BuatViewModel(string? logoPath, StatusLisensi? statusLisensi = null)
    {
        using var database = new MigratedDatabase();
        return new LoginViewModel(database.Connection, "1.0.0", "SMP Negeri Uji", logoPath, statusLisensi);
    }

    [Fact]
    public void Konstruksi_LogoPathNull_TampilkanPlaceholder()
    {
        var viewModel = BuatViewModel(logoPath: null);

        Assert.Null(viewModel.Logo);
        Assert.True(viewModel.TampilkanPlaceholderLogo);
    }

    [Fact]
    public void Konstruksi_BerkasLogoTidakAda_TampilkanPlaceholder()
    {
        var viewModel = BuatViewModel(logoPath: "logo-yang-tidak-ada.png");

        Assert.Null(viewModel.Logo);
        Assert.True(viewModel.TampilkanPlaceholderLogo);
    }

    [Fact]
    public void Konstruksi_LisensiAktif_TeksMenyebutAktif()
    {
        var viewModel = BuatViewModel(logoPath: null, new StatusLisensi("KUNCI-1", Aktif: true, DateTime.UtcNow));

        Assert.Equal("Lisensi: Aktif", viewModel.TeksStatusLisensi);
    }

    [Fact]
    public void Konstruksi_LisensiBelumAda_TeksMenyebutBelumTersedia()
    {
        var viewModel = BuatViewModel(logoPath: null, statusLisensi: null);

        Assert.Equal("Lisensi: (belum tersedia)", viewModel.TeksStatusLisensi);
    }
}
