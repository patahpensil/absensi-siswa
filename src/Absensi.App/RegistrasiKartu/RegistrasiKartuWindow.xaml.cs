using System.Windows;
using System.Windows.Input;

namespace Absensi.App.RegistrasiKartu;

public partial class RegistrasiKartuWindow : Window
{
    private readonly RegistrasiKartuViewModel _viewModel;

    public RegistrasiKartuWindow(RegistrasiKartuViewModel viewModel)
    {
        InitializeComponent();
        _viewModel = viewModel;
        DataContext = viewModel;
        viewModel.Selesai += (_, _) => Close();

        Loaded += (_, _) => FokusKolomTap();
        Activated += (_, _) => FokusKolomTap();
    }

    // Mode Registrasi wajib mati begitu layar ini hilang, dengan cara
    // apa pun ia tertutup (Simpan, Batal, atau tombol X) — gagal-aman
    // sesuai StatusModeRegistrasi.
    protected override void OnClosed(EventArgs e)
    {
        _viewModel.MatikanModeRegistrasi();
        base.OnClosed(e);
    }

    private void KolomTap_PreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key != Key.Enter)
        {
            return;
        }

        var uid = KolomTap.Text.Trim();
        KolomTap.Clear();
        e.Handled = true;

        if (uid.Length > 0)
        {
            _viewModel.TapUid(uid);
        }
    }

    private void KolomTap_LostFocus(object sender, RoutedEventArgs e) => FokusKolomTap();

    private void FokusKolomTap() => KolomTap.Focus();

    private void TombolBatal_Click(object sender, RoutedEventArgs e) => Close();
}
