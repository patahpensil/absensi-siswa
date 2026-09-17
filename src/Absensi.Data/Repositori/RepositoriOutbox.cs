using Microsoft.Data.Sqlite;

namespace Absensi.Data.Repositori;

// `outbox` diisi/dikirim oleh Absensi.Telegram (RENCANA-TUGAS.md Tugas 10,
// belum dibangun) — repositori ini hanya untuk bagian yang sudah dibutuhkan
// lebih dulu oleh Tugas 9 (PRD §6: menandai tanggal libur membatalkan
// seluruh notifikasi yang masih antre pada tanggal itu).
public sealed class RepositoriOutbox
{
    // Baris outbox tidak pernah dihapus (CLAUDE.md §4.2 aturan 8) — status
    // diubah jadi 'dibatalkan', riwayatnya tetap ada untuk ditelusuri.
    public int BatalkanAntreUntukTanggal(SqliteConnection connection, SqliteTransaction transaction, DateOnly tanggal)
    {
        using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = """
            UPDATE outbox SET status = 'dibatalkan'
            WHERE tanggal = $tanggal AND status = 'antre';
            """;
        command.Parameters.AddWithValue("$tanggal", tanggal.ToString("yyyy-MM-dd"));
        return command.ExecuteNonQuery();
    }
}
