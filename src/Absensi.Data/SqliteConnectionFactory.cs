using Microsoft.Data.Sqlite;

namespace Absensi.Data;

// PRD/CLAUDE.md §11 skema: setiap koneksi wajib foreign_keys ON (SQLite
// mematikannya secara bawaan) dan journal_mode WAL (agar tap tidak terhalang
// baca laporan).
public static class SqliteConnectionFactory
{
    public static SqliteConnection Open(string dataSource)
    {
        var connection = new SqliteConnection($"Data Source={dataSource}");
        connection.Open();

        Execute(connection, "PRAGMA foreign_keys = ON;");
        Execute(connection, "PRAGMA journal_mode = WAL;");

        return connection;
    }

    private static void Execute(SqliteConnection connection, string sql)
    {
        using var command = connection.CreateCommand();
        command.CommandText = sql;
        command.ExecuteNonQuery();
    }
}
