namespace Absensi.Domain.Laporan;

// PRD §11.4: satu baris per siswa/guru, rentang mengikuti batas
// semester tahun ajaran aktif (bukan batas bulan) — tanpa rincian
// per-kejadian, hanya rekap (beda dari KartuOrang/§11.3).
public sealed record BarisSemester(string Nama, string Identitas, string? Kelas, RekapKehadiran Rekap);
