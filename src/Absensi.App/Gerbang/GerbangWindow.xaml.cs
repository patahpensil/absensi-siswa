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
    private bool _fokusUlangDijadwalkan;

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
            // docs/INVESTIGASI-CRASH-2026-09-21.md — Crash D-2 (TERBUKTI):
            // memanggil Close() sinkron di sini (masih di dalam OnClosing
            // yang sedang berjalan untuk permintaan tutup PERTAMA) melanggar
            // Window.VerifyNotClosing() -> InvalidOperationException lolos
            // sampai batas WndProc. Sama persis pola bug Wizard yang sudah
            // diperbaiki di commit e2ec306: jadwalkan Close() lewat
            // Dispatcher.BeginInvoke supaya jalan SETELAH OnClosing ini
            // selesai — OnClosing KEDUA (dipicu Close() terjadwal) langsung
            // lolos lewat cabang _diizinkanTutup di atas, tanpa dialog lagi.
            _diizinkanTutup = true;
            Dispatcher.BeginInvoke(() => Close());
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
    // docs/INVESTIGASI-CRASH-2026-09-21.md §11: memanggil Focus() SINKRON di
    // sini bisa memicu rekursi WPF-internal (KeyboardDevice.Focus ->
    // ChangeFocus -> ... -> GotKeyboardFocus -> ... -> LostFocus lagi) sampai
    // StackOverflowException saat sesuatu di luar (mis. klien UI Automation)
    // ikut memperebutkan fokus pada saat yang sama. Menjadwalkan lewat
    // Dispatcher.BeginInvoke memutus rantai panggilan SINKRON itu (pola yang
    // sama seperti perbaikan Crash D-2 di OnClosing) -- flag mencegah lebih
    // dari satu penjadwalan tertunda menumpuk kalau LostFocus terpicu lagi
    // sebelum yang tertunda sempat berjalan.
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
}
