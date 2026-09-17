using System.Windows;

namespace Absensi.App.MasterData;

public partial class AlasanTidakAktifWindow : Window
{
    public string? AlasanTerpilih { get; private set; }

    public AlasanTidakAktifWindow(AlasanTidakAktifViewModel viewModel)
    {
        InitializeComponent();
        DataContext = viewModel;
        viewModel.Dikonfirmasi += (_, alasan) =>
        {
            AlasanTerpilih = alasan;
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
