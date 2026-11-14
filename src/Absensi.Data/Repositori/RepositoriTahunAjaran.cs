using Microsoft.Data.Sqlite;

namespace Absensi.Data.Repositori;

public sealed class RepositoriTahunAjaran
{
    public IReadOnlyList<(int Id, string Nama, bool Aktif)> DaftarUntukImpor(SqliteConnection connection)
    {
        using var command = connection.CreateCommand();
        command.CommandText = "SELECT id, nama, aktif FROM tahun_ajaran ORDER BY aktif DESC, tgl_mulai DESC, id DESC;";
        using var reader = command.ExecuteReader();
        var hasil = new List<(int Id, string Nama, bool Aktif)>();
        while (reader.Read())
        {
            hasil.Add((reader.GetInt32(0), reader.GetString(1), reader.GetBoolean(2)));
        }

        return hasil;
    }

    public IReadOnlyList<AcademicPeriod> ListPeriods(SqliteConnection connection, SqliteTransaction? transaction = null)
    {
        using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = """
            SELECT id, nama, semester, tgl_mulai, tgl_selesai, aktif FROM tahun_ajaran ORDER BY tgl_mulai DESC, id DESC;
            """;
        using var reader = command.ExecuteReader();
        var periods = new List<AcademicPeriod>();
        while (reader.Read())
        {
            periods.Add(new AcademicPeriod(reader.GetInt32(0), reader.GetString(1), reader.GetString(2),
                DateOnly.ParseExact(reader.GetString(3), "yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture),
                DateOnly.ParseExact(reader.GetString(4), "yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture),
                reader.GetBoolean(5)));
        }

        return periods;
    }

    public int? FindForDate(SqliteConnection connection, DateOnly date, SqliteTransaction? transaction = null)
    {
        using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = """
            SELECT id FROM tahun_ajaran WHERE $date BETWEEN tgl_mulai AND tgl_selesai
            ORDER BY aktif DESC, tgl_mulai DESC, id DESC LIMIT 1;
            """;
        command.Parameters.AddWithValue("$date", date.ToString("yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture));
        return command.ExecuteScalar() is long id ? checked((int)id) : null;
    }

    internal void Activate(SqliteConnection connection, SqliteTransaction transaction, int id)
    {
        using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = """
            UPDATE tahun_ajaran SET aktif = CASE WHEN id = $id THEN 1 ELSE 0 END WHERE aktif = 1 OR id = $id;
            """;
        command.Parameters.AddWithValue("$id", id);
        command.ExecuteNonQuery();
    }

    public int? CariIdAktif(SqliteConnection connection)
    {
        using var command = connection.CreateCommand();
        command.CommandText = "SELECT id FROM tahun_ajaran WHERE aktif = 1 LIMIT 1;";
        var hasil = command.ExecuteScalar();
        return hasil is null ? null : Convert.ToInt32(hasil);
    }

    // Dipakai header jendela utama (PRD §14.2): nama tahun ajaran aktif
    // tampil sebagai pengaman supaya admin sadar bila masih memakai tahun
    // ajaran lama.
    public string? CariNamaAktif(SqliteConnection connection)
    {
        using var command = connection.CreateCommand();
        command.CommandText = "SELECT nama FROM tahun_ajaran WHERE aktif = 1 LIMIT 1;";
        return command.ExecuteScalar() as string;
    }

    // Dipakai LayananJadwalHari untuk memeriksa kunci bulan di seluruh
    // rentang tahun ajaran (jadwal_hari berdampak retroaktif ke semua
    // bulannya, bukan cuma bulan yang sedang dilihat admin).
    public (DateOnly TglMulai, DateOnly TglSelesai)? CariRentang(SqliteConnection connection, int tahunAjaranId)
    {
        using var command = connection.CreateCommand();
        command.CommandText = "SELECT tgl_mulai, tgl_selesai FROM tahun_ajaran WHERE id = $id;";
        command.Parameters.AddWithValue("$id", tahunAjaranId);

        using var reader = command.ExecuteReader();
        if (!reader.Read())
        {
            return null;
        }

        return (DateOnly.Parse(reader.GetString(0)), DateOnly.Parse(reader.GetString(1)));
    }

    // Hanya dipanggil dari Absensi.Data.Wizard.LayananWizard (langkah 3
    // wizard, PRD §16.1). Belum ada layar "Kenaikan Tahun Ajaran" (PRD
    // §16.14) di versi ini, jadi ini satu-satunya jalur baris tahun_ajaran
    // pertama dibuat — ditandai aktif langsung karena wizard hanya berjalan
    // saat belum ada tahun ajaran apa pun.
    public int Tambah(SqliteConnection connection, SqliteTransaction transaction, string nama, string semester, DateOnly tglMulai, DateOnly tglSelesai, bool aktif = true)
    {
        using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = """
            INSERT INTO tahun_ajaran (nama, semester, tgl_mulai, tgl_selesai, aktif)
            VALUES ($nama, $semester, $tglMulai, $tglSelesai, $aktif);
            """;
        command.Parameters.AddWithValue("$nama", nama);
        command.Parameters.AddWithValue("$semester", semester);
        command.Parameters.AddWithValue("$tglMulai", tglMulai.ToString("yyyy-MM-dd"));
        command.Parameters.AddWithValue("$tglSelesai", tglSelesai.ToString("yyyy-MM-dd"));
        command.Parameters.AddWithValue("$aktif", aktif ? 1 : 0);
        command.ExecuteNonQuery();

        using var commandId = connection.CreateCommand();
        commandId.Transaction = transaction;
        commandId.CommandText = "SELECT last_insert_rowid();";
        return Convert.ToInt32(commandId.ExecuteScalar());
    }

    internal void UpdateRange(SqliteConnection connection, SqliteTransaction transaction, int id, DateOnly start, DateOnly end)
    {
        using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = "UPDATE tahun_ajaran SET tgl_mulai = $start, tgl_selesai = $end WHERE id = $id;";
        command.Parameters.AddWithValue("$id", id);
        command.Parameters.AddWithValue("$start", start.ToString("yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture));
        command.Parameters.AddWithValue("$end", end.ToString("yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture));
        if (command.ExecuteNonQuery() != 1)
            throw new InvalidOperationException("Tahun ajaran tidak ditemukan.");
    }

    // Earliest day of the period with attendance already recorded (an opened
    // school day or a student attendance row) that lies outside [start, end].
    internal DateOnly? FindRecordedDayOutside(SqliteConnection connection, SqliteTransaction transaction, int id, DateOnly start, DateOnly end)
    {
        using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = """
            SELECT MIN(day) FROM (
                SELECT date AS day FROM opened_school_day
                WHERE academic_period_id = $id AND (date < $start OR date > $end)
                UNION ALL
                SELECT a.tanggal FROM absensi_siswa a JOIN kelas k ON k.id = a.kelas_id
                WHERE k.tahun_ajaran_id = $id AND (a.tanggal < $start OR a.tanggal > $end)
            );
            """;
        command.Parameters.AddWithValue("$id", id);
        command.Parameters.AddWithValue("$start", start.ToString("yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture));
        command.Parameters.AddWithValue("$end", end.ToString("yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture));
        return command.ExecuteScalar() is string day
            ? DateOnly.ParseExact(day, "yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture)
            : null;
    }
}

public sealed record AcademicPeriod(int Id, string Name, string Semester, DateOnly Start, DateOnly End, bool Active);
