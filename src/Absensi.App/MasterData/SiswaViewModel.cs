using System.Collections.ObjectModel;
using System.IO;
using Absensi.App.Mvvm;
using Absensi.App.RegistrasiKartu;
using Absensi.Data.Akun;
using Absensi.Data.MasterData;
using Absensi.Data.Repositori;
using Absensi.Domain;
using Absensi.Domain.Kehadiran;
using Microsoft.Data.Sqlite;
using Microsoft.Win32;

namespace Absensi.App.MasterData;

// PRD §14.4: daftar (dengan penanda Kartu/TG), impor Excel, impor foto,
// tambah/edit, dan mutasi (tidak aktif).
public sealed class SiswaViewModel : ObservableObject
{
    private static readonly string[] AlasanTidakAktif = { "lulus", "pindah", "keluar" };

    // Sentinel "semua kelas" untuk filter Daftar (PRD §14.4) — id=0 tidak
    // pernah dipakai kelas sungguhan (INTEGER PRIMARY KEY dimulai dari 1).
    private static readonly KelasDaftar SemuaKelas = new(0, "Semua kelas", 0, null, null);

    private readonly SqliteConnection _connection;
    private readonly AkunTersimpan _akun;
    private readonly IPenyediaWaktu _penyediaWaktu;
    private readonly StatusModeRegistrasi _statusModeRegistrasi;
    private readonly int _tahunAjaranId;
    private readonly RepositoriSiswa _repositoriSiswa = new();
    private readonly LayananSiswa _layananSiswa = new();
    private readonly ImporSiswaExcel _importirSiswa = new();

    private IReadOnlyList<SiswaDaftar> _semuaSiswa = Array.Empty<SiswaDaftar>();
    private KelasDaftar _kelasFilterTerpilih;
    private string? _ringkasanKelas;

    private string _nisn = string.Empty;
    private string _nama = string.Empty;
    private string _jenisKelamin = "L";
    private string? _tempatLahir;
    private string? _tanggalLahir;
    private string? _alamat;
    private string? _namaOrtu;
    private string? _noHpOrtu;
    private KelasDaftar? _kelasTerpilih;
    private SiswaDaftar? _sedangDiedit;
    private string? _pesanGalat;
    private string? _pesanStatus;

    public SiswaViewModel(
        SqliteConnection connection,
        AkunTersimpan akun,
        IPenyediaWaktu penyediaWaktu,
        StatusModeRegistrasi statusModeRegistrasi,
        int tahunAjaranId)
    {
        _connection = connection;
        _akun = akun;
        _penyediaWaktu = penyediaWaktu;
        _statusModeRegistrasi = statusModeRegistrasi;
        _tahunAjaranId = tahunAjaranId;

        SimpanCommand = new RelayCommand(Simpan, BisaSimpan);
        EditCommand = new RelayCommand<SiswaDaftar>(MulaiEdit);
        BatalEditCommand = new RelayCommand(BatalEdit);
        NonaktifkanCommand = new RelayCommand<SiswaDaftar>(Nonaktifkan);
        UnggahFotoCommand = new RelayCommand<SiswaDaftar>(UnggahFoto);
        UnduhTemplateCommand = new RelayCommand(UnduhTemplate);
        ImporExcelCommand = new RelayCommand(ImporExcel);
        ImporFotoCommand = new RelayCommand(ImporFoto);

        // §16.11: penggantian kartu hilang hanya bermakna bila siswa sudah
        // punya kartu aktif — pendaftaran kartu pertama lewat layar
        // Registrasi Kartu per kelas (§16.5), bukan di sini.
        GantiKartuCommand = new RelayCommand<SiswaDaftar>(BukaDialogGantiKartu, s => s is not null && s.PunyaKartu);

        DaftarKelasFilter.Add(SemuaKelas);
        foreach (var kelas in new RepositoriKelas().DaftarUntukTahunAjaran(connection, tahunAjaranId))
        {
            DaftarKelas.Add(kelas);
            DaftarKelasFilter.Add(kelas);
        }

        _kelasFilterTerpilih = SemuaKelas;

        MuatUlang();
    }

    public ObservableCollection<SiswaDaftar> Daftar { get; } = new();

    public ObservableCollection<KelasDaftar> DaftarKelas { get; } = new();

    public ObservableCollection<KelasDaftar> DaftarKelasFilter { get; } = new();

    // PRD §14.4: "Di atas tabel ditampilkan hitungan untuk kelas yang
    // sedang dipilih, mis. 'Kelas 7A · 6 siswa belum punya kartu · 14
    // belum berfoto'." Null (Semua kelas) berarti tidak ditampilkan.
    public KelasDaftar KelasFilterTerpilih
    {
        get => _kelasFilterTerpilih;
        set
        {
            if (SetField(ref _kelasFilterTerpilih, value))
            {
                TerapkanFilterKelas();
            }
        }
    }

