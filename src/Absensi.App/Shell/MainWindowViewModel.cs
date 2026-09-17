using System.Collections.ObjectModel;
using Absensi.App.Mvvm;

namespace Absensi.App.Shell;

// Kerangka jendela utama (RENCANA-TUGAS.md Tugas 5): header + menu samping
// sesuai PRD §14.2. Belum ada layar sungguhan di balik tiap menu — itu
// tugas-tugas berikutnya (Absensi = Tugas 6, Siswa = Tugas 7, dst).
public sealed class MainWindowViewModel : ObservableObject
{
    private string _menuAktif;

    public MainWindowViewModel(string namaSekolah, string? logoPath, string? tahunAjaranAktif)
    {
        NamaSekolah = string.IsNullOrWhiteSpace(namaSekolah) ? "Nama sekolah belum diatur" : namaSekolah;
        LogoPath = logoPath;
        TahunAjaranAktif = string.IsNullOrWhiteSpace(tahunAjaranAktif) ? "Tahun ajaran belum diatur" : tahunAjaranAktif;

        MenuItems = new ObservableCollection<string>
        {
            "Dashboard", "Absensi", "Siswa", "Guru", "Kelas", "Koreksi",
            "Kalender", "Laporan", "Notifikasi", "Audit", "Pengaturan",
        };

        _menuAktif = MenuItems[0];
    }

    public string NamaSekolah { get; }

    public string? LogoPath { get; }

    public string TahunAjaranAktif { get; }

    public ObservableCollection<string> MenuItems { get; }

    public string MenuAktif
    {
        get => _menuAktif;
        set
        {
            if (SetField(ref _menuAktif, value))
            {
                OnPropertyChanged(nameof(PesanKonten));
            }
        }
    }

    public string PesanKonten => $"Layar \"{MenuAktif}\" belum dibangun.";
}
