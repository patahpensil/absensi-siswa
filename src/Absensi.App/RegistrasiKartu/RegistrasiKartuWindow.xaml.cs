using System.Windows;
using System.Windows.Input;

namespace Absensi.App.RegistrasiKartu;

public partial class RegistrasiKartuWindow : Window
{
    private readonly RegistrasiKartuViewModel _viewModel;
    private bool _fokusUlangDijadwalkan;

    public RegistrasiKartuWindow(RegistrasiKartuViewModel viewModel)
    {
        InitializeComponent();
        _viewModel = viewModel;
        DataContext = viewModel;
        viewModel.Selesai += (_, _) => Close();

        Loaded += (_, _) => FokusKolomTap();
        Activated += (_, _) => FokusKolomTap();

        // UAT Bug #11 (2026-09-23) — akar masalah TERBUKTI lewat log
        // diagnostik produksi (PencatatGalat): ComboBox.DropDownOpened
        // terpicu PULUHAN KALI berturut-turut dalam hitungan detik dengan
        // SelectedItem tetap null di setiap kejadian — bukan interaksi
        // pengguna normal, itu loop buka-tutup paksa. Begitu operator klik
        // ComboBox Kelas, KolomTap kehilangan fokus -> KolomTap_LostFocus
        // (di bawah) langsung menjadwalkan Focus() balik ke KolomTap ->
        // ComboBox kehilangan fokus -> dropdown-nya tertutup paksa
        // sebelum operator sempat mengklik satu item pun. Operator
        // melihat ini sebagai "dropdown tidak bisa dibuka/kosong".
        // DropDownClosed di sini yang sekarang bertanggung jawab
        // mengembalikan fokus ke KolomTap SETELAH operator selesai
        // memilih kelas (lihat KolomTap_LostFocus untuk sisi lainnya).
        KolomKelas.DropDownClosed += (_, _) => JadwalkanFokusUlangKolomTap(lewatiKalauDropdownTerbuka: false);
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

    // docs/INVESTIGASI-CRASH-2026-09-21.md §11: Focus() SINKRON di sini bisa
    // memicu rekursi WPF-internal sampai StackOverflowException kalau
    // sesuatu di luar (mis. klien UI Automation) ikut memperebutkan fokus.
    // Dispatcher.BeginInvoke memutus rantai panggilan sinkron itu (pola sama
    // seperti perbaikan Crash D-2 di GerbangWindow.OnClosing); flag mencegah
    // penjadwalan tertunda menumpuk.
    //
    // UAT Bug #11 (2026-09-23): lewatiKalauDropdownTerbuka=true di sini —
    // kalau KolomTap kehilangan fokus KARENA ComboBox Kelas sedang dibuka
    // operator, JANGAN rebut fokus balik (itu akar masalahnya, lihat
    // komentar di constructor). DropDownClosed yang akan mengembalikan
    // fokus setelah operator selesai memilih.
    private void KolomTap_LostFocus(object sender, RoutedEventArgs e) =>
        JadwalkanFokusUlangKolomTap(lewatiKalauDropdownTerbuka: true);

    private void JadwalkanFokusUlangKolomTap(bool lewatiKalauDropdownTerbuka)
    {
        if (_fokusUlangDijadwalkan)
        {
            return;
        }

        _fokusUlangDijadwalkan = true;
        Dispatcher.BeginInvoke(() =>
        {
            _fokusUlangDijadwalkan = false;

            // Diperiksa di DALAM callback tertunda (bukan cuma sebelum
            // dijadwalkan) supaya status IsDropDownOpen yang dibaca sudah
            // stabil — klik pembuka ComboBox dan event LostFocus KolomTap
            // tidak selalu selesai dalam urutan yang sama persis.
            if (lewatiKalauDropdownTerbuka && KolomKelas.IsDropDownOpen)
            {
                return;
            }

            FokusKolomTap();
        });
    }

    private void FokusKolomTap() => KolomTap.Focus();

    private void TombolBatal_Click(object sender, RoutedEventArgs e) => Close();
}
