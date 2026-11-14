using System.Windows;
using System.Windows.Threading;
using Absensi.Domain;

namespace Absensi.App.Shell;

public partial class MainWindow : Window
{
    // Roda mouse diteruskan ke ScrollViewer halaman bila kontrol di bawah kursor
    // (tabel tanpa luapan, kolom isian, ComboBox tertutup) tidak bisa menggulir —
    // lihat PenerusRodaMouse. Idempoten; dipasang juga di App.OnStartup.
    static MainWindow()
    {
        Absensi.App.Perilaku.PenerusRodaMouse.Pasang();
        WindowActivity.Install();
        Absensi.App.Gerbang.GateStaysEnabled.Install();
    }

    private readonly IdleTimeout _idle;
    private readonly DispatcherTimer _idleTimer = new() { Interval = TimeSpan.FromSeconds(15) };

    // True when the window closed because of the 10-minute idle limit (P-24).
    public bool SignedOutForIdle { get; private set; }

    public MainWindow(
        MainWindowViewModel viewModel,
        Func<Window> bukaGerbang,
        Func<Window?> bukaRegistrasiKartu,
        Action<byte[]> restoreSelesaiPerluTutupAplikasi,
        IPenyediaWaktu? clock = null)
    {
        InitializeComponent();
        DataContext = viewModel;
        _idle = new IdleTimeout(clock ?? new PenyediaWaktuSistem());
        WindowActivity.Input += _idle.RecordActivity;
        _idleTimer.Tick += (_, _) =>
        {
            if (_idle.IsExpired)
            {
                SignOutForIdle();
            }
        };
        _idleTimer.Start();
        Window? gate = null;
        viewModel.BukaGerbangDiminta += (_, _) =>
        {
            if (gate is not null) { gate.Activate(); return; }
            gate = bukaGerbang();
            gate.Closed += (_, _) => gate = null;
            gate.Show();
        };

        // Dialog modal (bukan fullscreen seperti gerbang) — pengguna
        // menyelesaikan atau membatalkan sesi registrasi sebelum kembali
        // ke layar lain (RENCANA-TUGAS.md Tugas 8).
        //
        // UAT Bug #11 (2026-09-22): bukaRegistrasiKartu() sekarang bisa
        // mengembalikan null (tidak ada tahun ajaran aktif — App.xaml.cs
        // sudah menampilkan pesannya sendiri) alih-alih melempar exception
        // yang dulu bocor sampai ke crash handler global dan menutup
        // aplikasi. null berarti: jangan buka jendela apa pun.
        viewModel.BukaRegistrasiKartuDiminta += (_, _) =>
        {
            var jendela = bukaRegistrasiKartu();
            if (jendela is null)
            {
                return;
            }

            jendela.Owner = this;
            jendela.ShowDialog();

            // UAT Bug #11 (2026-09-23, putaran kelima): RegistrasiKartuWindow
            // TIDAK PERNAH terhubung ke SiswaViewModel sama sekali — kalau
            // operator sudah pernah membuka panel Siswa sebelumnya (jadi
            // ter-cache lewat "??=" di MainWindowViewModel), kolom Kartu di
            // sana tetap menunjukkan status LAMA (False) walau kartu
            // BERHASIL disimpan lewat dialog ini, sampai operator pindah
            // menu dan balik lagi secara manual. Panggilan ini SELALU
            // dijalankan begitu dialog tertutup (Simpan MAUPUN Batal —
            // reload data yang sudah benar tidak berbahaya walau tidak ada
            // perubahan) supaya Kartu=True langsung terlihat tanpa restart.
            viewModel.MuatUlangSiswaSetelahRegistrasiKartu();
        };

        viewModel.RestoreSelesaiPerluTutupAplikasi += (_, data) => restoreSelesaiPerluTutupAplikasi(data);

        viewModel.KeluarDiminta += (_, _) => Close();

        // P-24: closing the working window signs the user out and App shows
        // the login window again; the gate window, if open, keeps running.
        Closed += (_, _) =>
        {
            _idleTimer.Stop();
            WindowActivity.Input -= _idle.RecordActivity;
        };
    }

    private void SignOutForIdle()
    {
        _idleTimer.Stop();
        SignedOutForIdle = true;
        // Dialogs opened from this window close first so their ShowDialog
        // calls return before the owner closes.
        foreach (var owned in OwnedWindows.Cast<Window>().ToList())
        {
            owned.Close();
        }

        Close();
    }
}
