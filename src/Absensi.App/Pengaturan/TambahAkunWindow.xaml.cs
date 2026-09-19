using System.Windows;
using Absensi.Data.Repositori;

namespace Absensi.App.Pengaturan;

public partial class TambahAkunWindow : Window
{
    public AkunTersimpan? AkunBaru { get; private set; }

    public TambahAkunWindow(TambahAkunViewModel viewModel)
    {
        InitializeComponent();
        DataContext = viewModel;
        viewModel.Tersimpan += (_, akun) =>
        {
            AkunBaru = akun;
            DialogResult = true;
            Close();
        };
    }

    private void KolomSandi_PasswordChanged(object sender, RoutedEventArgs e) =>
        ((TambahAkunViewModel)DataContext).AturSandi(KolomSandi.Password);

    private void KolomKonfirmasiSandi_PasswordChanged(object sender, RoutedEventArgs e) =>
        ((TambahAkunViewModel)DataContext).AturKonfirmasiSandi(KolomKonfirmasiSandi.Password);

    private void TombolBatal_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
        Close();
    }
}
