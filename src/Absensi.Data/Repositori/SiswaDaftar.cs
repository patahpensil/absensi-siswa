namespace Absensi.Data.Repositori;

// Baris tabel Daftar di layar Siswa (docs/PRD.md §14.4): penanda cepat
// Kartu dan TG (Telegram) selain identitas dasar. PunyaFoto dipakai
// hitungan "N belum berfoto" di atas tabel.
public sealed record SiswaDaftar(
    int Id,
    string Nisn,
    string Nama,
    string JenisKelamin,
    bool Aktif,
    string? AlasanNonaktif,
    string? KelasNama,
    bool PunyaKartu,
    bool TertautTelegram,
    bool PunyaFoto);
