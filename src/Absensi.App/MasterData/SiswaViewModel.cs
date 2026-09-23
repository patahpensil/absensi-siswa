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

// PRD §14.4: daftar (dengan penanda Kartu), impor Excel, unggah foto per
// siswa, tambah/edit, dan mutasi (tidak aktif). UAT 2026-09-23: tombol impor
// foto massal (folder dicocokkan ke NISN, §16.4) dihapus dari panel ini atas
// instruksi eksplisit -- ImporFotoSiswa.cs TETAP ADA (tidak dihapus, PRD
// §16.4 masih mendokumentasikan alurnya) tapi untuk saat ini tidak ada
// entry point UI yang memanggilnya. Kolom TG (TertautTelegram) dihapus dari
// tabel Daftar juga -- fieldnya tetap dipakai menu Notifikasi.
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
        TambahCommand = new RelayCommand(MulaiTambah);
        EditCommand = new RelayCommand<SiswaDaftar>(MulaiEdit);
        BatalEditCommand = new RelayCommand(BatalEdit);
        // Siswa yang sudah tidak aktif tidak dinonaktifkan lagi (menimpa alasan);
        // `is not null`: CanExecute(null) transien dari WPF (pola crash E-2).
        NonaktifkanCommand = new RelayCommand<SiswaDaftar>(Nonaktifkan, s => s is not null && s.Aktif);
        UnggahFotoCommand = new RelayCommand<SiswaDaftar>(UnggahFoto);
        UnduhTemplateCommand = new RelayCommand(UnduhTemplate);
        ImporExcelCommand = new RelayCommand(ImporExcel);

        // §16.11: penggantian kartu hilang hanya bermakna bila siswa sudah
        // punya kartu aktif — pendaftaran kartu pertama lewat layar
        // Registrasi Kartu per kelas (§16.5), bukan di sini.
        GantiKartuCommand = new RelayCommand<SiswaDaftar>(BukaDialogGantiKartu, s => s is not null && s.PunyaKartu);

        _kelasFilterTerpilih = SemuaKelas;

        MuatUlangDaftarKelas();
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

    // A-2 (Master Bug & UI/UX Register): BisaSimpan() mensyaratkan KelasTerpilih
    // tidak null, tapi setter ini tidak pernah memberi tahu SimpanCommand untuk
    // requery -- RelayCommand.CanExecuteChanged murni manual, bukan
    // CommandManager.RequerySuggested. Bila Kelas dipilih terakhir (urutan di
    // BUG-REPORT-UAT-2026-09-20.md §Bug #1), tombol Simpan tetap tampak
    // nonaktif walau BisaSimpan() sebenarnya sudah true.
    public KelasDaftar? KelasTerpilih { get => _kelasTerpilih; set { if (SetField(ref _kelasTerpilih, value)) SimpanCommand.RaiseCanExecuteChanged(); } }

    public string? PesanGalat { get => _pesanGalat; private set => SetField(ref _pesanGalat, value); }

    public string? PesanStatus { get => _pesanStatus; private set => SetField(ref _pesanStatus, value); }

    public RelayCommand SimpanCommand { get; }

    public RelayCommand TambahCommand { get; }

    // Formulir tambah/ubah tampil di dialog (FormSiswaWindow), dibuka oleh
    // SiswaView saat event ini terpicu dan ditutup saat FormulirSelesai
    // (Simpan berhasil atau Batal). Audit UI 2026-09-24: panel formulir 320 px
    // di kanan tabel menyisakan ±730 px untuk tabel (±690 px di 1280) sehingga
    // kolom aksi terpotong dan tombol Simpan/Batal berada di bawah lipatan.
    public event EventHandler? FormulirDiminta;

    public event EventHandler? FormulirSelesai;

    // NISN adalah identitas siswa dan LayananSiswa.Perbarui tidak mengubahnya —
    // hanya bisa diisi saat menambah siswa baru.
    public bool NisnHanyaBaca => _sedangDiedit is not null;

    public RelayCommand<SiswaDaftar> EditCommand { get; }

    public RelayCommand BatalEditCommand { get; }

    public RelayCommand<SiswaDaftar> NonaktifkanCommand { get; }

    public RelayCommand<SiswaDaftar> UnggahFotoCommand { get; }

    public RelayCommand<SiswaDaftar> GantiKartuCommand { get; }

    public RelayCommand UnduhTemplateCommand { get; }

    public RelayCommand ImporExcelCommand { get; }

    private bool BisaSimpan() => !string.IsNullOrWhiteSpace(_nisn) && !string.IsNullOrWhiteSpace(_nama) && _kelasTerpilih is not null;

    private void MulaiTambah()
    {
        ResetFormulir();
        FormulirDiminta?.Invoke(this, EventArgs.Empty);
    }

    private void MulaiEdit(SiswaDaftar siswa)
    {
        _sedangDiedit = siswa;
        Nisn = siswa.Nisn;
        Nama = siswa.Nama;
        JenisKelamin = siswa.JenisKelamin;
        // Tempat/tanggal lahir, alamat, dan data orang tua tidak ada di baris
        // daftar — dimuat dari database. Sebelumnya dibiarkan kosong sehingga
        // Simpan menimpa keempat kolom itu dengan null (data hilang diam-diam).
        var tersimpan = _repositoriSiswa.CariUntukAudit(_connection, null, siswa.Id)?.Data;
        TempatLahir = tersimpan?.TempatLahir;
        TanggalLahir = tersimpan?.TanggalLahir;
        Alamat = tersimpan?.Alamat;
        NamaOrtu = tersimpan?.NamaOrtu;
        NoHpOrtu = tersimpan?.NoHpOrtu;
        KelasTerpilih = DaftarKelas.FirstOrDefault(k => k.Nama == siswa.KelasNama);
        PesanGalat = null;
        OnPropertyChanged(nameof(JudulFormulir));
        OnPropertyChanged(nameof(NisnHanyaBaca));
        SimpanCommand.RaiseCanExecuteChanged();
        FormulirDiminta?.Invoke(this, EventArgs.Empty);
    }

    private void BatalEdit()
    {
        ResetFormulir();
        FormulirSelesai?.Invoke(this, EventArgs.Empty);
    }

    private void ResetFormulir()
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
        OnPropertyChanged(nameof(NisnHanyaBaca));
    }

    private static readonly System.Text.RegularExpressions.Regex FormatNisn = new(@"^\d{10}$");

    private void Simpan()
    {
        var nisn = Nisn.Trim();
        var nama = Nama.Trim();

        // Validasi (CLAUDE.md §4.2 aturan 9: NISN teks 10 digit, nol di depan bertahan).
        if (KelasTerpilih is null)
        {
            PesanGalat = "Pilih kelas terlebih dahulu.";
            return;
        }

        if (_sedangDiedit is null && !FormatNisn.IsMatch(nisn))
        {
            PesanGalat = "NISN harus tepat 10 angka (tanpa spasi atau huruf).";
            return;
        }

        if (!string.IsNullOrWhiteSpace(TanggalLahir)
            && !DateOnly.TryParseExact(TanggalLahir.Trim(), "yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture, System.Globalization.DateTimeStyles.None, out _))
        {
            PesanGalat = "Tanggal lahir harus berformat TTTT-BB-HH, mis. 2011-03-15.";
            return;
        }

        if (_sedangDiedit is null && _repositoriSiswa.CariIdBerdasarkanNisn(_connection, nisn) is { } idAda)
        {
            // Nama pemilik NISN itu disebut (bukan data pribadi lain) supaya operator tahu siswa mana.
            PesanGalat = $"NISN {nisn} sudah terdaftar atas nama {_repositoriSiswa.CariNamaUntukAudit(_connection, idAda)}.";
            return;
        }

        var waktu = _penyediaWaktu.Sekarang;
        var data = new DataSiswa(
            nisn, nama, JenisKelamin,
            Bersihkan(TempatLahir), Bersihkan(TanggalLahir), Bersihkan(Alamat), Bersihkan(NamaOrtu), Bersihkan(NoHpOrtu));
        var kelas = (_tahunAjaranId, KelasTerpilih.Id);
        var sedangDiedit = _sedangDiedit;

        var berhasil = PenangananGalat.Coba(
            () =>
            {
                if (sedangDiedit is { } siswa)
                {
                    _layananSiswa.Perbarui(_connection, _akun, waktu, siswa.Id, data, kelas);
                }
                else
                {
                    _layananSiswa.Tambah(_connection, _akun, waktu, data, kelas);
                }
            },
            pesan => PesanGalat = pesan,
            "SiswaViewModel.Simpan");

        if (!berhasil)
        {
            PesanStatus = null;
            return;
        }

        BatalEdit();
        MuatUlang();
        PesanStatus = $"Siswa {nama} tersimpan.";
    }

    private static string? Bersihkan(string? nilai) => string.IsNullOrWhiteSpace(nilai) ? null : nilai.Trim();

    private void Nonaktifkan(SiswaDaftar siswa)
    {
        var dialogViewModel = new AlasanTidakAktifViewModel(AlasanTidakAktif);
        var dialog = new AlasanTidakAktifWindow(dialogViewModel);
        if (dialog.ShowDialog() != true || dialog.AlasanTerpilih is null)
        {
            return;
        }

        var alasan = dialog.AlasanTerpilih;
        PesanStatus = null;
        if (!PenangananGalat.Coba(
                () => _layananSiswa.TandaiTidakAktif(_connection, _akun, _penyediaWaktu.Sekarang, siswa.Id, alasan),
                pesan => PesanGalat = pesan,
                "SiswaViewModel.Nonaktifkan"))
        {
            return;
        }

        PesanGalat = null;
        MuatUlang();
        PesanStatus = $"Siswa {siswa.Nama} ditandai tidak aktif ({alasan}).";
    }

    private void UnggahFoto(SiswaDaftar siswa)
    {
        var dialog = new OpenFileDialog { Filter = "Gambar|*.jpg;*.jpeg;*.png;*.bmp" };
        if (dialog.ShowDialog() != true)
        {
            return;
        }

        UnggahFotoDariBerkas(siswa, dialog.FileName);
    }

    // UAT 2026-09-23: dipisah dari UnggahFoto supaya bisa diuji tanpa
    // OpenFileDialog sungguhan -- pola sama seperti SimpanTemplateKeBerkas
    // (A-8). Sebelumnya TIDAK ada try/catch sama sekali -- kegagalan
    // menyalin/mengubah ukuran gambar (berkas rusak, akses ditolak, folder
    // data read-only) melempar exception tak tertangani sampai handler
    // global, dan "Foto ... tersimpan." tidak pernah bisa salah tampil
    // karena baris itu memang tidak pernah dicapai kalau ada exception --
    // tapi operator hanya melihat dialog galat generik, bukan alasan nyata.
    // Sekarang: pesan berhasil BARU tampil setelah PengolahFoto DAN
    // LayananSiswa.SimpanFoto (yang menulis lewat JalurPerubahanData,
    // atomik) sama-sama sukses; kegagalan menampilkan alasan yang jelas,
    // bukan pesan berhasil.
    public void UnggahFotoDariBerkas(SiswaDaftar siswa, string pathSumber)
    {
        var namaBerkasTujuan = $"{siswa.Nisn}.png";
        var pathTujuan = System.IO.Path.Combine(LokasiData.FolderData, "foto", namaBerkasTujuan);

        try
        {
            PengolahFoto.SimpanDiubahUkuran(pathSumber, pathTujuan);
            _layananSiswa.SimpanFoto(_connection, _akun, _penyediaWaktu.Sekarang, siswa.Id, namaBerkasTujuan);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or NotSupportedException)
        {
            PesanGalat = PesanUntukKegagalanUnggahFoto(ex);
            PesanStatus = null;
            return;
        }

        PesanGalat = null;
        PesanStatus = $"Foto {siswa.Nama} berhasil diunggah.";
        MuatUlang();
    }

    private static string PesanUntukKegagalanUnggahFoto(Exception ex) => ex switch
    {
        UnauthorizedAccessException => "Foto tidak bisa disimpan karena akses ditolak. Periksa izin folder data, lalu coba lagi.",
        NotSupportedException => "Format gambar tidak dikenali. Gunakan berkas JPG, PNG, atau BMP yang valid.",
        IOException => "Berkas foto sedang dipakai aplikasi lain atau tidak bisa dibaca. Tutup berkasnya, lalu coba lagi.",
        _ => "Gagal mengunggah foto. Coba lagi; bila terus terjadi, laporkan ke pengelola aplikasi.",
    };

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


    // Bug #3 (Master Bug & UI/UX Register A-20 / UAT 2026-09-22): panel
    // Siswa di-cache oleh MainWindowViewModel (`_siswaViewModel ??= ...`) —
    // constructor ViewModel ini hanya jalan SEKALI per sesi, jadi kelas
    // baru yang dibuat lewat panel Kelas SETELAH panel Siswa pertama kali
    // dibuka tidak pernah masuk ke DaftarKelas/DaftarKelasFilter tanpa
    // restart. MainWindowViewModel.PerbaruiKontenAktif memanggil method
    // PUBLIC ini setiap kali menu "Siswa" dipilih (dibuat baru MAUPUN
    // dipakai ulang dari cache) — Clear() dulu sebelum re-add supaya
    // refresh berulang tidak pernah menghasilkan duplikat. KelasDaftar
    // adalah record (kesetaraan berbasis nilai) jadi ComboBox.SelectedItem
    // yang sedang dipilih otomatis cocok lagi ke instance baru asalkan
    // datanya sama persis — tidak perlu penyimpanan/pemulihan seleksi
    // manual di sini.
    public void MuatUlangDaftarKelas()
    {
        // WPF Selector (ComboBox) kehilangan seleksi begitu ObservableCollection
        // memancarkan notifikasi Reset (Clear()) — kalau binding SelectedItem
        // dua arah, itu bisa balik mendorong null ke KelasFilterTerpilih/
        // KelasTerpilih. Id ditangkap dulu di sini, dipulihkan eksplisit di
        // bawah lewat property setter (bukan dibiarkan WPF menebak sendiri).
        var idFilterSebelumnya = _kelasFilterTerpilih.Id;
        var idTerpilihSebelumnya = _kelasTerpilih?.Id;

        DaftarKelas.Clear();
        DaftarKelasFilter.Clear();

        DaftarKelasFilter.Add(SemuaKelas);
        foreach (var kelas in new RepositoriKelas().DaftarUntukTahunAjaran(_connection, _tahunAjaranId))
        {
            DaftarKelas.Add(kelas);
            DaftarKelasFilter.Add(kelas);
        }

        KelasFilterTerpilih = DaftarKelasFilter.FirstOrDefault(k => k.Id == idFilterSebelumnya) ?? SemuaKelas;
        KelasTerpilih = idTerpilihSebelumnya is { } idTerpilih ? DaftarKelas.FirstOrDefault(k => k.Id == idTerpilih) : null;
    }

    // UAT Bug #11 (2026-09-23, putaran kelima): public supaya
    // MainWindowViewModel bisa memanggilnya setelah RegistrasiKartuWindow
    // (dialog TERPISAH, tidak tahu-menahu soal SiswaViewModel) tertutup —
    // status Kartu siswa yang baru didaftarkan tidak pernah muncul di panel
    // Siswa yang sudah ter-cache tanpa panggilan eksplisit ini.
    public void MuatUlang()
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
