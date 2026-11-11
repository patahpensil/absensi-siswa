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
}

public sealed record AcademicPeriod(int Id, string Name, string Semester, DateOnly Start, DateOnly End, bool Active);
