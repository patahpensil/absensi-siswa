using Absensi.Domain.Notifikasi;
using Microsoft.Data.Sqlite;

namespace Absensi.Data.Repositori;

// `outbox` diisi lewat Absensi.Data.Notifikasi.LayananNotifikasi (dipanggil
// dari titik pemicu PRD §9.2/§9.3) dan dikirim oleh Absensi.Telegram
// (RENCANA-TUGAS.md Tugas 10). Pesan tidak pernah dikirim langsung dari
// kode pemicunya (CLAUDE.md §4.4 aturan 15) — hanya dibuat di sini.
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

    // INSERT OR IGNORE bersandar pada ix_outbox_sekali (docs/SKEMA-DATABASE.md
    // §8: satu jenis pesan per subjek per tanggal, kecuali 'ralat') — jadi
    // aman dipanggil berulang untuk subjek yang sama tanpa mengecek dulu
    // (dipakai baik oleh proses batch yang bisa berjalan ulang, maupun tap
    // real-time). Transaksi opsional: dipanggil sendirian (real-time, satu
    // baris) maupun sebagai bagian transaksi batch yang lebih besar.
    public bool Antrekan(
        SqliteConnection connection,
        SqliteTransaction? transaction,
        DateOnly tanggal,
        JenisSubjekOutbox subjekTipe,
        int subjekId,
        JenisNotifikasi jenis,
        string chatId,
        string isi,
        DateTime waktu)
    {
        using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = """
            INSERT OR IGNORE INTO outbox (tanggal, subjek_tipe, subjek_id, jenis, chat_id, isi, status, dibuat_pada)
            VALUES ($tanggal, $subjekTipe, $subjekId, $jenis, $chatId, $isi, 'antre', $waktu);
            """;
        command.Parameters.AddWithValue("$tanggal", tanggal.ToString("yyyy-MM-dd"));
        command.Parameters.AddWithValue("$subjekTipe", PemetaanEnum.TeksDariSubjekOutbox(subjekTipe));
        command.Parameters.AddWithValue("$subjekId", subjekId);
        command.Parameters.AddWithValue("$jenis", PemetaanEnum.TeksDariJenisNotifikasi(jenis));
        command.Parameters.AddWithValue("$chatId", chatId);
        command.Parameters.AddWithValue("$isi", isi);
        command.Parameters.AddWithValue("$waktu", waktu.ToString("o"));
        return command.ExecuteNonQuery() > 0;
    }

    // Dipakai Absensi.Telegram.PengirimOutbox.
    public IReadOnlyList<OutboxBaris> DaftarAntre(SqliteConnection connection)
    {
        using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT id, tanggal, subjek_tipe, subjek_id, jenis, chat_id, isi, status, percobaan, dibuat_pada, terkirim_pada, galat
            FROM outbox WHERE status = 'antre' ORDER BY dibuat_pada;
            """;

        using var reader = command.ExecuteReader();
        var daftar = new List<OutboxBaris>();
        while (reader.Read())
        {
            daftar.Add(BacaBaris(reader));
        }

        return daftar;
    }

    public void TandaiTerkirim(SqliteConnection connection, int id, DateTime waktu)
    {
        using var command = connection.CreateCommand();
        command.CommandText = "UPDATE outbox SET status = 'terkirim', terkirim_pada = $waktu WHERE id = $id;";
        command.Parameters.AddWithValue("$waktu", waktu.ToString("o"));
        command.Parameters.AddWithValue("$id", id);
        command.ExecuteNonQuery();
    }

    // Tetap 'antre' (dicoba lagi siklus berikutnya) — "mengulang saat
    // gagal" (RENCANA-TUGAS.md Tugas 10).
    public void TandaiGagalSementara(SqliteConnection connection, int id, string galat)
    {
        using var command = connection.CreateCommand();
        command.CommandText = "UPDATE outbox SET percobaan = percobaan + 1, galat = $galat WHERE id = $id;";
        command.Parameters.AddWithValue("$galat", galat);
        command.Parameters.AddWithValue("$id", id);
        command.ExecuteNonQuery();
    }

    // Menyerah setelah percobaan berkali-kali (mis. chat_id tidak valid
    // lagi — pengguna memblokir bot) — berhenti diulang, tapi tetap
    // tersimpan sebagai riwayat (CLAUDE.md §4.2 aturan 8).
    public void TandaiGagalPermanen(SqliteConnection connection, int id, string galat)
    {
        using var command = connection.CreateCommand();
        command.CommandText = "UPDATE outbox SET status = 'gagal', percobaan = percobaan + 1, galat = $galat WHERE id = $id;";
        command.Parameters.AddWithValue("$galat", galat);
        command.Parameters.AddWithValue("$id", id);
        command.ExecuteNonQuery();
    }

    // PRD §16.9 langkah 3 / §16.10 langkah 7.2-7.3: "periksa penanda sudah
    // terkirim" — dipakai LayananKoreksi untuk memutuskan antara notifikasi
    // normal (belum pernah) atau PESAN RALAT (sudah pernah). 'ralat'
    // dikecualikan karena bukan penanda "sudah pernah dinotifikasi normal".
    public bool SudahPernahAdaUntukSubjek(SqliteConnection connection, DateOnly tanggal, JenisSubjekOutbox subjekTipe, int subjekId)
    {
        using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT COUNT(*) FROM outbox
            WHERE tanggal = $tanggal AND subjek_tipe = $subjekTipe AND subjek_id = $subjekId AND jenis <> 'ralat';
            """;
        command.Parameters.AddWithValue("$tanggal", tanggal.ToString("yyyy-MM-dd"));
        command.Parameters.AddWithValue("$subjekTipe", PemetaanEnum.TeksDariSubjekOutbox(subjekTipe));
        command.Parameters.AddWithValue("$subjekId", subjekId);
        return Convert.ToInt32(command.ExecuteScalar()) > 0;
    }

    // Layar Notifikasi (PANDUAN-UI-UX.md §9: "riwayat kirim").
    public IReadOnlyList<OutboxBaris> DaftarRiwayat(SqliteConnection connection, int maksimal = 200)
    {
        using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT id, tanggal, subjek_tipe, subjek_id, jenis, chat_id, isi, status, percobaan, dibuat_pada, terkirim_pada, galat
            FROM outbox WHERE status <> 'antre' ORDER BY dibuat_pada DESC LIMIT $maksimal;
            """;
        command.Parameters.AddWithValue("$maksimal", maksimal);

        using var reader = command.ExecuteReader();
        var daftar = new List<OutboxBaris>();
        while (reader.Read())
        {
            daftar.Add(BacaBaris(reader));
        }

        return daftar;
    }

    private static OutboxBaris BacaBaris(SqliteDataReader reader) => new(
        reader.GetInt32(0),
        DateOnly.Parse(reader.GetString(1)),
        PemetaanEnum.SubjekOutboxDariTeks(reader.GetString(2)),
        reader.GetInt32(3),
        PemetaanEnum.JenisNotifikasiDariTeks(reader.GetString(4)),
        reader.GetString(5),
        reader.GetString(6),
        reader.GetString(7),
        reader.GetInt32(8),
        DateTime.Parse(reader.GetString(9)),
        reader.IsDBNull(10) ? null : DateTime.Parse(reader.GetString(10)),
        reader.IsDBNull(11) ? null : reader.GetString(11));
}
