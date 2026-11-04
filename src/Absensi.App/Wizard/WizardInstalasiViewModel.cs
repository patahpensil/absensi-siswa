using System.Collections.ObjectModel;
using System.IO;
using Absensi.App.MasterData;
using Absensi.App.Mvvm;
using Absensi.App.Pengaturan;
using Absensi.Data.Backup;
using Absensi.Data.Diagnostik;
using Absensi.Data.Lisensi;
using Absensi.Data.Repositori;
using Absensi.Data.Wizard;
using Absensi.Domain;
using Microsoft.Data.Sqlite;
using Microsoft.Win32;

namespace Absensi.App.Wizard;

// PRD §16.1: wizard instalasi pertama, enam langkah. Hanya ditampilkan
// App.xaml.cs saat belum ada baris `sekolah` — tidak bisa dijalankan ulang
// setelah wizard ini selesai (baris itu jadi penandanya).
public sealed class WizardInstalasiViewModel : ObservableObject
{
    private static readonly string[] NamaHari = { "Senin", "Selasa", "Rabu", "Kamis", "Jumat", "Sabtu", "Minggu" };

    private readonly SqliteConnection _connection;
    private readonly IPenyediaWaktu _penyediaWaktu;
    private readonly ILayananLisensi _layananLisensi;
    private readonly IPenyimpanCloudBackup _penyimpanCloud;
    private readonly LayananWizard _layananWizard = new();

    private LangkahWizard _langkahSaatIni = LangkahWizard.AktivasiLisensi;
    private string? _pesanGalat;

    private string _kunciLisensi = string.Empty;

    private string _namaSekolah = string.Empty;
    private string? _npsn;
    private string? _alamat;
    private string? _logoPathSumber;

    private string _namaTahunAjaran = string.Empty;
    private string _semester = "ganjil";
    private DateTime? _tglMulaiTahunAjaran;
    private DateTime? _tglSelesaiTahunAjaran;

    private string _usernameKepalaSekolah = string.Empty;
    private string _sandiKepalaSekolah = string.Empty;
    private string _konfirmasiSandi = string.Empty;

    private AkunTersimpan? _akunKepalaSekolahDibuat;

    public WizardInstalasiViewModel(
        SqliteConnection connection,
        IPenyediaWaktu penyediaWaktu,
        ILayananLisensi layananLisensi,
        IPenyimpanCloudBackup penyimpanCloud)
    {
        _connection = connection;
        _penyediaWaktu = penyediaWaktu;
        _layananLisensi = layananLisensi;
        _penyimpanCloud = penyimpanCloud;

        for (var hari = 1; hari <= 7; hari++)
        {
            var (jamMasuk, notTappedListTime, jamRekap, batasTapMasuk, mulaiPulang) = JadwalBawaan(hari);
            JadwalHarian.Add(new JadwalHariBarisViewModel(hari, NamaHari[hari - 1], hariSekolah: hari != 7, jamMasuk, jamRekap, batasTapMasuk, mulaiPulang, notTappedListTime));
        }

        InstalasiBaruCommand = new RelayCommand(InstalasiBaru, () => !string.IsNullOrWhiteSpace(KunciLisensi));
        PulihkanDariPcLamaCommand = new RelayCommand(PulihkanDariPcLama, () => !string.IsNullOrWhiteSpace(KunciLisensi));
        LanjutKeTahunAjaranCommand = new RelayCommand(() => LangkahSaatIni = LangkahWizard.TahunAjaran, () => !string.IsNullOrWhiteSpace(NamaSekolah));
        LanjutKeJadwalCommand = new RelayCommand(LanjutKeJadwal);
        LanjutKeAkunCommand = new RelayCommand(() => LangkahSaatIni = LangkahWizard.AkunKepalaSekolah);
        KembaliCommand = new RelayCommand(Kembali);
        PilihLogoCommand = new RelayCommand(PilihLogo);
        SelesaikanCommand = new RelayCommand(Selesaikan, BisaSelesaikan);
    }

    public event EventHandler<AkunTersimpan>? WizardSelesai;

    public event EventHandler<byte[]>? DipulihkanDariCloud;

    public LangkahWizard LangkahSaatIni { get => _langkahSaatIni; private set => SetField(ref _langkahSaatIni, value); }

