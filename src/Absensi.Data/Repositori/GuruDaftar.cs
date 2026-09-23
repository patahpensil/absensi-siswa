namespace Absensi.Data.Repositori;

// Baris tabel Daftar di layar Guru. TertautTelegram tetap ada (dipakai menu
// Notifikasi untuk pairing guru, PRD §16.15 langkah 7) walau kolom TG-nya
// dihapus dari tabel Data Guru (2026-09-24).
//
// KartuUid, Pangkat, dan Jabatan ditambahkan sebagai parameter TERAKHIR
// dengan default null supaya call site positional lama tetap kompilasi:
//  - KartuUid null = belum punya kartu aktif; panel Guru menampilkan UID
//    sebenarnya (bukan True/False) atau "Belum terdaftar", sama seperti
//    panel Siswa. PunyaKartu dipertahankan untuk CanExecute/dialog kartu.
//  - Pangkat/Jabatan dibutuhkan agar form Ubah menampilkan nilai tersimpan;
//    tanpa itu Simpan menimpa keduanya dengan null.
public sealed record GuruDaftar(
    int Id,
    string Nip,
    string Nama,
    bool Aktif,
    string? AlasanNonaktif,
    bool PunyaKartu,
    bool TertautTelegram,
    string? KartuUid = null,
    string? Pangkat = null,
    string? Jabatan = null);
