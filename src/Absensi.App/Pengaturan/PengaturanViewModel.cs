using System.Collections.ObjectModel;
using Absensi.App.Mvvm;
using Absensi.Data.Akun;
using Absensi.Data.Repositori;
using Absensi.Data.Waktu;
using Absensi.Domain;
using Absensi.Domain.Akun;
using Microsoft.Data.Sqlite;

namespace Absensi.App.Pengaturan;

// RENCANA-TUGAS.md Tugas 9 — hanya bagian "jadwal harian" dari layar
// Pengaturan (PANDUAN-UI-UX.md §9: "Pengaturan | Identitas sekolah,
// jadwal harian, tahun ajaran, token bot, backup"). Bagian lain menunggu
// Tugas masing-masing — bukan cakupan Tugas 9.
public sealed class PengaturanViewModel : ObservableObject
{
    // hari: 1=Senin..7=Minggu (docs/SKEMA-DATABASE.md §4).
    private static readonly string[] NamaHari = { "Senin", "Selasa", "Rabu", "Kamis", "Jumat", "Sabtu", "Minggu" };

    private readonly SqliteConnection _connection;
    private readonly AkunTersimpan _akun;
    private readonly IPenyediaWaktu _penyediaWaktu;
    private readonly int _tahunAjaranId;
    private readonly RepositoriJadwalHari _repositoriJadwalHari = new();
    private readonly LayananJadwalHari _layananJadwalHari = new();

    private string? _pesanGalat;
    private string? _pesanStatus;

    public PengaturanViewModel(SqliteConnection connection, AkunTersimpan akun, IPenyediaWaktu penyediaWaktu, int tahunAjaranId)
    {
        _connection = connection;
        _akun = akun;
        _penyediaWaktu = penyediaWaktu;
        _tahunAjaranId = tahunAjaranId;

        SimpanJadwalCommand = new RelayCommand(SimpanJadwal, () => BolehMengubah);

        MuatJadwal();
    }

    public ObservableCollection<JadwalHariBarisViewModel> JadwalHarian { get; } = new();

    // PRD §3: pengaturan adalah kewenangan penuh (Kepala Sekolah/Admin) —
    // wali kelas tidak berwenang. Ditegakkan juga (dan sebenarnya oleh)
    // LayananJadwalHari; ini sekadar menyembunyikan tombol Simpan.
    public bool BolehMengubah => _akun.Peran != PeranAkun.WaliKelas;

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

    public RelayCommand SimpanJadwalCommand { get; }

    private void MuatJadwal()
    {
        JadwalHarian.Clear();
        foreach (var baris in _repositoriJadwalHari.DaftarUntukTahunAjaran(_connection, _tahunAjaranId))
        {
            JadwalHarian.Add(new JadwalHariBarisViewModel(
                baris.Hari,
                NamaHari[baris.Hari - 1],
                baris.HariSekolah,
                FormatJam(baris.JamMasuk),
                FormatJam(baris.JamRekap),
                FormatJam(baris.BatasTapMasuk),
                FormatJam(baris.MulaiPulang)));
        }
    }

    private static string? FormatJam(TimeOnly? jam) => jam?.ToString("HH:mm");

    private void SimpanJadwal()
    {
        var hasil = new List<JadwalHariBaris>();

        foreach (var baris in JadwalHarian)
        {
            if (!CobaUraikanJam(baris.NamaHari, "jam masuk", baris.JamMasuk, out var jamMasuk)
                || !CobaUraikanJam(baris.NamaHari, "jam rekap", baris.JamRekap, out var jamRekap)
                || !CobaUraikanJam(baris.NamaHari, "batas akhir tap masuk", baris.BatasTapMasuk, out var batasTapMasuk)
                || !CobaUraikanJam(baris.NamaHari, "mulai absen pulang", baris.MulaiPulang, out var mulaiPulang))
            {
                PesanStatus = null;
                return;
            }

            hasil.Add(new JadwalHariBaris(baris.Hari, baris.HariSekolah, jamMasuk, jamRekap, batasTapMasuk, mulaiPulang));
        }

        PesanGalat = null;
        _layananJadwalHari.Perbarui(_connection, _akun, _penyediaWaktu.Sekarang, _tahunAjaranId, hasil);
        PesanStatus = "Jadwal harian tersimpan.";
    }

    private bool CobaUraikanJam(string namaHari, string namaKolom, string? teks, out TimeOnly? hasil)
    {
        hasil = null;
        if (string.IsNullOrWhiteSpace(teks))
        {
            return true;
        }

        if (!TimeOnly.TryParse(teks, out var jam))
        {
            PesanGalat = $"{namaHari}: format {namaKolom} tidak valid — pakai HH:mm, misalnya 07:30.";
            return false;
        }

        hasil = jam;
        return true;
    }
}
