namespace Absensi.Data.MasterData;

// docs/PRD.md §16.15 langkah 2: kolom NAMA, NIP, PANGKAT, JABATAN, NO_KARTU.
// NO_KARTU sengaja tidak diproses di sini — registrasi kartu adalah lingkup
// Tugas 8 (Registrasi Kartu), termasuk aturan pemeriksaan duplikat lintas
// master siswa dan guru (§16.15 langkah 4.1) yang belum ada infrastrukturnya.
// SudahAdaDiDatabase dipakai layar impor untuk pratinjau Baru/Diperbarui
// (docs/PRD.md §14.5).
public sealed record BarisImporGuru(
    int NomorBaris,
    string? Nip,
    string? Nama,
    string? Pangkat,
    string? Jabatan,
    IReadOnlyList<string> Kesalahan,
    bool SudahAdaDiDatabase = false)
{
    public bool Sah => Kesalahan.Count == 0;

    public string TeksKesalahan => string.Join("; ", Kesalahan);
}
