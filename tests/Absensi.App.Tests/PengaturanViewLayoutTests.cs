using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Threading;
using Absensi.App.Pengaturan;
using Absensi.Data.Backup;
using Absensi.Data.Repositori;
using Absensi.Domain.Akun;

namespace Absensi.App.Tests;

// UAT (2026-09-23): pada layar 1366x768, tabel "Jadwal Harian" terpotong
// dan halaman Pengaturan tidak bisa digulir sampai baris terakhir maupun
// tombol "Simpan Jadwal Harian" — StackPanel tiga kartu (Bot Telegram,
// Jadwal Harian, Backup) langsung di dalam TabItem "Umum" tanpa
// ScrollViewer (PengaturanView.xaml). Diperbaiki dengan membungkusnya
// dalam ScrollViewer (pola sama seperti panel formulir SiswaView.xaml) —
// tidak ada logika atau nilai jadwal yang diubah.
//
// Test ini memakai WINDOW SUNGGUHAN (Show() + siklus Measure/Arrange
// penuh) — perilaku overflow/scroll TIDAK bisa dibuktikan tanpa itu, sama
// seperti keterbatasan yang didokumentasikan di SiswaGuruTabelKolomTests.cs
// untuk kasus serupa. SATU tes, SATU Window/StaHost.Jalankan (pola sama
// seperti RegistrasiKartuWindowTests.cs — Window KEDUA yang di-Close() di
// StaHost yang dibagi TERBUKTI mematikan Application untuk sisa proses tes
// karena ShutdownMode bawaan OnLastWindowClose): window yang sama di-
// RESIZE dari 1366x768 turun ke 1280x720 (MainWindow.xaml MinWidth/
// MinHeight — proksi kasus terburuk skala Windows 125%, bukan Close()
// lalu buka Window baru).
public sealed class PengaturanViewLayoutTests
{
    private static readonly DateTime Waktu = new(2026, 9, 23, 8, 0, 0);
    private static readonly AkunTersimpan Kepsek = new(1, "kepsek", "x", PeranAkun.KepalaSekolah, null, true);

    private static LayananBackup BuatLayananBackup() => new(
        Path.Combine(Path.GetTempPath(), $"absensi-test-backup-{Guid.NewGuid():N}"),
        new PenyimpanCloudBackupLokal(Path.Combine(Path.GetTempPath(), $"absensi-test-cloud-{Guid.NewGuid():N}")));

    [Fact]
    public void PengaturanView_LayarSempit_JadwalHarianBisaDigulirSampaiTombolSimpanDanKetujuhHariTerjangkau() => StaHost.Jalankan(() =>
    {
        using var database = new MigratedDatabase();
        database.SeedKelasUntukRegistrasiKartu();
        var viewModel = new PengaturanViewModel(database.Connection, Kepsek, new PenyediaWaktuUji(Waktu), tahunAjaranId: 1, BuatLayananBackup());
        var view = new PengaturanView { DataContext = viewModel };

        var window = new Window
        {
            Content = view,
            Width = 1366,
            Height = 768,
            WindowStartupLocation = WindowStartupLocation.Manual,
            Left = 0,
            Top = 0,
            ShowInTaskbar = false,
        };

        window.Show();
        PompaDispatcher(5);

        // Ukuran layar yang dilaporkan (MainWindow.xaml bawaan 1366x768),
        // lalu MinWidth/MinHeight MainWindow.xaml (1280x720 — proksi kasus
        // terburuk skala Windows 125%) di Window YANG SAMA.
        VerifikasiBisaDigulirSampaiSemuaTerjangkau(window, view, "1366x768");

        window.Width = 1280;
        window.Height = 720;
        PompaDispatcher(5);

        VerifikasiBisaDigulirSampaiSemuaTerjangkau(window, view, "1280x720");

        window.Close();
    });

