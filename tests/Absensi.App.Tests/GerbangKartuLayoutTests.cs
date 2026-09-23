using System.IO;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using Absensi.App.Gerbang;
using Absensi.Domain.Kehadiran;

namespace Absensi.App.Tests;

// UAT 2026-09-23 (layar 1366x768): kartu tap Gerbang memakai ukuran
// rancangan 1920x1080 (foto 300x400, nama 64 px, ...) — label, nama,
// NISN/NIP, dan status terpotong. Perbaikan pertama (Viewbox seluruh
// kartu) menyusutkan semuanya ke skala ±0,56 (NISN/NIP ±16 px, status ±22
// px) — terlalu kecil. Perbaikan sekarang: ukuran teks/jarak BERTINGKAT
// per tinggi layar (TipografiGerbang: besar = PANDUAN §8.1 apa adanya,
// kompak = teks lebih kecil tapi tetap besar), header + ringkasan harian
// sebaris di kompak, foto (3:4) mengambil sisa tinggi, dan panel Riwayat
// setinggi tetap supaya kartu tidak berubah ukuran saat riwayat muncul.
//
// Test ini memakai Window WPF SUNGGUHAN (siklus Measure/Arrange penuh) —
// clipping tidak bisa dibuktikan tanpa itu (pola sama seperti
// PengaturanViewLayoutTests). Window WPF tidak bisa lebih besar dari layar
// fisik (permintaan 1920x1080 di laptop 1366x768 terjepit ~1386x788),
// jadi ukuran diuji dengan memindahkan isi GerbangWindow (Grid akar, XAML
// dan ViewModel persis yang dipakai aplikasi) ke Window uji berborder
// ukuran TETAP = area klien yang diuji. Ditambah dua test yang memakai
// GerbangWindow itu sendiri (Show()) pada 1366x728 dan 1366x768.
public sealed class GerbangKartuLayoutTests
{
    private static readonly DateTime Waktu = new(2026, 9, 23, 7, 20, 0);

    // Area kerja (dikurangi taskbar 40 px) dan layar penuh, sesuai UAT.
    public static TheoryData<int, int> Ukuran => new()
    {
        { 1366, 728 },
        { 1366, 768 },
        { 1920, 1040 },
        { 1920, 1080 },
    };

    private static BitmapSource BuatFoto()
    {
        var piksel = new byte[200 * 200 * 4];
        for (var i = 0; i < piksel.Length; i += 4)
        {
            piksel[i] = 90;
            piksel[i + 1] = 120;
            piksel[i + 2] = 160;
            piksel[i + 3] = 255;
        }

        var bitmap = BitmapSource.Create(200, 200, 96, 96, PixelFormats.Bgra32, null, piksel, 200 * 4);
        bitmap.Freeze();
        return bitmap;
    }

    private static KartuTapViewModel Kartu(JenisPemilikKartu? tipe, string nama, string id, ImageSource? foto, string status, KategoriWarnaTap warna) =>
        new(tipe, nama, id, foto, status, Waktu, warna);

    // Empat kartu terburuk-yang-realistis: nama pendek berfoto, guru
    // bergelar tanpa foto (NIP 18 digit), nama sangat panjang, dan kartu
    // tak dikenal dengan status paling panjang.
    private static List<KartuTapViewModel> EmpatKartu()
    {
        var foto = BuatFoto();
        return new List<KartuTapViewModel>
        {
            Kartu(JenisPemilikKartu.Siswa, "Ahmad Fikri UAT", "0061234567", foto, "Hadir", KategoriWarnaTap.Hadir),
            Kartu(JenisPemilikKartu.Guru, "Dewi Lestari, S.Pd.", "198706152012011001", null, "Masuk", KategoriWarnaTap.Hadir),
            Kartu(JenisPemilikKartu.Siswa, "Muhammad Rizky Ramadhan Putra Pratama", "0061299999", foto, "Terlambat", KategoriWarnaTap.Terlambat),
            Kartu(null, "Kartu tidak dikenal", string.Empty, null, "Jadwal belum diatur", KategoriWarnaTap.Ditolak),
        };
    }

