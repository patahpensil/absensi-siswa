using System.Windows;

namespace Absensi.App.Wizard;

public partial class WizardInstalasiWindow : Window
{
    public WizardInstalasiViewModel ViewModel { get; }

    // Terisi hanya kalau wizard diselesaikan lewat jalur pemulihan cloud
    // (PRD §18.3) — App.xaml.cs memakainya untuk membedakan dua jalur
    // selesai (isi kembali database dari sini, atau lanjut biasa karena
    // ViewModel sudah menulis lewat LayananWizard).
    public byte[]? DataDipulihkanDariCloud { get; private set; }

    public WizardInstalasiWindow(WizardInstalasiViewModel viewModel)
    {
        InitializeComponent();
        ViewModel = viewModel;
        DataContext = viewModel;

        viewModel.DipulihkanDariCloud += (_, data) =>
        {
            DataDipulihkanDariCloud = data;
            DialogResult = true;
            Close();
        };
    }

    // PasswordBox.Password bukan DependencyProperty (alasan keamanan), jadi
    // tidak bisa dua-arah lewat binding biasa (pola sama seperti LoginWindow).
    private void KolomSandi_PasswordChanged(object sender, RoutedEventArgs e) => ViewModel.SandiKepalaSekolah = KolomSandi.Password;

    private void KolomKonfirmasiSandi_PasswordChanged(object sender, RoutedEventArgs e) => ViewModel.KonfirmasiSandi = KolomKonfirmasiSandi.Password;

    private void TombolSelesai_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = true;
        Close();
    }
}
