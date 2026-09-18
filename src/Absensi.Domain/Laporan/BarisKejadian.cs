namespace Absensi.Domain.Laporan;

// PRD §11.1/§11.5: satu baris per (orang, tanggal) — dipakai Laporan
// Harian (satu tanggal) dan Mingguan (Senin-Sabtu, beberapa tanggal
// sekaligus). Identitas = NISN untuk siswa, NIP untuk guru.
public sealed record BarisKejadian(
    DateOnly Tanggal,
    string Nama,
    string Identitas,
    string? Kelas,
    string StatusHuruf,
    TimeOnly? JamMasuk,
    TimeOnly? JamPulang,
    string? Keterangan);
