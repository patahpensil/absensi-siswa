namespace Absensi.Data.Repositori;

// Konversi tanggal ke nomor hari 1=Senin..7=Minggu dipakai jadwal_hari
// (docs/SKEMA-DATABASE.md §4), berbeda dari DayOfWeek bawaan .NET (Minggu=0).
// Publik (bukan internal) karena Absensi.App.Penjadwal (Tugas 10) juga
// perlu memetakan tanggal ke hari yang persis sama, tanpa menduplikasi
// rumusnya.
public static class HariKalender
{
    public static int DariTanggal(DateOnly tanggal) => ((int)tanggal.DayOfWeek + 6) % 7 + 1;
}
