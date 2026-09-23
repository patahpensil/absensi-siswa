using System.IO;
using System.Windows;
using System.Windows.Automation.Peers;
using System.Windows.Automation.Provider;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using Absensi.App.Converters;
using Absensi.App.MasterData;
using Absensi.App.Shell;
using Absensi.Data.Akun;
using Absensi.Data.Backup;
using Absensi.Data.Repositori;
using Absensi.Domain.Akun;

namespace Absensi.App.Tests;

// Rapikan Panel Guru -> Data Guru (2026-09-24), mengikuti pola Panel Siswa
// (A-35): kolom Kartu = UID sebenarnya / "Belum terdaftar", kolom TG
// dihapus dari tampilan (TertautTelegram TETAP untuk menu Notifikasi),
// kolom Status = teks (bukan Boolean mentah), tombol Impor foto dihapus,
// formulir jadi bilah di atas tabel, dan seluruh aksi muat utuh di
// 1366x768 tanpa scroll horizontal.
//
// Tes tata letak memakai MainWindow SUNGGUHAN (Show(), siklus Measure/
// Arrange penuh, menu "Guru" dipilih lewat MainWindowViewModel — jalur yang
// sama dengan aplikasi). Window TIDAK di-Close() (MainWindow.Closed mematikan
// Application; lihat komentar StaHost). Bila variabel lingkungan
// ABSENSI_SCREENSHOT_DIR diisi, tes juga menyimpan PNG render untuk review.
public sealed class GuruPanelTests
{
    private static readonly DateTime Waktu = new(2026, 9, 24, 8, 0, 0);
    private static readonly AkunTersimpan Admin = new(1, "admin1", "x", PeranAkun.Admin, null, true);
    private static readonly AkunTersimpan WaliKelas = new(2, "wali1", "x", PeranAkun.WaliKelas, 1, true);

    // ---------- data uji ----------

    private static MigratedDatabase BuatDatabase()
    {
        var database = new MigratedDatabase();
        database.SeedSekolahUntukGerbang(); // guru 1 "Guru Satu" (NIP 111) + kartu UID-GURU-1
        database.TambahAkun(1, "admin1", "admin", guruId: null);
        database.Execute("""
            INSERT INTO guru (id, nip, nama, pangkat, jabatan, aktif) VALUES (2, '198706152012013006', 'Sumitro, S.Pd', 'Penata Muda', 'Guru Mapel', 1);
            INSERT INTO guru (id, nip, nama, pangkat, jabatan, aktif) VALUES (3, '198706152012011001', 'Dewi Lestari, S.Pd.', 'Penata', 'Wali Kelas 7A', 1);
            INSERT INTO guru (id, nip, nama, pangkat, jabatan, aktif) VALUES (4, '197001011995031002', 'Drs. H. Muhammad Abdul Rahman Hakim, M.Pd.', 'Pembina', 'Wakil Kepala Sekolah', 1);
            INSERT INTO guru (id, nip, nama, aktif, alasan_nonaktif) VALUES (5, '196501011990031001', 'Budi Santoso, S.Pd.', 0, 'pensiun');
            INSERT INTO guru (id, nip, nama, aktif, alasan_nonaktif) VALUES (6, '198001011999031005', 'Rina Marlina, S.Pd.', 0, 'pindah');
            INSERT INTO kartu (uid, pemilik_tipe, pemilik_id, aktif, didaftar_pada) VALUES ('0001340274', 'guru', 3, 1, '2026-09-01T00:00:00');
            INSERT INTO kartu (uid, pemilik_tipe, pemilik_id, aktif, didaftar_pada) VALUES ('0001340999', 'guru', 4, 1, '2026-09-01T00:00:00');
            INSERT INTO kartu (uid, pemilik_tipe, pemilik_id, aktif, didaftar_pada) VALUES ('0001300001', 'guru', 5, 0, '2026-08-01T00:00:00');
            """);
        return database;
    }

    private static GuruViewModel BuatViewModel(MigratedDatabase database, AkunTersimpan? akun = null) =>
        new(database.Connection, akun ?? Admin, new PenyediaWaktuUji(Waktu), new StatusModeRegistrasi(), tahunAjaranId: 1);

