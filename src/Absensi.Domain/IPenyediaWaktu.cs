namespace Absensi.Domain;

// Satu sumber waktu yang bisa diganti saat pengujian (PRD §24.3).
// Kode di luar Absensi.Domain membaca waktu sekarang lewat sini, tidak
// pernah lewat DateTime.Now langsung.
public interface IPenyediaWaktu
{
    DateTime Sekarang { get; }
}
