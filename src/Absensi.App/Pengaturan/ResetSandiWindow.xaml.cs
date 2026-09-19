using System.Windows;

namespace Absensi.App.Pengaturan;

public partial class ResetSandiWindow : Window
{
    public ResetSandiWindow(ResetSandiViewModel viewModel)
    {
        InitializeComponent();
        DataContext = viewModel;
        viewModel.Selesai += (_, _) =>
        {
            DialogResult = true;
            Close();
        };
    }

    private void KolomSandiBaru_PasswordChanged(object sender, RoutedEventArgs e) =>
        ((ResetSandiViewModel)DataContext).AturSandiBaru(KolomSandiBaru.Password);

    private void KolomKonfirmasiSandi_PasswordChanged(object sender, RoutedEventArgs e) =>
        ((ResetSandiViewModel)DataContext).AturKonfirmasiSandi(KolomKonfirmasiSandi.Password);

    private void TombolBatal_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
        Close();
    }
}
