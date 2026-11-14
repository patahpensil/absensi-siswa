using System.Collections.ObjectModel;
using Absensi.App.Mvvm;
using Absensi.Data.Akun;
using Absensi.Data.MasterData;
using Absensi.Data.Repositori;
using Absensi.Domain;
using Absensi.Domain.Akun;
using Microsoft.Data.Sqlite;

namespace Absensi.App.MasterData;

// PRD §14.4/§5.2: daftar kelas, tingkat 1-9, wali kelas dipilih dari
// daftar guru (bukan diketik bebas).
public sealed class KelasViewModel : ObservableObject
{
    private readonly SqliteConnection _connection;
    private readonly AkunTersimpan _akun;
    private readonly IPenyediaWaktu _penyediaWaktu;
    private int _tahunAjaranId;
    private PeriodOption? _selectedPeriod;
    private readonly LayananKelas _layananKelas = new();
    private readonly RepositoriKelas _repositoriKelas = new();

    private string _nama = string.Empty;
    private int _tingkat = 7;
    private GuruRingkas? _waliTerpilih;
    private KelasDaftar? _sedangDiedit;
    private string? _pesanGalat;
    private string? _pesanStatus;

    public KelasViewModel(SqliteConnection connection, AkunTersimpan akun, IPenyediaWaktu penyediaWaktu, int tahunAjaranId)
    {
        _connection = connection;
        _akun = akun;
        _penyediaWaktu = penyediaWaktu;
        _tahunAjaranId = tahunAjaranId;

        SimpanCommand = new RelayCommand(Simpan, () => !string.IsNullOrWhiteSpace(_nama));
        EditCommand = new RelayCommand<KelasDaftar>(MulaiEdit);
        BatalEditCommand = new RelayCommand(BatalEdit);
        OpenAcademicPeriodsCommand = new RelayCommand(OpenAcademicPeriods, () => CanManagePeriods);
        ReloadPeriods();

        foreach (var guru in new RepositoriGuru().DaftarAktifRingkas(connection))
        {
            DaftarGuru.Add(guru);
        }

        MuatUlang();
    }

    public ObservableCollection<KelasDaftar> Daftar { get; } = new();

    // PRD §16.14: classes for the next period are created here before promoting.
    public ObservableCollection<PeriodOption> Periods { get; } = new();

    public PeriodOption? SelectedPeriod
    {
        get => _selectedPeriod;
        set
        {
            if (!SetField(ref _selectedPeriod, value) || value is null) return;
            _tahunAjaranId = value.Id;
            BatalEdit();
            MuatUlang();
        }
    }

    public bool CanManagePeriods => _akun.Peran is PeranAkun.KepalaSekolah or PeranAkun.Admin;

    public RelayCommand OpenAcademicPeriodsCommand { get; }

    // Overridable in tests; opens the dialog modally and reports when it closed.
    internal Action<AcademicPeriodViewModel> ShowAcademicPeriods { get; set; } =
        viewModel => new AcademicPeriodWindow(viewModel) { Owner = System.Windows.Application.Current?.MainWindow }.ShowDialog();

    // PRD §5.2: tingkat = pilihan 1-9 (bukan ketikan bebas — ketikan bukan angka
    // sebelumnya gagal tanpa pesan apa pun).
    public IReadOnlyList<int> DaftarTingkat { get; } = new[] { 1, 2, 3, 4, 5, 6, 7, 8, 9 };

    public ObservableCollection<GuruRingkas> DaftarGuru { get; } = new();

    public string JudulFormulir => _sedangDiedit is null ? "Tambah kelas" : $"Ubah kelas {_sedangDiedit.Nama}";

    public string Nama
    {
        get => _nama;
        set
        {
            if (SetField(ref _nama, value))
            {
                SimpanCommand.RaiseCanExecuteChanged();
            }
        }
    }

    public int Tingkat
    {
        get => _tingkat;
        set => SetField(ref _tingkat, value);
    }

    public GuruRingkas? WaliTerpilih
    {
        get => _waliTerpilih;
        set => SetField(ref _waliTerpilih, value);
    }

    public string? PesanGalat
    {
        get => _pesanGalat;
        private set => SetField(ref _pesanGalat, value);
    }

    public string? PesanStatus
    {
        get => _pesanStatus;
        private set => SetField(ref _pesanStatus, value);
    }

    public RelayCommand SimpanCommand { get; }

    public RelayCommand<KelasDaftar> EditCommand { get; }

    public RelayCommand BatalEditCommand { get; }

    private void MulaiEdit(KelasDaftar kelas)
    {
        _sedangDiedit = kelas;
        Nama = kelas.Nama;
        Tingkat = kelas.Tingkat;
        WaliTerpilih = DaftarGuru.FirstOrDefault(g => g.Id == kelas.WaliGuruId);
        OnPropertyChanged(nameof(JudulFormulir));
    }

    private void BatalEdit()
    {
        _sedangDiedit = null;
        Nama = string.Empty;
        Tingkat = 7;
        WaliTerpilih = null;
        PesanGalat = null;
        OnPropertyChanged(nameof(JudulFormulir));
    }

    private void Simpan()
    {
        var nama = Nama.Trim();
        if (nama.Length == 0)
        {
            PesanGalat = "Nama kelas wajib diisi.";
            return;
        }

        if (Tingkat is < 1 or > 9)
        {
            PesanGalat = "Tingkat harus antara 1 dan 9.";
            return;
        }

        // Nama kelas unik per tahun ajaran; dicek di sini supaya tampil sebagai pesan
        // di formulir, bukan pelanggaran UNIQUE yang lolos ke penanganan galat global.
        if (_repositoriKelas.CariIdBerdasarkanNama(_connection, _tahunAjaranId, nama) is { } idAda && idAda != _sedangDiedit?.Id)
        {
            PesanGalat = $"Kelas {nama} sudah ada pada tahun ajaran ini.";
            return;
        }

        var waktu = _penyediaWaktu.Sekarang;
        var sedangDiedit = _sedangDiedit;
        var tingkat = Tingkat;
        var waliId = WaliTerpilih?.Id;

        PesanStatus = null;
        if (!PenangananGalat.Coba(
                () =>
                {
                    if (sedangDiedit is { } kelas)
                    {
                        _layananKelas.Perbarui(_connection, _akun, waktu, kelas.Id, nama, tingkat, waliId);
                    }
                    else
                    {
                        _layananKelas.Tambah(_connection, _akun, waktu, _tahunAjaranId, nama, tingkat, waliId);
                    }
                },
                pesan => PesanGalat = pesan,
                "KelasViewModel.Simpan"))
        {
            return;
        }

        BatalEdit();
        MuatUlang();
        PesanStatus = $"Kelas {nama} tersimpan.";
    }

    private void OpenAcademicPeriods()
    {
        ShowAcademicPeriods(new AcademicPeriodViewModel(_connection, _akun, _penyediaWaktu));
        ReloadPeriods();
        MuatUlang();
    }

    private void ReloadPeriods()
    {
        var selectedId = _tahunAjaranId;
        Periods.Clear();
        foreach (var period in new RepositoriTahunAjaran().ListPeriods(_connection))
        {
            Periods.Add(new PeriodOption(period));
        }

        _selectedPeriod = Periods.FirstOrDefault(option => option.Id == selectedId);
        OnPropertyChanged(nameof(SelectedPeriod));
    }

    private void MuatUlang()
    {
        Daftar.Clear();
        foreach (var kelas in _repositoriKelas.DaftarUntukTahunAjaran(_connection, _tahunAjaranId))
        {
            Daftar.Add(kelas);
        }
    }
}