    private static LayananBackup BuatLayananBackup() => new(
        Path.Combine(Path.GetTempPath(), $"absensi-test-backup-{Guid.NewGuid():N}"),
        new PenyimpanCloudBackupLokal(Path.Combine(Path.GetTempPath(), $"absensi-test-cloud-{Guid.NewGuid():N}")));

    // ---------- Kartu / TG / Status: data ----------

    [Fact]
    public void DaftarLengkap_KartuUidTerisiHanyaUntukKartuAktif_TertautTelegramTetapAda()
    {
        using var database = BuatDatabase();
        database.Execute("INSERT INTO pairing (subjek_tipe, subjek_id, token, chat_id, status, dibuat_pada) VALUES ('guru', 2, 'tok-2', '555', 'tertaut', '2026-09-01T00:00:00');");

        var daftar = new RepositoriGuru().DaftarLengkap(database.Connection).ToDictionary(g => g.Id);

        Assert.Equal("UID-GURU-1", daftar[1].KartuUid);
        Assert.True(daftar[1].PunyaKartu);
        Assert.Equal("0001340274", daftar[3].KartuUid);
        Assert.Null(daftar[2].KartuUid);
        Assert.False(daftar[2].PunyaKartu);
        // Kartu nonaktif (guru 5, alasan hilang/ganti) TIDAK dihitung.
        Assert.Null(daftar[5].KartuUid);
        Assert.False(daftar[5].PunyaKartu);

        // TG dihapus dari TAMPILAN saja: data pairing tetap terbaca (dipakai
        // menu Notifikasi, PRD §16.15 langkah 7).
        Assert.True(daftar[2].TertautTelegram);
        Assert.False(daftar[1].TertautTelegram);
    }

    [Fact]
    public void StatusAktifKeTeks_TeksBacaTanpaBooleanMentah()
    {
        var konversi = new StatusAktifKeTeksConverter();
        string Teks(bool aktif, string? alasan) =>
            (string)konversi.Convert(new object?[] { aktif, alasan }!, typeof(string), null!, System.Globalization.CultureInfo.GetCultureInfo("id-ID"));

        Assert.Equal("Aktif", Teks(true, null));
        Assert.Equal("Aktif", Teks(true, "pensiun")); // alasan basi diabaikan untuk yang aktif
        Assert.Equal("Pensiun", Teks(false, "pensiun"));
        Assert.Equal("Pindah", Teks(false, "pindah"));
        Assert.Equal("Nonaktif", Teks(false, null));
        Assert.Equal("Nonaktif", Teks(false, "  "));
        Assert.DoesNotContain("True", Teks(true, null));
        Assert.DoesNotContain("False", Teks(false, null));
    }

    [Fact]
    public void KartuUidKeTeks_UidAtauBelumTerdaftar()
    {
        var konversi = new KartuUidKeTeksConverter();
        var budaya = System.Globalization.CultureInfo.InvariantCulture;

        Assert.Equal("0001340274", konversi.Convert("0001340274", typeof(string), null, budaya));
        Assert.Equal("Belum terdaftar", konversi.Convert(null, typeof(string), null, budaya));
        Assert.Equal("Belum terdaftar", konversi.Convert("", typeof(string), null, budaya));
    }

    // ---------- ViewModel: tombol berfungsi ----------

    [Fact]
    public void ImporFoto_TidakAdaLagiDiViewModel()
    {
        Assert.Null(typeof(GuruViewModel).GetProperty("ImporFotoCommand"));
    }

    [Fact]
    public void Ubah_MemuatPangkatJabatan_SimpanTidakMenimpaDenganNull()
    {
        using var database = BuatDatabase();
        var viewModel = BuatViewModel(database);
        var sumitro = viewModel.Daftar.Single(g => g.Id == 2);

        viewModel.EditCommand.Execute(sumitro);
        Assert.Equal("Penata Muda", viewModel.Pangkat);
        Assert.Equal("Guru Mapel", viewModel.Jabatan);
        Assert.True(viewModel.NipHanyaBaca);

        viewModel.Nama = "Sumitro Baru, S.Pd";
        viewModel.SimpanCommand.Execute(null);

        var sesudah = new RepositoriGuru().DaftarLengkap(database.Connection).Single(g => g.Id == 2);
        Assert.Equal("Sumitro Baru, S.Pd", sesudah.Nama);
        Assert.Equal("Penata Muda", sesudah.Pangkat);
        Assert.Equal("Guru Mapel", sesudah.Jabatan);
        Assert.Equal("198706152012013006", sesudah.Nip);
        Assert.False(viewModel.NipHanyaBaca);
        Assert.Equal("Guru Sumitro Baru, S.Pd tersimpan.", viewModel.PesanStatus);
    }

