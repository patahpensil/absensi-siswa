using System.Windows;

namespace Absensi.App.Pengaturan;

public partial class KonfirmasiRestoreWindow : Window
{
    public KonfirmasiRestoreWindow(KonfirmasiRestoreViewModel viewModel)
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
