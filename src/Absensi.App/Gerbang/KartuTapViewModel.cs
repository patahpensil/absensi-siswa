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
        KategoriWarnaTap kategoriWarna)
    {
        PemilikTipe = pemilikTipe;
        Nama = nama;
        NisnAtauNip = nisnAtauNip;
        Foto = foto;
        TeksStatus = teksStatus;
        Waktu = waktu;
        KategoriWarna = kategoriWarna;
    }

    public JenisPemilikKartu? PemilikTipe { get; }

    // Sentence case (PANDUAN-UI-UX.md §4.3): "Guru" dan "Siswa", bukan huruf besar semua.
    public string LabelJenis => PemilikTipe switch
    {
        JenisPemilikKartu.Guru => "Guru",
        JenisPemilikKartu.Siswa => "Siswa",
        _ => "",
    };

    public string Nama { get; }

    public string NisnAtauNip { get; }

    public ImageSource? Foto { get; }

    public string TeksStatus { get; }

    public DateTime Waktu { get; }

    public string TeksJam => Waktu.ToString("HH:mm");

    public KategoriWarnaTap KategoriWarna { get; }
}
