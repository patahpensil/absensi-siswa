using System.Collections.ObjectModel;
using Absensi.App.Mvvm;
using Absensi.Data.Akun;
using Absensi.Data.Repositori;
using Absensi.Data.Waktu;
using Absensi.Domain;
using Absensi.Domain.Akun;
using Absensi.Domain.Kehadiran;
using Microsoft.Data.Sqlite;

namespace Absensi.App.Kalender;

// RENCANA-TUGAS.md Tugas 9 — layar Kalender (PRD §6). "Tampilan bulanan"
// (PANDUAN-UI-UX.md §9) memakai kontrol Calendar bawaan WPF (SelectionMode
// MultipleRange) untuk pilih tanggal/rentang; kursor tetap bisa memilih
// ulang tanggal yang sudah libur (perlu bisa dikembalikan jadi hari
// sekolah untuk MPLS/Sumatif), jadi TanggalLibur di sini hanya dipakai
// untuk pewarnaan (lihat TanggalLiburMultiConverter), bukan BlackoutDates.
public sealed class KalenderViewModel : ObservableObject
{
    private static readonly System.Globalization.CultureInfo BudayaIndonesia = new("id-ID");

    private readonly SqliteConnection _connection;
    private readonly AkunTersimpan _akun;
    private readonly IPenyediaWaktu _penyediaWaktu;
    private readonly LayananKalender _layananKalender = new();
    private readonly RepositoriKalender _repositoriKalender = new();

    private DateTime _bulanTampil;
    private DateOnly? _tanggalAwalTerpilih;
    private DateOnly? _tanggalAkhirTerpilih;
    private string? _keterangan;
    private HashSet<DateOnly> _tanggalLibur = new();
    private string? _pesanGalat;
    private string? _pesanStatus;

    public KalenderViewModel(SqliteConnection connection, AkunTersimpan akun, IPenyediaWaktu penyediaWaktu)
    {
        _connection = connection;
        _akun = akun;
        _penyediaWaktu = penyediaWaktu;
        _bulanTampil = new DateTime(penyediaWaktu.Sekarang.Year, penyediaWaktu.Sekarang.Month, 1);

        TandaiLiburCommand = new RelayCommand(() => Tandai(JenisHariKalender.Libur), BisaTandai);
        TandaiSekolahCommand = new RelayCommand(() => Tandai(JenisHariKalender.Sekolah), () => RentangTerpilihValid);

        MuatBulan();
    }

    public ObservableCollection<KalenderEntri> DaftarBulanIni { get; } = new();

    // Dibaca lewat MultiBinding (KalenderView.xaml) — HashSet baru setiap
    // kali diisi ulang (bukan Clear+Add) supaya SetField mendeteksi
    // perubahan dan sel kalender yang sedang tampil ikut menggambar ulang.
    public HashSet<DateOnly> TanggalLibur
    {
        get => _tanggalLibur;
        private set => SetField(ref _tanggalLibur, value);
    }

    public string BulanTeks => _bulanTampil.ToString("MMMM yyyy", BudayaIndonesia);

    public string? RentangTeks
    {
        get
        {
            if (_tanggalAwalTerpilih is not { } awal || _tanggalAkhirTerpilih is not { } akhir)
            {
                return null;
            }

            return awal == akhir
                ? awal.ToString("d MMMM yyyy", BudayaIndonesia)
                : $"{awal.ToString("d MMMM yyyy", BudayaIndonesia)} – {akhir.ToString("d MMMM yyyy", BudayaIndonesia)}";
        }
    }

    public string? Keterangan
    {
        get => _keterangan;
        set => SetField(ref _keterangan, value);
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

    public RelayCommand TandaiLiburCommand { get; }

    public RelayCommand TandaiSekolahCommand { get; }

    // PRD §3: kalender pendidikan adalah kewenangan penuh (Kepala
    // Sekolah/Admin) — wali kelas tidak berwenang. Ditegakkan juga (dan
    // sebenarnya oleh) LayananKalender; ini sekadar menyembunyikan tombol.
    public bool BolehMengubah => _akun.Peran != PeranAkun.WaliKelas;

    private bool RentangTerpilihValid => BolehMengubah && _tanggalAwalTerpilih is not null;

    private bool BisaTandai() => RentangTerpilihValid && !string.IsNullOrWhiteSpace(_keterangan);

    // Dipanggil KalenderView code-behind saat Calendar.SelectedDatesChanged
    // — Calendar sendiri yang menegakkan aturan seleksi (drag/shift untuk
    // rentang), di sini cukup ambil batas bawah/atasnya (PRD §6: "per
    // tanggal atau per rentang").
    public void TetapkanTanggalTerpilih(IReadOnlyList<DateTime> tanggalTerpilih)
    {
        if (tanggalTerpilih.Count == 0)
        {
            _tanggalAwalTerpilih = null;
            _tanggalAkhirTerpilih = null;
        }
        else
        {
            _tanggalAwalTerpilih = DateOnly.FromDateTime(tanggalTerpilih.Min());
            _tanggalAkhirTerpilih = DateOnly.FromDateTime(tanggalTerpilih.Max());
        }

        PesanGalat = null;
        OnPropertyChanged(nameof(RentangTeks));
        TandaiLiburCommand.RaiseCanExecuteChanged();
        TandaiSekolahCommand.RaiseCanExecuteChanged();
    }

    // Dipanggil KalenderView code-behind saat Calendar.DisplayDateChanged.
    public void PindahBulan(DateTime bulanBaru)
    {
        _bulanTampil = new DateTime(bulanBaru.Year, bulanBaru.Month, 1);
        OnPropertyChanged(nameof(BulanTeks));
        MuatBulan();
    }

    private void Tandai(JenisHariKalender jenis)
    {
        if (_tanggalAwalTerpilih is not { } awal || _tanggalAkhirTerpilih is not { } akhir)
        {
            return;
        }

        if (jenis == JenisHariKalender.Libur && string.IsNullOrWhiteSpace(_keterangan))
        {
            PesanGalat = "Keterangan wajib diisi saat menandai tanggal sebagai libur.";
            return;
        }

        // Bulan dikunci (PRD §10/§16.13) dan wali kelas tidak berwenang dilempar layanan
        // sebagai InvalidOperationException berpesan siap-tampil — sebelumnya lolos ke
        // penanganan galat global dan MENUTUP aplikasi.
        PesanStatus = null;
        if (!PenangananGalat.Coba(
                () => _layananKalender.TandaiRentang(_connection, _akun, _penyediaWaktu.Sekarang, awal, akhir, jenis, _keterangan),
                pesan => PesanGalat = pesan,
                "KalenderViewModel.Tandai"))
        {
            return;
        }

        PesanGalat = null;
        PesanStatus = jenis == JenisHariKalender.Libur
            ? $"{RentangTeks} ditandai libur. Notifikasi yang masih antre pada rentang itu dibatalkan."
            : $"{RentangTeks} ditandai hari sekolah.";
        Keterangan = null;
        MuatBulan();
    }

    private void MuatBulan()
    {
        DaftarBulanIni.Clear();
        var daftarBaru = new HashSet<DateOnly>();

        foreach (var entri in _repositoriKalender.DaftarUntukBulan(_connection, _bulanTampil.Year, _bulanTampil.Month))
        {
            DaftarBulanIni.Add(entri);
            if (entri.Jenis == JenisHariKalender.Libur)
            {
                daftarBaru.Add(entri.Tanggal);
            }
        }

        TanggalLibur = daftarBaru;
    }
}
