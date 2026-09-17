using System.Collections.ObjectModel;
using Absensi.App.MasterData;
using Absensi.App.Mvvm;
using Absensi.Data.Repositori;
using Absensi.Domain;
using Microsoft.Data.Sqlite;

namespace Absensi.App.Shell;

// Kerangka jendela utama (RENCANA-TUGAS.md Tugas 5) + isi nyata untuk menu
// Siswa/Guru/Kelas (Tugas 7). Sisa menu (Dashboard, Koreksi, Kalender,
// dst.) masih menunggu tugasnya sendiri dan tetap menampilkan placeholder.
// Menu "Absensi" tetap istimewa: satu tombol ("Buka layar gerbang") karena
// gerbang adalah jendela fullscreen terpisah, bukan konten yang disisipkan.
public sealed class MainWindowViewModel : ObservableObject
{
    private readonly SqliteConnection _connection;
    private readonly AkunTersimpan _akun;
    private readonly IPenyediaWaktu _penyediaWaktu;
    private readonly int? _tahunAjaranId;

    private string _menuAktif;
    private object? _kontenAktif;
    private SiswaViewModel? _siswaViewModel;
    private GuruViewModel? _guruViewModel;
    private KelasViewModel? _kelasViewModel;

    public MainWindowViewModel(
        SqliteConnection connection,
        AkunTersimpan akun,
        IPenyediaWaktu penyediaWaktu,
        string namaSekolah,
        string? logoPath,
        string? tahunAjaranAktif,
        int? tahunAjaranId)
    {
        _connection = connection;
        _akun = akun;
        _penyediaWaktu = penyediaWaktu;
        _tahunAjaranId = tahunAjaranId;

        NamaSekolah = string.IsNullOrWhiteSpace(namaSekolah) ? "Nama sekolah belum diatur" : namaSekolah;
        LogoPath = logoPath;
        TahunAjaranAktif = string.IsNullOrWhiteSpace(tahunAjaranAktif) ? "Tahun ajaran belum diatur" : tahunAjaranAktif;

        MenuItems = new ObservableCollection<string>
        {
            "Dashboard", "Absensi", "Siswa", "Guru", "Kelas", "Koreksi",
            "Kalender", "Laporan", "Notifikasi", "Audit", "Pengaturan",
        };

        _menuAktif = MenuItems[0];
        BukaGerbangCommand = new RelayCommand(() => BukaGerbangDiminta?.Invoke(this, EventArgs.Empty));
        PerbaruiKontenAktif();
    }

    public event EventHandler? BukaGerbangDiminta;

    public string NamaSekolah { get; }

    public string? LogoPath { get; }

    public string TahunAjaranAktif { get; }

    public ObservableCollection<string> MenuItems { get; }

    public RelayCommand BukaGerbangCommand { get; }

    public string MenuAktif
    {
        get => _menuAktif;
        set
        {
            if (SetField(ref _menuAktif, value))
            {
                OnPropertyChanged(nameof(PesanKonten));
                OnPropertyChanged(nameof(TampilkanTombolGerbang));
                PerbaruiKontenAktif();
            }
        }
    }

    public bool TampilkanTombolGerbang => MenuAktif == "Absensi";

    public string PesanKonten => _tahunAjaranId is null
        ? "Tahun ajaran aktif belum diatur."
        : $"Layar \"{MenuAktif}\" belum dibangun.";

    // Ditampilkan lewat DataTemplate implisit (Resources/GayaAplikasi.xaml)
    // saat MenuAktif == "Siswa"/"Guru"/"Kelas" dan tahun ajaran aktif ada;
    // null berarti tampilkan PesanKonten sebagai teks biasa.
    public object? KontenAktif
    {
        get => _kontenAktif;
        private set
        {
            if (SetField(ref _kontenAktif, value))
            {
                OnPropertyChanged(nameof(TampilkanPlaceholder));
            }
        }
    }

    public bool TampilkanPlaceholder => KontenAktif is null;

    private void PerbaruiKontenAktif()
    {
        if (_tahunAjaranId is not { } tahunAjaranId)
        {
            KontenAktif = null;
            return;
        }

        KontenAktif = MenuAktif switch
        {
            "Siswa" => _siswaViewModel ??= new SiswaViewModel(_connection, _akun, _penyediaWaktu, tahunAjaranId),
            "Guru" => _guruViewModel ??= new GuruViewModel(_connection, _akun, _penyediaWaktu),
            "Kelas" => _kelasViewModel ??= new KelasViewModel(_connection, _akun, _penyediaWaktu, tahunAjaranId),
            _ => null,
        };
    }
}