    public string? RingkasanKelas
    {
        get => _ringkasanKelas;
        private set => SetField(ref _ringkasanKelas, value);
    }

    public string JudulFormulir => _sedangDiedit is null ? "Tambah siswa" : $"Ubah siswa {_sedangDiedit.Nama}";

    public string Nisn { get => _nisn; set { if (SetField(ref _nisn, value)) SimpanCommand.RaiseCanExecuteChanged(); } }

    public string Nama { get => _nama; set { if (SetField(ref _nama, value)) SimpanCommand.RaiseCanExecuteChanged(); } }

    public string JenisKelamin { get => _jenisKelamin; set => SetField(ref _jenisKelamin, value); }

    public string? TempatLahir { get => _tempatLahir; set => SetField(ref _tempatLahir, value); }

    public string? TanggalLahir { get => _tanggalLahir; set => SetField(ref _tanggalLahir, value); }

    public string? Alamat { get => _alamat; set => SetField(ref _alamat, value); }

    public string? NamaOrtu { get => _namaOrtu; set => SetField(ref _namaOrtu, value); }

    public string? NoHpOrtu { get => _noHpOrtu; set => SetField(ref _noHpOrtu, value); }

    public KelasDaftar? KelasTerpilih { get => _kelasTerpilih; set => SetField(ref _kelasTerpilih, value); }

    public string? PesanGalat { get => _pesanGalat; private set => SetField(ref _pesanGalat, value); }

    public string? PesanStatus { get => _pesanStatus; private set => SetField(ref _pesanStatus, value); }

    public RelayCommand SimpanCommand { get; }

    public RelayCommand<SiswaDaftar> EditCommand { get; }

    public RelayCommand BatalEditCommand { get; }

    public RelayCommand<SiswaDaftar> NonaktifkanCommand { get; }

    public RelayCommand<SiswaDaftar> UnggahFotoCommand { get; }

    public RelayCommand<SiswaDaftar> GantiKartuCommand { get; }

    public RelayCommand UnduhTemplateCommand { get; }

    public RelayCommand ImporExcelCommand { get; }

    public RelayCommand ImporFotoCommand { get; }

    private bool BisaSimpan() => !string.IsNullOrWhiteSpace(_nisn) && !string.IsNullOrWhiteSpace(_nama) && _kelasTerpilih is not null;

    private void MulaiEdit(SiswaDaftar siswa)
    {
        _sedangDiedit = siswa;
        Nisn = siswa.Nisn;
        Nama = siswa.Nama;
        JenisKelamin = siswa.JenisKelamin;
        KelasTerpilih = DaftarKelas.FirstOrDefault(k => k.Nama == siswa.KelasNama);
        OnPropertyChanged(nameof(JudulFormulir));
        SimpanCommand.RaiseCanExecuteChanged();
    }

    private void BatalEdit()
    {
        _sedangDiedit = null;
        Nisn = string.Empty;
        Nama = string.Empty;
        JenisKelamin = "L";
        TempatLahir = null;
        TanggalLahir = null;
        Alamat = null;
        NamaOrtu = null;
        NoHpOrtu = null;
        KelasTerpilih = null;
        PesanGalat = null;
        OnPropertyChanged(nameof(JudulFormulir));
    }

    private void Simpan()
    {
        if (KelasTerpilih is null)
        {
            PesanGalat = "Pilih kelas terlebih dahulu.";
            return;
        }

        var waktu = _penyediaWaktu.Sekarang;
        var data = new DataSiswa(Nisn, Nama, JenisKelamin, TempatLahir, TanggalLahir, Alamat, NamaOrtu, NoHpOrtu);
        var kelas = (_tahunAjaranId, KelasTerpilih.Id);

        if (_sedangDiedit is { } siswa)
        {
            _layananSiswa.Perbarui(_connection, _akun, waktu, siswa.Id, data, kelas);
        }
        else
        {
            _layananSiswa.Tambah(_connection, _akun, waktu, data, kelas);
        }

        BatalEdit();
        MuatUlang();
    }

    private void Nonaktifkan(SiswaDaftar siswa)
    {
        var dialogViewModel = new AlasanTidakAktifViewModel(AlasanTidakAktif);
        var dialog = new AlasanTidakAktifWindow(dialogViewModel);
        if (dialog.ShowDialog() != true || dialog.AlasanTerpilih is null)
        {
            return;
        }

        _layananSiswa.TandaiTidakAktif(_connection, _akun, _penyediaWaktu.Sekarang, siswa.Id, dialog.AlasanTerpilih);
        MuatUlang();
    }

    private void UnggahFoto(SiswaDaftar siswa)
    {
        var dialog = new OpenFileDialog { Filter = "Gambar|*.jpg;*.jpeg;*.png;*.bmp" };
        if (dialog.ShowDialog() != true)
        {
            return;
        }

        var namaBerkasTujuan = $"{siswa.Nisn}.png";
        var pathTujuan = System.IO.Path.Combine(LokasiData.FolderData, "foto", namaBerkasTujuan);
        PengolahFoto.SimpanDiubahUkuran(dialog.FileName, pathTujuan);
        _layananSiswa.SimpanFoto(_connection, _akun, _penyediaWaktu.Sekarang, siswa.Id, namaBerkasTujuan);
        PesanStatus = $"Foto {siswa.Nama} tersimpan.";
    }