    public string? PesanGalat { get => _pesanGalat; private set => SetField(ref _pesanGalat, value); }

    // Langkah 1 — PRD §13.1/§16.1/§18.3: dua cabang eksplisit. "Instalasi
    // Baru" mengaktifkan lisensi lalu lanjut ke langkah 2. "Pulihkan dari
    // PC Lama" mengaktifkan lisensi yang sama, lalu mengambil backup
    // terakhir dari cadangan sekunder dan melompati langkah 2-5 sepenuhnya
    // (identitas sekolah/tahun ajaran/jadwal/akun semuanya sudah ada di
    // backup itu). "Cadangan sekunder" -- BUKAN "cloud": penyimpanannya
    // (PenyimpanCloudBackupLokal) saat ini masih folder lokal, bukan
    // server terpisah dari PC -- lihat catatan di kelas itu.
    public string KunciLisensi
    {
        get => _kunciLisensi;
        set
        {
            if (SetField(ref _kunciLisensi, value))
            {
                InstalasiBaruCommand.RaiseCanExecuteChanged();
                PulihkanDariPcLamaCommand.RaiseCanExecuteChanged();
            }
        }
    }

    public RelayCommand InstalasiBaruCommand { get; }

    public RelayCommand PulihkanDariPcLamaCommand { get; }

    // Langkah 2 — PRD §16.1 3.2.
    public string NamaSekolah
    {
        get => _namaSekolah;
        set
        {
            if (SetField(ref _namaSekolah, value))
            {
                LanjutKeTahunAjaranCommand.RaiseCanExecuteChanged();
            }
        }
    }

    public string? Npsn { get => _npsn; set => SetField(ref _npsn, value); }

    public string? Alamat { get => _alamat; set => SetField(ref _alamat, value); }

    public string? NamaBerkasLogo => _logoPathSumber is null ? null : Path.GetFileName(_logoPathSumber);

    public RelayCommand PilihLogoCommand { get; }

    public RelayCommand LanjutKeTahunAjaranCommand { get; }

    // Langkah 3 — PRD §16.1 3.3.
    public string NamaTahunAjaran { get => _namaTahunAjaran; set => SetField(ref _namaTahunAjaran, value); }

    public string Semester { get => _semester; set => SetField(ref _semester, value); }

    public DateTime? TglMulaiTahunAjaran { get => _tglMulaiTahunAjaran; set => SetField(ref _tglMulaiTahunAjaran, value); }

    public DateTime? TglSelesaiTahunAjaran { get => _tglSelesaiTahunAjaran; set => SetField(ref _tglSelesaiTahunAjaran, value); }

    public RelayCommand LanjutKeJadwalCommand { get; }

    // Langkah 4 — PRD §16.1 3.4.
    public ObservableCollection<JadwalHariBarisViewModel> JadwalHarian { get; } = new();

    public RelayCommand LanjutKeAkunCommand { get; }

    public RelayCommand KembaliCommand { get; }

    // Langkah 5 — PRD §16.1 3.5.
    public string UsernameKepalaSekolah { get => _usernameKepalaSekolah; set { if (SetField(ref _usernameKepalaSekolah, value)) SelesaikanCommand.RaiseCanExecuteChanged(); } }

    public string SandiKepalaSekolah { get => _sandiKepalaSekolah; set { if (SetField(ref _sandiKepalaSekolah, value)) SelesaikanCommand.RaiseCanExecuteChanged(); } }

    public string KonfirmasiSandi { get => _konfirmasiSandi; set { if (SetField(ref _konfirmasiSandi, value)) SelesaikanCommand.RaiseCanExecuteChanged(); } }

    public RelayCommand SelesaikanCommand { get; }

    // Langkah 6.
    public AkunTersimpan? AkunKepalaSekolahDibuat { get => _akunKepalaSekolahDibuat; private set => SetField(ref _akunKepalaSekolahDibuat, value); }

    // Cabang "Instalasi Baru" (PRD §16.1): aktifkan lisensi lalu lanjut
    // mengisi identitas sekolah — langkah 2-5 seperti biasa.
    private void InstalasiBaru()
    {
        if (!CobaAktifkanLisensi())
        {
            return;
        }

        LangkahSaatIni = LangkahWizard.IdentitasSekolah;
    }

