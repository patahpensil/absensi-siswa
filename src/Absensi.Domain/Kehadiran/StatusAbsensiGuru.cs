namespace Absensi.Domain.Kehadiran;

// Sesuai CHECK(status) tabel absensi_guru di docs/SKEMA-DATABASE.md §7.
public enum StatusAbsensiGuru
{
    Alpa,
    Masuk,
    Hadir,
    TidakLengkap,
    Izin,
    Sakit,
    DinasLuar,
    Cuti,
}
