using Absensi.Domain.Akun;
using Microsoft.Data.Sqlite;

namespace Absensi.Data.Repositori;

public sealed class RepositoriAkun
{
    public AkunTersimpan? CariBerdasarkanUsername(SqliteConnection connection, string username)
    {
        using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT id, username, sandi_hash, peran, guru_id, aktif
            FROM akun WHERE username = $username;
            """;
        command.Parameters.AddWithValue("$username", username);

        using var reader = command.ExecuteReader();
        return reader.Read() ? Petakan(reader) : null;
    }

    public AkunTersimpan? CariBerdasarkanId(SqliteConnection connection, int id)
    {
        using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT id, username, sandi_hash, peran, guru_id, aktif
            FROM akun WHERE id = $id;
            """;
        command.Parameters.AddWithValue("$id", id);

        using var reader = command.ExecuteReader();
        return reader.Read() ? Petakan(reader) : null;
    }

    // Hanya dipanggil lewat Absensi.Data.Akun.LayananAkun.BuatAkun, yang
    // membungkusnya dengan JalurPerubahanData supaya pembuatan akun selalu
    // tercatat di audit (CLAUDE.md §4.1 aturan 4). Jangan dipanggil langsung
    // dari tempat lain.
    public int Tambah(
        SqliteConnection connection,
        SqliteTransaction transaction,
        string username,
        string sandiHash,
        PeranAkun peran,
        int? guruId,
        int? dibuatOleh,
        DateTime waktu)
    {
        using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = """
            INSERT INTO akun (username, sandi_hash, peran, guru_id, aktif, dibuat_oleh, dibuat_pada)
            VALUES ($username, $sandiHash, $peran, $guruId, 1, $dibuatOleh, $waktu);
            """;
        command.Parameters.AddWithValue("$username", username);
        command.Parameters.AddWithValue("$sandiHash", sandiHash);
        command.Parameters.AddWithValue("$peran", PemetaanEnum.TeksDariPeran(peran));
        command.Parameters.AddWithValue("$guruId", (object?)guruId ?? DBNull.Value);
        command.Parameters.AddWithValue("$dibuatOleh", (object?)dibuatOleh ?? DBNull.Value);
        command.Parameters.AddWithValue("$waktu", waktu.ToString("o"));
        command.ExecuteNonQuery();

        using var commandId = connection.CreateCommand();
        commandId.Transaction = transaction;
        commandId.CommandText = "SELECT last_insert_rowid();";
        return Convert.ToInt32(commandId.ExecuteScalar());
    }

    private static AkunTersimpan Petakan(SqliteDataReader reader) => new(
        Id: reader.GetInt32(0),
        Username: reader.GetString(1),
        SandiHash: reader.GetString(2),
        Peran: PemetaanEnum.PeranDariTeks(reader.GetString(3)),
        GuruId: reader.IsDBNull(4) ? null : reader.GetInt32(4),
        Aktif: reader.GetInt32(5) == 1);
}
