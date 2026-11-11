using Absensi.Data.Akun;
using Absensi.Data.Repositori;
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

    public void KunciBulan(SqliteConnection connection, AkunTersimpan akun, DateTime waktu, int tahun, int bulan) =>
        Lock(connection, akun, waktu, new DateOnly(tahun, bulan, 1), verifyLocalAttendance: false);

    public void LockVerifiedLocalMonth(SqliteConnection connection, AkunTersimpan account, DateTime time, DateOnly month) =>
        Lock(connection, account, time, new DateOnly(month.Year, month.Month, 1), verifyLocalAttendance: true);

    private void Lock(SqliteConnection connection, AkunTersimpan account, DateTime time, DateOnly month, bool verifyLocalAttendance)
    {
        AuthorizationGuard.EnsureFullAuthority(account, "mengunci bulan");
        _jalurPerubahanData.Jalankan(connection, account, time, "kunci_bulan", "kunci_bulan", transaction =>
        {
            if (_repositoriKuncimBulan.SudahDikunci(connection, month.Year, month.Month, transaction))
                throw new InvalidOperationException($"Bulan {month:MM/yyyy} sudah terkunci.");

            if (verifyLocalAttendance)
            {
                var today = DateOnly.FromDateTime(time);
                if (month.Year == today.Year && month.Month == today.Month || month > today)
                    throw new InvalidOperationException("Bulan absensi belum berakhir.");

                var pending = new LocalAttendanceClosure().PendingDays(connection, month, transaction);
                if (pending.Count > 0)
                    throw new InvalidOperationException($"Bulan belum bisa dikunci. {pending.Count} hari sekolah belum ditutup.");
            }

            _repositoriKuncimBulan.Kunci(connection, transaction, month.Year, month.Month, account.Id, time);
            return null;
        }, nilaiBaru: $"{month:yyyy-MM}");
    }
}