    [Fact]
    public void Ubah_NipTidakBisaDiubah_PerubahanNipTidakDiamDiamDiabaikanKarenaFieldHanyaBaca()
    {
        using var database = BuatDatabase();
        var viewModel = BuatViewModel(database);

        viewModel.EditCommand.Execute(viewModel.Daftar.Single(g => g.Id == 2));
        Assert.True(viewModel.NipHanyaBaca);

        viewModel.BatalEditCommand.Execute(null);
        Assert.False(viewModel.NipHanyaBaca);
        Assert.Equal(string.Empty, viewModel.Nip);
        Assert.Null(viewModel.Pangkat);
        Assert.Null(viewModel.Jabatan);
    }

    [Fact]
    public void Tambah_NipKembar_PesanJelasDanTidakLolosKeGalatGlobal()
    {
        using var database = BuatDatabase();
        var viewModel = BuatViewModel(database);
        var jumlahAwal = viewModel.Daftar.Count;

        viewModel.Nip = "  198706152012013006 ";
        viewModel.Nama = "Guru Kembar";
        viewModel.SimpanCommand.Execute(null);

        Assert.Equal("NIP 198706152012013006 sudah dipakai guru lain.", viewModel.PesanGalat);
        Assert.Equal(jumlahAwal, viewModel.Daftar.Count);
        Assert.DoesNotContain(viewModel.Daftar, g => g.Nama == "Guru Kembar");
    }

    [Fact]
    public void Tambah_Berhasil_TrimDanPesanBerhasil_NipTetapTeksDenganNolDiDepan()
    {
        using var database = BuatDatabase();
        var viewModel = BuatViewModel(database);

        viewModel.Nip = " 0012345678 ";
        viewModel.Nama = "  Guru Baru ";
        viewModel.Pangkat = "  ";
        viewModel.SimpanCommand.Execute(null);

        var baru = new RepositoriGuru().DaftarLengkap(database.Connection).Single(g => g.Nama == "Guru Baru");
        Assert.Equal("0012345678", baru.Nip);
        Assert.Null(baru.Pangkat);
        Assert.Equal("Guru Guru Baru tersimpan.", viewModel.PesanStatus);
        Assert.Null(viewModel.PesanGalat);
    }

    [Fact]
    public void Nonaktifkan_HanyaUntukGuruAktif_KartuHanyaUntukAktifDanBukanWaliKelas()
    {
        using var database = BuatDatabase();
        var admin = BuatViewModel(database);
        var wali = BuatViewModel(database, WaliKelas);
        var aktif = admin.Daftar.Single(g => g.Id == 2);
        var pensiun = admin.Daftar.Single(g => g.Id == 5);

        Assert.True(admin.NonaktifkanCommand.CanExecute(aktif));
        Assert.False(admin.NonaktifkanCommand.CanExecute(pensiun));

        Assert.True(admin.KartuCommand.CanExecute(aktif));
        Assert.False(admin.KartuCommand.CanExecute(pensiun));
        Assert.False(wali.KartuCommand.CanExecute(aktif)); // PRD §17.10

        // CanExecute(null) transien dari WPF sebelum CommandParameter terikat
        // (Tahap E-2): tidak boleh NullReferenceException.
        Assert.False(admin.NonaktifkanCommand.CanExecute(null));
        Assert.False(admin.KartuCommand.CanExecute(null));

        Assert.True(admin.EditCommand.CanExecute(pensiun));
        Assert.True(admin.UnggahFotoCommand.CanExecute(pensiun));
    }

    private static string BuatBerkasGambarSementara()
    {
        var bitmap = new WriteableBitmap(8, 8, 96, 96, PixelFormats.Bgr32, null);
        var path = Path.Combine(Path.GetTempPath(), $"foto-guru-uji-{Guid.NewGuid():N}.png");
        using var stream = File.Create(path);
        var encoder = new PngBitmapEncoder();
        encoder.Frames.Add(BitmapFrame.Create(bitmap));
        encoder.Save(stream);
        return path;
    }

