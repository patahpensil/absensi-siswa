using System.IO;
using System.Windows.Media.Imaging;

namespace Absensi.App;

// Dipakai CacheTampilanGerbang (foto siswa/guru + logo sekolah di layar
// gerbang, Tugas 6) dan LoginViewModel (logo sekolah di layar Login, Tugas
// 13) — satu jalur pemuatan gambar dari folder data/ supaya tidak ditulis
// ulang di keduanya.
public static class PemuatGambar
{
    public static BitmapImage? MuatDariFolder(string folder, string? namaBerkas)
    {
        if (string.IsNullOrWhiteSpace(namaBerkas))
        {
            return null;
        }

        var pathLengkap = Path.IsPathRooted(namaBerkas) ? namaBerkas : Path.Combine(folder, namaBerkas);
        if (!File.Exists(pathLengkap))
        {
            return null;
        }

        var bitmap = new BitmapImage();
        bitmap.BeginInit();
        bitmap.UriSource = new Uri(pathLengkap, UriKind.Absolute);
        bitmap.CacheOption = BitmapCacheOption.OnLoad;
        bitmap.EndInit();
        bitmap.Freeze();
        return bitmap;
    }
}
