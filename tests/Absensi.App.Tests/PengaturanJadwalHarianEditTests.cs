using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Threading;
using Absensi.App.Pengaturan;
using Absensi.Data.Backup;
using Absensi.Data.Repositori;
using Absensi.Domain.Akun;

namespace Absensi.App.Tests;

// UAT 2026-09-23: pesan "Jadwal harian tersimpan." muncul, tapi jadwal_hari
// UAT tetap NULL untuk semua hari sekolah. Akar masalah TERBUKTI lewat
// Window WPF sungguhan: TabelStyle (GayaAplikasi.xaml, dipakai SEMUA
// DataGrid) menetapkan IsReadOnly=True di tingkat grid — nilai itu
// MENGALAHKAN IsReadOnly per-kolom manapun, jadi checkbox "Hari sekolah"
// dan keempat kolom jam TIDAK PERNAH bisa masuk mode edit sama sekali,
// mouse maupun keyboard, meski bindingnya (UpdateSourceTrigger=
// PropertyChanged) sendiri sudah benar sejak awal. Diperbaiki dengan
// IsReadOnly="False" HANYA pada elemen DataGrid Jadwal Harian
// (PengaturanView.xaml) — TabelStyle (gaya global) dan DataGrid Backup
// (sengaja tetap read-only, daftar riwayat) TIDAK disentuh.
//
// Test ini memakai Window WPF sungguhan + DataGrid.BeginEdit/CommitEdit
// sungguhan (bukan menyetel properti ViewModel langsung dari C#, yang
// tidak akan pernah menangkap bug IsReadOnly ini) — pola konsisten dengan
// PengaturanViewLayoutTests.cs dan RegistrasiKartuWindowTests.cs untuk
// perilaku WPF yang tidak bisa dibuktikan tanpa Window nyata.
public sealed class PengaturanJadwalHarianEditTests
{
    private static readonly DateTime Waktu = new(2026, 9, 23, 8, 0, 0);
    private static readonly AkunTersimpan Kepsek = new(1, "kepsek", "x", PeranAkun.KepalaSekolah, null, true);

    private static LayananBackup BuatLayananBackup() => new(
        System.IO.Path.Combine(System.IO.Path.GetTempPath(), $"absensi-test-backup-{Guid.NewGuid():N}"),
        new PenyimpanCloudBackupLokal(System.IO.Path.Combine(System.IO.Path.GetTempPath(), $"absensi-test-cloud-{Guid.NewGuid():N}")));

    [Fact]
    public void PengaturanView_GridJadwalHarian_TidakReadOnlyDiTingkatGrid() => StaHost.Jalankan(() =>
    {
        using var database = new MigratedDatabase();
        database.SeedKelasUntukRegistrasiKartu();
        var viewModel = new PengaturanViewModel(database.Connection, Kepsek, new PenyediaWaktuUji(Waktu), tahunAjaranId: 1, BuatLayananBackup());
        var view = new PengaturanView { DataContext = viewModel };

        var window = new Window { Content = view, Width = 1366, Height = 768, ShowInTaskbar = false };
        try
        {
            window.Show();
            PompaDispatcher(5);

            var tabelJadwal = CariTabelJadwalHarian(view);
            Assert.NotNull(tabelJadwal);
            Assert.False(tabelJadwal!.IsReadOnly, "DataGrid Jadwal Harian masih IsReadOnly=True — checkbox dan kolom jam tidak akan pernah bisa diedit.");

            // 2026-09-24: Backup kini tab sendiri — pilih dulu supaya isinya terbentuk.
            var tabControl = (TabControl)LogicalTreeHelper.GetChildren(view).OfType<DockPanel>().Single().Children.OfType<TabControl>().Single();
            tabControl.SelectedItem = tabControl.Items.OfType<TabItem>().Single(t => t.Header as string == "Backup");
            PompaDispatcher(5);

            var tabelBackup = CariTabelBackup(view);
            Assert.NotNull(tabelBackup);
            Assert.True(tabelBackup!.IsReadOnly, "DataGrid Backup seharusnya TETAP read-only (daftar riwayat) — jangan ikut dibuka.");
        }
        finally
        {
            window.Close();
        }
    });