    private static GerbangViewModel BuatViewModel(MigratedDatabase database, IEnumerable<KartuTapViewModel> kartu)
    {
        database.SeedSekolahUntukGerbang();
        var cache = CacheTampilanGerbang.Muat(database.Connection);
        var viewModel = new GerbangViewModel(database.Connection, new PenyediaWaktuUji(Waktu), new PemutarBunyiDiam(), cache, new StatusModeRegistrasi());
        foreach (var k in kartu)
        {
            viewModel.KartuAktif.Add(k);
        }

        return viewModel;
    }

    // Pindahkan isi GerbangWindow ke Window uji berukuran-area-klien tetap,
    // dan atur tingkat tipografi seperti yang dilakukan GerbangWindow
    // (SizeChanged -> SesuaikanDenganTinggi).
    private static (Window Host, Grid Akar, GerbangViewModel ViewModel) HostUkuranTetap(
        MigratedDatabase database, IEnumerable<KartuTapViewModel> kartu, int lebar, int tinggi)
    {
        var viewModel = BuatViewModel(database, kartu);
        var gerbang = new GerbangWindow(viewModel, database.Connection);
        var akar = (Grid)gerbang.Content;
        gerbang.Content = null;
        akar.DataContext = viewModel;
        viewModel.SesuaikanDenganTinggi(tinggi);

        var host = new Window
        {
            Content = new ScrollViewer
            {
                HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled,
                VerticalScrollBarVisibility = ScrollBarVisibility.Disabled,
                Content = new Border
                {
                    Width = lebar,
                    Height = tinggi,
                    Background = (Brush)Application.Current.Resources["TintaBrush"],
                    Child = akar,
                },
            },
            WindowStyle = WindowStyle.None,
            ShowInTaskbar = false,
            WindowStartupLocation = WindowStartupLocation.Manual,
            Left = -6000,
            Top = 0,
            Width = 400,
            Height = 300,
        };
        return (host, akar, viewModel);
    }

    private sealed record HasilKartu(
        Border Kartu, Rect Isi, Rect Label, Rect Foto, Rect Nama, Rect Nisn, Rect Status,
        double FontLabel, double FontNama, double FontNisn, double FontStatus, KartuTapViewModel Data);

    private static UIElement Baris(Grid grid, int baris) =>
        grid.Children.Cast<UIElement>().First(c => Grid.GetRow(c) == baris);

    // Ukuran font seperti yang benar-benar tampil di layar (font x skala
    // Viewbox pembungkusnya). 0 bila teks kosong.
    private static double FontEfektif(TextBlock teks, Rect batas) =>
        teks.ActualWidth > 0 ? teks.FontSize * (batas.Width / teks.ActualWidth) : 0;

    private static List<HasilKartu> UkurKartu(DependencyObject akar)
    {
        var gayaKartu = (Style)Application.Current.Resources["KartuGerbangBesarStyle"];
        var hasil = new List<HasilKartu>();

        foreach (var kartu in Semua<Border>(akar).Where(b => ReferenceEquals(b.Style, gayaKartu) && b.Child is Grid))
        {
            var data = (KartuTapViewModel)kartu.DataContext;
            var grid = (Grid)kartu.Child;

            Rect Batas(FrameworkElement e) =>
                e.TransformToAncestor(kartu).TransformBounds(new Rect(0, 0, e.ActualWidth, e.ActualHeight));

            var label = (TextBlock)Baris(grid, 0);
            var foto = Semua<Border>(Baris(grid, 1)).First(b => b.Width == 300 && b.Height == 400);
            var nama = Semua<TextBlock>(Baris(grid, 2)).Single();
            var nisn = Semua<TextBlock>(Baris(grid, 3)).Single();
            var status = Semua<TextBlock>(Baris(grid, 4)).Single();

            var isi = new Rect(kartu.Padding.Left, kartu.Padding.Top,
                kartu.ActualWidth - kartu.Padding.Left - kartu.Padding.Right,
                kartu.ActualHeight - kartu.Padding.Top - kartu.Padding.Bottom);

            var bLabel = Batas(label);
            var bNama = Batas(nama);
            var bNisn = Batas(nisn);
            var bStatus = Batas(status);

            hasil.Add(new HasilKartu(
                kartu, isi, bLabel, Batas(foto), bNama, bNisn, bStatus,
                FontEfektif(label, bLabel), FontEfektif(nama, bNama), FontEfektif(nisn, bNisn), FontEfektif(status, bStatus), data));
        }

        return hasil;
    }

