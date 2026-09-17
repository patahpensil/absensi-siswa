namespace Absensi.App.Gerbang;

// PANDUAN-UI-UX.md §8.2: "Bunyi berbeda untuk berhasil dan gagal." Lewat
// antarmuka supaya pengujian tidak benar-benar berbunyi.
public interface IPemutarBunyi
{
    void Berhasil();

    void Gagal();
}
