using Microsoft.Data.Sqlite;

namespace Absensi.Data.Repositori;

// Hanya dipanggil lewat Absensi.Data.Akun.JalurPerubahanData — itulah satu
// jalur yang dilewati semua perubahan data (CLAUDE.md §4.1 aturan 4) —
// KECUALI Absensi.Data.Wizard.LayananWizard, yang menulis akun pertama
// sekolah dan karena itu belum punya akun lain untuk berperan sebagai
// aktor JalurPerubahanData (lihat catatan di LayananWizard).
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
            INSERT INTO audit (waktu, akun_id, aksi, entitas, entitas_id, nilai_lama, nilai_baru, atas_permintaan, audit_uid)
            VALUES ($waktu, $akunId, $aksi, $entitas, $entitasId, $nilaiLama, $nilaiBaru, $atasPermintaan, $auditUid);
            """;
        command.Parameters.AddWithValue("$waktu", waktu.ToString("o"));
        command.Parameters.AddWithValue("$akunId", akunId);
        command.Parameters.AddWithValue("$aksi", aksi);
        command.Parameters.AddWithValue("$entitas", entitas);
        command.Parameters.AddWithValue("$entitasId", (object?)entitasId ?? DBNull.Value);
        command.Parameters.AddWithValue("$nilaiLama", (object?)nilaiLama ?? DBNull.Value);
        command.Parameters.AddWithValue("$nilaiBaru", (object?)nilaiBaru ?? DBNull.Value);
        command.Parameters.AddWithValue("$atasPermintaan", (object?)atasPermintaan ?? DBNull.Value);
        // Master Bug & UI/UX Register A-39: GUID acak, BUKAN fingerprint --
        // baris baru hanya pernah di-INSERT sekali, jadi identitas acak
        // aman (beda dari backfill migrasi 003 untuk baris LAMA, yang
        // harus deterministik). Lihat Migration003TambahAuditUid.cs.
        command.Parameters.AddWithValue("$auditUid", Guid.NewGuid().ToString());
        command.ExecuteNonQuery();
    }
}