    private static void VerifikasiTidakTerpotong(List<HasilKartu> hasil, string ukuran)
    {
        Assert.Equal(4, hasil.Count);

        foreach (var h in hasil)
        {
            void DiDalam(string nama, Rect r)
            {
                Assert.True(
                    r.Left >= h.Isi.Left - 1 && r.Right <= h.Isi.Right + 1 && r.Top >= h.Isi.Top - 1 && r.Bottom <= h.Isi.Bottom + 1,
                    $"[{ukuran}] '{nama}' pada kartu '{h.Data.Nama}' keluar dari area isi kartu " +
                    $"(elemen L={r.Left:F0} T={r.Top:F0} R={r.Right:F0} B={r.Bottom:F0}; area isi L={h.Isi.Left:F0} T={h.Isi.Top:F0} R={h.Isi.Right:F0} B={h.Isi.Bottom:F0}).");
            }

            DiDalam("label jenis", h.Label);
            DiDalam("foto/placeholder", h.Foto);
            DiDalam("nama", h.Nama);
            DiDalam("NISN/NIP", h.Nisn);
            DiDalam("status dan jam", h.Status);

            // Tidak saling menimpa: urutan vertikal label < foto < nama < NISN < status.
            Assert.True(h.Label.Bottom <= h.Foto.Top + 1, $"[{ukuran}] label menimpa foto ('{h.Data.Nama}').");
            Assert.True(h.Foto.Bottom <= h.Nama.Top + 1, $"[{ukuran}] foto menimpa nama ('{h.Data.Nama}').");
            Assert.True(h.Nama.Bottom <= h.Nisn.Top + 1, $"[{ukuran}] nama menimpa NISN/NIP ('{h.Data.Nama}').");
            Assert.True(h.Nisn.Bottom <= h.Status.Top + 1, $"[{ukuran}] NISN/NIP menimpa status ('{h.Data.Nama}').");

            // Nama tidak berdesakan dengan foto: celah >= 8 px di antara keduanya.
            Assert.True(h.Nama.Top - h.Foto.Bottom >= 8, $"[{ukuran}] celah foto-nama hanya {h.Nama.Top - h.Foto.Bottom:F1} px ('{h.Data.Nama}').");

            // Foto selalu 3:4 (PANDUAN-UI-UX.md §8.1).
            Assert.InRange(h.Foto.Width / h.Foto.Height, 0.749, 0.751);
        }

        // Semua kartu sejajar: foto berukuran dan berposisi vertikal SAMA
        // (termasuk kartu tak dikenal yang tanpa label Siswa/Guru).
        var acuan = hasil[0];
        foreach (var h in hasil)
        {
            Assert.True(
                Math.Abs(h.Foto.Height - acuan.Foto.Height) < 0.5 && Math.Abs(h.Foto.Top - acuan.Foto.Top) < 0.5,
                $"[{ukuran}] foto kartu '{h.Data.Nama}' tidak sejajar/sama besar dengan kartu '{acuan.Data.Nama}' " +
                $"(Top {h.Foto.Top:F1} vs {acuan.Foto.Top:F1}, tinggi {h.Foto.Height:F1} vs {acuan.Foto.Height:F1}).");
        }
    }

    private static void Tampilkan(Window host)
    {
        host.Show();
        PompaDispatcher(8);
        host.UpdateLayout();
    }

