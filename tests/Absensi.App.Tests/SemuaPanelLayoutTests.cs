using System.IO;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using Absensi.App.Shell;
using Absensi.Data.Backup;
using Absensi.Data.Repositori;
using Absensi.Domain.Akun;

namespace Absensi.App.Tests;

// Audit panel 2026-09-24: setiap menu dibuka di MainWindow SUNGGUHAN pada 1366x728
// dan 1280x720 (ukuran terkecil), dengan data terisi (nama panjang) maupun kosong.
// Tombol di luar tabel harus terlihat penuh di dalam jendela — tidak terpotong dan
// tidak tersembunyi di bawah lipatan. Bila ABSENSI_AUDIT_DIR diisi, PNG tiap menu
// dan daftar tombol juga ditulis ke sana untuk ditinjau manual. Database uji
// terpisah; tidak menyentuh database UAT.
public sealed class SemuaPanelLayoutTests
{
    private static readonly DateTime Waktu = new(2026, 9, 24, 8, 0, 0);
    private static readonly AkunTersimpan Kepsek = new(1, "kepsek", "x", PeranAkun.KepalaSekolah, null, true);

    private static MigratedDatabase Isi(bool kosong)
    {
        var d = new MigratedDatabase();
        d.Execute("""
            INSERT INTO tahun_ajaran (id, nama, semester, tgl_mulai, tgl_selesai, aktif) VALUES (1, '2026/2027', 'ganjil', '2026-07-01', '2026-12-19', 1);
            INSERT INTO akun (id, username, sandi_hash, peran, guru_id, aktif, dibuat_pada) VALUES (1, 'kepsek', 'x', 'kepala_sekolah', NULL, 1, '2026-09-01T00:00:00');
            INSERT INTO jadwal_hari (tahun_ajaran_id, hari, hari_sekolah, jam_masuk, jam_rekap, batas_tap_masuk, mulai_pulang) VALUES
              (1,1,1,'07:30','08:00','12:00','13:10'),(1,2,1,'07:30','08:00','12:00','13:10'),(1,3,1,'07:30','08:00','12:00','13:10'),
              (1,4,1,'07:30','08:00','12:00','13:10'),(1,5,1,'07:30','08:00','10:00','11:30'),(1,6,1,'07:30','08:00','12:00','13:10'),(1,7,0,NULL,NULL,NULL,NULL);
            """);
        if (kosong)
        {
            return d;
        }

        var sql = new StringBuilder();
        sql.AppendLine("INSERT INTO guru (id, nip, nama, pangkat, jabatan, aktif) VALUES (1, '198706152012011001', 'Dewi Lestari, S.Pd.', 'Penata', 'Wali Kelas 7A', 1);");
        sql.AppendLine("INSERT INTO guru (id, nip, nama, pangkat, jabatan, aktif) VALUES (2, '197001011995031002', 'Drs. H. Muhammad Abdul Rahman Hakim, M.Pd., M.Si., Dr. (HC)', 'Pembina', 'Wakil Kepala Sekolah', 1);");
        sql.AppendLine("INSERT INTO guru (id, nip, nama, aktif, alasan_nonaktif) VALUES (3, '196501011990031001', 'Budi Santoso, S.Pd.', 0, 'pensiun');");
        sql.AppendLine("INSERT INTO kelas (id, tahun_ajaran_id, nama, tingkat, wali_guru_id) VALUES (1, 1, '7A', 7, 1);");
        sql.AppendLine("INSERT INTO kelas (id, tahun_ajaran_id, nama, tingkat, wali_guru_id) VALUES (2, 1, '7B Melati Harum Sekali', 7, 2);");
        sql.AppendLine("INSERT INTO kelas (id, tahun_ajaran_id, nama, tingkat, wali_guru_id) VALUES (3, 1, '8A', 8, NULL);");
        for (var i = 1; i <= 24; i++)
        {
            var nama = i == 5 ? "Muhammad Rizky Ramadhan Putra Pratama Wijaya Kusuma Negara" : $"Siswa Uji Nomor {i:00}";
            sql.AppendLine($"INSERT INTO siswa (id, nisn, nama, jenis_kelamin, aktif) VALUES ({i}, '00{i:00000000}', '{nama}', '{(i % 2 == 0 ? "L" : "P")}', 1);");
            sql.AppendLine($"INSERT INTO siswa_kelas (siswa_id, kelas_id, tahun_ajaran_id) VALUES ({i}, {(i % 3) + 1}, 1);");
            if (i % 2 == 0)
            {
                sql.AppendLine($"INSERT INTO kartu (uid, pemilik_tipe, pemilik_id, aktif, didaftar_pada) VALUES ('000{i:0000000}', 'siswa', {i}, 1, '2026-09-01T00:00:00');");
            }

            sql.AppendLine($"INSERT INTO absensi_siswa (tanggal, siswa_id, kelas_id, status) VALUES ('2026-09-24', {i}, {(i % 3) + 1}, 'alpa');");
        }

        d.Execute(sql.ToString());
        return d;
    }

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

