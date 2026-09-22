using System.Windows;

namespace Absensi.App.Gerbang;

public partial class KonfirmasiTutupWindow : Window
{
    public KonfirmasiTutupWindow(KonfirmasiTutupViewModel viewModel)
    {
        InitializeComponent();
        DataContext = viewModel;
        viewModel.DiizinkanTutup += (_, _) =>
        {
            DialogResult = true;
            Close();
        };

        // UAT 2026-09-22: operator harus langsung bisa mengetik username
        // tanpa klik dulu (layar gerbang tidak pakai mouse, PRD §14.1).
        Loaded += (_, _) => KolomUsername.Focus();
    }

    private void KolomSandi_PasswordChanged(object sender, RoutedEventArgs e)
    {
        ((KonfirmasiTutupViewModel)DataContext).AturSandi(KolomSandi.Password);
    }

    private void TombolBatal_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
        Close();
    }
}
