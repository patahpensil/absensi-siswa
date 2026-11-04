using Absensi.Data.Akun;
using Absensi.Data.Repositori;
using Absensi.Domain.Akun;
using Microsoft.Data.Sqlite;

namespace Absensi.Data.Koreksi;

// RENCANA-TUGAS.md Tugas 11 — Kunci Bulan (PRD §10, §3). "Kewenangan penuh"
// (Kepala Sekolah/Admin) — wali kelas tidak berwenang. Tidak ada jalur
// buka-kunci: PRD tidak menjabarkannya, dan PRD §5.3/§12
// ("tidak ada penghapusan") membuat mengunci sesuatu yang sengaja
// dirancang tidak bisa dibatalkan lewat aplikasi.
public sealed class LayananKuncimBulan
{
    private readonly RepositoriKuncimBulan _repositoriKuncimBulan = new();
    private readonly JalurPerubahanData _jalurPerubahanData = new();

    public bool SudahDikunci(SqliteConnection connection, int tahun, int bulan) =>
        _repositoriKuncimBulan.SudahDikunci(connection, tahun, bulan);

    public void KunciBulan(SqliteConnection connection, AkunTersimpan akun, DateTime waktu, int tahun, int bulan)
    {
        if (akun.Peran == PeranAkun.WaliKelas)
        {
            throw new InvalidOperationException("Wali kelas tidak berwenang mengunci bulan (PRD §10).");
        }

        if (_repositoriKuncimBulan.SudahDikunci(connection, tahun, bulan))
        {
            throw new InvalidOperationException($"Bulan {bulan:00}/{tahun} sudah terkunci.");
        }

        _jalurPerubahanData.Jalankan(
            connection,
            akun,
            waktu,
            aksi: "kunci_bulan",
            entitas: "kunci_bulan",
            perubahan: transaction =>
            {
                _repositoriKuncimBulan.Kunci(connection, transaction, tahun, bulan, akun.Id, waktu);
                return null;
            },
            nilaiBaru: $"{tahun}-{bulan:00}");
    }
}
