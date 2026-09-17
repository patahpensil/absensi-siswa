namespace Absensi.Domain.Notifikasi;

// docs/SKEMA-DATABASE.md §8 (outbox.jenis).
public enum JenisNotifikasi
{
    Hadir,
    Terlambat,
    Alpa,
    Ralat,
    RekapWaliBelumTap,
    BolosWali,
    GuruTanpaKabar,
}
