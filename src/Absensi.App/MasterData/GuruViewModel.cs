using System.Collections.ObjectModel;
using System.IO;
using Absensi.App.Koreksi;
using Absensi.App.Mvvm;
using Absensi.App.RegistrasiKartu;
using Absensi.Data.Akun;
using Absensi.Data.MasterData;
using Absensi.Data.Repositori;
using Absensi.Domain;
using Absensi.Domain.Akun;
using Absensi.Domain.Kehadiran;
using Microsoft.Data.Sqlite;
using Microsoft.Win32;

namespace Absensi.App.MasterData;

// PRD §14.4-setara untuk guru + §16.15: daftar, impor Excel, impor foto,
// tambah/edit, dan penandaan tidak aktif. Tab "Kehadiran" (Tugas 11, PRD
// §16.17) memakai ViewModel terpisah (StatusGuru) — layar Data Guru dan
// Kehadiran berdiri sendiri-sendiri, cuma berbagi jendela yang sama.
public sealed class GuruViewModel : ObservableObject
{
    private static readonly string[] AlasanTidakAktif = { "pensiun", "pindah" };

    private readonly SqliteConnection _connection;
    private readonly AkunTersimpan _akun;
    private readonly IPenyediaWaktu _penyediaWaktu;
    private readonly StatusModeRegistrasi _statusModeRegistrasi;
    private readonly int _tahunAjaranId;
    private readonly RepositoriGuru _repositoriGuru = new();
    private readonly LayananGuru _layananGuru = new();
    private readonly ImporGuruExcel _importirGuru = new();

    private string _nip = string.Empty;
    private string _nama = string.Empty;
    private string? _pangkat;
    private string? _jabatan;
    private GuruDaftar? _sedangDiedit;
    private string? _pesanGalat;
    private string? _pesanStatus;

    public GuruViewModel(
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

        SimpanCommand = new RelayCommand(Simpan, () => !string.IsNullOrWhiteSpace(_nip) && !string.IsNullOrWhiteSpace(_nama));
        EditCommand = new RelayCommand<GuruDaftar>(MulaiEdit);
        BatalEditCommand = new RelayCommand(BatalEdit);
        NonaktifkanCommand = new RelayCommand<GuruDaftar>(Nonaktifkan);
        UnggahFotoCommand = new RelayCommand<GuruDaftar>(UnggahFoto);
        UnduhTemplateCommand = new RelayCommand(UnduhTemplate);
        ImporExcelCommand = new RelayCommand(ImporExcel);
        ImporFotoCommand = new RelayCommand(ImporFoto);

        // PRD §17.10: registrasi/ganti kartu guru hanya TU/Admin/Kepala
        // Sekolah — wali kelas tidak berwenang (ditegakkan juga di
        // LayananRegistrasiKartu; ini cuma menyembunyikan tombolnya).
        KartuCommand = new RelayCommand<GuruDaftar>(BukaDialogKartu, _ => _akun.Peran != PeranAkun.WaliKelas);

        StatusGuru = new StatusGuruViewModel(connection, akun, penyediaWaktu);

        MuatUlang();
    }

    public ObservableCollection<GuruDaftar> Daftar { get; } = new();

    public StatusGuruViewModel StatusGuru { get; }

    public string JudulFormulir => _sedangDiedit is null ? "Tambah guru" : $"Ubah guru {_sedangDiedit.Nama}";

    public string Nip { get => _nip; set { if (SetField(ref _nip, value)) SimpanCommand.RaiseCanExecuteChanged(); } }

    public string Nama { get => _nama; set { if (SetField(ref _nama, value)) SimpanCommand.RaiseCanExecuteChanged(); } }

    public string? Pangkat { get => _pangkat; set => SetField(ref _pangkat, value); }

    public string? Jabatan { get => _jabatan; set => SetField(ref _jabatan, value); }

    public string? PesanGalat { get => _pesanGalat; private set => SetField(ref _pesanGalat, value); }

    public string? PesanStatus { get => _pesanStatus; private set => SetField(ref _pesanStatus, value); }

    public RelayCommand SimpanCommand { get; }

    public RelayCommand<GuruDaftar> EditCommand { get; }

    public RelayCommand BatalEditCommand { get; }

