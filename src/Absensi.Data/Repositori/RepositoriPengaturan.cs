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
}