    [Theory]
    [MemberData(nameof(Ukuran))]
    public void KartuGerbang_SemuaUnsurMuatDiAreaKartuTanpaTerpotongDanTanpaTumpangTindih(int lebar, int tinggi) => StaHost.Jalankan(() =>
    {
        using var database = new MigratedDatabase();
        var (host, akar, _) = HostUkuranTetap(database, EmpatKartu(), lebar, tinggi);

        Tampilkan(host);

        VerifikasiTidakTerpotong(UkurKartu(akar), $"{lebar}x{tinggi}");

        host.Hide();
    });

    [Theory]
    [InlineData(728)]
    [InlineData(768)]
    public void KartuGerbang_1366_TeksTetapBesarTanpaMenyusutkanSeluruhKartu(int tinggi) => StaHost.Jalankan(() =>
    {
        using var database = new MigratedDatabase();
        var (host, akar, viewModel) = HostUkuranTetap(database, EmpatKartu(), 1366, tinggi);

        Tampilkan(host);

        Assert.True(viewModel.Tipografi.Kompak);
        var hasil = UkurKartu(akar);
        VerifikasiTidakTerpotong(hasil, $"1366x{tinggi}");

        // Ukuran font tampil (bukan hasil skala 0,56 seluruh kartu):
        // nama pendek 44 px, NISN/NIP 24 px, status 32 px, label 20 px
        // — nilai kelas kompak APA ADANYA.
        var pendek = hasil[0];
        Assert.InRange(pendek.FontNama, 43.9, 44.1);
        Assert.InRange(pendek.FontNisn, 23.9, 24.1);
        Assert.InRange(pendek.FontStatus, 31.9, 32.1);
        Assert.InRange(pendek.FontLabel, 19.9, 20.1);

        // Guru bergelar (19 karakter, NIP 18 digit): nama turun satu
        // tingkat tapi NIP dan status tetap ukuran penuh.
        var guru = hasil[1];
        Assert.True(guru.FontNama >= 35, $"Nama guru {guru.FontNama:F1} px.");
        Assert.InRange(guru.FontNisn, 23.9, 24.1);
        Assert.InRange(guru.FontStatus, 31.9, 32.1);

        // Nama sangat panjang: turun bertahap, tetap di atas 26 px
        // (sebelumnya ±23 px pada skala 0,56).
        var panjang = hasil[2];
        Assert.True(panjang.FontNama >= 26, $"Nama panjang {panjang.FontNama:F1} px.");

        // Status terpanjang ("Jadwal belum diatur · 07:20") boleh menyusut
        // muat lebar kartu, tapi tidak di bawah 22 px (sebelumnya ±22 px
        // untuk status paling PENDEK).
        var ditolak = hasil[3];
        Assert.True(ditolak.FontStatus >= 22, $"Status terpanjang {ditolak.FontStatus:F1} px.");

        // Foto mengambil sisa tinggi dan MEMENUHI minimum 320 px panduan
        // §8.1 — juga di 1366x728 (area kerja dengan taskbar), tanpa
        // mengecilkan nama/NISN/status (asersi font di atas).
        Assert.True(hasil.All(h => h.Foto.Height >= 320), $"Tinggi foto {hasil[0].Foto.Height:F1} px pada 1366x{tinggi} (min 320).");

        host.Hide();
    });

    [Fact]
    public void KartuGerbang_1920x1080_TampilanRancanganTidakBerubah() => StaHost.Jalankan(() =>
    {
        using var database = new MigratedDatabase();
        var (host, akar, viewModel) = HostUkuranTetap(database, EmpatKartu(), 1920, 1080);

        Tampilkan(host);

        Assert.False(viewModel.Tipografi.Kompak);
        var hasil = UkurKartu(akar);
        VerifikasiTidakTerpotong(hasil, "1920x1080");

        // Nama pendek pada layar rancangan: ukuran PERSIS panduan §8.1.
        var pendek = hasil[0];
        Assert.InRange(pendek.FontNama, 63.9, 64.1);
        Assert.InRange(pendek.FontNisn, 27.9, 28.1);
        Assert.InRange(pendek.FontStatus, 39.9, 40.1);
        Assert.InRange(pendek.FontLabel, 23.9, 24.1);
        Assert.Equal(300, pendek.Foto.Width, precision: 0);
        Assert.Equal(400, pendek.Foto.Height, precision: 0);

        host.Hide();
    });

