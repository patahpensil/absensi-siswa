using System.Windows;

namespace Absensi.App.MasterData;

public partial class AcademicPeriodWindow : Window
{
    public AcademicPeriodWindow(AcademicPeriodViewModel viewModel)
    {
        InitializeComponent();
        DataContext = viewModel;
    }
}
