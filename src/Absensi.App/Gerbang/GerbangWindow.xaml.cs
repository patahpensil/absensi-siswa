using System.ComponentModel;
using System.Windows;
using System.Windows.Input;
using System.Windows.Threading;
using Microsoft.Data.Sqlite;

namespace Absensi.App.Gerbang;

public partial class GerbangWindow : Window
{
    private readonly GerbangViewModel _viewModel;
    private readonly SqliteConnection _connection;
    private readonly DispatcherTimer _timer;
    private bool _diizinkanTutup;

    public GerbangWindow(GerbangViewModel viewModel, SqliteConnection connection)
    {
        InitializeComponent();
        _viewModel = viewModel;
        _connection = connection;
        DataContext = viewModel;

        _timer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
        _timer.Tick += (_, _) => _viewModel.EvaluasiKartuKedaluwarsa();
        _timer.Start();

        Loaded += (_, _) => FokusKolomTap();
        Activated += (_, _) => FokusKolomTap();
    }

    // RENCANA-TUGAS.md Tugas 6: "Layar tidak bisa ditutup tanpa sandi."
    // Berlaku untuk Alt+F4 dan sumber Close() apa pun, tidak hanya tombol
    // (tidak ada tombol tutup — WindowStyle="None").
    protected override void OnClosing(CancelEventArgs e)
    {
        if (_diizinkanTutup)
        {
            _timer.Stop();
            base.OnClosing(e);
            return;
        }

        e.Cancel = true;

        var dialog = new KonfirmasiTutupWindow(new KonfirmasiTutupViewModel(_connection)) { Owner = this };
        if (dialog.ShowDialog() == true)
        {
            _diizinkanTutup = true;
            Close();
        }
        else
        {
            FokusKolomTap();
        }
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
            _viewModel.ProsesTap(uid);
        }
    }

    // PANDUAN-UI-UX.md §10: fokus tidak boleh lepas dari kolom pembaca kartu.
    private void KolomTap_LostFocus(object sender, RoutedEventArgs e) => FokusKolomTap();

    private void FokusKolomTap() => KolomTap.Focus();
}