    [Fact]
    public void UnggahFotoDariBerkas_Berhasil_PesanJelasDanFotoPathTersimpanDiDatabase()
    {
        using var database = BuatDatabase();
        var viewModel = BuatViewModel(database);
        var guru = viewModel.Daftar.Single(g => g.Id == 2);
        var pathSumber = BuatBerkasGambarSementara();
        var pathTujuan = Path.Combine(LokasiData.FolderData, "foto", $"{guru.Nip}.png");

        try
        {
            viewModel.UnggahFotoDariBerkas(guru, pathSumber);

            Assert.Null(viewModel.PesanGalat);
            Assert.Equal($"Foto {guru.Nama} berhasil diunggah.", viewModel.PesanStatus);
            Assert.True(File.Exists(pathTujuan));
            Assert.Equal($"{guru.Nip}.png", new RepositoriGuru().CariFotoPath(database.Connection, guru.Id));
        }
        finally
        {
            File.Delete(pathSumber);
            if (File.Exists(pathTujuan))
            {
                File.Delete(pathTujuan);
            }
        }
    }

    [Fact]
    public void UnggahFotoDariBerkas_BukanGambar_PesanGagalJelas_DatabaseTidakBerubah()
    {
        using var database = BuatDatabase();
        var viewModel = BuatViewModel(database);
        var guru = viewModel.Daftar.Single(g => g.Id == 2);
        var pathSumber = Path.Combine(Path.GetTempPath(), $"foto-guru-rusak-{Guid.NewGuid():N}.jpg");
        File.WriteAllBytes(pathSumber, new byte[] { 1, 2, 3, 4, 5 });
        var sebelum = new RepositoriGuru().CariFotoPath(database.Connection, guru.Id);

        try
        {
            viewModel.UnggahFotoDariBerkas(guru, pathSumber);

            Assert.NotNull(viewModel.PesanGalat);
            Assert.Null(viewModel.PesanStatus);
            Assert.Equal(sebelum, new RepositoriGuru().CariFotoPath(database.Connection, guru.Id));
        }
        finally
        {
            File.Delete(pathSumber);
            var sisa = Path.Combine(LokasiData.FolderData, "foto", $"{guru.Nip}.png");
            if (File.Exists(sisa))
            {
                File.Delete(sisa);
            }
        }
    }

    // ---------- Tata letak di MainWindow sungguhan ----------

    public static TheoryData<int, int> UkuranJendela => new()
    {
        { 1366, 728 },  // 1366x768 dengan taskbar (jendela maksimum)
        { 1366, 768 },  // 1366x768 tanpa taskbar
        { 1280, 720 },  // MinWidth/MinHeight MainWindow (Windows skala 125%)
    };

    private sealed record Baris(DataGridRow Row, GuruDaftar Data, Rect Batas, Dictionary<string, Button> Tombol);

    private static (MainWindow Window, GuruViewModel ViewModel, GuruView View, DataGrid Tabel) BukaPanelGuru(
        MigratedDatabase database, int lebar, int tinggi)
    {
        var mainViewModel = new MainWindowViewModel(
            database.Connection, Admin, new PenyediaWaktuUji(Waktu), new StatusModeRegistrasi(),
            "SMP Uji", null, "2026/2027", 1, BuatLayananBackup());
        var window = new MainWindow(mainViewModel, () => new Window(), () => null, _ => { })
        {
            ShowInTaskbar = false,
            WindowState = WindowState.Normal,
            WindowStartupLocation = WindowStartupLocation.Manual,
            Left = -6000,
            Top = 0,
            Width = lebar,
            Height = tinggi,
        };

        mainViewModel.MenuAktif = "Guru";
        window.Show();
        PompaDispatcher(10);
        window.UpdateLayout();

        var view = Cari<GuruView>(window)
            ?? throw new InvalidOperationException($"GuruView tidak ada di visual tree (KontenAktif={mainViewModel.KontenAktif?.GetType().Name ?? "null"}).");
        var tabel = view.FindName("TabelGuru") as DataGrid
            ?? Semua<DataGrid>(view).FirstOrDefault()
            ?? throw new InvalidOperationException("DataGrid TabelGuru tidak ditemukan.");
        return (window, (GuruViewModel)mainViewModel.KontenAktif!, view, tabel);
    }

