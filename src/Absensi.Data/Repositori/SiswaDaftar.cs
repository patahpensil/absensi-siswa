namespace Absensi.Data.Repositori;

// Baris tabel Daftar di layar Siswa (docs/PRD.md §14.4): penanda cepat
// Kartu dan TG (Telegram) selain identitas dasar. PunyaFoto dipakai
// hitungan "N belum berfoto" di atas tabel.
//
// UAT Bug #11 (2026-09-23, redesain fundamental Registrasi Kartu Siswa):
// KartuUid ditambahkan sebagai parameter TERAKHIR dengan default null
// supaya call site lama (positional, tanpa argumen ini) tetap kompilasi.
// Panel Siswa sekarang menampilkan UID sebenarnya (bukan True/False) —
// null berarti belum punya kartu aktif sama sekali, dibedakan dari
// PunyaKartu=false (dipertahankan untuk compat CanExecute GantiKartuCommand
// dkk. yang sudah ada, bukan diganti supaya blast radius perubahan kecil).
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
    bool PunyaFoto,
    string? KartuUid = null);
