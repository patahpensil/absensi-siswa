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
            throw new InvalidOperationException("Admin tidak berwenang membuat akun Admin lain — hanya Kepala Sekolah yang bisa.");
        }

        // PRD §9 (daftar layar Kelas): wali kelas ditentukan dari
        // `akun.guru_id` yang cocok dengan `kelas.wali_guru_id" — tanpa
        // guru_id, akun Wali Kelas tidak bisa menyentuh kelas mana pun.
        if (peran == PeranAkun.WaliKelas && guruId is null)
        {
            throw new InvalidOperationException("Akun Wali Kelas wajib ditautkan ke seorang guru.");
        }

        username = (username ?? string.Empty).Trim();
        PastikanUsernameValid(username);
        PastikanSandiValid(sandi);
        if (_repositoriAkun.CariBerdasarkanUsername(connection, username) is not null)
        {
            throw new InvalidOperationException($"Username \"{username}\" sudah dipakai. Pilih username lain.");
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
    // PRD §24.3) — dicek di sini, bukan cuma disembunyikan UI.
    public IReadOnlyList<AkunDaftar> DaftarAkun(SqliteConnection connection, AkunTersimpan akun)
    {
        PastikanBolehMengelolaAkun(akun);
        return _repositoriAkun.DaftarSemua(connection);
    }

    // Bukan penghapusan (PRD §5.3/§12) — akun keluar/dimutasi
    // ditandai tidak aktif, sama seperti siswa/guru/kartu.
    public void NonaktifkanAkun(SqliteConnection connection, AkunTersimpan pembuat, DateTime waktu, int akunIdTarget)
    {
        PastikanBolehMengelolaAkun(pembuat);
        PastikanBerwenangAtasTarget(connection, pembuat, akunIdTarget);

        if (pembuat.Id == akunIdTarget)
        {
            throw new InvalidOperationException("Akun yang sedang dipakai tidak bisa menonaktifkan dirinya sendiri.");
        }

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
        PastikanBerwenangAtasTarget(connection, pembuat, akunIdTarget);
        PastikanSandiValid(sandiBaru);

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

    // PRD §3.1: Kepala Sekolah → akun Admin (dan Wali Kelas); Admin → akun
    // Wali Kelas SAJA. Wewenang mereset/menonaktifkan mengikuti wewenang
    // membuat, jadi Admin tidak boleh menyentuh akun Kepala Sekolah, Admin
    // lain, maupun dirinya sendiri lewat jalur ini.
    private void PastikanBerwenangAtasTarget(SqliteConnection connection, AkunTersimpan pembuat, int akunIdTarget)
    {
        var target = _repositoriAkun.CariBerdasarkanId(connection, akunIdTarget)
            ?? throw new InvalidOperationException("Akun yang dituju tidak ditemukan.");

        if (pembuat.Peran == PeranAkun.Admin && target.Peran != PeranAkun.WaliKelas)
        {
            throw new InvalidOperationException("Admin hanya berwenang atas akun Wali Kelas.");
        }
    }

    // Aturan minimum yang sama untuk aplikasi desktop dan web uji. Desain
    // autentikasi online final (MFA, ASVS L3) tetap mengikuti PRD §22.
    internal static void PastikanUsernameValid(string username)
    {
        if (!System.Text.RegularExpressions.Regex.IsMatch(username, "^[a-z0-9._]{3,32}$"))
        {
            throw new InvalidOperationException("Username 3–32 karakter: huruf kecil, angka, titik atau garis bawah.");
        }
    }

    internal static void PastikanSandiValid(string? sandi)
    {
        if (sandi is null || sandi.Length < 8)
        {
            throw new InvalidOperationException("Sandi minimal 8 karakter.");
        }
    }

    private static void PastikanBolehMengelolaAkun(AkunTersimpan akun)
    {
        if (akun.Peran is not (PeranAkun.KepalaSekolah or PeranAkun.Admin))
        {
            throw new InvalidOperationException("Wali kelas tidak berwenang mengelola akun.");
        }
    }

    // PRD §3: dipakai pemanggil (mis. layar koreksi di
    // Tugas 11) sebelum mengizinkan perubahan pada kelas tertentu.
    public bool BolehMenyentuhKelas(SqliteConnection connection, AkunTersimpan akun, int kelasId, SqliteTransaction? transaction = null)
    {
        if (akun.Peran is PeranAkun.KepalaSekolah or PeranAkun.Admin)
        {
            return true;
        }

        var waliGuruId = _repositoriKelas.CariWaliGuruId(connection, kelasId, transaction);
        return waliGuruId is { } id && _penentuAksesKelas.BolehMenyentuh(akun.Peran, akun.GuruId, id);
    }
}