    private static List<Baris> AmbilBaris(DataGrid tabel)
    {
        var hasil = new List<Baris>();
        foreach (var row in Semua<DataGridRow>(tabel))
        {
            if (row.Item is not GuruDaftar data)
            {
                continue;
            }

            var tombol = new Dictionary<string, Button>();
            foreach (var b in Semua<Button>(row))
            {
                if (b.IsVisible && b.Content is string teks)
                {
                    tombol[teks] = b;
                }
            }

            hasil.Add(new Baris(row, data, row.TransformToAncestor(tabel).TransformBounds(new Rect(0, 0, row.ActualWidth, row.ActualHeight)), tombol));
        }

        return hasil;
    }

    [Theory]
    [MemberData(nameof(UkuranJendela))]
    public void PanelGuru_SemuaKolomDanTombolAksiUtuhSejajar_TanpaScrollHorizontal(int lebar, int tinggi) => StaHost.Jalankan(() =>
    {
        using var database = BuatDatabase();
        var (window, _, view, tabel) = BukaPanelGuru(database, lebar, tinggi);
        var ukuran = $"{lebar}x{tinggi}";

        Assert.True(window.ActualWidth >= lebar - 1, $"Window hanya {window.ActualWidth}x{window.ActualHeight} — layar uji terlalu kecil.");

        // Kolom: TG hilang, urutan Nama-NIP-Kartu-Status-aksi.
        Assert.Equal(new[] { "Nama", "NIP", "Kartu", "Status", "" }, tabel.Columns.Select(k => k.Header?.ToString() ?? "").ToArray());

        // Tidak ada scroll horizontal yang menyembunyikan Nama.
        var gulir = Semua<ScrollViewer>(tabel).First();
        Assert.True(gulir.ScrollableWidth < 0.5, $"[{ukuran}] tabel butuh scroll horizontal ({gulir.ScrollableWidth:F0} px) — kolom aksi/Nama tersembunyi.");
        Assert.True(tabel.Columns[0].ActualWidth >= 170, $"[{ukuran}] kolom Nama hanya {tabel.Columns[0].ActualWidth:F0} px.");
        Assert.True(tabel.Columns.Sum(k => k.ActualWidth) <= tabel.ActualWidth + 1, $"[{ukuran}] jumlah kolom melebihi lebar tabel.");

        var baris = AmbilBaris(tabel);
        Assert.Equal(6, baris.Count(b => b.Data is not null));

        // Semua tombol utuh di dalam tabel, teksnya tidak terpotong.
        foreach (var b in baris)
        {
            foreach (var (teks, tombol) in b.Tombol)
            {
                var kotak = tombol.TransformToAncestor(tabel).TransformBounds(new Rect(0, 0, tombol.ActualWidth, tombol.ActualHeight));
                Assert.True(kotak.Left >= 0 && kotak.Right <= tabel.ActualWidth + 0.5, $"[{ukuran}] tombol '{teks}' ({b.Data.Nama}) keluar dari tabel (L={kotak.Left:F0}, R={kotak.Right:F0}, lebar tabel {tabel.ActualWidth:F0}).");
                tombol.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
                Assert.True(tombol.DesiredSize.Width <= tombol.ActualWidth + 0.5, $"[{ukuran}] teks tombol '{teks}' terpotong (butuh {tombol.DesiredSize.Width:F0}, ada {tombol.ActualWidth:F0}).");
            }
        }

        window.UpdateLayout();

        // Sejajar antar baris: tiap tombol punya offset horizontal & vertikal yang sama di barisnya.
        foreach (var teks in new[] { "Ubah", "Unggah foto", "Nonaktifkan" })
        {
            var posisi = baris.Where(b => b.Tombol.ContainsKey(teks))
                .Select(b => b.Tombol[teks].TransformToAncestor(b.Row).Transform(new Point(0, 0)))
                .ToList();
            Assert.True(posisi.Count >= 2);
            Assert.True(posisi.All(p => Math.Abs(p.X - posisi[0].X) < 0.5 && Math.Abs(p.Y - posisi[0].Y) < 0.5), $"[{ukuran}] tombol '{teks}' tidak sejajar antar baris.");
        }

        var posisiKartu = baris
            .Select(b => b.Tombol.FirstOrDefault(t => t.Key is "Daftarkan kartu" or "Ganti kartu").Value)
            .Where(t => t is not null)
            .Select((t, i) => t.TransformToAncestor(baris.First(b => b.Tombol.ContainsValue(t)).Row).Transform(new Point(0, 0)))
            .ToList();
        Assert.True(posisiKartu.Count >= 2 && posisiKartu.All(p => Math.Abs(p.X - posisiKartu[0].X) < 0.5 && Math.Abs(p.Y - posisiKartu[0].Y) < 0.5), $"[{ukuran}] tombol kartu tidak sejajar.");

        // Isi sel: UID sebenarnya atau "Belum terdaftar"; tidak ada True/False mentah.
        var teksSel = Semua<TextBlock>(tabel).Select(t => t.Text).ToList();
        Assert.Contains("UID-GURU-1", teksSel);
        Assert.Contains("0001340274", teksSel);
        Assert.Contains("Belum terdaftar", teksSel);
        Assert.DoesNotContain("True", teksSel);
        Assert.DoesNotContain("False", teksSel);
        Assert.DoesNotContain(teksSel, t => t == "TG");
        Assert.Contains("Aktif", teksSel);
        Assert.Contains("Pensiun", teksSel);
        Assert.Contains("Pindah", teksSel);

        // Tombol per status: aktif tanpa kartu = Daftarkan; aktif berkartu = Ganti; nonaktif = hanya Ubah + Unggah foto.
        var sumitro = baris.Single(b => b.Data.Id == 2);
        Assert.Contains("Daftarkan kartu", sumitro.Tombol.Keys);
        Assert.DoesNotContain("Ganti kartu", sumitro.Tombol.Keys);
        var dewi = baris.Single(b => b.Data.Id == 3);
        Assert.Contains("Ganti kartu", dewi.Tombol.Keys);
        Assert.DoesNotContain("Daftarkan kartu", dewi.Tombol.Keys);
        var pensiun = baris.Single(b => b.Data.Id == 5);
        Assert.Equal(new[] { "Ubah", "Unggah foto" }, pensiun.Tombol.Keys.OrderBy(k => k, StringComparer.Ordinal).ToArray());

        // Tombol "Impor foto" (massal) hilang; Unduh Template dan Impor Excel ada.
        var tombolAtas = Semua<Button>(view).Where(b => b.IsVisible && b.Content is string).Select(b => (string)b.Content).ToList();
        Assert.DoesNotContain("Impor foto", tombolAtas);
        Assert.Contains("Unduh Template", tombolAtas);
        Assert.Contains("Impor Excel", tombolAtas);

        // Baris tabel yang terlihat: minimal empat baris utuh di area tabel.
        Assert.True(baris.Count(b => b.Batas.Top >= 0 && b.Batas.Bottom <= tabel.ActualHeight + 1) >= 4, $"[{ukuran}] kurang dari empat baris terlihat utuh (tinggi tabel {tabel.ActualHeight:F0}).");

        Simpan(window, $"guru_{lebar}x{tinggi}_daftar");
        window.Hide();
    });

