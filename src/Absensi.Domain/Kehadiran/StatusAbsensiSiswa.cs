namespace Absensi.Domain.Kehadiran;

// Sesuai CHECK(status) tabel absensi_siswa di docs/SKEMA-DATABASE.md §7.
public enum StatusAbsensiSiswa
{
    Alpa,
    Hadir,
    Terlambat,
    Bolos,
    Izin,
    Sakit,
}
