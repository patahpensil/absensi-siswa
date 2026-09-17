using System.Collections.ObjectModel;
using Absensi.App.Mvvm;
using Absensi.Data.Akun;
using Absensi.Data.MasterData;
using Absensi.Data.Repositori;
using Absensi.Domain;
using Microsoft.Data.Sqlite;

namespace Absensi.App.MasterData;

// PRD §14.4/§5.2: daftar kelas, tingkat 1-9, wali kelas dipilih dari
// daftar guru (bukan diketik bebas).
public sealed class KelasViewModel : ObservableObject
{
    private readonly SqliteConnection _connection;
    private readonly AkunTersimpan _akun;
    private readonly IPenyediaWaktu _penyediaWaktu;
    private readonly int _tahunAjaranId;
    private readonly LayananKelas _layananKelas = new();
    private readonly RepositoriKelas _repositoriKelas = new();

    private string _nama = string.Empty;
    private int _tingkat = 7;
    private GuruRingkas? _waliTerpilih;
    private KelasDaftar? _sedangDiedit;
    private string? _pesanGalat;

    public KelasViewModel(SqliteConnection connection, AkunTersimpan akun, IPenyediaWaktu penyediaWaktu, int tahunAjaranId)
    {
        _connection = connection;
        _akun = akun;
        _penyediaWaktu = penyediaWaktu;
        _tahunAjaranId = tahunAjaranId;

        SimpanCommand = new RelayCommand(Simpan, () => !string.IsNullOrWhiteSpace(_nama));
        EditCommand = new RelayCommand<KelasDaftar>(MulaiEdit);
        BatalEditCommand = new RelayCommand(BatalEdit);

        foreach (var guru in new RepositoriGuru().DaftarAktifRingkas(connection))
        {
            DaftarGuru.Add(guru);
        }

        MuatUlang();
    }

    public ObservableCollection<KelasDaftar> Daftar { get; } = new();

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
        if (Tingkat is < 1 or > 9)
        {
            PesanGalat = "Tingkat harus antara 1 dan 9.";
            return;
        }

        var waktu = _penyediaWaktu.Sekarang;

        if (_sedangDiedit is { } kelas)
        {
            _layananKelas.Perbarui(_connection, _akun, waktu, kelas.Id, Nama, Tingkat, WaliTerpilih?.Id);
        }
        else
        {
            _layananKelas.Tambah(_connection, _akun, waktu, _tahunAjaranId, Nama, Tingkat, WaliTerpilih?.Id);
        }

        BatalEdit();
        MuatUlang();
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
