using Absensi.Domain.Kehadiran;

namespace Absensi.Data.Repositori;

// Setara BarisAbsensiSiswaMentah untuk guru (RENCANA-TUGAS.md Tugas 12,
// PRD §17.8) — tanpa kelas, guru tidak dikelompokkan per kelas.
public sealed record BarisAbsensiGuruMentah(
    DateOnly Tanggal,
    int GuruId,
    string Nama,
    string Nip,
    StatusAbsensiGuru Status,
    TimeOnly? JamMasuk,
    TimeOnly? JamPulang,
    string? Keterangan);
