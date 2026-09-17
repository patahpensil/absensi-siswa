namespace Absensi.Data.Repositori;

// Kolom Master Guru yang diisi lewat impor Excel atau formulir tambah/edit
// (docs/PRD.md §16.15).
public sealed record DataGuru(string Nip, string Nama, string? Pangkat, string? Jabatan);
