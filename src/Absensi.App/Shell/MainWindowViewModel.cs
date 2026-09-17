using System.Collections.ObjectModel;
using Absensi.App.Kalender;
using Absensi.App.MasterData;
using Absensi.App.Mvvm;
using Absensi.App.Notifikasi;
using Absensi.App.Pengaturan;
using Absensi.Data.Repositori;
using Absensi.Domain;
using Microsoft.Data.Sqlite;

namespace Absensi.App.Shell;

// Kerangka jendela utama (RENCANA-TUGAS.md Tugas 5) + isi nyata untuk menu
// Siswa/Guru/Kelas (Tugas 7). Sisa menu (Dashboard, Koreksi, Kalender,
// dst.) masih menunggu tugasnya sendiri dan tetap menampilkan placeholder.
// Menu "Absensi" tetap istimewa: dua tombol ("Buka layar gerbang" ·
// "Registrasi", persis PRD §14.3) karena keduanya jendela terpisah
// (gerbang fullscreen, registrasi dialog per kelas — Tugas 8), bukan
// konten yang disisipkan seperti Siswa/Guru/Kelas.
public sealed class MainWindowViewModel : ObservableObject
{
    private readonly SqliteConnection _connection;
    private readonly AkunTersimpan _akun;
    private readonly IPenyediaWaktu _penyediaWaktu;
    private readonly StatusModeRegistrasi _statusModeRegistrasi;
    private readonly int? _tahunAjaranId;

    private string _menuAktif;
    private object? _kontenAktif;
    private SiswaViewModel? _siswaViewModel;
    private GuruViewModel? _guruViewModel;
    private KelasViewModel? _kelasViewModel;
    private KalenderViewModel? _kalenderViewModel;
    private PengaturanViewModel? _pengaturanViewModel;
    private NotifikasiViewModel? _notifikasiViewModel;

    public MainWindowViewModel(
        SqliteConnection connection,
        AkunTersimpan akun,
        IPenyediaWaktu penyediaWaktu,
        StatusModeRegistrasi statusModeRegistrasi,
        string namaSekolah,
        string? logoPath,
        string? tahunAjaranAktif,
        int? tahunAjaranId)
    {
        _connection = connection;
        _akun = akun;
        _penyediaWaktu = penyediaWaktu;
        _statusModeRegistrasi = statusModeRegistrasi;
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
        BukaRegistrasiKartuCommand = new RelayCommand(() => BukaRegistrasiKartuDiminta?.Invoke(this, EventArgs.Empty));
        PerbaruiKontenAktif();
    }

    public event EventHandler? BukaGerbangDiminta;

    public event EventHandler? BukaRegistrasiKartuDiminta;

    public string NamaSekolah { get; }

    public string? LogoPath { get; }

    public string TahunAjaranAktif { get; }

    public ObservableCollection<string> MenuItems { get; }

    public RelayCommand BukaGerbangCommand { get; }

    public RelayCommand BukaRegistrasiKartuCommand { get; }

    public string MenuAktif
    {
        get => _menuAktif;
        set
        {
            if (SetField(ref _menuAktif, value))
            {
                OnPropertyChanged(nameof(PesanKonten));
                OnPropertyChanged(nameof(TampilkanTombolAbsensi));
                PerbaruiKontenAktif();
            }
        }
    }

    public bool TampilkanTombolAbsensi => MenuAktif == "Absensi";

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
            "Siswa" => _siswaViewModel ??= new SiswaViewModel(_connection, _akun, _penyediaWaktu, _statusModeRegistrasi, tahunAjaranId),
            "Guru" => _guruViewModel ??= new GuruViewModel(_connection, _akun, _penyediaWaktu, _statusModeRegistrasi, tahunAjaranId),
            "Kelas" => _kelasViewModel ??= new KelasViewModel(_connection, _akun, _penyediaWaktu, tahunAjaranId),
            "Kalender" => _kalenderViewModel ??= new KalenderViewModel(_connection, _akun, _penyediaWaktu),
            "Pengaturan" => _pengaturanViewModel ??= new PengaturanViewModel(_connection, _akun, _penyediaWaktu, tahunAjaranId),
            "Notifikasi" => _notifikasiViewModel ??= new NotifikasiViewModel(_connection, _akun, _penyediaWaktu, tahunAjaranId),
            _ => null,
        };
    }
}