    [Fact]
    public void PengaturanView_KetikDiSelJamMasuk_ViewModelIkutBerubah() => StaHost.Jalankan(() =>
    {
        using var database = new MigratedDatabase();
        database.SeedKelasUntukRegistrasiKartu();
        var viewModel = new PengaturanViewModel(database.Connection, Kepsek, new PenyediaWaktuUji(Waktu), tahunAjaranId: 1, BuatLayananBackup());
        var view = new PengaturanView { DataContext = viewModel };

        var window = new Window { Content = view, Width = 1366, Height = 768, ShowInTaskbar = false };
        try
        {
            window.Show();
            PompaDispatcher(5);

            var tabelJadwal = CariTabelJadwalHarian(view);
            Assert.NotNull(tabelJadwal);

            var kolomJamMasuk = tabelJadwal!.Columns.Single(k => k.Header?.ToString() == "Jam masuk");
            var baris = viewModel.JadwalHarian[0]; // Senin

            // BeginEdit/CommitEdit SUNGGUHAN pada grid (bukan menyetel
            // properti ViewModel langsung) — ini yang tidak mungkin
            // berjalan selama IsReadOnly=True di tingkat grid.
            tabelJadwal.CurrentCell = new DataGridCellInfo(baris, kolomJamMasuk);
            tabelJadwal.Focus();
            var mulaiEdit = tabelJadwal.BeginEdit();
            PompaDispatcher(5);
            Assert.True(mulaiEdit, "DataGrid.BeginEdit() menolak masuk mode edit — sel masih terkunci.");

            var kotakTeks = CariKotakTeksSelSaatIni(tabelJadwal);
            Assert.NotNull(kotakTeks);
            kotakTeks!.Text = "07:30";

            tabelJadwal.CommitEdit(DataGridEditingUnit.Cell, true);
            tabelJadwal.CommitEdit(DataGridEditingUnit.Row, true);
            PompaDispatcher(5);

            Assert.Equal("07:30", baris.JamMasuk);
        }
        finally
        {
            window.Close();
        }
    });

    [Fact]
    public void PengaturanView_ToggleCheckboxHariSekolah_ViewModelIkutBerubah() => StaHost.Jalankan(() =>
    {
        using var database = new MigratedDatabase();
        database.SeedKelasUntukRegistrasiKartu();
        var viewModel = new PengaturanViewModel(database.Connection, Kepsek, new PenyediaWaktuUji(Waktu), tahunAjaranId: 1, BuatLayananBackup());
        var view = new PengaturanView { DataContext = viewModel };

        var window = new Window { Content = view, Width = 1366, Height = 768, ShowInTaskbar = false };
        try
        {
            window.Show();
            PompaDispatcher(5);

            var tabelJadwal = CariTabelJadwalHarian(view);
            Assert.NotNull(tabelJadwal);

            var baris = viewModel.JadwalHarian[6]; // Minggu, bawaan HariSekolah=false
            Assert.False(baris.HariSekolah);

            var kolomHariSekolah = tabelJadwal!.Columns.Single(k => k.Header?.ToString() == "Hari sekolah");
            tabelJadwal.CurrentCell = new DataGridCellInfo(baris, kolomHariSekolah);
            tabelJadwal.Focus();
            var mulaiEdit = tabelJadwal.BeginEdit();
            PompaDispatcher(5);
            Assert.True(mulaiEdit, "DataGrid.BeginEdit() menolak masuk mode edit pada kolom Hari sekolah — sel masih terkunci.");

            var kotakCentang = CariKotakCentangSelSaatIni(tabelJadwal);
            Assert.NotNull(kotakCentang);
            kotakCentang!.IsChecked = true;

            tabelJadwal.CommitEdit(DataGridEditingUnit.Cell, true);
            tabelJadwal.CommitEdit(DataGridEditingUnit.Row, true);
            PompaDispatcher(5);

            Assert.True(baris.HariSekolah);
        }
        finally
        {
            window.Close();
        }
    });

    private static DataGrid? CariTabelJadwalHarian(DependencyObject akar) =>
        CariSemuaKeturunan<DataGrid>(akar).FirstOrDefault(dg => dg.Columns.Count > 0 && dg.Columns[0].Header?.ToString() == "Hari");

    private static DataGrid? CariTabelBackup(DependencyObject akar) =>
        CariSemuaKeturunan<DataGrid>(akar).FirstOrDefault(dg => dg.Columns.Count > 0 && dg.Columns[0].Header?.ToString() == "Berkas");

    private static TextBox? CariKotakTeksSelSaatIni(DataGrid tabel)
    {
        var baris = (DataGridRow?)tabel.ItemContainerGenerator.ContainerFromItem(tabel.CurrentCell.Item);
        if (baris is null)
        {
            return null;
        }

        return CariSemuaKeturunan<TextBox>(baris).FirstOrDefault();
    }

    private static CheckBox? CariKotakCentangSelSaatIni(DataGrid tabel)
    {
        var baris = (DataGridRow?)tabel.ItemContainerGenerator.ContainerFromItem(tabel.CurrentCell.Item);
        if (baris is null)
        {
            return null;
        }

        return CariSemuaKeturunan<CheckBox>(baris).FirstOrDefault();
    }

    private static IEnumerable<T> CariSemuaKeturunan<T>(DependencyObject akar) where T : DependencyObject
    {
        for (var i = 0; i < VisualTreeHelper.GetChildrenCount(akar); i++)
        {
            var anak = VisualTreeHelper.GetChild(akar, i);
            if (anak is T ditemukan)
            {
                yield return ditemukan;
            }

            foreach (var hasil in CariSemuaKeturunan<T>(anak))
            {
                yield return hasil;
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
