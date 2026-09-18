using System.Windows;

namespace Absensi.App.Koreksi;

public partial class KonfirmasiKunciBulanWindow : Window
{
    public KonfirmasiKunciBulanWindow(KonfirmasiKunciBulanViewModel viewModel)
    {
        InitializeComponent();
        DataContext = viewModel;
        viewModel.Dikonfirmasi += (_, _) =>
        {
            DialogResult = true;
            Close();
        };
    }

    private void TombolBatal_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
        Close();
    }
}
