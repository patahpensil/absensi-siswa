using System.IO;
using System.Windows.Media.Imaging;
using QRCoder;

namespace Absensi.App.Notifikasi;

// RENCANA-TUGAS.md Tugas 10 — QR pairing (PRD §16.6). QRCoder (MIT, murni
// C#) dipilih atas persetujuan pemilik proyek 2026-09-18 — tidak ada cara
// wajar membuat encoder QR sendiri.
public static class PembuatQr
{
    public static byte[] BuatPng(string konten)
    {
        using var generator = new QRCodeGenerator();
        using var data = generator.CreateQrCode(konten, QRCodeGenerator.ECCLevel.Q);
        var png = new PngByteQRCode(data);
        return png.GetGraphic(pixelsPerModule: 20);
    }

    public static BitmapImage BuatBitmapImage(string konten)
    {
        var bytes = BuatPng(konten);
        using var stream = new MemoryStream(bytes);

        var image = new BitmapImage();
        image.BeginInit();
        image.CacheOption = BitmapCacheOption.OnLoad;
        image.StreamSource = stream;
        image.EndInit();
        image.Freeze();
        return image;
    }
}
