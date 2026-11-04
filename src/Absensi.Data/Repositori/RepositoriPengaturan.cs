using Microsoft.Data.Sqlite;

namespace Absensi.Data.Repositori;

public sealed class RepositoriPengaturan
{
    public int BacaInt(SqliteConnection connection, string kunci, int nilaiBawaan)
    {
        using var command = connection.CreateCommand();
        command.CommandText = "SELECT nilai FROM pengaturan WHERE kunci = $kunci;";
        command.Parameters.AddWithValue("$kunci", kunci);

        var nilai = command.ExecuteScalar() as string;
        return nilai is null ? nilaiBawaan : int.Parse(nilai);
    }

    public string? BacaString(SqliteConnection connection, string kunci)
    {
        using var command = connection.CreateCommand();
        command.CommandText = "SELECT nilai FROM pengaturan WHERE kunci = $kunci;";
        command.Parameters.AddWithValue("$kunci", kunci);
        return command.ExecuteScalar() as string;
    }

    // Hanya dipanggil lewat Absensi.Data.Pengaturan.LayananPengaturan, yang
    // membungkusnya dengan JalurPerubahanData (PRD §12).
    public void Tulis(SqliteConnection connection, SqliteTransaction transaction, string kunci, string nilai)
    {
        using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = """
            INSERT INTO pengaturan (kunci, nilai) VALUES ($kunci, $nilai)
            ON CONFLICT(kunci) DO UPDATE SET nilai = excluded.nilai;
            """;
        command.Parameters.AddWithValue("$kunci", kunci);
        command.Parameters.AddWithValue("$nilai", nilai);
        command.ExecuteNonQuery();
    }
}