    // Cabang "Pulihkan dari PC Lama" (PRD §18.3): "Pasang aplikasi di PC
    // baru → masukkan kunci lisensi → ambil backup terakhir" —
    // menggantikan seluruh langkah 2-5 wizard, karena identitas
    // sekolah/tahun ajaran/jadwal/akun semuanya sudah ada di database yang
    // dipulihkan. Tidak ditemukan backup untuk kunci ini bukan galat fatal
    // — admin bisa mencoba kunci lain atau beralih ke Instalasi Baru.
    private void PulihkanDariPcLama()
    {
        if (!CobaAktifkanLisensi())
        {
            return;
        }

        byte[]? data;
        try
        {
            var hasil = _penyimpanCloud.UnduhTerakhirAsync(KunciLisensi).GetAwaiter().GetResult();
            data = hasil is null ? null : EnkripsiBackup.Dekripsi(hasil.DataTerenkripsi, KunciLisensi);
        }
        catch (Exception galat)
        {
            data = null;
            PencatatGalat.Catat(galat, sumber: "WizardInstalasiViewModel.PulihkanDariPcLama", level: "ERROR");
        }

        if (data is null)
        {
            PesanGalat = "Tidak ditemukan cadangan sekunder untuk kunci lisensi ini. Periksa kembali kunci lisensi, pastikan penyimpanan cadangan lama masih bisa diakses dari PC ini, atau pilih \"Instalasi Baru\" bila ini pemasangan pertama.";
            return;
        }

        PesanGalat = null;
        DipulihkanDariCloud?.Invoke(this, data);
    }

    private bool CobaAktifkanLisensi()
    {
        var hasil = _layananLisensi.Aktifkan(KunciLisensi);
        if (!hasil.Berhasil)
        {
            PesanGalat = hasil.PesanGalat;
            return false;
        }

        PesanGalat = null;
        return true;
    }

    private void LanjutKeJadwal() => LangkahSaatIni = LangkahWizard.JadwalHarian;

    private void Kembali()
    {
        LangkahSaatIni = LangkahSaatIni switch
        {
            LangkahWizard.TahunAjaran => LangkahWizard.IdentitasSekolah,
            LangkahWizard.JadwalHarian => LangkahWizard.TahunAjaran,
            LangkahWizard.AkunKepalaSekolah => LangkahWizard.JadwalHarian,
            _ => LangkahSaatIni,
        };
    }

    private void PilihLogo()
    {
        var dialog = new OpenFileDialog { Filter = "Gambar (*.png;*.jpg;*.jpeg)|*.png;*.jpg;*.jpeg" };
        if (dialog.ShowDialog() != true)
        {
            return;
        }

        AturLogoSumber(dialog.FileName);
    }

    // Dipisah dari PilihLogo supaya bisa diuji tanpa OpenFileDialog
    // sungguhan (pola sama seperti ImporSiswaWizardViewModel.AnalisisBerkas).
    public void AturLogoSumber(string pathSumber)
    {
        _logoPathSumber = pathSumber;
        OnPropertyChanged(nameof(NamaBerkasLogo));
    }

    private bool BisaSelesaikan() =>
        !string.IsNullOrWhiteSpace(UsernameKepalaSekolah)
        && !string.IsNullOrEmpty(SandiKepalaSekolah)
        && SandiKepalaSekolah == KonfirmasiSandi;

