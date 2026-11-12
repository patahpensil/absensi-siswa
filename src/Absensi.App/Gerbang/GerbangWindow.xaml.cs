using System.IO;
using System.Text.Json;
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
    private bool _dialogTutupSedangTerbuka;
    private SerialGateReaders? _serialReaders;
    private bool _serialRequested;
    private bool _readerFault;
    private bool _closing;

    public GerbangWindow(GerbangViewModel viewModel, SqliteConnection connection)
    {
        InitializeComponent();
        _viewModel = viewModel;
        _connection = connection;
        DataContext = viewModel;

        _timer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
        _timer.Tick += (_, _) =>
        {
            _viewModel.EvaluasiKartuKedaluwarsa();
            if (_serialReaders is not null) CheckSerialReaders();
        };
        _timer.Start();

        Loaded += (_, _) => StartReaders();
        Activated += (_, _) => RefreshReaderStatus();
        Deactivated += (_, _) =>
        {
            KolomTap.Clear();
            RefreshReaderStatus();
        };
        Closed += (_, _) =>
        {
            _closing = true;
            _timer.Stop();
            _serialReaders?.Dispose();
        };

        // Tingkat ukuran teks/jarak mengikuti tinggi area jendela
        // (TipografiGerbang): 1366x768 -> kompak, 1920x1080 -> panduan §8.1.
        SizeChanged += (_, e) => _viewModel.SesuaikanDenganTinggi(e.NewSize.Height);
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

        if (_dialogTutupSedangTerbuka)
        {
            // UAT 2026-09-22: cegah dialog konfirmasi dobel kalau Close()
            // terpanggil lagi (mis. Alt+F4 ganda) selagi dialog PERTAMA
            // masih terbuka — pola flag yang sama seperti
            // _sedangMenanganiGalatFatal di App.xaml.cs untuk masalah
            // sejenis (mencegah tumpukan dialog).
            return;
        }

        _dialogTutupSedangTerbuka = true;
        RefreshReaderStatus();
        try
        {
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
        finally
        {
            _dialogTutupSedangTerbuka = false;
            RefreshReaderStatus();
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

        if (uid.Length > 0 && !_serialRequested && IsActive && !_dialogTutupSedangTerbuka)
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

    private void StartReaders()
    {
        var path = Path.Combine(LokasiData.FolderData, "rfid-serial.json");
        _serialRequested = File.Exists(path);
        try
        {
            _serialReaders = SerialGateReaders.Open(path,
                uid => Dispatcher.BeginInvoke(() =>
                {
                    if (!_closing && !_diizinkanTutup && !_dialogTutupSedangTerbuka && !_readerFault)
                        _viewModel.ProsesTap(uid);
                }),
                () => Dispatcher.BeginInvoke(() =>
                {
                    _readerFault = true;
                    RefreshReaderStatus();
                }));
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or InvalidOperationException or JsonException or ArgumentException)
        {
            _readerFault = true;
            Absensi.Data.Diagnostik.PencatatGalat.Catat(ex, "Gerbang.Serial", "ERROR");
        }
        RefreshReaderStatus();
    }

    private void CheckSerialReaders()
    {
        try
        {
            _readerFault |= !_serialReaders!.AllConnected();
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            _readerFault = true;
        }
        RefreshReaderStatus();
    }

    private void RefreshReaderStatus()
    {
        ReaderStatus.Text = _readerFault ? "Reader serial bermasalah. Periksa sambungan, lalu buka ulang gerbang."
            : _dialogTutupSedangTerbuka ? "Tap dijeda selama konfirmasi penutupan."
            : _serialRequested ? "Reader serial siap menerima tap."
            : IsActive ? "Reader keyboard siap menerima tap."
            : "Tap dijeda. Aktifkan Layar Gerbang sebelum menempelkan kartu.";
        FokusKolomTap();
    }

    private void FokusKolomTap()
    {
        if (!_serialRequested && IsActive && !_dialogTutupSedangTerbuka && !_diizinkanTutup)
            KolomTap.Focus();
    }
}
