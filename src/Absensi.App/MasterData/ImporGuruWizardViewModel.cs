using System.Collections.ObjectModel;
using System.IO;
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
        ImporCommand = new RelayCommand(Impor, () => _analisis is { AdaKesalahan: false } && _hasilImpor is null);
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
        private set
        {
            if (SetField(ref _hasilImpor, value))
            {
                OnPropertyChanged(nameof(TeksTombolImpor));
                ImporCommand.RaiseCanExecuteChanged();
            }
        }
    }

    public string TeksTombolImpor => _hasilImpor is not null
        ? "Sudah diimpor"
        : _analisis is { } analisis
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
        // TODO-VERSI-BERIKUTNYA.md A.3.2 / Master Bug & UI/UX Register A-8:
        // catch semula "catch (Exception ex)" terlalu luas -- menampilkan
        // ex.Message mentah (bahasa Inggris, kadang menyingkap path berkas
        // lengkap) untuk exception apa pun, termasuk kegagalan internal
        // yang seharusnya tidak tertelan diam-diam. Jenis di bawah
        // diverifikasi langsung lewat percobaan nyata terhadap ClosedXML
        // 0.105.1 (bukan tebakan): berkas tidak ada -> FileNotFoundException
        // (turunan IOException), berkas terkunci proses lain -> IOException,
        // berkas 0-byte/bukan xlsx sah / 0 worksheet -> FileFormatException
        // atau FormatException (lihat juga ImporGuruExcel.Analisis, yang
        // sengaja TIDAK lagi melempar InvalidOperationException generik
        // bawaan Linq -- tipe itu tidak aman ditangkap di sini karena bisa
        // juga berasal dari kegagalan internal tak terkait). Di luar jenis
        // ini (mis. SqliteException dari lapisan data) dibiarkan lolos ke
        // handler global (App.xaml.cs/PencatatGalat) yang sudah mencatat
        // detail teknis lengkap dan menampilkan pesan generik -- bukan
        // ditelan di sini.
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or FormatException)
        {
            _analisis = null;
            PesanGalat = PesanUntukKegagalanBerkas(ex);
        }

        NotifikasiSemuaBerubah();
    }

    private static string PesanUntukKegagalanBerkas(Exception ex) => ex switch
    {
        FileNotFoundException => "Berkas tidak ditemukan. Pastikan lokasinya masih benar, lalu pilih berkas lagi.",
        UnauthorizedAccessException => "Berkas tidak bisa dibaca karena akses ditolak. Periksa izin berkas, lalu coba lagi.",
        IOException => "Berkas sedang dipakai aplikasi lain (mis. Excel). Tutup berkasnya, lalu coba lagi.",
        FileFormatException => "Berkas ini bukan berkas Excel (.xlsx) yang sah, atau isinya rusak. Periksa kembali berkasnya.",
        // FormatException dari validasi kolom header atau worksheet kosong
        // (ImporGuruExcel.Analisis) -- pesannya sendiri sudah jelas dan
        // Bahasa Indonesia.
        _ => ex.Message,
    };

    private void Impor()
    {
        if (_analisis is null)
        {
            return;
        }

        // Klik berulang: setelah berhasil, ImporCommand nonaktif (HasilImpor terisi) — tak ada
        // impor ganda. Kegagalan (mis. DB terkunci) jadi pesan aman, bukan menutup aplikasi.
        PesanGalat = null;
        var baris = _analisis.Baris;
        if (PenangananGalat.Coba(
                () => HasilImpor = _importir.Impor(_connection, _akun, _penyediaWaktu.Sekarang, baris),
                pesan => PesanGalat = "Impor gagal, tidak ada data yang diubah. " + pesan,
                "ImporGuruWizardViewModel.Impor"))
        {
            LangkahSaatIni = LangkahImpor.Impor;
        }
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
