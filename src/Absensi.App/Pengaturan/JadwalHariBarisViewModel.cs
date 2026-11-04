using Absensi.App.Mvvm;

namespace Absensi.App.Pengaturan;

// Satu baris formulir jadwal harian (PRD §7) — jam disunting sebagai teks
// "HH:mm" (pola sama seperti TanggalLahir di SiswaViewModel), diuraikan
// dan divalidasi saat Simpan (PengaturanViewModel.Simpan).
public sealed class JadwalHariBarisViewModel : ObservableObject
{
    private bool _hariSekolah;
    private string? _jamMasuk;
    private string? _jamRekap;
    private string? _batasTapMasuk;
    private string? _mulaiPulang;
    private string? _notTappedListTime;

    public JadwalHariBarisViewModel(
        int hari,
        string namaHari,
        bool hariSekolah,
        string? jamMasuk,
        string? jamRekap,
        string? batasTapMasuk,
        string? mulaiPulang,
        string? notTappedListTime = null)
    {
        Hari = hari;
        NamaHari = namaHari;
        _hariSekolah = hariSekolah;
        _jamMasuk = jamMasuk;
        _jamRekap = jamRekap;
        _batasTapMasuk = batasTapMasuk;
        _mulaiPulang = mulaiPulang;
        _notTappedListTime = notTappedListTime;
    }

    public int Hari { get; }

    public string NamaHari { get; }

    public bool HariSekolah { get => _hariSekolah; set => SetField(ref _hariSekolah, value); }

    public string? JamMasuk { get => _jamMasuk; set => SetField(ref _jamMasuk, value); }

    // PRD §7 P-06: jam rekap notifikasi orang tua (sekolah rujukan 08.10).
    public string? JamRekap { get => _jamRekap; set => SetField(ref _jamRekap, value); }

    // PRD §7 P-06: jam daftar belum tap untuk wali kelas (sekolah rujukan
    // 08.00), jadwal terpisah dari rekap orang tua.
    public string? NotTappedListTime { get => _notTappedListTime; set => SetField(ref _notTappedListTime, value); }

    public string? BatasTapMasuk { get => _batasTapMasuk; set => SetField(ref _batasTapMasuk, value); }

    public string? MulaiPulang { get => _mulaiPulang; set => SetField(ref _mulaiPulang, value); }
}
