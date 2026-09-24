using System.IO;
using System.Printing;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Markup;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace Absensi.App.Laporan;

// Fitur Cetak (terpisah dari Ekspor PDF, instruksi eksplisit pemilik
// proyek 2026-09-25): pratinjau semua halaman dulu, baru PrintDialog
// bawaan Windows (printer, ukuran kertas yang didukung printer,
// orientasi, jumlah salinan) saat tombol "Cetak..." ditekan. Halaman
// dicetak APA ADANYA sebagai gambar penuh ke area cetak yang dipilih
// (AspectFit, dipusatkan) -- tidak menghitung ulang tata letak/kolom;
// itu sudah selesai dan teruji di PenulisPdfLaporan (dokumen QuestPDF
// yang sama dengan Ekspor PDF).
//
// Galat PrintDialog/PrintDocument ditangani LANGSUNG di sini (bukan
// dilempar ke ILayananCetakLaporan.Cetak pemanggil) supaya pesannya pasti
// terlihat pengguna yang fokusnya masih di jendela pratinjau ini, tanpa
// bergantung pada exception dari dalam nested dispatcher ShowDialog()
// berhasil "naik" lewat penanganan galat global aplikasi.
public partial class JendelaPratinjauCetak : Window
{
    private readonly IReadOnlyList<byte[]> _halamanPng;

    public JendelaPratinjauCetak(IReadOnlyList<byte[]> halamanPng, string judulJob)
    {
        InitializeComponent();
        _halamanPng = halamanPng;
        Title = $"Pratinjau Cetak - {judulJob}";
        TeksJumlahHalaman.Text = halamanPng.Count == 1 ? "1 halaman" : $"{halamanPng.Count} halaman";

        foreach (var png in halamanPng)
        {
            PanelHalaman.Children.Add(BuatTampilanHalaman(png));
        }
    }

    private static Border BuatTampilanHalaman(byte[] png) => new()
    {
        BorderBrush = Brushes.Gray,
        BorderThickness = new Thickness(1),
        Margin = new Thickness(0, 0, 0, 16),
        Child = new Image { Source = MuatBitmap(png), Stretch = Stretch.Uniform, MaxWidth = 760 },
    };

    private static BitmapImage MuatBitmap(byte[] png)
    {
        var bitmap = new BitmapImage();
        using var stream = new MemoryStream(png);
        bitmap.BeginInit();
        bitmap.CacheOption = BitmapCacheOption.OnLoad;
        bitmap.StreamSource = stream;
        bitmap.EndInit();
        bitmap.Freeze();
        return bitmap;
    }

    private void TombolTutup_Click(object sender, RoutedEventArgs e) => Close();

    private void TombolCetak_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            var dialog = new PrintDialog();
            if (dialog.ShowDialog() != true)
            {
                return;
            }

            var dokumen = BangunDokumenCetak(_halamanPng, dialog.PrintableAreaWidth, dialog.PrintableAreaHeight);
            dialog.PrintDocument(((IDocumentPaginatorSource)dokumen).DocumentPaginator, Title);
            Close();
        }
        catch (Exception ex) when (ex is PrintSystemException or System.ComponentModel.Win32Exception)
        {
            MessageBox.Show(
                "Printer tidak tersedia atau tidak bisa diakses. Periksa apakah printer sudah terpasang, menyala, dan tersambung, lalu coba lagi.",
                "Absensi", MessageBoxButton.OK, MessageBoxImage.Warning);
        }
    }

    // Satu FixedPage per gambar, diskalakan AspectFit (Stretch.Uniform) ke
    // seluruh area cetak yang dipilih pengguna di PrintDialog -- apa pun
    // ukuran kertas/orientasi yang mereka pilih, isi tetap utuh dan
    // proporsional, tidak pernah terpotong atau melar.
    private static FixedDocument BangunDokumenCetak(IReadOnlyList<byte[]> halamanPng, double lebar, double tinggi)
    {
        var dokumen = new FixedDocument();
        foreach (var png in halamanPng)
        {
            var image = new Image
            {
                Source = MuatBitmap(png),
                Stretch = Stretch.Uniform,
                Width = lebar,
                Height = tinggi,
            };

            var halaman = new FixedPage { Width = lebar, Height = tinggi };
            halaman.Children.Add(image);

            var kontenHalaman = new PageContent();
            ((IAddChild)kontenHalaman).AddChild(halaman);
            dokumen.Pages.Add(kontenHalaman);
        }

        return dokumen;
    }
}
