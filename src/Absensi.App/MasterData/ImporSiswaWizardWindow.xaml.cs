using System.Windows;

namespace Absensi.App.MasterData;

public partial class ImporSiswaWizardWindow : Window
{
    public ImporSiswaWizardWindow(ImporSiswaWizardViewModel viewModel)
    {
        InitializeComponent();
        DataContext = viewModel;
    }
}
