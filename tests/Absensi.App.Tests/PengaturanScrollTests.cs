using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;
using Absensi.App.Shell;
using Absensi.Data.Backup;
using Absensi.Data.Repositori;
using Absensi.Domain.Akun;

namespace Absensi.App.Tests;

// Pengaturan: "scroll kadang berfungsi dan kadang tidak" (laporan pemilik,
// 2026-09-24). Tes ini mereproduksi masalahnya dengan MainWindow SUNGGUHAN:
// roda mouse dikirim ke elemen YANG BENAR-BENAR terkena hit-test pada titik
// tertentu (persis jalur WPF: PreviewMouseWheel lalu MouseWheel pada
// elemen di bawah kursor) — di atas area kosong di antara kartu, di atas
// kolom isian (TextBox), di atas DataGrid, dan di atas kartu itu sendiri —
// lalu memeriksa apakah ScrollViewer halaman bergeser.
public sealed class PengaturanScrollTests
{
    private static readonly DateTime Waktu = new(2026, 9, 24, 8, 0, 0);
    private static readonly AkunTersimpan Kepsek = new(1, "kepsek", "x", PeranAkun.KepalaSekolah, null, true);

    private static void Pompa(int n)
    {
        for (var i = 0; i < n; i++)
        {
            var frame = new DispatcherFrame();
            Dispatcher.CurrentDispatcher.BeginInvoke(DispatcherPriority.Background, new Action(() => frame.Continue = false));
            Dispatcher.PushFrame(frame);
        }
    }

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

    private static (MainWindow Window, MigratedDatabase Db) Buka(int lebar, int tinggi)
    {
        var db = new MigratedDatabase();
        db.Execute("""
            INSERT INTO tahun_ajaran (id, nama, semester, tgl_mulai, tgl_selesai, aktif) VALUES (1, '2026/2027', 'ganjil', '2026-07-01', '2026-12-19', 1);
            INSERT INTO akun (id, username, sandi_hash, peran, guru_id, aktif, dibuat_pada) VALUES (1, 'kepsek', 'x', 'kepala_sekolah', NULL, 1, '2026-09-01T00:00:00');
            INSERT INTO jadwal_hari (tahun_ajaran_id, hari, hari_sekolah, jam_masuk, jam_rekap, batas_tap_masuk, mulai_pulang) VALUES
              (1,1,1,'07:30','08:00','12:00','13:10'),(1,2,1,'07:30','08:00','12:00','13:10'),(1,3,1,'07:30','08:00','12:00','13:10'),
              (1,4,1,'07:30','08:00','12:00','13:10'),(1,5,1,'07:30','08:00','10:00','11:30'),(1,6,1,'07:30','08:00','12:00','13:10'),(1,7,0,NULL,NULL,NULL,NULL);
            """);
        var vm = new MainWindowViewModel(db.Connection, Kepsek, new PenyediaWaktuUji(Waktu), new StatusModeRegistrasi(), "SMP Uji", null, "2026/2027", 1,
            new LayananBackup(Path.Combine(Path.GetTempPath(), "b" + Guid.NewGuid().ToString("N")), new PenyimpanCloudBackupLokal(Path.Combine(Path.GetTempPath(), "c" + Guid.NewGuid().ToString("N")))));
        var w = new MainWindow(vm, () => new Window(), () => null, _ => { })
        {
            ShowInTaskbar = false, WindowState = WindowState.Normal, WindowStartupLocation = WindowStartupLocation.Manual,
            Left = -6000, Top = 0, Width = lebar, Height = tinggi,
        };
        w.Show();
        Pompa(8);
        vm.MenuAktif = "Pengaturan";
        Pompa(10);
        w.UpdateLayout();
        return (w, db);
    }

    // Kirim roda mouse seperti WPF: elemen di bawah titik (hit-test), tunneling lalu bubbling.
    private static UIElement KirimRodaDiTitik(Window w, Point titikDiJendela, int delta)
    {
        UIElement? target = null;
        VisualTreeHelper.HitTest(w, null, hasil =>
        {
            if (hasil.VisualHit is UIElement e && e.IsHitTestVisible)
            {
                target = e;
                return HitTestResultBehavior.Stop;
            }

            return HitTestResultBehavior.Continue;
        }, new PointHitTestParameters(titikDiJendela));

        target ??= w;
        var waktu = Environment.TickCount;
        var preview = new MouseWheelEventArgs(Mouse.PrimaryDevice, waktu, delta) { RoutedEvent = UIElement.PreviewMouseWheelEvent, Source = target };
        target.RaiseEvent(preview);
        if (!preview.Handled)
        {
            var bubble = new MouseWheelEventArgs(Mouse.PrimaryDevice, waktu, delta) { RoutedEvent = UIElement.MouseWheelEvent, Source = target };
            target.RaiseEvent(bubble);
        }

        return target;
    }

    private static Point TitikTengah(FrameworkElement e, Window w)
    {
        var kotak = e.TransformToAncestor(w).TransformBounds(new Rect(0, 0, e.ActualWidth, e.ActualHeight));
        return new Point(kotak.Left + kotak.Width / 2, kotak.Top + kotak.Height / 2);
    }