    [Fact]
    public void KartuGerbang_1920x1040_FotoTetapMinimal320() => StaHost.Jalankan(() =>
    {
        using var database = new MigratedDatabase();
        var (host, akar, _) = HostUkuranTetap(database, EmpatKartu(), 1920, 1040);

        Tampilkan(host);

        var hasil = UkurKartu(akar);
        Assert.True(hasil.All(h => h.Foto.Height >= 320), $"Tinggi foto {hasil[0].Foto.Height:F0} px pada 1920x1040.");
        Assert.InRange(hasil[0].FontNama, 63.9, 64.1);

        host.Hide();
    });

    [Theory]
    [InlineData(1366, 728)]
    [InlineData(1920, 1080)]
    public void PanelRingkasan_MunculTanpaMengubahUkuranKartu(int lebar, int tinggi) => StaHost.Jalankan(() =>
    {
        using var database = new MigratedDatabase();
        var (host, akar, viewModel) = HostUkuranTetap(database, EmpatKartu(), lebar, tinggi);

        Tampilkan(host);
        var sebelum = UkurKartu(akar);
        Assert.Empty(viewModel.Ringkasan);

        // Riwayat: kartu lama turun ke panel bawah (GerbangViewModel
        // .EvaluasiKartuKedaluwarsa) — tiga sekaligus, satu panjang.
        var foto = BuatFoto();
        viewModel.Ringkasan.Insert(0, Kartu(JenisPemilikKartu.Siswa, "Siti Aisyah", "0061111111", foto, "Hadir", KategoriWarnaTap.Hadir));
        viewModel.Ringkasan.Insert(0, Kartu(JenisPemilikKartu.Siswa, "Muhammad Rizky Ramadhan Putra Pratama", "0061299999", foto, "Terlambat", KategoriWarnaTap.Terlambat));
        viewModel.Ringkasan.Insert(0, Kartu(JenisPemilikKartu.Guru, "Dewi Lestari, S.Pd.", "198706152012011001", null, "Masuk", KategoriWarnaTap.Hadir));
        PompaDispatcher(8);
        host.UpdateLayout();

        var sesudah = UkurKartu(akar);
        Assert.Equal(sebelum.Count, sesudah.Count);

        for (var i = 0; i < sebelum.Count; i++)
        {
            var a = sebelum[i];
            var b = sesudah[i];
            string Pesan(string apa) => $"[{lebar}x{tinggi}] {apa} kartu '{a.Data.Nama}' berubah saat riwayat muncul.";

            Assert.True(Math.Abs(a.Kartu.ActualWidth - b.Kartu.ActualWidth) < 0.01 && Math.Abs(a.Kartu.ActualHeight - b.Kartu.ActualHeight) < 0.01, Pesan("Ukuran"));
            Assert.True(Math.Abs(a.Foto.Height - b.Foto.Height) < 0.01 && Math.Abs(a.Foto.Width - b.Foto.Width) < 0.01, Pesan("Ukuran foto"));
            Assert.True(Math.Abs(a.FontNama - b.FontNama) < 0.01 && Math.Abs(a.FontStatus - b.FontStatus) < 0.01 && Math.Abs(a.FontNisn - b.FontNisn) < 0.01, Pesan("Ukuran font"));
            Assert.True(Math.Abs(a.Nama.Top - b.Nama.Top) < 0.01 && Math.Abs(a.Status.Top - b.Status.Top) < 0.01, Pesan("Posisi teks"));
        }

        // Panel riwayat sendiri: tinggi tetap sesuai tingkat, dan
        // menampilkan tiga entri (terbaru di kiri).
        var panel = Semua<Border>(akar).Single(x => x.Height == viewModel.Tipografi.TinggiRiwayat && x.ClipToBounds);
        Assert.Equal(viewModel.Tipografi.TinggiRiwayat, panel.ActualHeight, precision: 1);
        Assert.Equal(3, Semua<Border>(panel).Count(x => x.Child is TextBlock t && t.Inlines.Count >= 5));

        host.Hide();
    });