    [Fact]
    public void PanelGuru_TombolUbahBatalSimpan_BekerjaLewatKlikSungguhan_FormTerlihatUtuh() => StaHost.Jalankan(() =>
    {
        using var database = BuatDatabase();
        var (window, viewModel, view, tabel) = BukaPanelGuru(database, 1366, 728);
        var baris = AmbilBaris(tabel);

        // Ubah (klik lewat ButtonAutomationPeer = jalur klik nyata)
        Klik(baris.Single(b => b.Data.Id == 2).Tombol["Ubah"]);
        PompaDispatcher(6);
        window.UpdateLayout();

        Assert.Equal("Ubah guru Sumitro, S.Pd", viewModel.JudulFormulir);
        Assert.Equal("198706152012013006", viewModel.Nip);
        Assert.Equal("Penata Muda", viewModel.Pangkat);
        var kolomNip = Semua<TextBox>(view).First(t => t.ToolTip is string s && s.StartsWith("NIP tidak bisa diubah"));
        Assert.True(kolomNip.IsReadOnly);

        // Semua field form dan tombol Simpan/Batal terlihat utuh di dalam jendela.
        var tombolFormulir = Semua<Button>(view).Where(b => b.IsVisible && b.Content is "Simpan" or "Batal").ToList();
        Assert.Equal(2, tombolFormulir.Count);
        foreach (var kontrol in Semua<TextBox>(view).Where(t => t.IsVisible).Cast<FrameworkElement>().Concat(tombolFormulir))
        {
            var kotak = kontrol.TransformToAncestor(window).TransformBounds(new Rect(0, 0, kontrol.ActualWidth, kontrol.ActualHeight));
            Assert.True(kotak.Left >= 0 && kotak.Right <= window.ActualWidth && kotak.Bottom <= window.ActualHeight, $"Kontrol formulir keluar dari jendela ({kotak}).");
        }

        Simpan(window, "guru_1366x728_ubah");

        // Simpan (nama diubah) lewat klik
        viewModel.Nama = "Sumitro Klik, S.Pd";
        Klik(Semua<Button>(view).First(b => b.Content is "Simpan"));
        PompaDispatcher(6);
        Assert.Equal("Guru Sumitro Klik, S.Pd tersimpan.", viewModel.PesanStatus);
        Assert.Contains(viewModel.Daftar, g => g.Nama == "Sumitro Klik, S.Pd" && g.Pangkat == "Penata Muda");

        // Ubah lalu Batal lewat klik: form kembali ke "Tambah guru"
        Klik(AmbilBaris(tabel).Single(b => b.Data.Id == 3).Tombol["Ubah"]);
        PompaDispatcher(4);
        Assert.StartsWith("Ubah guru", viewModel.JudulFormulir);
        Klik(Semua<Button>(view).First(b => b.Content is "Batal"));
        PompaDispatcher(4);
        Assert.Equal("Tambah guru", viewModel.JudulFormulir);
        Assert.Equal(string.Empty, viewModel.Nip);

        window.Hide();
    });