    private static ScrollViewer PenggulirHalaman(Window w, TabItem tab)
    {
        var isi = (DependencyObject)tab.Content;
        return isi as ScrollViewer ?? Semua<ScrollViewer>(isi).First();
    }

    // Titik-titik uji per tab: kosong (celah antar kartu), kolom isian, tabel, kartu.
    private static List<(string Nama, Point Titik)> TitikUji(Window w, ScrollViewer sv)
    {
        var titik = new List<(string, Point)>();

        var kartu = Semua<Border>(sv).Where(b => b.Background is SolidColorBrush && b.ActualHeight > 60 && b.ActualWidth > 300).ToList();
        if (kartu.Count > 0)
        {
            var k = kartu[0];
            var kotak = k.TransformToAncestor(w).TransformBounds(new Rect(0, 0, k.ActualWidth, k.ActualHeight));
            titik.Add(("kartu (judul/teks)", new Point(kotak.Left + 30, kotak.Top + 12)));
            titik.Add(("celah antar kartu", new Point(kotak.Left + kotak.Width / 2, kotak.Bottom + 6)));
        }

        var kolom = Semua<TextBox>(sv).FirstOrDefault(t => t.IsVisible && t.ActualHeight > 0);
        if (kolom is not null)
        {
            titik.Add(("kolom isian (TextBox)", TitikTengah(kolom, w)));
        }

        // Setiap DataGrid diuji pada baris pertamanya (di dalam viewport bila sedang terlihat;
        // bila di bawah, halaman digulir dulu supaya titiknya benar-benar di dalam tampilan).
        foreach (var (tabel, indeks) in Semua<DataGrid>(sv).Where(t => t.IsVisible && t.ActualHeight > 60).Select((t, i) => (t, i)))
        {
            var svKotak0 = sv.TransformToAncestor(w).TransformBounds(new Rect(0, 0, sv.ActualWidth, sv.ActualHeight));
            var g = tabel.TransformToAncestor(w).TransformBounds(new Rect(0, 0, tabel.ActualWidth, tabel.ActualHeight));
            var y = Math.Min(g.Top + 60, svKotak0.Bottom - 10);
            if (y > g.Top + 5 && y < svKotak0.Bottom)
            {
                titik.Add(($"tabel #{indeks + 1} (DataGrid)", new Point(g.Left + g.Width / 2, y)));
            }
        }

        // tepi kiri sekali: margin StackPanel (di dalam ScrollViewer, di luar kartu)
        var svKotak = sv.TransformToAncestor(w).TransformBounds(new Rect(0, 0, sv.ActualWidth, sv.ActualHeight));
        titik.Add(("margin halaman kiri", new Point(svKotak.Left + 6, svKotak.Top + svKotak.Height / 2)));
        return titik;
    }

    [Fact]
    public void Pengaturan_RodaMouse_MenggulirHalamanDiSemuaAreaDanSeluruhIsiTerjangkau() => StaHost.Jalankan(() =>
    {
        var (w, db) = Buka(1366, 728);
        using var _ = db;
        var tabControl = Semua<TabControl>(w).First(t => t.Items.OfType<TabItem>().Any(i => i.Header as string is "Jadwal Harian" or "Umum" or "Akun"));
        var galat = new List<string>();

        for (var i = 0; i < tabControl.Items.Count; i++)
        {
            tabControl.SelectedIndex = i;
            Pompa(8);
            w.UpdateLayout();
            var tab = (TabItem)tabControl.Items[i];
            if (tab.Visibility != Visibility.Visible)
            {
                continue;
            }

            var sv = PenggulirHalaman(w, tab);
            if (sv.ScrollableHeight < 1)
            {
                continue; // tab ini muat tanpa gulir
            }

            foreach (var (nama, titik) in TitikUji(w, sv))
            {
                sv.ScrollToTop();
                Pompa(2);
                var sebelum = sv.VerticalOffset;
                var target = KirimRodaDiTitik(w, titik, -120);
                Pompa(2);
                if (sv.VerticalOffset <= sebelum + 0.5)
                {
                    galat.Add($"tab '{tab.Header}' / {nama}: roda TIDAK menggulir (target hit-test={target.GetType().Name}, offset {sebelum:F0}->{sv.VerticalOffset:F0}, ScrollableHeight={sv.ScrollableHeight:F0})");
                }
            }

            // seluruh isi terjangkau: gulir sampai dasar lewat roda di area kosong, lalu tombol terakhir terlihat
            sv.ScrollToTop();
            Pompa(2);
            var titikKosong = TitikUji(w, sv).First(t => t.Nama == "margin halaman kiri").Titik;
            for (var n = 0; n < 200 && sv.VerticalOffset < sv.ScrollableHeight - 0.5; n++)
            {
                KirimRodaDiTitik(w, titikKosong, -120);
                w.UpdateLayout(); // offset ScrollViewer baru berubah setelah layout
            }

            if (sv.VerticalOffset < sv.ScrollableHeight - 0.5)
            {
                galat.Add($"tab '{tab.Header}': dasar halaman tidak tercapai lewat roda (offset {sv.VerticalOffset:F0}/{sv.ScrollableHeight:F0})");
            }
        }

        w.Hide();
        Assert.True(galat.Count == 0, "Masalah scroll Pengaturan:\n" + string.Join("\n", galat));
    });
}
