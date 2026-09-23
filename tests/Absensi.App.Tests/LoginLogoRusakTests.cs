using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;
using Path = System.IO.Path;
using System.Windows.Threading;
using Absensi.App.Login;

namespace Absensi.App.Tests;

// PemuatGambar dipakai bersama CacheTampilanGerbang (foto/logo) dan
// LoginViewModel (logo sekolah). Sebelumnya berkas logo yang ada tapi rusak
// melempar NotSupportedException DARI KONSTRUKTOR LoginViewModel — aplikasi
// gagal menampilkan layar Login sama sekali. Sekarang logo rusak jatuh ke
// placeholder lingkaran (jalur "belum ada logo" yang sudah ada).
//
// Memakai LoginWindow SUNGGUHAN (Show(), bukan Close() — lihat komentar
// StaHost: Close() di StaHost bersama mematikan Application untuk sisa
// proses tes).
public sealed class LoginLogoRusakTests
{
    private static string BuatFolderUji() =>
        Directory.CreateDirectory(Path.Combine(Path.GetTempPath(), $"absensi-logo-{Guid.NewGuid():N}")).FullName;

    private static void TulisPngValid(string path)
    {
        var piksel = new byte[64 * 64 * 4];
        Array.Fill(piksel, (byte)200);
        var bitmap = BitmapSource.Create(64, 64, 96, 96, PixelFormats.Bgra32, null, piksel, 64 * 4);
        using var stream = File.Create(path);
        var encoder = new PngBitmapEncoder();
        encoder.Frames.Add(BitmapFrame.Create(bitmap));
        encoder.Save(stream);
    }

    private static LoginWindow BukaLogin(MigratedDatabase database, string? logoPath)
    {
        var viewModel = new LoginViewModel(database.Connection, "1.0.0", "SMP Negeri Uji", logoPath, statusLisensi: null);
        var window = new LoginWindow(viewModel)
        {
            ShowInTaskbar = false,
            WindowStartupLocation = WindowStartupLocation.Manual,
            Left = -6000,
            Top = 0,
        };
        window.Show();
        for (var i = 0; i < 6; i++)
        {
            var frame = new DispatcherFrame();
            Dispatcher.CurrentDispatcher.BeginInvoke(DispatcherPriority.Background, new Action(() => frame.Continue = false));
            Dispatcher.PushFrame(frame);
        }

        window.UpdateLayout();
        return window;
    }

    private static (Image Logo, Ellipse Placeholder) CariLogoDanPlaceholder(Window window)
    {
        Image? logo = null;
        Ellipse? placeholder = null;

        void Telusuri(DependencyObject akar)
        {
            for (var i = 0; i < VisualTreeHelper.GetChildrenCount(akar); i++)
            {
                var anak = VisualTreeHelper.GetChild(akar, i);
                if (anak is Image { Width: 56, Height: 56 } gambar)
                {
                    logo = gambar;
                }
                else if (anak is Ellipse { Width: 56, Height: 56 } lingkaran)
                {
                    placeholder = lingkaran;
                }

                Telusuri(anak);
            }
        }

        Telusuri(window);
        return (logo!, placeholder!);
    }

    [Fact]
    public void LoginViewModel_LogoRusak_TidakMelemparGalat_LogoNullDanPlaceholderAktif() => StaHost.Jalankan(() =>
    {
        var folder = BuatFolderUji();
        try
        {
            var rusak = Path.Combine(folder, "logo-rusak.png");
            File.WriteAllText(rusak, "ini bukan gambar");
            using var database = new MigratedDatabase();

            var viewModel = new LoginViewModel(database.Connection, "1.0.0", "SMP Negeri Uji", rusak, statusLisensi: null);

            Assert.Null(viewModel.Logo);
            Assert.True(viewModel.TampilkanPlaceholderLogo);
            Assert.Equal("SMP Negeri Uji", viewModel.NamaSekolah);
        }
        finally
        {
            Directory.Delete(folder, recursive: true);
        }
    });

    [Fact]
    public void LoginWindow_LogoRusak_LayarTerbukaDenganPlaceholderLingkaran() => StaHost.Jalankan(() =>
    {
        var folder = BuatFolderUji();
        try
        {
            var rusak = Path.Combine(folder, "logo-rusak.png");
            File.WriteAllBytes(rusak, new byte[] { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A, 0x00, 0x01 }); // header PNG terpotong
            using var database = new MigratedDatabase();

            var window = BukaLogin(database, rusak);
            var (logo, placeholder) = CariLogoDanPlaceholder(window);

            Assert.True(window.IsVisible, "LoginWindow tidak terbuka.");
            Assert.Null(window.Icon);
            Assert.False(logo.IsVisible, "Gambar logo tampil padahal berkas rusak.");
            Assert.True(placeholder.IsVisible, "Placeholder lingkaran tidak tampil untuk logo rusak.");
            Assert.True(placeholder.ActualWidth > 0);

            window.Hide();
        }
        finally
        {
            Directory.Delete(folder, recursive: true);
        }
    });

    [Fact]
    public void LoginWindow_LogoValid_LogoTampilDanPlaceholderTersembunyi() => StaHost.Jalankan(() =>
    {
        var folder = BuatFolderUji();
        try
        {
            var bagus = Path.Combine(folder, "logo.png");
            TulisPngValid(bagus);
            using var database = new MigratedDatabase();

            var window = BukaLogin(database, bagus);
            var (logo, placeholder) = CariLogoDanPlaceholder(window);

            Assert.NotNull(logo.Source);
            Assert.True(logo.IsVisible);
            Assert.False(placeholder.IsVisible);

            window.Hide();
        }
        finally
        {
            Directory.Delete(folder, recursive: true);
        }
    });
}
