using System.IO;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace Absensi.App.MasterData;

// PRD §5.5/§16.4: foto dikecilkan ke ±200x200 saat impor, disimpan ke
// data/foto/. Skala agar MUAT (sisi terpanjang jadi 200px) lalu ditengahkan
// di kanvas 200x200 dengan padding transparan — bukan potong tengah, karena
// foto pelajar biasanya potret 3:4 dan potong tengah bisa memotong kepala.
// PRD §5.5 tidak menyebut pemotongan.
public static class PengolahFoto
{
    private const int UkuranSisi = 200;

    public static void SimpanDiubahUkuran(string pathSumber, string pathTujuan)
    {
        using var streamSumber = File.OpenRead(pathSumber);
        var decoder = BitmapDecoder.Create(streamSumber, BitmapCreateOptions.None, BitmapCacheOption.OnLoad);
        var sumber = decoder.Frames[0];

        var skala = (double)UkuranSisi / Math.Max(sumber.PixelWidth, sumber.PixelHeight);
        var diskalakan = new TransformedBitmap(sumber, new ScaleTransform(skala, skala));

        var offsetX = (UkuranSisi - diskalakan.PixelWidth) / 2.0;
        var offsetY = (UkuranSisi - diskalakan.PixelHeight) / 2.0;

        var visual = new DrawingVisual();
        using (var context = visual.RenderOpen())
        {
            context.DrawImage(diskalakan, new Rect(offsetX, offsetY, diskalakan.PixelWidth, diskalakan.PixelHeight));
        }

        var kanvas = new RenderTargetBitmap(UkuranSisi, UkuranSisi, 96, 96, PixelFormats.Pbgra32);
        kanvas.Render(visual);

        var folderTujuan = Path.GetDirectoryName(pathTujuan);
        if (!string.IsNullOrEmpty(folderTujuan))
        {
            Directory.CreateDirectory(folderTujuan);
        }

        using var streamTujuan = File.Create(pathTujuan);
        var encoder = new PngBitmapEncoder();
        encoder.Frames.Add(BitmapFrame.Create(kanvas));
        encoder.Save(streamTujuan);
    }
}
