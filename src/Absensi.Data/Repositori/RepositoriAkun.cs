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

    // RENCANA-TUGAS.md Tugas 14 — layar Manajemen Akun (Pengaturan → Akun).
    // LEFT JOIN guru supaya akun Admin tanpa guru_id (opsional untuknya)
    // tetap tampil, bukan hilang dari daftar.
    public IReadOnlyList<AkunDaftar> DaftarSemua(SqliteConnection connection)
    {
        using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT akun.id, akun.username, akun.peran, akun.guru_id, guru.nama, akun.aktif
            FROM akun
            LEFT JOIN guru ON guru.id = akun.guru_id
            ORDER BY akun.username;
            """;

        using var reader = command.ExecuteReader();
        var daftar = new List<AkunDaftar>();
        while (reader.Read())
        {
            daftar.Add(new AkunDaftar(
                Id: reader.GetInt32(0),
                Username: reader.GetString(1),
                Peran: PemetaanEnum.PeranDariTeks(reader.GetString(2)),
                GuruId: reader.IsDBNull(3) ? null : reader.GetInt32(3),
                NamaGuru: reader.IsDBNull(4) ? null : reader.GetString(4),
                Aktif: reader.GetInt32(5) == 1));
        }

        return daftar;
    }

    // Hanya dipanggil lewat Absensi.Data.Akun.LayananAkun — bukan
    // penghapusan (CLAUDE.md §4.2 aturan 8), sesuai pola nonaktifkan siswa
    // guru/kartu di tugas-tugas sebelumnya.
    public void Nonaktifkan(SqliteConnection connection, SqliteTransaction transaction, int id)
    {
        using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = "UPDATE akun SET aktif = 0 WHERE id = $id;";
        command.Parameters.AddWithValue("$id", id);
        command.ExecuteNonQuery();
    }

    // Hanya dipanggil lewat Absensi.Data.Akun.LayananAkun.
    public void PerbaruiSandi(SqliteConnection connection, SqliteTransaction transaction, int id, string sandiHashBaru)
    {
        using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = "UPDATE akun SET sandi_hash = $sandiHash WHERE id = $id;";
        command.Parameters.AddWithValue("$sandiHash", sandiHashBaru);
        command.Parameters.AddWithValue("$id", id);
        command.ExecuteNonQuery();
    }

    private static AkunTersimpan Petakan(SqliteDataReader reader) => new(
        Id: reader.GetInt32(0),
        Username: reader.GetString(1),
        SandiHash: reader.GetString(2),
        Peran: PemetaanEnum.PeranDariTeks(reader.GetString(3)),
        GuruId: reader.IsDBNull(4) ? null : reader.GetInt32(4),
        Aktif: reader.GetInt32(5) == 1);
}

public sealed record AkunDaftar(int Id, string Username, PeranAkun Peran, int? GuruId, string? NamaGuru, bool Aktif);