    [Fact]
    public void PanelGuru_TombolAksiDialog_TerikatDanAktifUntukAdmin_Nonaktif_UntukWaliKelas() => StaHost.Jalankan(() =>
    {
        using var database = BuatDatabase();
        var (window, viewModel, _, tabel) = BukaPanelGuru(database, 1366, 728);
        var baris = AmbilBaris(tabel);

        // Unggah foto / Daftarkan kartu / Ganti kartu / Nonaktifkan membuka
        // dialog modal (OpenFileDialog, TapKartuTunggalWindow,
        // AlasanTidakAktifWindow) yang tidak bisa dikendalikan di xUnit —
        // di sini dibuktikan tombolnya AKTIF dan terikat ke command yang
        // logikanya diuji di tes ViewModel di atas.
        foreach (var (id, teks) in new[] { (2, "Unggah foto"), (2, "Daftarkan kartu"), (3, "Ganti kartu"), (2, "Nonaktifkan"), (5, "Unggah foto") })
        {
            var tombol = baris.Single(b => b.Data.Id == id).Tombol[teks];
            Assert.True(tombol.IsEnabled, $"Tombol '{teks}' guru {id} tidak aktif untuk admin.");
            Assert.NotNull(tombol.Command);
        }

        Assert.False(viewModel.KartuCommand.CanExecute(baris.Single(b => b.Data.Id == 5).Data));
        window.Hide();
    });

    // ---------- pembantu ----------

    private static void Simpan(Window window, string nama)
    {
        var folder = Environment.GetEnvironmentVariable("ABSENSI_SCREENSHOT_DIR");
        if (string.IsNullOrWhiteSpace(folder))
        {
            return;
        }

        Directory.CreateDirectory(folder);
        window.UpdateLayout();
        var akar = (Visual)VisualTreeHelper.GetChild(window, 0);
        var bitmap = new RenderTargetBitmap((int)window.ActualWidth, (int)window.ActualHeight, 96, 96, PixelFormats.Pbgra32);
        bitmap.Render(akar);
        var encoder = new PngBitmapEncoder();
        encoder.Frames.Add(BitmapFrame.Create(bitmap));
        using var stream = File.Create(System.IO.Path.Combine(folder, $"{nama}.png"));
        encoder.Save(stream);
    }

    private static void Klik(Button tombol)
    {
        var peer = new ButtonAutomationPeer(tombol);
        ((IInvokeProvider)peer.GetPattern(PatternInterface.Invoke)).Invoke();
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

    private static T? Cari<T>(DependencyObject akar) where T : DependencyObject => Semua<T>(akar).FirstOrDefault();

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
}