    private void Selesaikan()
    {
        if (TglMulaiTahunAjaran is not { } tglMulai || TglSelesaiTahunAjaran is not { } tglSelesai)
        {
            PesanGalat = "Tanggal mulai dan selesai tahun ajaran wajib diisi.";
            return;
        }

        if (tglSelesai <= tglMulai)
        {
            PesanGalat = "Tanggal selesai tahun ajaran harus setelah tanggal mulai.";
            return;
        }

        var jadwal = new List<JadwalHariBaris>();
        foreach (var baris in JadwalHarian)
        {
            if (!CobaUraikanJam(baris.NamaHari, "jam masuk", baris.JamMasuk, baris.HariSekolah, out var jamMasuk)
                || !CobaUraikanJam(baris.NamaHari, "jam daftar belum tap", baris.NotTappedListTime, baris.HariSekolah, out var notTappedListTime)
                || !CobaUraikanJam(baris.NamaHari, "jam rekap orang tua", baris.JamRekap, baris.HariSekolah, out var jamRekap)
                || !CobaUraikanJam(baris.NamaHari, "batas akhir tap masuk", baris.BatasTapMasuk, baris.HariSekolah, out var batasTapMasuk)
                || !CobaUraikanJam(baris.NamaHari, "mulai absen pulang", baris.MulaiPulang, baris.HariSekolah, out var mulaiPulang))
            {
                LangkahSaatIni = LangkahWizard.JadwalHarian;
                return;
            }

            jadwal.Add(new JadwalHariBaris(baris.Hari, baris.HariSekolah, jamMasuk, jamRekap, batasTapMasuk, mulaiPulang, notTappedListTime));
        }

        string? logoPathTujuan = null;
        if (_logoPathSumber is not null)
        {
            logoPathTujuan = Path.Combine(LokasiData.FolderData, "foto", "logo-sekolah.png");
            PengolahFoto.SimpanDiubahUkuran(_logoPathSumber, logoPathTujuan);
        }

        var data = new DataWizardInstalasi(
            KunciLisensi,
            NamaSekolah,
            string.IsNullOrWhiteSpace(Npsn) ? null : Npsn,
            string.IsNullOrWhiteSpace(Alamat) ? null : Alamat,
            logoPathTujuan,
            NamaTahunAjaran,
            Semester,
            DateOnly.FromDateTime(tglMulai),
            DateOnly.FromDateTime(tglSelesai),
            jadwal,
            UsernameKepalaSekolah,
            SandiKepalaSekolah);

        PesanGalat = null;
        AkunKepalaSekolahDibuat = _layananWizard.Selesaikan(_connection, _penyediaWaktu.Sekarang, data);
        LangkahSaatIni = LangkahWizard.Selesai;
        WizardSelesai?.Invoke(this, AkunKepalaSekolahDibuat);
    }

    private bool CobaUraikanJam(string namaHari, string namaKolom, string? teks, bool wajib, out TimeOnly? hasil)
    {
        hasil = null;
        if (string.IsNullOrWhiteSpace(teks))
        {
            if (wajib)
            {
                PesanGalat = $"{namaHari}: {namaKolom} wajib diisi untuk hari sekolah.";
                return false;
            }

            return true;
        }

        if (!TimeOnly.TryParse(teks, out var jam))
        {
            PesanGalat = $"{namaHari}: format {namaKolom} tidak valid. Gunakan format 24 jam HH:mm.";
            return false;
        }

        hasil = jam;
        return true;
    }

    // PRD §7 — nilai bawaan pengisi awal formulir jadwal wizard, BUKAN
    // nilai yang ditegakkan kode (PRD §1/§15): admin tetap bebas
    // mengubahnya sebelum Selesaikan, dan CobaUraikanJam tetap memvalidasi
    // ulang teks yang akhirnya tersimpan di JadwalHarian, apa pun asalnya.
    // Sebelumnya keempat kolom diisi null (kosong) di sini, dan layar tidak
    // menampilkan nilai bawaan apa pun — operator mengikuti teks instruksi
    // "kosongkan jam pada hari yang bukan hari sekolah" apa adanya dan
    // mengosongkan SEMUA baris termasuk hari sekolah, membuat jadwal_hari
    // tersimpan NULL untuk seluruh sekolah (lihat
    // docs/MASTER-BUG-UIUX-REGISTER.md, analisis penolakan Gerbang). Hari 7
    // (Minggu) sengaja TETAP kosong di sini — bukan hari sekolah, PRD §7
    // sendiri tidak punya nilai untuk baris itu.
    // PRD §7 P-06: daftar belum tap wali kelas 08.00 dan rekap orang tua
    // 08.10 adalah dua kolom terpisah. Isian awal formulir, bukan nilai yang
    // ditegakkan di kode (PRD §1/§15).
    private static (string? JamMasuk, string? NotTappedListTime, string? JamRekap, string? BatasTapMasuk, string? MulaiPulang) JadwalBawaan(int hari)
    {
        if (hari == 7)
        {
            return (null, null, null, null, null);
        }

        return hari == 5
            ? ("07:30", "08:00", "08:10", "10:00", "11:30")
            : ("07:30", "08:00", "08:10", "12:00", "13:10");
    }
}