    public RelayCommand<GuruDaftar> NonaktifkanCommand { get; }

    public RelayCommand<GuruDaftar> UnggahFotoCommand { get; }

    public RelayCommand<GuruDaftar> KartuCommand { get; }

    public RelayCommand UnduhTemplateCommand { get; }

    public RelayCommand ImporExcelCommand { get; }

    public RelayCommand ImporFotoCommand { get; }

    private void MulaiEdit(GuruDaftar guru)
    {
        _sedangDiedit = guru;
        Nip = guru.Nip;
        Nama = guru.Nama;
        OnPropertyChanged(nameof(JudulFormulir));
    }

    private void BatalEdit()
    {
        _sedangDiedit = null;
        Nip = string.Empty;
        Nama = string.Empty;
        Pangkat = null;
        Jabatan = null;
        PesanGalat = null;
        OnPropertyChanged(nameof(JudulFormulir));
    }

    private void Simpan()
    {
        var waktu = _penyediaWaktu.Sekarang;
        var data = new DataGuru(Nip, Nama, Pangkat, Jabatan);

        if (_sedangDiedit is { } guru)
        {
            _layananGuru.Perbarui(_connection, _akun, waktu, guru.Id, data);
        }
        else
        {
            _layananGuru.Tambah(_connection, _akun, waktu, data);
        }

        BatalEdit();
        MuatUlang();
    }

    private void Nonaktifkan(GuruDaftar guru)
    {
        var dialogViewModel = new AlasanTidakAktifViewModel(AlasanTidakAktif);
        var dialog = new AlasanTidakAktifWindow(dialogViewModel);
        if (dialog.ShowDialog() != true || dialog.AlasanTerpilih is null)
        {
            return;
        }

        _layananGuru.TandaiTidakAktif(_connection, _akun, _penyediaWaktu.Sekarang, guru.Id, dialog.AlasanTerpilih);
        MuatUlang();
    }

    private void UnggahFoto(GuruDaftar guru)
    {
        var dialog = new OpenFileDialog { Filter = "Gambar|*.jpg;*.jpeg;*.png;*.bmp" };
        if (dialog.ShowDialog() != true)
        {
            return;
        }

        var namaBerkasTujuan = $"{guru.Nip}.png";
        var pathTujuan = System.IO.Path.Combine(LokasiData.FolderData, "foto", namaBerkasTujuan);
        PengolahFoto.SimpanDiubahUkuran(dialog.FileName, pathTujuan);
        _layananGuru.SimpanFoto(_connection, _akun, _penyediaWaktu.Sekarang, guru.Id, namaBerkasTujuan);
        PesanStatus = $"Foto {guru.Nama} tersimpan.";
    }

    // Kartu pertama guru (tidak ada alur per-kelas seperti siswa — §16.5
    // hanya menjabarkan itu untuk siswa) dan penggantian kartu hilang
    // (§16.11) memakai dialog satu-kartu yang sama.
    private void BukaDialogKartu(GuruDaftar guru)
    {
        var dialogViewModel = new TapKartuTunggalViewModel(
            _connection, _akun, _penyediaWaktu, _statusModeRegistrasi, _tahunAjaranId,
            JenisPemilikKartu.Guru, guru.Id, guru.Nama, modeGanti: guru.PunyaKartu);
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
            FileName = "template_import_guru.xlsx",
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
            File.WriteAllBytes(path, _importirGuru.BuatTemplate());
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
        var wizard = new ImporGuruWizardWindow(new ImporGuruWizardViewModel(_connection, _akun, _penyediaWaktu));
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

        var hasil = new ImporFotoGuru().Impor(_connection, _akun, _penyediaWaktu.Sekarang, dialog.FolderName);
        PesanStatus = hasil.TidakCocok.Count == 0
            ? $"{hasil.Cocok} foto tersimpan."
            : $"{hasil.Cocok} foto tersimpan · {hasil.TidakCocok.Count} berkas tidak cocok NIP mana pun.";
        MuatUlang();
    }

    private void MuatUlang()
    {
        Daftar.Clear();
        foreach (var guru in _repositoriGuru.DaftarLengkap(_connection))
        {
            Daftar.Add(guru);
        }
    }
}
