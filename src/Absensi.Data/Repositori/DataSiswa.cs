namespace Absensi.Data.Repositori;

// Kolom Master Siswa yang diisi lewat impor Excel atau formulir tambah/edit
// (docs/PRD.md §5.4). NISN adalah kunci upsert saat impor.
public sealed record DataSiswa(
    string Nisn,
    string Nama,
    string JenisKelamin,
    string? TempatLahir,
    string? TanggalLahir,
    string? Alamat,
    string? NamaOrtu,
    string? NoHpOrtu);
