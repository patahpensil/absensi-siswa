using Absensi.Domain.Kehadiran;

namespace Absensi.Data.Repositori;

// Baris mentah dari absensi_siswa untuk satu tanggal, dipakai
// Absensi.Data.Laporan.LayananLaporan membangun seluruh jenis laporan
// (RENCANA-TUGAS.md Tugas 12) — bukan DTO tampilan, itu tugas
// Absensi.Domain.Laporan.
public sealed record BarisAbsensiSiswaMentah(
    DateOnly Tanggal,
    int SiswaId,
    string Nama,
    string Nisn,
    int KelasId,
    string KelasNama,
    StatusAbsensiSiswa Status,
    TimeOnly? JamMasuk,
    TimeOnly? JamPulang,
    string? Keterangan);
