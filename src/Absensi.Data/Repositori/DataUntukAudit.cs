namespace Absensi.Data.Repositori;

// Keadaan baris SEBELUM diubah, dibaca di dalam transaksi perubahan supaya
// nilai lama di jejak audit (PRD §12) adalah nilai sebenarnya di database,
// bukan tebakan. Hanya dipakai lapisan layanan master.
public sealed record SiswaUntukAudit(DataSiswa Data, bool Aktif, string? AlasanNonaktif, string? FotoPath);

public sealed record GuruUntukAudit(
    string Nip, string Nama, string? Pangkat, string? Jabatan, bool Aktif, string? AlasanNonaktif, string? FotoPath);

public sealed record KelasUntukAudit(string Nama, int Tingkat, int? WaliGuruId);
