namespace Absensi.Domain.Laporan;

// PRD §11.2: grid satu baris per siswa, satu kolom per tanggal. Kolom
// dipisah dari baris (bukan di dalam BarisBulanan) karena kolomnya sama
// untuk seluruh baris dalam satu laporan — dipakai bersama untuk kepala
// tabel dan pengarsiran hari libur.
public sealed record KolomTanggalBulanan(DateOnly Tanggal, bool Libur, string? Alasan);

// KodePerTanggal hanya berisi tanggal yang punya baris absensi (hari
// efektif orang itu) — tanggal libur sengaja tidak diberi kode, kolomnya
// diarsir memakai KolomTanggalBulanan.Libur, bukan lewat kode "-" di sel.
public sealed record BarisBulanan(
    string Nama,
    string Identitas,
    string? Kelas,
    IReadOnlyDictionary<DateOnly, string> KodePerTanggal,
    RekapKehadiran Rekap);

public sealed record LaporanBulanan(IReadOnlyList<KolomTanggalBulanan> Kolom, IReadOnlyList<BarisBulanan> Baris);
