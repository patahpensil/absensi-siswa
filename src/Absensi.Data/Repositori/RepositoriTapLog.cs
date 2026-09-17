using Microsoft.Data.Sqlite;

namespace Absensi.Data.Repositori;

public sealed class RepositoriTapLog
{
    // Tap terakhir kartu ini, dari log manapun (termasuk yang ditolak) —
    // dipakai aturan jeda 15 detik (docs/SKEMA-DATABASE.md §6).
    public DateTime? CariWaktuTapTerakhir(SqliteConnection connection, string uid)
    {
        using var command = connection.CreateCommand();
        command.CommandText = "SELECT MAX(waktu) FROM tap_log WHERE uid = $uid;";
        command.Parameters.AddWithValue("$uid", uid);

        return command.ExecuteScalar() as string is { } waktu ? DateTime.Parse(waktu) : null;
    }

    public void Catat(
        SqliteConnection connection,
        SqliteTransaction transaction,
        DateTime waktu,
        string uid,
        string hasil,
        string? pemilikTipe,
        int? pemilikId,
        string? catatan)
    {
        using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = """
            INSERT INTO tap_log (waktu, uid, hasil, pemilik_tipe, pemilik_id, catatan)
            VALUES ($waktu, $uid, $hasil, $pemilikTipe, $pemilikId, $catatan);
            """;
        command.Parameters.AddWithValue("$waktu", waktu.ToString("o"));
        command.Parameters.AddWithValue("$uid", uid);
        command.Parameters.AddWithValue("$hasil", hasil);
        command.Parameters.AddWithValue("$pemilikTipe", (object?)pemilikTipe ?? DBNull.Value);
        command.Parameters.AddWithValue("$pemilikId", (object?)pemilikId ?? DBNull.Value);
        command.Parameters.AddWithValue("$catatan", (object?)catatan ?? DBNull.Value);
        command.ExecuteNonQuery();
    }
}
