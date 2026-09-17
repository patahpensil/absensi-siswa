using System.Windows;
using System.Windows.Controls;

namespace Absensi.App.Notifikasi;

public partial class QrWindow : Window
{
    public QrWindow(QrTampil data)
    {
        InitializeComponent();
        DataContext = new
        {
            data.Nama,
            data.NisnAtauNip,
            GambarQr = PembuatQr.BuatBitmapImage(data.Url),
        };
    }

    private void TombolTutup_Click(object sender, RoutedEventArgs e) => Close();

    private void TombolCetak_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new PrintDialog();
        if (dialog.ShowDialog() == true)
        {
            dialog.PrintVisual(PanelCetak, "QR Pairing");
        }
    }
}
