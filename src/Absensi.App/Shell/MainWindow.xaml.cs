using System.Windows;

namespace Absensi.App.Shell;

public partial class MainWindow : Window
{
    public MainWindow(MainWindowViewModel viewModel, Func<Window> bukaGerbang)
    {
        InitializeComponent();
        DataContext = viewModel;
        viewModel.BukaGerbangDiminta += (_, _) => bukaGerbang().Show();
    }
}
