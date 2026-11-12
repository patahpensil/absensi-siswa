using System.Globalization;
using Absensi.Data.Akun;
using Absensi.Data.Repositori;
using Absensi.Domain.Kehadiran;
using Microsoft.Data.Sqlite;

namespace Absensi.Data.Koreksi;

// Completeness evidence for the single-database test environment.
public sealed class LocalAttendanceClosure
{
    private readonly RepositoriTahunAjaran _years = new();
    private readonly RepositoriKalender _calendar = new();
    private readonly RepositoriKuncimBulan _locks = new();

    public bool IsOpened(SqliteConnection connection, DateOnly date) =>
        ReadDay(connection, null, date) is not null;

    public bool IsClosed(SqliteConnection connection, DateOnly date, SqliteTransaction? transaction = null) =>
        ReadDay(connection, transaction, date)?.Closed == true;

    public void FinalizeDay(SqliteConnection connection, AkunTersimpan account, DateTime time, DateOnly date)
    {
        AuthorizationGuard.EnsureFullAuthority(account, "menutup hari absensi");
        if (date >= DateOnly.FromDateTime(time)) throw new InvalidOperationException("Hari absensi belum berakhir.");

        new JalurPerubahanData().JalankanDenganCatatan(connection, account, time, "tutup_hari", "absensi", transaction =>
        {
            if (_locks.SudahDikunci(connection, date.Year, date.Month, transaction))
                throw new InvalidOperationException("Bulan absensi sudah dikunci.");
            var day = ReadDay(connection, transaction, date);
            var period = _years.FindForDate(connection, date, transaction);
            if (day is null || day.Value.Period != period)
                throw new InvalidOperationException("Pembukaan hari ini tidak tercatat. Periksa data absensi.");
            if (_calendar.TentukanJenisHari(connection, date, day.Value.Period, transaction) != JenisHariKalender.Sekolah)
                throw new InvalidOperationException("Tanggal ini tercatat sebagai libur.");
            if (day.Value.Closed)
                throw new InvalidOperationException("Hari ini sudah ditutup.");

            var (students, teachers) = ApplyEndOfDayRules(connection, transaction, date);
            using var command = connection.CreateCommand();
            command.Transaction = transaction;
            command.Parameters.AddWithValue("$date", DateText(date));
            command.Parameters.AddWithValue("$time", time.ToString("o"));
            command.CommandText = "UPDATE opened_school_day SET closed_at = $time WHERE date = $date;";
            command.ExecuteNonQuery();
            return new CatatanPerubahan(null, null, $"{date:yyyy-MM-dd} · {students} Bolos · {teachers} Tidak lengkap");
        });
    }

    // T3 (keputusan pemilik 12 November 2026): bila PC mati pada jam
    // finalisasi, aturan akhir hari (Bolos siswa, Tidak lengkap guru) tidak
    // pernah dijalankan. Saat aplikasi dibuka, setiap hari sekolah lampau yang
    // sudah dibuka tetapi belum ditutup diberi aturan yang sama. Tidak
    // mengirim notifikasi (pesan terlambat tidak lagi berguna), tidak menutup
    // hari (penutupan tetap tindakan admin), dan melewati bulan terkunci
    // serta tanggal yang kemudian ditetapkan libur. Idempoten.
    public int FinalizeMissedDays(SqliteConnection connection, DateOnly today)
    {
        var dates = new List<DateOnly>();
        using (var command = connection.CreateCommand())
        {
            command.CommandText = "SELECT date FROM opened_school_day WHERE date < $today AND closed_at IS NULL ORDER BY date;";
            command.Parameters.AddWithValue("$today", DateText(today));
            using var reader = command.ExecuteReader();
            while (reader.Read())
                dates.Add(DateOnly.ParseExact(reader.GetString(0), "yyyy-MM-dd", CultureInfo.InvariantCulture));
        }

        var changed = 0;
        foreach (var date in dates)
        {
            using var transaction = connection.BeginTransaction(deferred: false);
            var day = ReadDay(connection, transaction, date);
            if (day is not { Closed: false } open ||
                _locks.SudahDikunci(connection, date.Year, date.Month, transaction) ||
                _calendar.TentukanJenisHari(connection, date, open.Period, transaction) != JenisHariKalender.Sekolah)
                continue;

            var (students, teachers) = ApplyEndOfDayRules(connection, transaction, date);
            transaction.Commit();
            changed += students + teachers;
        }

        return changed;
    }

    // Aturan akhir hari yang sama dengan Penjadwal: masuk tanpa pulang →
    // Bolos (siswa, kecuali hasil koreksi) / Tidak lengkap (guru).
    private static (int Students, int Teachers) ApplyEndOfDayRules(SqliteConnection connection, SqliteTransaction transaction, DateOnly date)
    {
        using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.Parameters.AddWithValue("$date", DateText(date));
        command.CommandText = """
            UPDATE absensi_siswa SET status = 'bolos'
            WHERE tanggal = $date AND status IN ('hadir','terlambat') AND sumber <> 'koreksi'
              AND jam_masuk IS NOT NULL AND jam_pulang IS NULL;
            """;
        var students = command.ExecuteNonQuery();
        command.CommandText = """
            UPDATE absensi_guru SET status = 'tidak_lengkap'
            WHERE tanggal = $date AND status = 'masuk' AND jam_pulang IS NULL;
            """;
        return (students, command.ExecuteNonQuery());
    }

    public IReadOnlyList<DateOnly> PendingDays(SqliteConnection connection, DateOnly month, SqliteTransaction? transaction = null)
    {
        var start = new DateOnly(month.Year, month.Month, 1);
        var pending = new List<DateOnly>();
        var schoolDays = 0;
        for (var index = 0; index < DateTime.DaysInMonth(start.Year, start.Month); index++)
        {
            var date = start.AddDays(index);
            if (_years.FindForDate(connection, date, transaction) is not { } year ||
                _calendar.TentukanJenisHari(connection, date, year, transaction) != JenisHariKalender.Sekolah)
                continue;

            schoolDays++;
            var day = ReadDay(connection, transaction, date);
            if (day is null || !day.Value.Closed || day.Value.Period != year)
                pending.Add(date);
        }

        if (schoolDays == 0) throw new InvalidOperationException("Tidak ada hari sekolah pada periode bulan ini.");
        return pending;
    }

    internal static void InvalidateDay(SqliteConnection connection, SqliteTransaction transaction, DateOnly date)
    {
        using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = "UPDATE opened_school_day SET closed_at = NULL WHERE date = $date;";
        command.Parameters.AddWithValue("$date", DateText(date));
        command.ExecuteNonQuery();
    }

    private static (int Period, bool Closed)? ReadDay(SqliteConnection connection, SqliteTransaction? transaction, DateOnly date)
    {
        using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = "SELECT academic_period_id, closed_at FROM opened_school_day WHERE date = $date;";
        command.Parameters.AddWithValue("$date", DateText(date));
        using var reader = command.ExecuteReader();
        return reader.Read() ? (reader.GetInt32(0), !reader.IsDBNull(1)) : null;
    }

    private static string DateText(DateOnly date) => date.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
}
