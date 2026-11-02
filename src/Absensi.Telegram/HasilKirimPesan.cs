namespace Absensi.Telegram;

// Hasil satu percobaan kirim. Dipisah dari bool supaya PengirimOutbox bisa
// membedakan "tidak ada koneksi / server Telegram sedang bermasalah" (PRD §9.4:
// antre, kirim saat normal — TIDAK menghabiskan jatah percobaan) dari
// penolakan nyata oleh Telegram (token salah, chat tidak valid, bot diblokir).
public enum HasilKirimPesan
{
    Berhasil,
    GagalJaringan,
    Ditolak,
}
