using Microsoft.Data.Sqlite;

namespace Absensi.Data.Repositori;

// Hanya dipanggil lewat Absensi.Data.Akun.JalurPerubahanData — itulah satu
// jalur yang dilewati semua perubahan data (CLAUDE.md §4.1 aturan 4).
public sealed class RepositoriAudit
{
    public void Catat(
        SqliteConnection connection,
        SqliteTransaction transaction,
        DateTime waktu,
        int akunId,
        string aksi,
        string entitas,
        int? entitasId,
        string? nilaiLama,
        string? nilaiBaru,
        string? atasPermintaan)
    {
        using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = """
            INSERT INTO audit (waktu, akun_id, aksi, entitas, entitas_id, nilai_lama, nilai_baru, atas_permintaan)
            VALUES ($waktu, $akunId, $aksi, $entitas, $entitasId, $nilaiLama, $nilaiBaru, $atasPermintaan);
            """;
        command.Parameters.AddWithValue("$waktu", waktu.ToString("o"));
        command.Parameters.AddWithValue("$akunId", akunId);
        command.Parameters.AddWithValue("$aksi", aksi);
        command.Parameters.AddWithValue("$entitas", entitas);
        command.Parameters.AddWithValue("$entitasId", (object?)entitasId ?? DBNull.Value);
        command.Parameters.AddWithValue("$nilaiLama", (object?)nilaiLama ?? DBNull.Value);
        command.Parameters.AddWithValue("$nilaiBaru", (object?)nilaiBaru ?? DBNull.Value);
        command.Parameters.AddWithValue("$atasPermintaan", (object?)atasPermintaan ?? DBNull.Value);
        command.ExecuteNonQuery();
    }
}
