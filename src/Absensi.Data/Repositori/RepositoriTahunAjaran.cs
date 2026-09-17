using Microsoft.Data.Sqlite;

namespace Absensi.Data.Repositori;

public sealed class RepositoriTahunAjaran
{
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
}
