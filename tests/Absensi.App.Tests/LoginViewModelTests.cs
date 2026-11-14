using Absensi.App.Login;
using Absensi.Data.Akun;
using Absensi.Data.Lisensi;
using Absensi.Data.Repositori;

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

    // P-21: akun yang sandinya diatur ulang tidak masuk sebelum membuat sandi baru.
    [Fact]
    public void Login_SetelahResetSandi_WajibGantiSandiSebelumLoginBerhasil()
    {
        using var database = new MigratedDatabase();
        database.TambahAkun(1, "kepsek", "kepala_sekolah", guruId: null);
        database.TambahAkun(2, "admin1", "admin", guruId: null);
        var admin = new LayananAkun().Autentikasi(database.Connection, "admin1", MigratedDatabase.SandiUji)!;
        new LayananAkun().ResetSandi(database.Connection, admin, new DateTime(2026, 11, 14, 8, 0, 0), 1, "Sementara123");
        var viewModel = new LoginViewModel(database.Connection, "1.0.0", "SMP Negeri Uji", null, null,
            new PenyediaWaktuUji(new DateTime(2026, 11, 14, 9, 0, 0)));
        AkunTersimpan? loggedIn = null;
        viewModel.LoginBerhasil += (_, akun) => loggedIn = akun;

        viewModel.Username = "kepsek";
        viewModel.AturSandi("Sementara123");
        viewModel.LoginCommand.Execute(null);

        Assert.Null(loggedIn);
        Assert.True(viewModel.MustChangePassword);
        Assert.False(viewModel.ShowLogin);

        viewModel.SetNewPassword("SandiBaruKepsek1");
        viewModel.SetConfirmPassword("SandiBaruKepsekX");
        viewModel.ChangePasswordCommand.Execute(null);
        Assert.Null(loggedIn);
        Assert.Equal("Ulangi sandi baru tidak sama.", viewModel.PesanGalat);

        viewModel.SetConfirmPassword("SandiBaruKepsek1");
        viewModel.ChangePasswordCommand.Execute(null);

        Assert.NotNull(loggedIn);
        Assert.False(viewModel.MustChangePassword);
        Assert.False(new LayananAkun().RequiresPasswordChange(database.Connection, loggedIn!));
        Assert.NotNull(new LayananAkun().Autentikasi(database.Connection, "kepsek", "SandiBaruKepsek1"));
    }
}
