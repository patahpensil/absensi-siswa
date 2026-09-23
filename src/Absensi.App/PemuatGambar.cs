using System.IO;
using System.Windows.Media.Imaging;
using Absensi.Data.Diagnostik;

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

        // Berkas ada tapi tidak bisa dibaca sebagai gambar (rusak, terpotong,
        // format tak dikenal, terkunci proses lain, akses ditolak): satu
        // foto bermasalah TIDAK BOLEH menggagalkan pembukaan seluruh layar
        // gerbang (CacheTampilanGerbang.Muat memuat semua foto sekaligus).
        // Dicatat ke log dan dikembalikan null — layar gerbang lalu menampilkan
        // placeholder "Foto belum tersedia" untuk pemilik itu saja.
        try
        {
            // Lewat FileStream (bukan UriSource) supaya handle berkas SELALU
            // dilepas begitu selesai/gagal — BitmapImage yang gagal decode
            // via UriSource menahan handle sampai GC, sehingga mengunggah
            // ulang foto pengganti ke berkas yang sama bisa gagal "sedang dipakai".
            using var stream = new FileStream(pathLengkap, FileMode.Open, FileAccess.Read, FileShare.Read);
            var bitmap = new BitmapImage();
            bitmap.BeginInit();
            bitmap.StreamSource = stream;
            bitmap.CacheOption = BitmapCacheOption.OnLoad;
            bitmap.EndInit();
            bitmap.Freeze();
            return bitmap;
        }
        // Pemecah gambar WPF melempar ragam tipe menurut jenis kerusakan:
        // NotSupportedException (bukan gambar), FileFormatException
        // (turunan FormatException — PNG terpotong/header rusak),
        // COMException, IOException, UnauthorizedAccessException, dst.
        // Jalur ini best-effort (gagal = tanpa gambar), jadi semua
        // kecuali kehabisan memori diperlakukan sama.
        catch (Exception ex) when (ex is not OutOfMemoryException)
        {
            PencatatGalat.Catat(ex, $"PemuatGambar.MuatDariFolder ({namaBerkas})", "ERROR");
            return null;
        }
    }
}
