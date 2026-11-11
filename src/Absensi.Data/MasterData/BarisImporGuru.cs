namespace Absensi.Data.MasterData;

// docs/PRD.md §16.15 langkah 2: kolom NAMA, NIP, PANGKAT, JABATAN, NO_KARTU.
// SudahAdaDiDatabase dipakai layar impor untuk pratinjau Baru/Diperbarui
// (docs/PRD.md §14.5).
public sealed record BarisImporGuru(
    int NomorBaris,
    string? Nip,
    string? Nama,
    string? Pangkat,
    string? Jabatan,
    IReadOnlyList<string> Kesalahan,
    bool SudahAdaDiDatabase = false,
    string? NoKartu = null)
{
    public bool Sah => Kesalahan.Count == 0;

    public string TeksKesalahan => string.Join("; ", Kesalahan);
}
