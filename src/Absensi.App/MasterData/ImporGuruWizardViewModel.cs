using System.Collections.ObjectModel;
using Absensi.App.Mvvm;
using Absensi.Data.Akun;
using Absensi.Data.MasterData;
using Absensi.Data.Repositori;
using Absensi.Domain;
using Microsoft.Data.Sqlite;
using Microsoft.Win32;

namespace Absensi.App.MasterData;

// Alur impor guru (docs/PRD.md §16.15 langkah 2, tampilan §14.5): pilih
// berkas → analisis otomatis → tampilkan error/preview → konfirmasi →
// impor → hasil.
public sealed class ImporGuruWizardViewModel : ObservableObject
{
    private readonly SqliteConnection _connection;
    private readonly AkunTersimpan _akun;
    private readonly IPenyediaWaktu _penyediaWaktu;
    private readonly ImporGuruExcel _importir = new();

    private HasilAnalisisImporGuru? _analisis;
    private HasilImporGuru? _hasilImpor;
    private string? _pesanGalat;
    private LangkahImpor _langkahSaatIni = LangkahImpor.Analisis;

    public ImporGuruWizardViewModel(SqliteConnection connection, AkunTersimpan akun, IPenyediaWaktu penyediaWaktu)
    {
        _connection = connection;
        _akun = akun;
        _penyediaWaktu = penyediaWaktu;

        PilihBerkasCommand = new RelayCommand(PilihBerkas);
        ImporCommand = new RelayCommand(Impor, () => _analisis is { AdaKesalahan: false });
    }

    public ObservableCollection<BarisImporGuru> Baris { get; } = new();

    public bool SudahDianalisis => _analisis is not null;

    public LangkahImpor LangkahSaatIni
    {
        get => _langkahSaatIni;
        private set => SetField(ref _langkahSaatIni, value);
    }

    public string? PesanGalat
    {
        get => _pesanGalat;
        private set => SetField(ref _pesanGalat, value);
    }

    public int? JumlahBaru => _analisis?.JumlahBaru;

    public int? JumlahDiperbarui => _analisis?.JumlahDiperbarui;

    public int? JumlahBermasalah => _analisis?.JumlahBermasalah;

    public HasilImporGuru? HasilImpor
    {
        get => _hasilImpor;
        private set => SetField(ref _hasilImpor, value);
    }

    public string TeksTombolImpor => _analisis is { } analisis
        ? $"Impor {analisis.Baris.Count(b => b.Sah)} baris"
        : "Impor";

    public RelayCommand PilihBerkasCommand { get; }

    public RelayCommand ImporCommand { get; }

    private void PilihBerkas()
    {
        var dialog = new OpenFileDialog { Filter = "Berkas Excel (*.xlsx)|*.xlsx" };
        if (dialog.ShowDialog() != true)
        {
            return;
        }

        AnalisisBerkas(dialog.FileName);
    }

    // Dipisah dari PilihBerkas supaya bisa diuji tanpa OpenFileDialog sungguhan.
    public void AnalisisBerkas(string pathBerkas)
    {
        Baris.Clear();
        HasilImpor = null;
        LangkahSaatIni = LangkahImpor.Validasi;

        try
        {
            _analisis = _importir.Analisis(_connection, pathBerkas);
            foreach (var baris in _analisis.Baris)
            {
                Baris.Add(baris);
            }

            PesanGalat = null;
            LangkahSaatIni = LangkahImpor.Preview;
        }
        catch (Exception ex)
        {
            _analisis = null;
            PesanGalat = ex.Message;
        }

        NotifikasiSemuaBerubah();
    }

    private void Impor()
    {
        if (_analisis is null)
        {
            return;
        }

        HasilImpor = _importir.Impor(_connection, _akun, _penyediaWaktu.Sekarang, _analisis.Baris);
        LangkahSaatIni = LangkahImpor.Impor;
    }

    private void NotifikasiSemuaBerubah()
    {
        OnPropertyChanged(nameof(SudahDianalisis));
        OnPropertyChanged(nameof(JumlahBaru));
        OnPropertyChanged(nameof(JumlahDiperbarui));
        OnPropertyChanged(nameof(JumlahBermasalah));
        OnPropertyChanged(nameof(TeksTombolImpor));
        ImporCommand.RaiseCanExecuteChanged();
    }
}