    // PRD §16.11: "TU membuka Data Siswa → pilih siswa → nonaktifkan kartu
    // lama → aktifkan Mode Registrasi → tap kartu baru" — satu dialog.
    private void BukaDialogGantiKartu(SiswaDaftar siswa)
    {
        var dialogViewModel = new TapKartuTunggalViewModel(
            _connection, _akun, _penyediaWaktu, _statusModeRegistrasi, _tahunAjaranId,
            JenisPemilikKartu.Siswa, siswa.Id, siswa.Nama, modeGanti: true);
        var dialog = new TapKartuTunggalWindow(dialogViewModel);
        dialog.ShowDialog();
        MuatUlang();
    }

    // A-12 (Master Bug & UI/UX Register, docs/PRD.md §16.2 langkah 3):
    // template diunduh sebelum Impor Excel, mengikuti urutan kerja
    // "Unduh Template → isi berkas → Impor Excel".
    private void UnduhTemplate()
    {
        var dialog = new SaveFileDialog
        {
            FileName = "template_import_siswa.xlsx",
            Filter = "Berkas Excel (*.xlsx)|*.xlsx",
        };
        if (dialog.ShowDialog() != true)
        {
            return;
        }

        SimpanTemplateKeBerkas(dialog.FileName);
    }

    // Dipisah dari UnduhTemplate supaya bisa diuji tanpa SaveFileDialog
    // sungguhan -- pola sama seperti AnalisisBerkas (Tugas 7) dan
    // LaporanViewModel.SimpanKeBerkas (Tugas 12). A-8: kegagalan tulis yang
    // bisa dipulihkan pengguna (akses ditolak, berkas terkunci aplikasi
    // lain) dipetakan ke pesan Indonesia yang jelas -- bukan ex.Message
    // mentah -- sementara kegagalan lain dibiarkan lolos ke handler global
    // (App.xaml.cs/PencatatGalat), bukan ditelan di sini.
    public void SimpanTemplateKeBerkas(string path)
    {
        try
        {
            File.WriteAllBytes(path, _importirSiswa.BuatTemplate());
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            PesanGalat = PesanUntukKegagalanSimpanTemplate(ex);
            return;
        }

        PesanGalat = null;
        PesanStatus = $"Template tersimpan di {path}.";
    }

    private static string PesanUntukKegagalanSimpanTemplate(Exception ex) => ex switch
    {
        UnauthorizedAccessException => "Berkas tidak bisa disimpan karena akses ditolak. Pilih lokasi lain atau periksa izin berkas.",
        IOException => "Berkas sedang dipakai aplikasi lain (mis. Excel). Tutup berkasnya, lalu coba lagi.",
        _ => ex.Message,
    };

    private void ImporExcel()
    {
        var wizard = new ImporSiswaWizardWindow(new ImporSiswaWizardViewModel(_connection, _akun, _penyediaWaktu, _tahunAjaranId));
        wizard.ShowDialog();
        MuatUlang();
    }

    private void ImporFoto()
    {
        var dialog = new OpenFolderDialog();
        if (dialog.ShowDialog() != true)
        {
            return;
        }

        var hasil = new ImporFotoSiswa().Impor(_connection, _akun, _penyediaWaktu.Sekarang, dialog.FolderName);
        PesanStatus = hasil.TidakCocok.Count == 0
            ? $"{hasil.Cocok} foto tersimpan."
            : $"{hasil.Cocok} foto tersimpan · {hasil.TidakCocok.Count} berkas tidak cocok NISN mana pun.";
        MuatUlang();
    }

    private void MuatUlang()
    {
        _semuaSiswa = _repositoriSiswa.DaftarUntukTahunAjaran(_connection, _tahunAjaranId);
        TerapkanFilterKelas();
    }

    private void TerapkanFilterKelas()
    {
        var tersaring = _kelasFilterTerpilih.Id == 0
            ? _semuaSiswa
            : _semuaSiswa.Where(s => s.KelasNama == _kelasFilterTerpilih.Nama).ToList();

        Daftar.Clear();
        foreach (var siswa in tersaring)
        {
            Daftar.Add(siswa);
        }

        if (_kelasFilterTerpilih.Id == 0)
        {
            RingkasanKelas = null;
            return;
        }

        var belumKartu = tersaring.Count(s => !s.PunyaKartu);
        var belumFoto = tersaring.Count(s => !s.PunyaFoto);
        RingkasanKelas = $"Kelas {_kelasFilterTerpilih.Nama} · {belumKartu} siswa belum punya kartu · {belumFoto} belum berfoto";
    }
}
