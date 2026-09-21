using System.Windows;
using System.Windows.Input;

namespace Absensi.App.RegistrasiKartu;

public partial class TapKartuTunggalWindow : Window
{
    private readonly TapKartuTunggalViewModel _viewModel;
    private bool _fokusUlangDijadwalkan;

    public TapKartuTunggalWindow(TapKartuTunggalViewModel viewModel)
    {
        InitializeComponent();
        _viewModel = viewModel;
        DataContext = viewModel;
        viewModel.Selesai += (_, _) => Close();

        Loaded += (_, _) => FokusKolomTap();
        Activated += (_, _) => FokusKolomTap();
    }

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

    // docs/INVESTIGASI-CRASH-2026-09-21.md §11: Focus() SINKRON di sini bisa
    // memicu rekursi WPF-internal sampai StackOverflowException kalau
    // sesuatu di luar (mis. klien UI Automation) ikut memperebutkan fokus.
    // Dispatcher.BeginInvoke memutus rantai panggilan sinkron itu (pola sama
    // seperti perbaikan Crash D-2 di GerbangWindow.OnClosing); flag mencegah
    // penjadwalan tertunda menumpuk.
    private void KolomTap_LostFocus(object sender, RoutedEventArgs e)
    {
        if (_fokusUlangDijadwalkan)
        {
            return;
        }

        _fokusUlangDijadwalkan = true;
        Dispatcher.BeginInvoke(() =>
        {
            _fokusUlangDijadwalkan = false;
            FokusKolomTap();
        });
    }

    private void FokusKolomTap() => KolomTap.Focus();

    private void TombolBatal_Click(object sender, RoutedEventArgs e) => Close();
}
