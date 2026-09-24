using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Threading;
using Absensi.App.Pengaturan;
using Absensi.Data.Repositori;
using Absensi.Domain.Akun;

namespace Absensi.App.Tests;

// UAT 2026-09-25: TambahAkunWindow (Pengaturan -> Akun -> Tambah Akun)
// punya Height="480" TETAP -- konten sungguhan (judul + Peran + Username +
// Sandi + Konfirmasi sandi + Guru terkait + tombol) lebih tinggi dari itu
// bahkan TANPA pesan galat tampil, jadi tombol Simpan/Batal tidak pernah
// terlihat di layar operator (dilaporkan lewat UAT, dibuktikan di sini
// dengan window WPF sungguhan, bukan diasumsikan dari membaca XAML saja).
// Diperbaiki: SizeToContent="Height" (pola sama seperti
// FormSiswaWindow/RegistrasiKartuWindow/KonfirmasiTutupWindow).
public sealed class TambahAkunWindowLayoutTests
{
    private static readonly AkunTersimpan Kepsek = new(1, "kepsek", "x", PeranAkun.KepalaSekolah, null, true);
    private static readonly AkunTersimpan Admin = new(2, "admin1", "x", PeranAkun.Admin, null, true);

    private static readonly IReadOnlyList<GuruRingkas> DaftarGuru = new[]
    {
        new GuruRingkas(1, "Guru Satu", "111", null),
        new GuruRingkas(2, "Guru Dua", "222", null),
    };

    [Theory]
    [MemberData(nameof(Pembuat))]
    public void TambahAkunWindow_UkuranMenyesuaikanKonten_TombolSimpanBatalTerlihatDanBisaDiklik(AkunTersimpan pembuat) => StaHost.Jalankan(() =>
    {
        var viewModel = new TambahAkunViewModel(null!, pembuat, new PenyediaWaktuUji(DateTime.UtcNow), DaftarGuru);
        var window = new TambahAkunWindow(viewModel)
        {
            ShowInTaskbar = false,
            WindowStartupLocation = WindowStartupLocation.Manual,
            Left = -6000,
            Top = 0,
        };

        window.Show();
        PompaDispatcher(8);
        window.UpdateLayout();

        // Bekas Height="480" tetap membuat window SELALU 480px berapa pun
        // konten sesungguhnya butuh -- sekarang menyesuaikan konten, harus
        // tetap masuk akal untuk layar kecil (1280x720, dikurangi taskbar +
        // title bar: area kerja realistis sekitar 650px).
        Assert.True(window.ActualHeight < 650, $"Tinggi dialog {window.ActualHeight:F0}px -- berisiko tidak muat di layar 1280x720.");
        Assert.True(window.ActualHeight > 0, "Window tidak punya tinggi -- kemungkinan gagal dirender.");

        // PENTING: dibandingkan terhadap tinggi KONTEN (window.Content, area
        // klien SESUNGGUHNYA yang dilihat pengguna), BUKAN window.ActualHeight
        // (mencakup title bar/chrome, jadi SELALU lebih besar dari area
        // klien -- perbandingan ke window.ActualHeight ternyata gagal
        // menangkap bug ini sama sekali, dibuktikan lewat pengukuran manual:
        // Height="480" tetap -> akarKonten.ActualHeight cuma 417px padahal
        // tombol butuh sampai 468px, 51px KETERPOTONG -- persis laporan UAT
        // "Simpan/Batal tidak terlihat". Ditemukan lewat perbaikan tes ini
        // sendiri, bukan diasumsikan benar dari percobaan pertama.
        var akarKonten = (FrameworkElement)window.Content;

        var tombolSimpan = Semua<Button>(window).Single(b => Equals(b.Content, "Simpan"));
        var tombolBatal = Semua<Button>(window).Single(b => Equals(b.Content, "Batal"));

        Rect BatasDiKonten(FrameworkElement e) =>
            e.TransformToAncestor(akarKonten).TransformBounds(new Rect(0, 0, e.ActualWidth, e.ActualHeight));

        var batasSimpan = BatasDiKonten(tombolSimpan);
        var batasBatal = BatasDiKonten(tombolBatal);

        Assert.True(tombolSimpan.ActualHeight > 0 && tombolBatal.ActualHeight > 0, "Tombol tidak punya ukuran -- kemungkinan tidak dirender sama sekali.");
        Assert.True(batasSimpan.Bottom <= akarKonten.ActualHeight + 0.5,
            $"Tombol Simpan berakhir di {batasSimpan.Bottom:F0}px, melebihi area klien {akarKonten.ActualHeight:F0}px -- terpotong/tidak terlihat.");
        Assert.True(batasBatal.Bottom <= akarKonten.ActualHeight + 0.5,
            $"Tombol Batal berakhir di {batasBatal.Bottom:F0}px, melebihi area klien {akarKonten.ActualHeight:F0}px -- terpotong/tidak terlihat.");
        Assert.True(tombolSimpan.IsVisible && tombolBatal.IsVisible, "Salah satu tombol tidak IsVisible.");

        window.Close();
    });

    // Kepala Sekolah -> dropdown Peran 2 pilihan (Admin/Wali Kelas), teks
    // "(wajib untuk Wali Kelas)" muncul untuk peran bawaan (Admin dulu,
    // jadi teks itu TIDAK tampil di kondisi awal -- tapi Admin sebagai
    // pembuat cuma 1 pilihan (Wali Kelas), teks itu SELALU tampil sejak
    // awal) -- dua kondisi konten paling berbeda tingginya, keduanya diuji.
    public static TheoryData<AkunTersimpan> Pembuat => new() { Kepsek, Admin };

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