    [Fact]
    public void PanelRingkasan_Kompak_RingkasanHarianSebarisDenganHeader_TidakAdaBarisTerpisah() => StaHost.Jalankan(() =>
    {
        using var database = new MigratedDatabase();
        var (host, akar, _) = HostUkuranTetap(database, EmpatKartu(), 1366, 728);

        Tampilkan(host);

        // Dua salinan ContentControl ringkasan harian (header & baris
        // terpisah): tepat SATU yang terlihat per tingkat.
        var salinan = Semua<ContentControl>(akar).Where(c => c.ContentTemplate is not null).ToList();
        Assert.Equal(2, salinan.Count);
        Assert.Single(salinan, c => c.IsVisible);
        Assert.Equal(0, Grid.GetRow(salinan.Single(c => c.IsVisible).Parent as UIElement ?? salinan.Single(c => c.IsVisible)));

        host.Hide();
    });

    [Fact]
    public void GerbangWindowSungguhan_1366x728_TingkatKompakDanSemuaUnsurMuat() => StaHost.Jalankan(() =>
    {
        using var database = new MigratedDatabase();
        var viewModel = BuatViewModel(database, EmpatKartu());
        var gerbang = new GerbangWindow(viewModel, database.Connection)
        {
            ShowInTaskbar = false,
            WindowState = WindowState.Normal,
            WindowStartupLocation = WindowStartupLocation.Manual,
            Left = -6000,
            Top = 0,
            Width = 1366,
            Height = 728,
        };

        gerbang.Show();
        PompaDispatcher(8);
        gerbang.UpdateLayout();

        // Window tidak boleh lebih kecil dari yang diminta (layar >= 1366 px).
        Assert.True(gerbang.ActualWidth >= 1365, $"Window hanya {gerbang.ActualWidth}x{gerbang.ActualHeight} — layar uji terlalu kecil.");

        // SizeChanged -> SesuaikanDenganTinggi dipasang di GerbangWindow itu sendiri.
        Assert.True(viewModel.Tipografi.Kompak, $"Tingkat tipografi tidak menjadi kompak pada tinggi {gerbang.ActualHeight}.");

        var hasil = UkurKartu(gerbang);
        VerifikasiTidakTerpotong(hasil, $"GerbangWindow {gerbang.ActualWidth:F0}x{gerbang.ActualHeight:F0}");
        Assert.InRange(hasil[0].FontNama, 43.9, 44.1);

        // GerbangWindow.OnClosing meminta sandi — tidak di-Close() di tes.
        gerbang.Hide();
    });

    [Fact]
    public void GerbangWindowSungguhan_1366x768_TingkatKompakDanFotoMinimal320() => StaHost.Jalankan(() =>
    {
        using var database = new MigratedDatabase();
        var viewModel = BuatViewModel(database, EmpatKartu());
        var gerbang = new GerbangWindow(viewModel, database.Connection)
        {
            ShowInTaskbar = false,
            WindowState = WindowState.Normal,
            WindowStartupLocation = WindowStartupLocation.Manual,
            Left = -6000,
            Top = 0,
            Width = 1366,
            Height = 768,
        };

        gerbang.Show();
        PompaDispatcher(8);
        gerbang.UpdateLayout();

        Assert.True(gerbang.ActualWidth >= 1365 && gerbang.ActualHeight >= 767, $"Window hanya {gerbang.ActualWidth}x{gerbang.ActualHeight} — layar uji terlalu kecil.");
        Assert.True(viewModel.Tipografi.Kompak);

        var hasil = UkurKartu(gerbang);
        VerifikasiTidakTerpotong(hasil, $"GerbangWindow {gerbang.ActualWidth:F0}x{gerbang.ActualHeight:F0}");
        Assert.True(hasil.All(h => h.Foto.Height >= 320), $"Tinggi foto {hasil[0].Foto.Height:F0} px.");

        gerbang.Hide();
    });

