using Absensi.Data.Akun;
using Absensi.Data.Repositori;
using Absensi.Domain.Notifikasi;
using Microsoft.Data.Sqlite;

namespace Absensi.Data.Notifikasi;

// PRD P-20: the parent's chosen channel is recorded by Kepala Sekolah/Admin
// (master data authority, PRD §3) and every change is audited (PRD §12).
public sealed class ParentChannelService
{
    private readonly JalurPerubahanData _jalurPerubahanData = new();
    private readonly ParentRecipientResolver _resolver = new();

    public void SetChannel(SqliteConnection connection, AkunTersimpan akun, DateTime waktu, int studentId, NotificationChannel channel)
    {
        AuthorizationGuard.EnsureFullAuthority(akun, "mengubah kanal notifikasi orang tua");

        _jalurPerubahanData.JalankanDenganCatatan(
            connection,
            akun,
            waktu,
            aksi: "ubah_kanal_notifikasi",
            entitas: "siswa",
            perubahan: transaction =>
            {
                var current = _resolver.Read(connection, studentId, transaction)
                    ?? throw new InvalidOperationException("Siswa tidak ditemukan.");

                // P-20 uses the stored number directly, so a missing or
                // malformed number would silently drop every message.
                // Refuse the switch instead and say what to fix.
                if (channel == NotificationChannel.WhatsApp && current.WhatsAppNumber is null)
                {
                    throw new InvalidOperationException(
                        "Nomor HP orang tua belum diisi atau tidak valid. Isi nomor Indonesia (mis. 0812…) pada data siswa sebelum memilih WhatsApp.");
                }

                using var command = connection.CreateCommand();
                command.Transaction = transaction;
                command.CommandText = "UPDATE siswa SET notification_channel = $channel WHERE id = $id;";
                command.Parameters.AddWithValue("$channel", NotificationChannelText.From(channel));
                command.Parameters.AddWithValue("$id", studentId);
                command.ExecuteNonQuery();

                return RingkasanPerubahan.Bandingkan(studentId, new RingkasanPerubahan.Kolom(
                    "notification_channel", NotificationChannelText.From(current.Channel), NotificationChannelText.From(channel)));
            });
    }
}
