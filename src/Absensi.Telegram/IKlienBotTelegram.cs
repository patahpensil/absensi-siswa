namespace Absensi.Telegram;

// RENCANA-TUGAS.md Tugas 10: "memakai Bot API lewat HTTPS langsung, tanpa
// pustaka pihak ketiga. Dua panggilan: sendMessage dan getUpdates."
// Diabstraksikan lewat antarmuka supaya PengirimOutbox dan
// PemrosesPembaruanPairing bisa diuji tanpa jaringan sungguhan (pola sama
// seperti IPenyediaWaktu/IPemutarBunyi di Absensi.App).
public interface IKlienBotTelegram
{
    Task<HasilKirimPesan> KirimPesanAsync(string tokenBot, string chatId, string teks);

    Task<IReadOnlyList<PembaruanTelegram>> AmbilPembaruanAsync(string tokenBot, long offsetTerakhir);
}
