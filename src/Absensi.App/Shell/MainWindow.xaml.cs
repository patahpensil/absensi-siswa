using System.Windows;

namespace Absensi.App.Shell;

public partial class MainWindow : Window
{
    public MainWindow(
        MainWindowViewModel viewModel,
        Func<Window> bukaGerbang,
        Func<Window?> bukaRegistrasiKartu,
        Action<byte[]> restoreSelesaiPerluTutupAplikasi)
    {
        InitializeComponent();
        DataContext = viewModel;
        viewModel.BukaGerbangDiminta += (_, _) => bukaGerbang().Show();

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
        };

        viewModel.RestoreSelesaiPerluTutupAplikasi += (_, data) => restoreSelesaiPerluTutupAplikasi(data);

        viewModel.KeluarDiminta += (_, _) => Close();

        // Tugas 14: App.xaml ShutdownMode="OnExplicitShutdown" — menutup
        // jendela utama (lewat tombol Keluar DI ATAS maupun tombol X bawaan
        // Windows) tidak lagi otomatis mematikan aplikasi, jadi harus
        // dipicu eksplisit di sini.
        Closed += (_, _) => Application.Current.Shutdown();
    }
}
