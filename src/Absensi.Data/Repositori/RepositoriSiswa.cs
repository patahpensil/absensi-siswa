using Microsoft.Data.Sqlite;

namespace Absensi.Data.Repositori;

public sealed class RepositoriSiswa
{
    public bool IsAktif(SqliteConnection connection, int siswaId)
    {
        using var command = connection.CreateCommand();
        command.CommandText = "SELECT aktif FROM siswa WHERE id = $id;";
        command.Parameters.AddWithValue("$id", siswaId);

        var aktif = command.ExecuteScalar();
        return aktif is not null && Convert.ToInt32(aktif) == 1;
    }
}
