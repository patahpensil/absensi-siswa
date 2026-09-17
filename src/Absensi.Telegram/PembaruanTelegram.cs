namespace Absensi.Telegram;

// Satu update dari getUpdates yang berisi pesan teks (PRD §16.6 langkah
// 6.1: "Telegram terbuka, bot menerima token") — update tanpa pesan teks
// (mis. anggota grup baru) tidak pernah sampai ke pemanggil.
public sealed record PembaruanTelegram(long UpdateId, string ChatId, string Teks);
