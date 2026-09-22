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
        //
        // UAT 2026-09-23 (redesain fundamental): KolomSiswa (dropdown baru
        // "Nama siswa") rentan terhadap BUG YANG SAMA PERSIS — didaftarkan
        // dengan pola identik supaya operator juga bisa membuka dan memilih
        // dari dropdown ini tanpa direbut KolomTap.
        KolomKelas.DropDownClosed += (_, _) => JadwalkanFokusUlangKolomTap();
        KolomSiswa.DropDownClosed += (_, _) => JadwalkanFokusUlangKolomTap();
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
    // UAT Bug #11 (2026-09-23): kalau KolomTap kehilangan fokus KARENA
    // salah satu ComboBox (Kelas ATAU Siswa) sedang dibuka operator, JANGAN
    // rebut fokus balik (itu akar masalahnya, lihat komentar di
    // constructor). DropDownClosed masing-masing yang akan mengembalikan
    // fokus setelah operator selesai memilih.
    private void KolomTap_LostFocus(object sender, RoutedEventArgs e) =>
        JadwalkanFokusUlangKolomTap(lewatiKalauAdaDropdownTerbuka: true);

    private void JadwalkanFokusUlangKolomTap(bool lewatiKalauAdaDropdownTerbuka = false)
    {
        if (_fokusUlangDijadwalkan)
        {
            return;
        }

        _fokusUlangDijadwalkan = true;
        Dispatcher.BeginInvoke(() =>
        {
            _fokusUlangDijadwalkan = false;

            // UAT Bug #11 (2026-09-23, putaran keenam — AKAR MASALAH
            // TERBUKTI lewat log diagnostik sementara, lihat A-33 di
            // MASTER-BUG-UIUX-REGISTER.md): log membuktikan tombol Simpan
            // TIDAK PERNAH memicu Click SAMA SEKALI — akar masalahnya BUKAN
            // di ComboBox (sudah diperbaiki A-30), tapi POLA YANG SAMA
            // PERSIS sekarang menyasar TOMBOL. Klik mouse pada tombol
            // memindahkan keyboard focus ke tombol itu SEBELUM mouse-up
            // terjadi (FocusedElement='Button' tercatat di
            // KolomTap_LostFocus) — kalau callback tertunda ini kebetulan
            // berjalan SELAGI tombol masih ditekan/memegang mouse capture
            // (TombolSimpan.IsPressed=True, IsMouseCaptured=True tercatat
            // di log), memanggil KolomTap.Focus() di sini merebut fokus
            // DAN mengganggu mouse capture tombol — tombol kehilangan
            // capture TANPA PERNAH memicu OnClick/Click, persis gejala
            // "Simpan diklik, tidak ada hasil apa pun" yang dilaporkan.
            // Diperbaiki dengan memeriksa Mouse.Captured secara umum (bukan
            // cuma dua ComboBox) — selama ADA elemen mana pun (tombol,
            // dropdown, dst.) yang sedang memegang gestur mouse, JANGAN
            // rebut fokus balik ke KolomTap; biarkan gestur itu selesai
            // secara alami. Fokus dikembalikan ke KolomTap secara eksplisit
            // SETELAH klik benar-benar selesai lewat TombolSimpan_Click di
            // bawah (dipanggil dari event Click, yang sekarang bisa terpicu
            // dengan benar).
            if (lewatiKalauAdaDropdownTerbuka &&
                (KolomKelas.IsDropDownOpen || KolomSiswa.IsDropDownOpen || Mouse.Captured is not null))
            {
                return;
            }

            FokusKolomTap();
        });
    }

    private void FokusKolomTap() => KolomTap.Focus();

    private void TombolBatal_Click(object sender, RoutedEventArgs e) => Close();

    // UAT Bug #11 (2026-09-23, putaran keenam): dipasang BERSAMA
    // Command="{Binding SimpanCommand}" (WPF mengizinkan Click DAN Command
    // sekaligus — Click selalu terpicu begitu OnClick berjalan, TERLEPAS
    // dari CanExecute). Sekarang klik ini benar-benar bisa terjadi (lihat
    // perbaikan di JadwalkanFokusUlangKolomTap) — dipakai untuk
    // mengembalikan fokus ke KolomTap SETELAH gestur klik selesai (kapan
    // pun: Simpan berhasil, gagal, atau CanExecute ternyata false), supaya
    // reader RFID langsung siap menerima tap berikutnya tanpa operator
    // perlu klik manual ke kolom tersembunyi itu.
    private void TombolSimpan_Click(object sender, RoutedEventArgs e) => FokusKolomTap();
}
