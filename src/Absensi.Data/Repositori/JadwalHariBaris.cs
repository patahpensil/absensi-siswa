namespace Absensi.Data.Repositori;

// Satu baris `jadwal_hari` (1=Senin..7=Minggu) beserta nomor harinya —
// dipakai layar Pengaturan (PRD §7) untuk membaca dan menulis ketujuh hari
// sekaligus. Beda dari JadwalHariKonfigurasi (dipakai PemrosesTap), yang
// sudah tahu harinya dari parameter kueri dan tidak perlu membawanya balik.
public sealed record JadwalHariBaris(
    int Hari,
    bool HariSekolah,
    TimeOnly? JamMasuk,
    TimeOnly? JamRekap,
    TimeOnly? BatasTapMasuk,
    TimeOnly? MulaiPulang,
    // PRD §7 P-06: daftar belum tap wali kelas (lihat JadwalHariKonfigurasi).
    TimeOnly? NotTappedListTime = null);
