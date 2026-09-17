using Microsoft.Data.Sqlite;

namespace Absensi.Data.Repositori;

public sealed class RepositoriGuru
{
    public IReadOnlyList<int> DaftarIdAktif(SqliteConnection connection)
    {
        using var command = connection.CreateCommand();
        command.CommandText = "SELECT id FROM guru WHERE aktif = 1;";

        using var reader = command.ExecuteReader();
        var daftar = new List<int>();
        while (reader.Read())
        {
            daftar.Add(reader.GetInt32(0));
        }

        return daftar;
    }

    public bool IsAktif(SqliteConnection connection, int guruId)
    {
        using var command = connection.CreateCommand();
        command.CommandText = "SELECT aktif FROM guru WHERE id = $id;";
        command.Parameters.AddWithValue("$id", guruId);

        var aktif = command.ExecuteScalar();
        return aktif is not null && Convert.ToInt32(aktif) == 1;
    }
}
