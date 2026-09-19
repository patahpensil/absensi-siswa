using Absensi.Data.Repositori;
using Absensi.Domain.Akun;
using Microsoft.Data.Sqlite;

namespace Absensi.Data.Akun;

// "Lapisan layanan" tempat autentikasi dan pemeriksaan peran berada
// (RENCANA-TUGAS.md Tugas 4) — bukan di UI. Pemanggil (nanti ViewModel di
// Absensi.App) tidak pernah memeriksa peran sendiri; selalu lewat sini.
public sealed class LayananAkun
{
    private readonly RepositoriAkun _repositoriAkun = new();
    private readonly RepositoriKelas _repositoriKelas = new();
    private readonly PenentuAksesKelas _penentuAksesKelas = new();
    private readonly JalurPerubahanData _jalurPerubahanData = new();

    public AkunTersimpan? Autentikasi(SqliteConnection connection, string username, string sandi)
    {
        var akun = _repositoriAkun.CariBerdasarkanUsername(connection, username);
        if (akun is null || !akun.Aktif)
        {
            return null;
        }

        return HashSandi.Verifikasi(sandi, akun.SandiHash) ? akun : null;
    }

    // PRD §3/§3.1: kewenangan penuh (kepala sekolah/admin) mencakup
    // pembuatan akun; wali kelas tidak berwenang membuat akun sama sekali.
    // Hierarki §3.1 lebih spesifik dari itu: "Kepala Sekolah → akun Admin",
    // "Admin → akun Wali Kelas" — admin TIDAK boleh membuat admin lain,
    // hanya kepala sekolah yang bisa. Akun Kepala Sekolah sendiri hanya
    // dibuat sekali lewat wizard instalasi (Absensi.Data.Wizard.LayananWizard),
    // tidak pernah lewat jalur ini.
    public AkunTersimpan BuatAkun(
        SqliteConnection connection,
        AkunTersimpan pembuat,
        DateTime waktu,
        string username,
        string sandi,
        PeranAkun peran,
        int? guruId)
    {
        if (pembuat.Peran is not (PeranAkun.KepalaSekolah or PeranAkun.Admin))
        {
            throw new InvalidOperationException("Hanya kepala sekolah atau admin yang bisa membuat akun.");
        }

        if (peran == PeranAkun.KepalaSekolah)
        {
            throw new InvalidOperationException("Akun Kepala Sekolah hanya dibuat lewat wizard instalasi pertama.");
        }

        if (pembuat.Peran == PeranAkun.Admin && peran == PeranAkun.Admin)
        {
            throw new InvalidOperationException("Admin tidak berwenang membuat akun Admin lain — hanya Kepala Sekolah yang bisa (PRD §3.1).");
        }

        // PRD §9 (daftar layar Kelas): wali kelas ditentukan dari
        // `akun.guru_id` yang cocok dengan `kelas.wali_guru_id" — tanpa
        // guru_id, akun Wali Kelas tidak bisa menyentuh kelas mana pun.
        if (peran == PeranAkun.WaliKelas && guruId is null)
        {
            throw new InvalidOperationException("Akun Wali Kelas wajib ditautkan ke seorang guru.");
        }

        var sandiHash = HashSandi.Buat(sandi);
        int? idBaru = null;

        _jalurPerubahanData.Jalankan(
            connection,
            pembuat,
            waktu,
            aksi: "buat_akun",
            entitas: "akun",
            perubahan: transaction =>
            {
                idBaru = _repositoriAkun.Tambah(connection, transaction, username, sandiHash, peran, guruId, pembuat.Id, waktu);
                return idBaru;
            },
            nilaiBaru: username);

        return _repositoriAkun.CariBerdasarkanId(connection, idBaru!.Value)!;
    }

    // RENCANA-TUGAS.md Tugas 14 — layar Manajemen Akun. Wali kelas "tidak
    // bisa akses menu ini sama sekali" (bukan sekadar tombol disembunyikan,
    // CLAUDE.md §4.5 aturan 19) — dicek di sini, bukan cuma disembunyikan UI.
    public IReadOnlyList<AkunDaftar> DaftarAkun(SqliteConnection connection, AkunTersimpan akun)
    {
        PastikanBolehMengelolaAkun(akun);
        return _repositoriAkun.DaftarSemua(connection);
    }

    // Bukan penghapusan (CLAUDE.md §4.2 aturan 8) — akun keluar/dimutasi
    // ditandai tidak aktif, sama seperti siswa/guru/kartu.
    public void NonaktifkanAkun(SqliteConnection connection, AkunTersimpan pembuat, DateTime waktu, int akunIdTarget)
    {
        PastikanBolehMengelolaAkun(pembuat);

        _jalurPerubahanData.Jalankan(
            connection,
            pembuat,
            waktu,
            aksi: "nonaktifkan_akun",
            entitas: "akun",
            perubahan: transaction =>
            {
                _repositoriAkun.Nonaktifkan(connection, transaction, akunIdTarget);
                return akunIdTarget;
            });
    }

    // Sandi baru diisi langsung oleh Kepala Sekolah/Admin — tidak pernah
    // menampilkan sandi lama (memang tidak tersimpan sebagai teks biasa,
    // hanya hash-nya — Absensi.Data.Akun.HashSandi).
    public void ResetSandi(SqliteConnection connection, AkunTersimpan pembuat, DateTime waktu, int akunIdTarget, string sandiBaru)
    {
        PastikanBolehMengelolaAkun(pembuat);

        var sandiHash = HashSandi.Buat(sandiBaru);

        _jalurPerubahanData.Jalankan(
            connection,
            pembuat,
            waktu,
            aksi: "reset_sandi_akun",
            entitas: "akun",
            perubahan: transaction =>
            {
                _repositoriAkun.PerbaruiSandi(connection, transaction, akunIdTarget, sandiHash);
                return akunIdTarget;
            });
    }

    private static void PastikanBolehMengelolaAkun(AkunTersimpan akun)
    {
        if (akun.Peran is not (PeranAkun.KepalaSekolah or PeranAkun.Admin))
        {
            throw new InvalidOperationException("Wali kelas tidak berwenang mengelola akun (PRD §3).");
        }
    }

    // CLAUDE.md §4.5 aturan 20: dipakai pemanggil (mis. layar koreksi di
    // Tugas 11) sebelum mengizinkan perubahan pada kelas tertentu.
    public bool BolehMenyentuhKelas(SqliteConnection connection, AkunTersimpan akun, int kelasId)
    {
        if (akun.Peran is PeranAkun.KepalaSekolah or PeranAkun.Admin)
        {
            return true;
        }

        var waliGuruId = _repositoriKelas.CariWaliGuruId(connection, kelasId);
        return waliGuruId is { } id && _penentuAksesKelas.BolehMenyentuh(akun.Peran, akun.GuruId, id);
    }
}
