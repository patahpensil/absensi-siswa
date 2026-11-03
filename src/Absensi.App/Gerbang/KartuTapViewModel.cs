using System.Windows.Media;
using Absensi.App.Mvvm;
using Absensi.Domain.Kehadiran;

namespace Absensi.App.Gerbang;

// Satu kartu tap yang tampil di layar gerbang, baik sedang besar (aktif)
// maupun sudah turun ke daftar ringkas.
public sealed class KartuTapViewModel : ObservableObject
{
    public KartuTapViewModel(
        JenisPemilikKartu? pemilikTipe,
        string nama,
        string nisnAtauNip,
        ImageSource? foto,
        string teksStatus,
        DateTime waktu,
        KategoriWarnaTap kategoriWarna,
        string? petunjuk = null)
    {
        PemilikTipe = pemilikTipe;
        Nama = nama;
        NisnAtauNip = nisnAtauNip;
        Foto = foto;
        TeksStatus = teksStatus;
        Waktu = waktu;
        KategoriWarna = kategoriWarna;
        Petunjuk = petunjuk;
    }

    public JenisPemilikKartu? PemilikTipe { get; }

    public string? Petunjuk { get; }

    // Sentence case (PANDUAN-UI-UX.md §4.3): "Guru" dan "Siswa", bukan huruf besar semua.
    public string LabelJenis => PemilikTipe switch
    {
        JenisPemilikKartu.Guru => "Guru",
        JenisPemilikKartu.Siswa => "Siswa",
        _ => "",
    };

    public string Nama { get; }

    // PANDUAN-UI-UX.md §8.1: nama 64 px pada 1920x1080 (TipografiGerbang
    // .FontNama = ukuran dasar tingkat layar). Nama panjang (mis.
    // "Muhammad Rizky Ramadhan Putra Pratama") pada ukuran dasar
    // membutuhkan 5-6 baris — jauh melampaui kotak nama di
    // GerbangWindow.xaml. Menurunkan ukuran per panjang nama (faktor
    // terhadap ukuran dasar) menjaga nama tetap ≤ dua-tiga baris dan
    // terbaca; nama pendek/menengah memakai ukuran dasar apa adanya.
    // Dipanggil UkuranFontNamaConverter dengan dasar tingkat layar aktif.
    public static double UkuranFontUntukNama(string? nama, double dasar = 64)
    {
        var panjang = nama?.Trim().Length ?? 0;
        var faktor = panjang switch
        {
            <= 18 => 1.0,
            <= 26 => 52.0 / 64,
            <= 40 => 42.0 / 64,
            _ => 34.0 / 64,
        };

        return dasar * faktor;
    }

    public string NisnAtauNip { get; }

    public ImageSource? Foto { get; }

    public string TeksStatus { get; }

    public DateTime Waktu { get; }

    public string TeksJam => Waktu.ToString("HH:mm");

    public KategoriWarnaTap KategoriWarna { get; }
}
