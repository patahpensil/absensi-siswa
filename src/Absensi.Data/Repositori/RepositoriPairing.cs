using Absensi.Domain.Kehadiran;
using Microsoft.Data.Sqlite;

namespace Absensi.Data.Repositori;

public sealed class RepositoriPairing
{
    // Hanya dipanggil lewat Absensi.Data.Notifikasi.LayananPairing (dibuat
    // oleh tindakan admin — dibungkus JalurPerubahanData di sana, PRD
    // §12).
    public void BuatToken(
        SqliteConnection connection,
        SqliteTransaction transaction,
        JenisPemilikKartu subjekTipe,
        int subjekId,
        string token,
        DateTime waktu)
    {
        using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = """
            INSERT INTO pairing (subjek_tipe, subjek_id, token, status, dibuat_pada)
            VALUES ($subjekTipe, $subjekId, $token, 'menunggu', $waktu);
            """;
        command.Parameters.AddWithValue("$subjekTipe", PemetaanEnum.TeksDariPemilikTipe(subjekTipe));
        command.Parameters.AddWithValue("$subjekId", subjekId);
        command.Parameters.AddWithValue("$token", token);
        command.Parameters.AddWithValue("$waktu", waktu.ToString("o"));
        command.ExecuteNonQuery();
    }

    // PRD §16.6 langkah 9: "QR hilang → dibuatkan kembali (token lama
    // mati)" — berlaku baik token lama masih menunggu dipindai maupun
    // sudah tertaut (mis. HP orang tua rusak, perlu tertaut ulang ke HP
    // baru). Tidak menghapus baris lama (PRD §5.3/§12).
    public void BatalkanAktifUntukSubjek(SqliteConnection connection, SqliteTransaction transaction, JenisPemilikKartu subjekTipe, int subjekId)
    {
        using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = """
            UPDATE pairing SET status = 'dibatalkan'
            WHERE subjek_tipe = $subjekTipe AND subjek_id = $subjekId AND status IN ('menunggu', 'tertaut');
            """;
        command.Parameters.AddWithValue("$subjekTipe", PemetaanEnum.TeksDariPemilikTipe(subjekTipe));
        command.Parameters.AddWithValue("$subjekId", subjekId);
        command.ExecuteNonQuery();
    }

    public PairingAktif? CariMenungguBerdasarkanToken(SqliteConnection connection, string token)
    {
        using var command = connection.CreateCommand();
        command.CommandText = "SELECT id, subjek_tipe, subjek_id FROM pairing WHERE token = $token AND status = 'menunggu';";
        command.Parameters.AddWithValue("$token", token);

        using var reader = command.ExecuteReader();
        if (!reader.Read())
        {
            return null;
        }

        return new PairingAktif(reader.GetInt32(0), PemetaanEnum.PemilikTipeDariTeks(reader.GetString(1)), reader.GetInt32(2));
    }

    // PRD §16.6 langkah 6.3-6.4 dan langkah 7 ("orang tua kedua memindai QR
    // yang sama → ditolak"): dipanggil Absensi.Telegram saat menerima
    // /start dari Telegram — bukan tindakan pengguna aplikasi, jadi tidak
    // lewat JalurPerubahanData (sama seperti PembukaanHari, tidak ada akun
    // untuk diaudit). WHERE status = 'menunggu' membuat panggilan kedua ke
    // token yang sama otomatis tidak berpengaruh (0 baris terpengaruh).
    public bool TautkanChatId(SqliteConnection connection, int pairingId, string chatId, DateTime waktu)
    {
        using var command = connection.CreateCommand();
        command.CommandText = """
            UPDATE pairing SET status = 'tertaut', chat_id = $chatId, tertaut_pada = $waktu
            WHERE id = $id AND status = 'menunggu';
            """;
        command.Parameters.AddWithValue("$chatId", chatId);
        command.Parameters.AddWithValue("$waktu", waktu.ToString("o"));
        command.Parameters.AddWithValue("$id", pairingId);
        return command.ExecuteNonQuery() > 0;
    }

    // Dipakai LayananPairing.PastikanTokenUntukCetakMassal — token yang
    // sudah dicetak tapi belum dipindai harus dipakai ulang (QR yang sudah
    // di kertas harus tetap valid), bukan diganti setiap kali dicetak.
    public string? CariTokenMenungguUntukSubjek(SqliteConnection connection, JenisPemilikKartu subjekTipe, int subjekId)
    {
        using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT token FROM pairing
            WHERE subjek_tipe = $subjekTipe AND subjek_id = $subjekId AND status = 'menunggu';
            """;
        command.Parameters.AddWithValue("$subjekTipe", PemetaanEnum.TeksDariPemilikTipe(subjekTipe));
        command.Parameters.AddWithValue("$subjekId", subjekId);
        return command.ExecuteScalar() as string;
    }

    // Dipakai pengirim outbox (Absensi.Telegram) untuk menentukan chat_id
    // tujuan sebelum mengirim — null berarti belum tertaut, pesan tetap
    // antre (§9.4: "internet putus → antre" — kondisi ini serupa: belum
    // ada tujuan, jadi tidak dikirim, bukan galat).
    public string? CariChatIdAktif(SqliteConnection connection, JenisPemilikKartu subjekTipe, int subjekId)
    {
        using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT chat_id FROM pairing
            WHERE subjek_tipe = $subjekTipe AND subjek_id = $subjekId AND status = 'tertaut';
            """;
        command.Parameters.AddWithValue("$subjekTipe", PemetaanEnum.TeksDariPemilikTipe(subjekTipe));
        command.Parameters.AddWithValue("$subjekId", subjekId);
        return command.ExecuteScalar() as string;
    }
}
