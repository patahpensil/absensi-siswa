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

    // PRD §3: kewenangan penuh (kepala sekolah/admin) mencakup pembuatan
    // akun; wali kelas tidak berwenang membuat akun sama sekali.
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