    [Fact]
    public void KartuGerbang_FotoNull_PlaceholderTampil_FotoAda_PlaceholderTersembunyi() => StaHost.Jalankan(() =>
    {
        using var database = new MigratedDatabase();
        var (host, akar, _) = HostUkuranTetap(database, EmpatKartu(), 1366, 768);

        Tampilkan(host);

        var hasil = UkurKartu(akar);
        Assert.Equal(4, hasil.Count);

        foreach (var h in hasil)
        {
            var placeholder = Semua<Border>(h.Kartu)
                .Single(b => AutomationProperties.GetName(b) == "Foto belum tersedia");
            var teks = Semua<TextBlock>(placeholder).Single(t => t.Text == "Foto belum tersedia");

            if (h.Data.Foto is null)
            {
                Assert.Equal(Visibility.Visible, placeholder.Visibility);
                Assert.True(placeholder.ActualWidth > 0 && placeholder.ActualHeight > 0, $"Placeholder '{h.Data.Nama}' tidak punya ukuran.");
                Assert.True(teks.IsVisible, "Teks placeholder tidak terlihat.");
            }
            else
            {
                Assert.Equal(Visibility.Collapsed, placeholder.Visibility);
                Assert.False(teks.IsVisible, "Placeholder tampil padahal foto tersedia.");
            }
        }

        // Dua kartu tanpa foto (guru + kartu tak dikenal), dua berfoto.
        Assert.Equal(2, hasil.Count(h => h.Data.Foto is null));

        host.Hide();
    });

    [Fact]
    public void TipografiGerbang_Untuk_TinggiPendekKompak_TinggiPanjangBesar()
    {
        Assert.True(TipografiGerbang.Untuk(728).Kompak);
        Assert.True(TipografiGerbang.Untuk(768).Kompak);
        Assert.True(TipografiGerbang.Untuk(860).Kompak);
        Assert.False(TipografiGerbang.Untuk(900).Kompak);
        Assert.False(TipografiGerbang.Untuk(1040).Kompak);
        Assert.False(TipografiGerbang.Untuk(1080).Kompak);

        // Tingkat kompak merapatkan JARAK baris nama (bukan hurufnya): faktor
        // >= 1,0 supaya glyph tidak terpotong; tingkat besar memakai bawaan font.
        Assert.True(TipografiGerbang.PresetKompak.FaktorJarakBarisNama >= 1.0);
        Assert.Equal(0, TipografiGerbang.PresetBesar.FaktorJarakBarisNama);

        // Tingkat besar = angka PANDUAN-UI-UX.md §8.1.
        var besar = TipografiGerbang.PresetBesar;
        Assert.Equal((64d, 40d, 28d, 24d), (besar.FontNama, besar.FontStatus, besar.FontNisn, besar.FontLabel));
    }

    [Fact]
    public void KartuTapViewModel_UkuranFontNama_PendekTetapDasar_PanjangTurunBertahap()
    {
        Assert.Equal(64, KartuTapViewModel.UkuranFontUntukNama("Bunya"));
        Assert.Equal(64, KartuTapViewModel.UkuranFontUntukNama("Ahmad Fikri UAT"));
        Assert.Equal(52, KartuTapViewModel.UkuranFontUntukNama("Dewi Lestari, S.Pd."));
        Assert.Equal(42, KartuTapViewModel.UkuranFontUntukNama("Muhammad Rizky Ramadhan Putra Pratama"));
        Assert.Equal(34, KartuTapViewModel.UkuranFontUntukNama("Muhammad Rizky Ramadhan Putra Pratama Wijaya"));
        Assert.Equal(64, KartuTapViewModel.UkuranFontUntukNama(null));
        Assert.Equal(64, KartuTapViewModel.UkuranFontUntukNama(string.Empty));

        // Dasar tingkat kompak (44): faktor yang sama.
        Assert.Equal(44, KartuTapViewModel.UkuranFontUntukNama("Bunya", 44));
        Assert.Equal(44 * 52.0 / 64, KartuTapViewModel.UkuranFontUntukNama("Dewi Lestari, S.Pd.", 44));
    }

