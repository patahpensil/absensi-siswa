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
    public int Tambah(SqliteConnection connection, SqliteTransaction transaction, string nama, string semester, DateOnly tglMulai, DateOnly tglSelesai)
    {
        using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = """
            INSERT INTO tahun_ajaran (nama, semester, tgl_mulai, tgl_selesai, aktif)
            VALUES ($nama, $semester, $tglMulai, $tglSelesai, 1);
            """;
        command.Parameters.AddWithValue("$nama", nama);
        command.Parameters.AddWithValue("$semester", semester);
        command.Parameters.AddWithValue("$tglMulai", tglMulai.ToString("yyyy-MM-dd"));
        command.Parameters.AddWithValue("$tglSelesai", tglSelesai.ToString("yyyy-MM-dd"));
        command.ExecuteNonQuery();

        using var commandId = connection.CreateCommand();
        commandId.Transaction = transaction;
        commandId.CommandText = "SELECT last_insert_rowid();";
        return Convert.ToInt32(commandId.ExecuteScalar());
    }
}