    private static void VerifikasiBisaDigulirSampaiSemuaTerjangkau(Window window, PengaturanView view, string labelUkuran)
    {
        window.UpdateLayout();

        var scrollViewer = CariKeturunan<ScrollViewer>(view);
        Assert.True(scrollViewer is not null, $"[{labelUkuran}] ScrollViewer tidak ditemukan di visual tree — pembungkus ScrollViewer hilang dari PengaturanView.xaml.");

        // Kalau ScrollableHeight = 0, seluruh konten sudah muat tanpa
        // digulir di ukuran ini — bagian selanjutnya dari test tidak
        // membuktikan perbaikan scroll apa pun untuk ukuran itu, tapi
        // bukan kegagalan (mungkin memang muat).
        var overflow = scrollViewer!.ScrollableHeight > 0;

        var tombolSimpan = CariTombol(view, "Simpan Jadwal Harian");
        Assert.True(tombolSimpan is not null, $"[{labelUkuran}] Tombol 'Simpan Jadwal Harian' tidak ditemukan di visual tree.");

        var tabelJadwal = CariKeturunan<DataGrid>(view);
        Assert.True(tabelJadwal is not null, $"[{labelUkuran}] DataGrid Jadwal Harian tidak ditemukan di visual tree.");
        Assert.Equal(7, tabelJadwal!.Items.Count);

        // BringIntoView() adalah mekanisme WPF sendiri (dipakai juga saat
        // navigasi Tab/fokus) untuk menggulir SELURUH ancestor yang perlu
        // sampai elemen benar-benar terlihat penuh — pembuktian paling
        // langsung untuk "bisa diakses lewat scroll", bukan menebak posisi
        // scroll secara manual.
        tabelJadwal.BringIntoView();
        PompaDispatcher(5);
        window.UpdateLayout();

        var batasTabel = tabelJadwal.TransformToAncestor(scrollViewer)
            .TransformBounds(new Rect(0, 0, tabelJadwal.ActualWidth, tabelJadwal.ActualHeight));

        Assert.True(
            batasTabel.Top >= -1 && batasTabel.Bottom <= scrollViewer.ViewportHeight + 1,
            $"[{labelUkuran}] Tabel Jadwal Harian (ketujuh hari) tidak muat utuh di viewport setelah BringIntoView " +
            $"(Top={batasTabel.Top:F1}, Bottom={batasTabel.Bottom:F1}, ViewportHeight={scrollViewer.ViewportHeight:F1}, overflow={overflow}).");

        tombolSimpan!.BringIntoView();
        PompaDispatcher(5);
        window.UpdateLayout();

        var batasTombol = tombolSimpan.TransformToAncestor(scrollViewer)
            .TransformBounds(new Rect(0, 0, tombolSimpan.ActualWidth, tombolSimpan.ActualHeight));

        Assert.True(
            batasTombol.Top >= -1 && batasTombol.Bottom <= scrollViewer.ViewportHeight + 1,
            $"[{labelUkuran}] Tombol 'Simpan Jadwal Harian' tidak muat utuh di viewport setelah BringIntoView " +
            $"(Top={batasTombol.Top:F1}, Bottom={batasTombol.Bottom:F1}, ViewportHeight={scrollViewer.ViewportHeight:F1}, overflow={overflow}).");
    }

    // Pola sama persis seperti RegistrasiKartuWindowTests.cs — DispatcherFrame
    // dipush berulang, bukan sekali Invoke(Background) — terbukti perlu
    // untuk Window/TabControl/ScrollViewer sungguhan menyelesaikan siklus
    // Loaded/layout penuh sebelum VisualTreeHelper bisa menemukan
    // keturunannya (kelemahan yang ditemukan lewat percobaan langsung di
    // sesi ini: satu Invoke(Background) tidak cukup, ScrollViewer belum
    // termaterialisasi).
    private static void PompaDispatcher(int jumlah)
    {
        for (var i = 0; i < jumlah; i++)
        {
            var frame = new DispatcherFrame();
            Dispatcher.CurrentDispatcher.BeginInvoke(DispatcherPriority.Background, new Action(() => frame.Continue = false));
            Dispatcher.PushFrame(frame);
        }
    }

    private static T? CariKeturunan<T>(DependencyObject akar) where T : DependencyObject
    {
        for (var i = 0; i < VisualTreeHelper.GetChildrenCount(akar); i++)
        {
            var anak = VisualTreeHelper.GetChild(akar, i);
            if (anak is T ditemukan)
            {
                return ditemukan;
            }

            var hasil = CariKeturunan<T>(anak);
            if (hasil is not null)
            {
                return hasil;
            }
        }

        return null;
    }

    private static Button? CariTombol(DependencyObject akar, string teks)
    {
        for (var i = 0; i < VisualTreeHelper.GetChildrenCount(akar); i++)
        {
            var anak = VisualTreeHelper.GetChild(akar, i);
            if (anak is Button tombol && tombol.Content as string == teks)
            {
                return tombol;
            }

            var hasil = CariTombol(anak, teks);
            if (hasil is not null)
            {
                return hasil;
            }
        }

        return null;
    }
}
