using Microsoft.Data.Sqlite;

namespace Absensi.Data.Repositori;

// Keanggotaan kelas siswa per tahun ajaran (docs/SKEMA-DATABASE.md §5) —
// dipisah dari RepositoriKelas karena ini tentang siswa_kelas (baris
// keanggotaan), bukan kelas itu sendiri.
public sealed class RepositoriSiswaKelas
{
    // UNIQUE(siswa_id, tahun_ajaran_id) menjamin satu kelas per siswa per
    // tahun ajaran (docs/SKEMA-DATABASE.md §10) — perbarui kelas_id bila
    // baris sudah ada (mis. impor ulang dengan KELAS berbeda), tambah baris
    // baru bila belum.
    public void Tetapkan(
        SqliteConnection connection,
        SqliteTransaction transaction,
        int siswaId,
        int kelasId,
        int tahunAjaranId,
        DateOnly? today = null)
    {
        using var commandUpdate = connection.CreateCommand();
        commandUpdate.Transaction = transaction;
        commandUpdate.CommandText = """
            UPDATE siswa_kelas SET kelas_id = $kelasId
            WHERE siswa_id = $siswaId AND tahun_ajaran_id = $tahunAjaranId;
            """;
        commandUpdate.Parameters.AddWithValue("$siswaId", siswaId);
        commandUpdate.Parameters.AddWithValue("$kelasId", kelasId);
        commandUpdate.Parameters.AddWithValue("$tahunAjaranId", tahunAjaranId);

        if (commandUpdate.ExecuteNonQuery() > 0)
        {
            return;
        }

        using var commandInsert = connection.CreateCommand();
        commandInsert.Transaction = transaction;
        commandInsert.CommandText = """
            INSERT INTO siswa_kelas (siswa_id, kelas_id, tahun_ajaran_id, start_date)
            VALUES ($siswaId, $kelasId, $tahunAjaranId,
                -- P-18: masuk di tengah periode bila periode ini sudah
                -- berjalan (sudah ada hari absensi yang dibuka sebelum hari ini).
                CASE WHEN $today IS NOT NULL AND EXISTS (
                    SELECT 1 FROM opened_school_day WHERE academic_period_id = $tahunAjaranId AND date < $today)
                THEN $today END);
            """;
        commandInsert.Parameters.AddWithValue("$today", today is { } d ? d.ToString("yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture) : DBNull.Value);
        commandInsert.Parameters.AddWithValue("$siswaId", siswaId);
        commandInsert.Parameters.AddWithValue("$kelasId", kelasId);
        commandInsert.Parameters.AddWithValue("$tahunAjaranId", tahunAjaranId);
        commandInsert.ExecuteNonQuery();
    }

    // Nama kelas siswa pada tahun ajaran itu (null bila belum punya kelas) —
    // nilai lama/baru "kelas" di jejak audit perubahan siswa.
    public string? CariNamaKelas(SqliteConnection connection, SqliteTransaction? transaction, int siswaId, int tahunAjaranId)
    {
        using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = "SELECT k.nama FROM siswa_kelas sk JOIN kelas k ON k.id = sk.kelas_id WHERE sk.siswa_id = $siswaId AND sk.tahun_ajaran_id = $tahunAjaranId;";
        command.Parameters.AddWithValue("$siswaId", siswaId);
        command.Parameters.AddWithValue("$tahunAjaranId", tahunAjaranId);
        return command.ExecuteScalar() as string;
    }

    // P-18: hari terakhir bersekolah pada keanggotaan periode itu.
    // Mengembalikan nilai lama (teks yyyy-MM-dd atau null) dan apakah ada
    // keanggotaan yang diubah — untuk jejak audit.
    public (bool Changed, string? OldValue) SetLastSchoolDay(SqliteConnection connection, SqliteTransaction transaction, int siswaId, int tahunAjaranId, DateOnly? lastDay)
    {
        string? oldValue;
        using (var read = connection.CreateCommand())
        {
            read.Transaction = transaction;
            read.CommandText = "SELECT end_date FROM siswa_kelas WHERE siswa_id = $siswaId AND tahun_ajaran_id = $period;";
            read.Parameters.AddWithValue("$siswaId", siswaId);
            read.Parameters.AddWithValue("$period", tahunAjaranId);
            using var reader = read.ExecuteReader();
            if (!reader.Read())
            {
                return (false, null);
            }

            oldValue = reader.IsDBNull(0) ? null : reader.GetString(0);
        }

        using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = "UPDATE siswa_kelas SET end_date = $end WHERE siswa_id = $siswaId AND tahun_ajaran_id = $period;";
        command.Parameters.AddWithValue("$end", lastDay is { } d ? d.ToString("yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture) : DBNull.Value);
        command.Parameters.AddWithValue("$siswaId", siswaId);
        command.Parameters.AddWithValue("$period", tahunAjaranId);
        return (command.ExecuteNonQuery() > 0, oldValue);
    }

    // P-18: cakupan tanggal per siswa pada satu periode; siswa tanpa tanggal
    // khusus tidak dicantumkan (mengikuti kalender periode).
    public IReadOnlyDictionary<int, (DateOnly? Start, DateOnly? End)> ListEnrollmentDates(SqliteConnection connection, int tahunAjaranId)
    {
        using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT siswa_id, start_date, end_date FROM siswa_kelas
            WHERE tahun_ajaran_id = $period AND (start_date IS NOT NULL OR end_date IS NOT NULL);
            """;
        command.Parameters.AddWithValue("$period", tahunAjaranId);
        using var reader = command.ExecuteReader();
        var result = new Dictionary<int, (DateOnly?, DateOnly?)>();
        while (reader.Read())
        {
            result[reader.GetInt32(0)] = (
                reader.IsDBNull(1) ? null : DateOnly.ParseExact(reader.GetString(1), "yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture),
                reader.IsDBNull(2) ? null : DateOnly.ParseExact(reader.GetString(2), "yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture));
        }

        return result;
    }
}
