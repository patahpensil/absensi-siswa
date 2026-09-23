using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace Absensi.App.Notifikasi;

public partial class NotifikasiView : UserControl
{
    public NotifikasiView()
    {
        InitializeComponent();
        DataContextChanged += (_, e) =>
        {
            if (e.OldValue is NotifikasiViewModel lama)
            {
                lama.TampilkanQrDiminta -= TampilkanQr;
                lama.CetakMassalSiap -= CetakMassal;
                lama.MintaKonfirmasi = null;
            }

            if (e.NewValue is NotifikasiViewModel baru)
            {
                baru.TampilkanQrDiminta += TampilkanQr;
                baru.CetakMassalSiap += CetakMassal;
                baru.MintaKonfirmasi = pesan => MessageBox.Show(
                    Window.GetWindow(this), pesan, "Konfirmasi", MessageBoxButton.YesNo, MessageBoxImage.Warning, MessageBoxResult.No) == MessageBoxResult.Yes;
            }
        };
    }

    private void TampilkanQr(object? sender, QrTampil data)
    {
        var jendela = new QrWindow(data) { Owner = Window.GetWindow(this) };
        jendela.ShowDialog();
    }

    // PRD §16.6 langkah 4: satu dialog cetak untuk seluruh QR kelas
    // sekaligus, bukan membuka dialog cetak berulang per siswa.
    private void CetakMassal(object? sender, IReadOnlyList<QrTampil> daftar)
    {
        var dokumen = new FlowDocument { PagePadding = new Thickness(24) };
        var wadah = new Section();

        foreach (var item in daftar)
        {
            var panel = new System.Windows.Controls.StackPanel
            {
                Width = 220,
                Margin = new Thickness(8),
                HorizontalAlignment = HorizontalAlignment.Left,
            };
            panel.Children.Add(new TextBlock
            {
                Text = item.Nama,
                FontWeight = FontWeights.Bold,
                TextWrapping = TextWrapping.Wrap,
                HorizontalAlignment = HorizontalAlignment.Center,
            });
            panel.Children.Add(new TextBlock
            {
                Text = item.NisnAtauNip,
                HorizontalAlignment = HorizontalAlignment.Center,
                Margin = new Thickness(0, 0, 0, 4),
            });
            panel.Children.Add(new Image
            {
                Source = PembuatQr.BuatBitmapImage(item.Url),
                Width = 180,
                Height = 180,
            });

            wadah.Blocks.Add(new BlockUIContainer(panel));
        }

        dokumen.Blocks.Add(wadah);

        var dialog = new PrintDialog();
        if (dialog.ShowDialog() == true)
        {
            IDocumentPaginatorSource sumber = dokumen;
            dialog.PrintDocument(sumber.DocumentPaginator, "QR Pairing Massal");
        }
    }
}
