namespace Absensi.App.Gerbang;

// Kategori tampilan kartu tap di layar gerbang (docs/PANDUAN-UI-UX.md §8.2:
// "latar kartu memakai warna status penuh"). Dipetakan ke brush resource
// oleh View, bukan di sini, supaya kelas ini tidak butuh Application WPF
// yang hidup — tetap bisa diuji tanpa jendela.
public enum KategoriWarnaTap
{
    Hadir,
    Terlambat,
    Ditolak,
    Info,
    Netral,
}
