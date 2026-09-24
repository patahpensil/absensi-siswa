using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Threading;
using Absensi.App.Shell;
using Absensi.Data.Backup;
using Absensi.Data.Repositori;
using Absensi.Domain.Akun;

namespace Absensi.App.Tests;

// Penanda akun aktif di header (mis. "Admin · admin1") -- diuji dengan
// MainWindow WPF SUNGGUHAN pada MinWidth aplikasi (1280, MainWindow.xaml)
// supaya kedua syarat eksplisit terbukti, bukan diasumsikan dari membaca
// XAML: (1) teks penanda tetap terbaca/tidak terpotong di jendela sekecil
// yang aplikasi izinkan, (2) tombol Keluar tidak ikut terdorong keluar
// area window walau nama sekolah dan username sama-sama panjang.
public sealed class MainWindowHeaderLayoutTests
{
    private static readonly DateTime Waktu = new(2026, 9, 25, 8, 0, 0);

    private static LayananBackup BuatLayananBackup() => new(
        Path.Combine(Path.GetTempPath(), $"absensi-header-backup-{Guid.NewGuid():N}"),
        new PenyimpanCloudBackupLokal(Path.Combine(Path.GetTempPath(), $"absensi-header-cloud-{Guid.NewGuid():N}")));

    [Theory]
    [InlineData("admin1", "Admin", "SMP Contoh", "Admin · admin1")]
    [InlineData("kepsek_operasional", "KepalaSekolah", "SMP Negeri 1 Sukamaju", "Kepala Sekolah · kepsek_operasional")]
    public void HeaderMainWindow_PadaMinWidthAplikasi_PenandaAkunTerbacaDanKeluarTidakTerdorongKeluarWindow(
        string username, string namaPeran, string namaSekolah, string diharapkan) => StaHost.Jalankan(() =>
    {
        using var database = new MigratedDatabase();
        database.SeedKelasUntukRegistrasiKartu();
        var peran = Enum.Parse<PeranAkun>(namaPeran);
        var akun = new AkunTersimpan(1, username, "x", peran, GuruId: null, true);
        var viewModel = new MainWindowViewModel(
            database.Connection,
            akun,
            new PenyediaWaktuUji(Waktu),
            new StatusModeRegistrasi(),
            namaSekolah,
            logoPath: null,
            tahunAjaranAktif: "2026/2027",
            tahunAjaranId: 1,
            BuatLayananBackup());

        var window = new MainWindow(
            viewModel,
            bukaGerbang: () => new Window(),
            bukaRegistrasiKartu: () => null,
            restoreSelesaiPerluTutupAplikasi: _ => { })
        {
            ShowInTaskbar = false,
            WindowStartupLocation = WindowStartupLocation.Manual,
            WindowState = WindowState.Normal, // bukan Maximized (bawaan) -- uji persis lebar sekecil yang diizinkan
            Left = -6000,
            Top = 0,
            Width = 1280, // MainWindow.xaml MinWidth="1280" -- skenario "jendela kecil" paling ketat yang app izinkan
            Height = 720,
        };

        try
        {
            window.Show();
            PompaDispatcher(8);
            window.UpdateLayout();

            var teksPenanda = Semua<TextBlock>(window).Single(t => Equals(t.Text, diharapkan));
            Assert.True(teksPenanda.IsVisible, "Teks penanda akun tidak IsVisible.");
            Assert.True(teksPenanda.ActualWidth > 0, "Teks penanda akun tidak punya lebar -- kemungkinan tidak dirender.");

            var tombolKeluar = Semua<Button>(window).Single(b => Equals(b.Content, "Keluar"));
            var batasKeluar = tombolKeluar.TransformToAncestor(window).TransformBounds(new Rect(0, 0, tombolKeluar.ActualWidth, tombolKeluar.ActualHeight));

            Assert.True(tombolKeluar.IsVisible, "Tombol Keluar tidak IsVisible.");
            Assert.True(batasKeluar.Right <= window.ActualWidth + 0.5,
                $"Tombol Keluar berakhir di X={batasKeluar.Right:F0}, melebihi lebar window {window.ActualWidth:F0} -- terdorong keluar layar.");
            Assert.True(batasKeluar.Left >= 0,
                $"Tombol Keluar mulai di X={batasKeluar.Left:F0} (negatif) -- sudah terdorong keluar dari sisi kiri window.");
        }
        finally
        {
            // BUKAN window.Close(): MainWindow.xaml.cs menyambungkan Closed
            // ke Application.Current.Shutdown() (Tugas 14, perilaku produksi
            // yang benar -- App.xaml ShutdownMode="OnExplicitShutdown"). Di
            // proses uji, Application dibagi oleh semua kasus lewat thread
            // StaHost yang sama, jadi Close() di sini mematikannya untuk
            // KASUS BERIKUTNYA juga (baru ditemukan: kasus kedua teori ini
            // gagal dengan "The Application object is being shut down" saat
            // membangun MainWindow baru). Hide() cukup untuk uji tata letak.
            window.Hide();
        }
    });

    private static IEnumerable<T> Semua<T>(DependencyObject akar) where T : DependencyObject
    {
        for (var i = 0; i < VisualTreeHelper.GetChildrenCount(akar); i++)
        {
            var anak = VisualTreeHelper.GetChild(akar, i);
            if (anak is T t)
            {
                yield return t;
            }

            foreach (var d in Semua<T>(anak))
            {
                yield return d;
            }
        }
    }

    private static void PompaDispatcher(int jumlah)
    {
        for (var i = 0; i < jumlah; i++)
        {
            var frame = new DispatcherFrame();
            Dispatcher.CurrentDispatcher.BeginInvoke(DispatcherPriority.Background, new Action(() => frame.Continue = false));
            Dispatcher.PushFrame(frame);
        }
    }
}
