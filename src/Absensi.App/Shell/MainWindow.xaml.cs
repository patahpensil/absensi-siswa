using System.Windows;

namespace Absensi.App.Shell;

public partial class MainWindow : Window
{
    // Roda mouse diteruskan ke ScrollViewer halaman bila kontrol di bawah kursor
    // (tabel tanpa luapan, kolom isian, ComboBox tertutup) tidak bisa menggulir —
    // lihat PenerusRodaMouse. Idempoten; dipasang juga di App.OnStartup.
    static MainWindow() => Absensi.App.Perilaku.PenerusRodaMouse.Pasang();

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

        // Tugas 14: App.xaml ShutdownMode="OnExplicitShutdown" — menutup
        // jendela utama (lewat tombol Keluar DI ATAS maupun tombol X bawaan
        // Windows) tidak lagi otomatis mematikan aplikasi, jadi harus
        // dipicu eksplisit di sini.
        Closed += (_, _) => Application.Current.Shutdown();
    }
}
