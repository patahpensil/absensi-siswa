using Absensi.Data.Repositori;
using Absensi.Domain.Kehadiran;
using Absensi.Domain.Notifikasi;
using Microsoft.Data.Sqlite;

namespace Absensi.Data.Notifikasi;

// Where a parent message for one student goes (PRD P-20).
public sealed record NotificationRecipient(NotificationChannel Channel, string Address);

// Current channel settings of one student, for display and editing.
public sealed record ParentChannelSettings(NotificationChannel Channel, string? WhatsAppNumber, bool TelegramLinked);

// Single place that decides the parent destination, so the real-time late
// tap, the scheduled recap/absence messages and correction messages cannot
// disagree about the channel. Null means "no usable destination": no message
// is queued, the same behaviour Telegram already had for unlinked parents.
public sealed class ParentRecipientResolver
{
    private readonly RepositoriPairing _repositoriPairing = new();

    public NotificationRecipient? Find(SqliteConnection connection, int studentId, SqliteTransaction? transaction = null)
    {
        var settings = Read(connection, studentId, transaction);
        if (settings is null)
        {
            return null;
        }

        if (settings.Channel == NotificationChannel.WhatsApp)
        {
            return settings.WhatsAppNumber is { } number ? new NotificationRecipient(NotificationChannel.WhatsApp, number) : null;
        }

        var chatId = _repositoriPairing.CariChatIdAktif(connection, JenisPemilikKartu.Siswa, studentId);
        return chatId is null ? null : new NotificationRecipient(NotificationChannel.Telegram, chatId);
    }

    public ParentChannelSettings? Read(SqliteConnection connection, int studentId, SqliteTransaction? transaction = null)
    {
        // CreateCommand already enlists the connection's open transaction;
        // only override it when the caller passes one explicitly.
        using var command = connection.CreateCommand();
        if (transaction is not null)
        {
            command.Transaction = transaction;
        }

        command.CommandText = "SELECT notification_channel, no_hp_ortu FROM siswa WHERE id = $id;";
        command.Parameters.AddWithValue("$id", studentId);

        string channelText;
        string? phone;
        using (var reader = command.ExecuteReader())
        {
            if (!reader.Read())
            {
                return null;
            }

            channelText = reader.GetString(0);
            phone = reader.IsDBNull(1) ? null : reader.GetString(1);
        }

        var linked = _repositoriPairing.CariChatIdAktif(connection, JenisPemilikKartu.Siswa, studentId) is not null;
        return new ParentChannelSettings(NotificationChannelText.Parse(channelText), ParentPhoneNumber.Normalize(phone), linked);
    }
}

// Database text for NotificationChannel (siswa.notification_channel, outbox.channel).
public static class NotificationChannelText
{
    public static string From(NotificationChannel channel) => channel switch
    {
        NotificationChannel.Telegram => "telegram",
        NotificationChannel.WhatsApp => "whatsapp",
        _ => throw new ArgumentOutOfRangeException(nameof(channel), channel, null),
    };

    public static NotificationChannel Parse(string text) => text switch
    {
        "telegram" => NotificationChannel.Telegram,
        "whatsapp" => NotificationChannel.WhatsApp,
        _ => throw new InvalidOperationException($"Kanal notifikasi tidak dikenal: {text}."),
    };
}
