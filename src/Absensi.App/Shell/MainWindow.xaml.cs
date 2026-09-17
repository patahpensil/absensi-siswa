using System.Windows;

namespace Absensi.App.Shell;

public partial class MainWindow : Window
{
    public MainWindow(MainWindowViewModel viewModel, Func<Window> bukaGerbang, Func<Window> bukaRegistrasiKartu)
    {
        InitializeComponent();
        DataContext = viewModel;
        viewModel.BukaGerbangDiminta += (_, _) => bukaGerbang().Show();

        // Dialog modal (bukan fullscreen seperti gerbang) — pengguna
        // menyelesaikan atau membatalkan sesi registrasi sebelum kembali
        // ke layar lain (RENCANA-TUGAS.md Tugas 8).
        viewModel.BukaRegistrasiKartuDiminta += (_, _) =>
        {
            var jendela = bukaRegistrasiKartu();
            jendela.Owner = this;
            jendela.ShowDialog();
        };
    }
}
