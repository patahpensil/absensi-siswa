using System.Windows.Input;
using Absensi.App.RegistrasiKartu;
using Absensi.Data.Repositori;
using Absensi.Domain.Akun;

namespace Absensi.App.Tests;

// UAT Bug #11 (2026-09-23, putaran keempat) — akar masalah TERBUKTI lewat
// log diagnostik produksi: KolomTap_LostFocus merebut fokus balik dari
// ComboBox Kelas SETIAP kali ComboBox mengambil fokus untuk membuka
// dropdown-nya, menutup paksa dropdown itu sebelum operator sempat
// mengklik item apa pun (log menunjukkan DropDownOpened terpicu puluhan
// kali berturut-turut, SelectedItem tetap null). Tes ini MENYIMULASIKAN
// interaksi persis itu lewat Window sungguhan (Show + pompa Dispatcher,
// pola sama seperti SiswaGuruTabelKolomTests) — bukan cuma memeriksa
// DaftarKelas terisi (yang sudah dibuktikan BENAR sejak awal dan bukan
// akar masalahnya).
//
// SATU tes, SATU Window/StaHost.Jalankan — mencakup kedua sisi perbaikan
// (dropdown tidak tertutup paksa DAN fokus kembali ke KolomTap setelah
// selesai) berurutan dalam satu sesi. Dipecah jadi dua [Fact] terpisah
// sebelumnya TERBUKTI membocorkan state Popup/fokus WPF antar tes lewat
// StaHost yang dibagi (dan thread STA terpisah per tes JUGA terbukti tidak
// stabil untuk Popup — DropDownClosed tidak reliable lintas thread dari
// thread yang membuat Application) — satu sesi berurutan jauh lebih
// deterministik dan tetap menguji persis skenario UAT nyata.
public sealed class RegistrasiKartuWindowTests
{
    private static readonly DateTime Waktu = new(2026, 9, 17, 8, 0, 0);
    private static readonly AkunTersimpan Admin = new(1, "admin1", "x", PeranAkun.Admin, null, true);

    [Fact]
    public void ComboBoxKelas_InteraksiPenuh_TidakTertutupPaksaDanFokusKembaliSetelahPilih()
    {
        using var database = new MigratedDatabase();
        database.SeedKelasUntukRegistrasiKartu();
        var viewModel = new RegistrasiKartuViewModel(
            database.Connection, Admin, new PenyediaWaktuUji(Waktu), new StatusModeRegistrasi(), tahunAjaranId: 1);

        var dropdownMasihTerbukaSetelahLostFocus = false;
        var dropDownClosedTerpicu = false;
        var kolomTapFokusSetelahPilihKelas = false;

        StaHost.Jalankan(() =>
        {
            var window = new RegistrasiKartuWindow(viewModel);
            window.Show();
            PompaDispatcher(5);

            var kolomKelas = (System.Windows.Controls.ComboBox)window.FindName("KolomKelas")!;
            var kolomTap = (System.Windows.Controls.TextBox)window.FindName("KolomTap")!;
            kolomKelas.DropDownClosed += (_, _) => dropDownClosedTerpicu = true;

            // KolomTap fokus awal (perilaku Loaded/Activated yang sudah
            // ada) — pastikan starting point sesuai keadaan sungguhan.
            Keyboard.Focus(kolomTap);
            PompaDispatcher(2);

            // --- Bagian 1: operator KLIK ComboBox Kelas untuk membuka
            // dropdown-nya — fokus pindah ke ComboBox (memicu
            // KolomTap_LostFocus), dropdown terbuka. Ini persis urutan WPF
            // menangani klik pada ComboBox tertutup (fokus dulu, baru buka).
            Keyboard.Focus(kolomKelas);
            kolomKelas.IsDropDownOpen = true;
            PompaDispatcher(5);

            dropdownMasihTerbukaSetelahLostFocus = kolomKelas.IsDropDownOpen;

            // --- Bagian 2: operator MEMILIH kelas, dropdown menutup —
            // reader RFID harus bisa langsung dipakai lagi tanpa klik
            // manual ke kolom tap.
            kolomKelas.SelectedItem = kolomKelas.Items[0];
            kolomKelas.IsDropDownOpen = false;

            for (var i = 0; i < 20 && !ReferenceEquals(Keyboard.FocusedElement, kolomTap); i++)
            {
                PompaDispatcher(1);
            }

            kolomTapFokusSetelahPilihKelas = ReferenceEquals(Keyboard.FocusedElement, kolomTap);

            window.Close();
        });

        Assert.True(
            dropdownMasihTerbukaSetelahLostFocus,
            "ComboBox Kelas tertutup paksa oleh KolomTap yang merebut fokus balik — ini persis Bug #11 yang dilaporkan UAT (dropdown tidak bisa dibuka).");
        Assert.True(dropDownClosedTerpicu, "Event DropDownClosed tidak terpicu — pemicu refocus KolomTap tidak akan jalan.");
        Assert.True(
            kolomTapFokusSetelahPilihKelas,
            "Setelah memilih kelas dan dropdown menutup, fokus harus kembali ke KolomTap (reader RFID) — bukan tertinggal di ComboBox.");
    }

    private static void PompaDispatcher(int jumlah)
    {
        for (var i = 0; i < jumlah; i++)
        {
            var frame = new System.Windows.Threading.DispatcherFrame();
            System.Windows.Threading.Dispatcher.CurrentDispatcher.BeginInvoke(
                System.Windows.Threading.DispatcherPriority.Background,
                new Action(() => frame.Continue = false));
            System.Windows.Threading.Dispatcher.PushFrame(frame);
        }
    }
}