    private static bool DalamTabel(DependencyObject d)
    {
        for (var p = VisualTreeHelper.GetParent(d); p is not null; p = VisualTreeHelper.GetParent(p))
        {
            if (p is DataGridCell or DataGridRow)
            {
                return true;
            }
        }

        return false;
    }

    [Theory]
    [InlineData(1366, 728, false)]
    [InlineData(1366, 728, true)]
    [InlineData(1280, 720, false)]
    public void SemuaTombolDiLuarTabelTerlihatDiDalamJendela(int lebar, int tinggi, bool kosong) => StaHost.Jalankan(() =>
    {
        var dir = Environment.GetEnvironmentVariable("ABSENSI_AUDIT_DIR");
        var tulis = !string.IsNullOrWhiteSpace(dir);
        if (tulis)
        {
            Directory.CreateDirectory(dir!);
        }

        var terpotong = new List<string>();
        using var db = Isi(kosong);
        var vm = new MainWindowViewModel(db.Connection, Kepsek, new PenyediaWaktuUji(Waktu), new StatusModeRegistrasi(), "SMP Uji", null, "2026/2027", 1,
            new LayananBackup(Path.Combine(Path.GetTempPath(), "b" + Guid.NewGuid().ToString("N")), new PenyimpanCloudBackupLokal(Path.Combine(Path.GetTempPath(), "c" + Guid.NewGuid().ToString("N")))));
        var w = new MainWindow(vm, () => new Window(), () => null, _ => { })
        {
            ShowInTaskbar = false, WindowState = WindowState.Normal, WindowStartupLocation = WindowStartupLocation.Manual,
            Left = -6000, Top = 0, Width = lebar, Height = tinggi,
        };
        w.Show();
        Pompa(8);

        var laporan = new StringBuilder();
        foreach (var menu in vm.MenuItems.ToList())
        {
            vm.MenuAktif = menu;
            Pompa(10);
            w.UpdateLayout();

            var nama = $"{lebar}x{tinggi}_{(kosong ? "kosong" : "isi")}_{menu}";
            if (tulis)
            {
                var akar = (Visual)VisualTreeHelper.GetChild(w, 0);
                var bmp = new RenderTargetBitmap((int)w.ActualWidth, (int)w.ActualHeight, 96, 96, PixelFormats.Pbgra32);
                bmp.Render(akar);
                var enc = new PngBitmapEncoder();
                enc.Frames.Add(BitmapFrame.Create(bmp));
                using var fs = File.Create(Path.Combine(dir!, nama + ".png"));
                enc.Save(fs);
            }

            laporan.AppendLine($"=== {nama} (jendela {w.ActualWidth:F0}x{w.ActualHeight:F0})");
            foreach (var b in Semua<Button>(w))
            {
                if (b.Content is not string teks || string.IsNullOrWhiteSpace(teks))
                {
                    continue;
                }

                var kotak = b.TransformToAncestor(w).TransformBounds(new Rect(0, 0, b.ActualWidth, b.ActualHeight));
                var dalam = kotak.Left >= 0 && kotak.Top >= 0 && kotak.Right <= w.ActualWidth + 0.5 && kotak.Bottom <= w.ActualHeight + 0.5;
                b.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
                var teksMuat = b.DesiredSize.Width <= b.ActualWidth + 0.5;
                // Tombol di dalam baris tabel boleh di bawah lipatan (tabel punya scroll sendiri).
                if (b.IsVisible && !dalam && b.TemplatedParent is null && !DalamTabel(b))
                {
                    terpotong.Add($"{nama}: '{teks}' @({kotak.Left:F0},{kotak.Top:F0}) {kotak.Width:F0}x{kotak.Height:F0}");
                }

                laporan.AppendLine($"  [{(b.IsVisible ? "V" : "-")}{(b.IsEnabled ? "E" : "-")}{(dalam ? "D" : "X")}{(teksMuat ? "T" : "x")}] '{teks}' @({kotak.Left:F0},{kotak.Top:F0}) {kotak.Width:F0}x{kotak.Height:F0}");
            }

            foreach (var sv in Semua<ScrollViewer>(w).Where(s => s.ScrollableHeight > 0.5 || s.ScrollableWidth > 0.5))
            {
                laporan.AppendLine($"  scroll: {sv.Name} tinggiIsi={sv.ExtentHeight:F0} viewport={sv.ViewportHeight:F0} scrollH={sv.ScrollableHeight:F0} scrollW={sv.ScrollableWidth:F0} vs={sv.VerticalScrollBarVisibility} parent={sv.TemplatedParent?.GetType().Name}");
            }
        }

        if (tulis)
        {
            File.WriteAllText(Path.Combine(dir!, $"laporan_{lebar}x{tinggi}_{(kosong ? "kosong" : "isi")}.txt"), laporan.ToString());
        }

        w.Hide();
        Assert.True(terpotong.Count == 0, "Tombol terpotong/di luar jendela:" + Environment.NewLine + string.Join(Environment.NewLine, terpotong));
    });
}
