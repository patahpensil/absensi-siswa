namespace Absensi.Domain.Notifikasi;

// PRD §9, P-19/P-20: parent notifications go to exactly one channel per
// student, chosen by the parent. Stored as 'telegram' / 'whatsapp'.
public enum NotificationChannel
{
    Telegram,
    WhatsApp,
}
