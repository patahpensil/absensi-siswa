using System.ComponentModel;
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

    // Bug UX pasca serah terima: wizard sebelumnya tidak punya tombol
    // Minimize (ResizeMode=NoResize menyembunyikan Minimize DAN Maximize
    // sekaligus di WPF) — sekarang CanMinimize (lihat .xaml), sehingga
    // tombol X title bar makin gampang dijangkau. Sebelumnya menutup lewat
    // X langsung menutup tanpa konfirmasi sama sekali (tidak ada tombol
    // Batal terpisah di wizard ini); di sini ditambahkan konfirmasi supaya
    // isian yang belum tersimpan tidak hilang tanpa sengaja.
    private void Window_Closing(object sender, CancelEventArgs e)
    {
        if (DialogResult == true)
        {
            // Sudah selesai lewat TombolSelesai_Click atau dipulihkan dari
            // cloud (DipulihkanDariCloud) — bukan pembatalan.
            return;
        }

        if (ViewModel.LangkahSaatIni == LangkahWizard.Selesai)
        {
            // Instalasi sudah tertulis ke database sejak "Selesaikan
            // Instalasi" di langkah 5 — menutup di sini sama saja dengan
            // klik "Buka Halaman Masuk", bukan pembatalan yang kehilangan
            // data. WPF melarang mengisi DialogResult selagi Closing masih
            // berjalan (InvalidOperationException, ditemukan lewat
            // smoke-test .exe — dilempar dari WndProc jadi TIDAK crash
            // terlihat, cuma X diam saja tidak menutup apa-apa): batalkan
            // penutupan yang sedang berjalan ini, lalu jadwalkan penutupan
            // BARU lewat Dispatcher supaya berjalan setelah Closing ini
            // selesai — persis alur TombolSelesai_Click. Dikonfirmasi lewat
            // smoke-test: mengisi DialogResult di sini memicu Close()
            // internal WPF sendiri (Closing terpanggil ulang dengan
            // DialogResult sudah true, langsung lolos cabang di atas).
            e.Cancel = true;
            Dispatcher.BeginInvoke(() =>
            {
                DialogResult = true;
                Close();
            });
            return;
        }

        var hasil = MessageBox.Show(
            this,
            "Instalasi belum selesai. Semua isian sejauh ini akan hilang. Batalkan instalasi?",
            "Batalkan instalasi?",
            MessageBoxButton.YesNo,
            MessageBoxImage.Warning,
            MessageBoxResult.No);

        if (hasil != MessageBoxResult.Yes)
        {
            e.Cancel = true;
        }
    }
}
