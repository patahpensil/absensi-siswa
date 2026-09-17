namespace Absensi.Data.Repositori;

public sealed record GuruDaftar(
    int Id,
    string Nip,
    string Nama,
    bool Aktif,
    string? AlasanNonaktif,
    bool PunyaKartu,
    bool TertautTelegram);
