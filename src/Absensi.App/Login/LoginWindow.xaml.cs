using System.Windows;
using System.Windows.Controls;
using Absensi.Data.Repositori;

namespace Absensi.App.Login;

public partial class LoginWindow : Window
{
    public AkunTersimpan? AkunLogin { get; private set; }

    public LoginWindow(LoginViewModel viewModel)
    {
        InitializeComponent();
        DataContext = viewModel;
        // Shown without ShowDialog (App.ShowLogin), so no DialogResult: App reads
        // AkunLogin when the window closes.
        viewModel.LoginBerhasil += (_, akun) =>
        {
            AkunLogin = akun;
            Close();
        };
    }

    private void KolomSandi_PasswordChanged(object sender, RoutedEventArgs e)
    {
        ((LoginViewModel)DataContext).AturSandi(KolomSandi.Password);
    }
}
