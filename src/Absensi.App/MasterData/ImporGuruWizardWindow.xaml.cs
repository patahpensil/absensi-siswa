using System.Windows;

namespace Absensi.App.MasterData;

public partial class ImporGuruWizardWindow : Window
{
    public ImporGuruWizardWindow(ImporGuruWizardViewModel viewModel)
    {
        InitializeComponent();
        DataContext = viewModel;
    }
}
