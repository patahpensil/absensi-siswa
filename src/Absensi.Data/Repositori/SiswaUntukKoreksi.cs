using Absensi.Domain.Kehadiran;

namespace Absensi.Data.Repositori;

// Satu baris layar Koreksi (PRD §16.10) — satu kelas, satu tanggal.
public sealed record SiswaUntukKoreksi(
    int SiswaId,
    string Nisn,
    string Nama,
    StatusAbsensiSiswa Status,
    TimeOnly? JamMasuk,
    TimeOnly? JamPulang,
    string? Keterangan);
