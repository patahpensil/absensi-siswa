namespace Absensi.Data.Repositori;

// Baris jadwal_hari untuk satu hari (1=Senin..7=Minggu) pada satu tahun
// ajaran (docs/SKEMA-DATABASE.md §4).
public sealed record JadwalHariKonfigurasi(
    bool HariSekolah,
    TimeOnly? JamMasuk,
    TimeOnly? JamRekap,
    TimeOnly? BatasTapMasuk,
    TimeOnly? MulaiPulang,
    // PRD §7 P-06: daftar belum tap wali kelas, terpisah dari JamRekap
    // (rekap orang tua). Null = tidak dijadwalkan.
    TimeOnly? JamDaftarBelumTap = null);
