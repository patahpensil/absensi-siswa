namespace Absensi.Data.Repositori;

// Baris jadwal_hari untuk satu hari (1=Senin..7=Minggu) pada satu tahun
// ajaran (docs/SKEMA-DATABASE.md §4).
public sealed record JadwalHariKonfigurasi(
    bool HariSekolah,
    TimeOnly? JamMasuk,
    TimeOnly? JamRekap,
    TimeOnly? BatasTapMasuk,
    TimeOnly? MulaiPulang);
