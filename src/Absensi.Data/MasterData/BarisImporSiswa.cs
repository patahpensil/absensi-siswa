namespace Absensi.Data.MasterData;

// Satu baris berkas impor siswa (docs/PRD.md §16.3 langkah 4), sebelum atau
// sesudah divalidasi. Kesalahan kosong berarti baris ini sah.
// SudahAdaDiDatabase dipakai layar impor untuk pratinjau Baru/Diperbarui
// sebelum operator menekan Impor (docs/PRD.md §14.5).
public sealed record BarisImporSiswa(
    int NomorBaris,
    string? Nisn,
    string? NamaLengkap,
    string? JenisKelamin,
    string? TempatLahir,
    string? TanggalLahir,
    string? Alamat,
    string? Kelas,
    string? NamaOrangTua,
    string? NoHpOrangTua,
    IReadOnlyList<string> Kesalahan,
    bool SudahAdaDiDatabase = false)
{
    public bool Sah => Kesalahan.Count == 0;

    public string TeksKesalahan => string.Join("; ", Kesalahan);
}
