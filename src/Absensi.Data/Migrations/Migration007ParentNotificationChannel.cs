namespace Absensi.Data.Migrations;

// PRD P-19/P-20: one parent notification channel per student, and the channel
// each queued message belongs to. Existing rows keep Telegram, which was the
// only channel before this migration, so installed schools behave the same.
internal sealed class Migration007ParentNotificationChannel : IMigration
{
    public int Number => 7;

    public string Sql => """
        ALTER TABLE siswa ADD COLUMN notification_channel TEXT NOT NULL DEFAULT 'telegram'
            CHECK (notification_channel IN ('telegram','whatsapp'));

        ALTER TABLE outbox ADD COLUMN channel TEXT NOT NULL DEFAULT 'telegram'
            CHECK (channel IN ('telegram','whatsapp'));
        """;
}
