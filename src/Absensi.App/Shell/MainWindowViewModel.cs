using System.Collections.ObjectModel;
using System.Windows.Media.Imaging;
using Absensi.App.Kalender;
using Absensi.App.Koreksi;
using Absensi.App.Laporan;
using Absensi.App.MasterData;
using Absensi.App.Mvvm;
using Absensi.App.Notifikasi;
using Absensi.App.Pengaturan;
using Absensi.Data.Backup;
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
    private readonly LayananBackup _layananBackup;

    private string _menuAktif;
    private object? _kontenAktif;
    private SiswaViewModel? _siswaViewModel;
    private GuruViewModel? _guruViewModel;
    private KelasViewModel? _kelasViewModel;
    private KalenderViewModel? _kalenderViewModel;
    private PengaturanViewModel? _pengaturanViewModel;
    private NotifikasiViewModel? _notifikasiViewModel;
    private KoreksiViewModel? _koreksiViewModel;
    private LaporanViewModel? _laporanViewModel;

    public MainWindowViewModel(
        SqliteConnection connection,
        AkunTersimpan akun,
        IPenyediaWaktu penyediaWaktu,
        StatusModeRegistrasi statusModeRegistrasi,
        string namaSekolah,
        string? logoPath,
        string? tahunAjaranAktif,
        int? tahunAjaranId,
        LayananBackup layananBackup)
    {
        _connection = connection;
        _akun = akun;
        _penyediaWaktu = penyediaWaktu;
        _statusModeRegistrasi = statusModeRegistrasi;
        _tahunAjaranId = tahunAjaranId;
        _layananBackup = layananBackup;

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
        KeluarCommand = new RelayCommand(() => KeluarDiminta?.Invoke(this, EventArgs.Empty));
        PerbaruiKontenAktif();
    }

    public event EventHandler? BukaGerbangDiminta;

    public event EventHandler? BukaRegistrasiKartuDiminta;

    // Tugas 14 — App.xaml sekarang ShutdownMode="OnExplicitShutdown" (lihat
    // catatan di App.xaml.cs), jadi menutup jendela utama TIDAK LAGI
    // otomatis mematikan aplikasi. MainWindow.xaml.cs memanggil
    // Application.Current.Shutdown() dari event Closed-nya sendiri (jadi
    // berlaku juga saat jendela ditutup lewat tombol X, tidak hanya lewat
    // tombol Keluar ini).
    public event EventHandler? KeluarDiminta;

    // RENCANA-TUGAS.md Tugas 13 — PRD §18.4: diteruskan dari PengaturanViewModel.
    // App.xaml.cs yang menutup koneksi, menimpa berkas database, dan menutup
    // aplikasi (bukan di sini — jendela utama tidak memegang siklus hidup
    // koneksi/aplikasi).
    public event EventHandler<byte[]>? RestoreSelesaiPerluTutupAplikasi;

    public string NamaSekolah { get; }

    public string? LogoPath { get; }

    // Bug UX pasca serah terima: MainWindow tidak punya Icon di title bar
    // sama sekali. Sumbernya logo sekolah yang sama dipakai LoginWindow
    // (PemuatGambar/LokasiData sudah dipakai di sana, pola sama di sini).
    public BitmapImage? Logo => PemuatGambar.MuatDariFolder(LokasiData.FolderData, LogoPath);

    // Bug UX pasca serah terima: title bar asli Windows selalu menampilkan
    // "Absensi" statis, tidak menyebut sekolah mana yang sedang dibuka.
    public string TitleJendela => $"Absensi - {NamaSekolah}";

    public string TahunAjaranAktif { get; }

    public ObservableCollection<string> MenuItems { get; }

    public RelayCommand BukaGerbangCommand { get; }

    public RelayCommand BukaRegistrasiKartuCommand { get; }

    public RelayCommand KeluarCommand { get; }

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
            "Siswa" => MuatUlangSiswaViewModel(tahunAjaranId),
            "Guru" => _guruViewModel ??= new GuruViewModel(_connection, _akun, _penyediaWaktu, _statusModeRegistrasi, tahunAjaranId),
            "Kelas" => _kelasViewModel ??= new KelasViewModel(_connection, _akun, _penyediaWaktu, tahunAjaranId),
            "Kalender" => _kalenderViewModel ??= new KalenderViewModel(_connection, _akun, _penyediaWaktu),
            "Pengaturan" => _pengaturanViewModel ??= BuatPengaturanViewModel(tahunAjaranId),
            "Notifikasi" => _notifikasiViewModel ??= new NotifikasiViewModel(_connection, _akun, _penyediaWaktu, tahunAjaranId),
            "Koreksi" => _koreksiViewModel ??= new KoreksiViewModel(_connection, _akun, _penyediaWaktu, tahunAjaranId),
            "Laporan" => _laporanViewModel ??= new LaporanViewModel(_connection, _akun, _penyediaWaktu, tahunAjaranId),
            _ => null,
        };
    }

    // Bug #3 (Master Bug & UI/UX Register A-20 / UAT 2026-09-22): SiswaViewModel
    // di-cache lewat "??=" — constructor-nya (dan pemuatan DaftarKelas di
    // dalamnya) hanya jalan SEKALI per sesi. Tanpa baris ini, kelas baru
    // yang dibuat lewat panel Kelas SETELAH panel Siswa pernah dibuka tidak
    // pernah muncul di dropdown Siswa sampai restart. MuatUlangDaftarKelas()
    // dipanggil di SINI (bukan di constructor SiswaViewModel saja) supaya
    // jalan setiap kali menu "Siswa" dipilih — baik saat ViewModel baru
    // dibuat MAUPUN dipakai ulang dari cache.
    private SiswaViewModel MuatUlangSiswaViewModel(int tahunAjaranId)
    {
        _siswaViewModel ??= new SiswaViewModel(_connection, _akun, _penyediaWaktu, _statusModeRegistrasi, tahunAjaranId);
        _siswaViewModel.MuatUlangDaftarKelas();
        return _siswaViewModel;
    }

    private PengaturanViewModel BuatPengaturanViewModel(int tahunAjaranId)
    {
        var viewModel = new PengaturanViewModel(_connection, _akun, _penyediaWaktu, tahunAjaranId, _layananBackup);
        viewModel.RestoreSelesaiPerluTutupAplikasi += (_, data) => RestoreSelesaiPerluTutupAplikasi?.Invoke(this, data);
        return viewModel;
    }
}
