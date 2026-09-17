using Absensi.Data;
using Absensi.Data.Migrations;
using Microsoft.Data.Sqlite;

namespace Absensi.Telegram.Tests;

// Salinan ringkas dari Absensi.Data.Tests.MigratedDatabase — sengaja tidak
// dibagi lintas proyek uji supaya masing-masing proyek uji berdiri sendiri.
internal sealed class MigratedDatabase : IDisposable
{
    private readonly string _path;

    public SqliteConnection Connection { get; }

    public MigratedDatabase()
    {
        _path = Path.Combine(Path.GetTempPath(), $"absensi-telegram-test-{Guid.NewGuid():N}.db");
        Connection = SqliteConnectionFactory.Open(_path);
        new MigrationRunner().Run(Connection);
    }

    public void Execute(string sql)
    {
        using var command = Connection.CreateCommand();
        command.CommandText = sql;
        command.ExecuteNonQuery();
    }

    public void TulisPengaturan(string kunci, string nilai)
    {
        using var command = Connection.CreateCommand();
        command.CommandText = "INSERT INTO pengaturan (kunci, nilai) VALUES ($kunci, $nilai);";
        command.Parameters.AddWithValue("$kunci", kunci);
        command.Parameters.AddWithValue("$nilai", nilai);
        command.ExecuteNonQuery();
    }

    public void TambahOutboxAntre(string chatId, string jenis = "hadir", string tanggal = "2026-09-17")
    {
        using var command = Connection.CreateCommand();
        command.CommandText = """
            INSERT INTO outbox (tanggal, subjek_tipe, subjek_id, jenis, chat_id, isi, status, dibuat_pada)
            VALUES ($tanggal, 'siswa', 1, $jenis, $chatId, 'isi pesan uji', 'antre', $tanggal);
            """;
        command.Parameters.AddWithValue("$tanggal", tanggal);
        command.Parameters.AddWithValue("$jenis", jenis);
        command.Parameters.AddWithValue("$chatId", chatId);
        command.ExecuteNonQuery();
    }

    public void TambahPairingMenunggu(string token, string subjekTipe = "siswa", int subjekId = 1)
    {
        using var command = Connection.CreateCommand();
        command.CommandText = """
            INSERT INTO pairing (subjek_tipe, subjek_id, token, status, dibuat_pada)
            VALUES ($subjekTipe, $subjekId, $token, 'menunggu', '2026-09-01T00:00:00');
            """;
        command.Parameters.AddWithValue("$subjekTipe", subjekTipe);
        command.Parameters.AddWithValue("$subjekId", subjekId);
        command.Parameters.AddWithValue("$token", token);
        command.ExecuteNonQuery();
    }

    public void Dispose()
    {
        Connection.Dispose();
        SqliteConnection.ClearAllPools();

        try
        {
            File.Delete(_path);
        }
        catch (IOException)
        {
            // File lock Windows kadang belum lepas persis setelah Dispose.
        }
    }
}
