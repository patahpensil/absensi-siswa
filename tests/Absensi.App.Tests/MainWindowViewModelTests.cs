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
}