    [Fact]
    public void PemuatGambar_BerkasRusak_KembalikanNullBukanLemparGalat() => StaHost.Jalankan(() =>
    {
        var folder = Path.Combine(Path.GetTempPath(), $"absensi-foto-rusak-{Guid.NewGuid():N}");
        Directory.CreateDirectory(folder);
        try
        {
            File.WriteAllText(Path.Combine(folder, "rusak.png"), "ini bukan gambar");
            File.WriteAllBytes(Path.Combine(folder, "kosong.png"), Array.Empty<byte>());
            // Tanda tangan PNG valid tapi terpotong: WPF melempar
            // FileFormatException (turunan FormatException), bukan
            // NotSupportedException seperti berkas teks.
            File.WriteAllBytes(Path.Combine(folder, "terpotong.png"), new byte[] { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A, 0x00, 0x01 });

            Assert.Null(PemuatGambar.MuatDariFolder(folder, "rusak.png"));
            Assert.Null(PemuatGambar.MuatDariFolder(folder, "kosong.png"));
            Assert.Null(PemuatGambar.MuatDariFolder(folder, "terpotong.png"));
            Assert.Null(PemuatGambar.MuatDariFolder(folder, "tidak-ada.png"));
            Assert.Null(PemuatGambar.MuatDariFolder(folder, null));

            // Handle berkas dilepas walau decode gagal: berkas rusak bisa
            // langsung ditimpa (mis. unggah foto pengganti).
            File.WriteAllText(Path.Combine(folder, "rusak.png"), "diganti");
        }
        finally
        {
            Directory.Delete(folder, recursive: true);
        }
    });

    [Fact]
    public void CacheTampilanGerbang_SatuFotoRusak_LayarTetapTerbukaDanFotoLainMuat() => StaHost.Jalankan(() =>
    {
        var folder = Path.Combine(Path.GetTempPath(), $"absensi-foto-cache-{Guid.NewGuid():N}");
        Directory.CreateDirectory(folder);
        try
        {
            var rusak = Path.Combine(folder, "rusak.png");
            var bagus = Path.Combine(folder, "bagus.png");
            File.WriteAllText(rusak, "ini bukan gambar");
            using (var stream = File.Create(bagus))
            {
                var encoder = new PngBitmapEncoder();
                encoder.Frames.Add(BitmapFrame.Create(BuatFoto()));
                encoder.Save(stream);
            }

            using var database = new MigratedDatabase();
            database.SeedSekolahUntukGerbang();
            // Path mutlak: PemuatGambar memakainya apa adanya (tidak menyentuh
            // folder data/ bersama yang dipakai LokasiDataTests).
            database.Execute($"UPDATE siswa SET foto_path = '{rusak.Replace("'", "''")}' WHERE id = 1;");
            database.Execute($"UPDATE siswa SET foto_path = '{bagus.Replace("'", "''")}' WHERE id = 2;");

            var cache = CacheTampilanGerbang.Muat(database.Connection);

            Assert.Null(cache.Cari(JenisPemilikKartu.Siswa, 1)!.Foto);
            Assert.NotNull(cache.Cari(JenisPemilikKartu.Siswa, 2)!.Foto);
            Assert.Equal("Siswa Satu", cache.Cari(JenisPemilikKartu.Siswa, 1)!.Nama);
        }
        finally
        {
            Directory.Delete(folder, recursive: true);
        }
    });

    private static void PompaDispatcher(int jumlah)
    {
        for (var i = 0; i < jumlah; i++)
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
}
