using Absensi.Data.Notifikasi;
using Absensi.Data.Repositori;
using Absensi.Domain.Akun;
using Absensi.Domain.Notifikasi;

namespace Absensi.Data.Tests;

// PRD P-19/P-20: one parent channel per student (Telegram or WhatsApp);
// WhatsApp uses the stored parent number directly; senders only take their
// own channel; only Kepala Sekolah/Admin may change it, with audit.
public sealed class ParentNotificationChannelTests
{
    private static readonly DateOnly Tanggal = new(2026, 9, 17);
    private static readonly DateTime Waktu = Tanggal.ToDateTime(new TimeOnly(8, 10));
    private static readonly AkunTersimpan Admin = new(1, "admin", "x", PeranAkun.Admin, null, true);
    private static readonly AkunTersimpan Wali = new(2, "wali", "x", PeranAkun.WaliKelas, 1, true);

    [Fact]
    public void ExistingStudent_DefaultsToTelegram_AndRecapKeepsTelegramBehaviour()
    {
        using var database = Prepare();

        new LayananNotifikasi().RunParentRecap(database.Connection, Tanggal, Waktu);

        Assert.Equal("telegram", Scalar(database, "SELECT notification_channel FROM siswa WHERE id = 1;"));
        Assert.Equal("telegram", Scalar(database, "SELECT channel FROM outbox WHERE chat_id = 'chat-siswa-1';"));
    }

    [Fact]
    public void WhatsAppStudent_RecapQueuedToNormalisedNumberOnly()
    {
        using var database = Prepare();
        database.Execute("UPDATE siswa SET no_hp_ortu = '0812-3456-7890' WHERE id = 1;");
        new ParentChannelService().SetChannel(database.Connection, Admin, Waktu, 1, NotificationChannel.WhatsApp);

        new LayananNotifikasi().RunParentRecap(database.Connection, Tanggal, Waktu);

        Assert.Equal("whatsapp", Scalar(database, "SELECT channel FROM outbox WHERE subjek_tipe = 'siswa' AND subjek_id = 1;"));
        Assert.Equal("6281234567890", Scalar(database, "SELECT chat_id FROM outbox WHERE subjek_tipe = 'siswa' AND subjek_id = 1;"));
        Assert.Equal(0L, Scalar(database, "SELECT COUNT(*) FROM outbox WHERE chat_id = 'chat-siswa-1';"));
        // Other students keep their own (Telegram) channel.
        Assert.Equal("telegram", Scalar(database, "SELECT channel FROM outbox WHERE chat_id = 'chat-siswa-2';"));
    }

    [Fact]
    public void WhatsAppStudent_NumberLaterMadeInvalid_NoMessageQueued()
    {
        using var database = Prepare();
        database.Execute("UPDATE siswa SET no_hp_ortu = '081234567890' WHERE id = 1;");
        new ParentChannelService().SetChannel(database.Connection, Admin, Waktu, 1, NotificationChannel.WhatsApp);
        database.Execute("UPDATE siswa SET no_hp_ortu = '0812' WHERE id = 1;");

        new LayananNotifikasi().RunParentRecap(database.Connection, Tanggal, Waktu);

        Assert.Equal(0L, Scalar(database, "SELECT COUNT(*) FROM outbox WHERE subjek_tipe = 'siswa' AND subjek_id = 1;"));
    }

    [Fact]
    public void SetChannel_WhatsAppWithoutValidNumber_RejectedAndUnchanged()
    {
        using var database = Prepare();

        var error = Assert.Throws<InvalidOperationException>(() =>
            new ParentChannelService().SetChannel(database.Connection, Admin, Waktu, 1, NotificationChannel.WhatsApp));

        Assert.Contains("Nomor HP orang tua", error.Message);
        Assert.Equal("telegram", Scalar(database, "SELECT notification_channel FROM siswa WHERE id = 1;"));
        Assert.Equal(0L, Scalar(database, "SELECT COUNT(*) FROM audit WHERE aksi = 'ubah_kanal_notifikasi';"));
    }

    [Fact]
    public void SetChannel_HomeroomTeacher_Rejected()
    {
        using var database = Prepare();
        database.Execute("UPDATE siswa SET no_hp_ortu = '081234567890' WHERE id = 1;");

        Assert.Throws<InvalidOperationException>(() =>
            new ParentChannelService().SetChannel(database.Connection, Wali, Waktu, 1, NotificationChannel.WhatsApp));

        Assert.Equal("telegram", Scalar(database, "SELECT notification_channel FROM siswa WHERE id = 1;"));
    }

    [Fact]
    public void SetChannel_Admin_WritesOldAndNewValueToAudit()
    {
        using var database = Prepare();
        database.Execute("UPDATE siswa SET no_hp_ortu = '081234567890' WHERE id = 1;");

        new ParentChannelService().SetChannel(database.Connection, Admin, Waktu, 1, NotificationChannel.WhatsApp);

        Assert.Equal("notification_channel=telegram", Scalar(database, "SELECT nilai_lama FROM audit WHERE aksi = 'ubah_kanal_notifikasi';"));
        Assert.Equal("notification_channel=whatsapp", Scalar(database, "SELECT nilai_baru FROM audit WHERE aksi = 'ubah_kanal_notifikasi';"));
    }

    [Fact]
    public void ListQueuedForChannel_TelegramSenderNeverSeesWhatsAppRows()
    {
        using var database = Prepare();
        database.Execute("UPDATE siswa SET no_hp_ortu = '081234567890' WHERE id = 1;");
        new ParentChannelService().SetChannel(database.Connection, Admin, Waktu, 1, NotificationChannel.WhatsApp);
        new LayananNotifikasi().RunParentRecap(database.Connection, Tanggal, Waktu);
        var repository = new RepositoriOutbox();

        var telegram = repository.ListQueuedForChannel(database.Connection, NotificationChannel.Telegram);
        var whatsApp = repository.ListQueuedForChannel(database.Connection, NotificationChannel.WhatsApp);

        Assert.DoesNotContain(telegram, row => row.Channel == NotificationChannel.WhatsApp || row.ChatId == "6281234567890");
        var whatsAppRow = Assert.Single(whatsApp);
        Assert.Equal("6281234567890", whatsAppRow.ChatId);
        Assert.Equal(repository.DaftarAntre(database.Connection).Count, telegram.Count + whatsApp.Count);
    }

    [Fact]
    public void Correction_WhatsAppStudent_QueuedOnWhatsApp()
    {
        using var database = Prepare();
        database.Execute("UPDATE siswa SET no_hp_ortu = '081234567890' WHERE id = 3;");
        new ParentChannelService().SetChannel(database.Connection, Admin, Waktu, 3, NotificationChannel.WhatsApp);

        new LayananNotifikasi().AntrekanNotifikasiKoreksi(database.Connection, Tanggal, 3, "Siswa Alpa",
            Absensi.Domain.Kehadiran.StatusAbsensiSiswa.Hadir, new TimeOnly(7, 20), Waktu);

        Assert.Equal("whatsapp", Scalar(database, "SELECT channel FROM outbox WHERE subjek_tipe = 'siswa' AND subjek_id = 3;"));
        Assert.Equal(0L, Scalar(database, "SELECT COUNT(*) FROM outbox WHERE chat_id = 'chat-siswa-3';"));
    }

    [Fact]
    public void RealtimeLateTap_WhatsAppStudent_QueuedOnWhatsApp()
    {
        using var database = new MigratedDatabase();
        database.SeedSekolahUntukTap();
        SeedAccounts(database);
        database.Execute("UPDATE siswa SET no_hp_ortu = '081234567890' WHERE id = 1;");
        new ParentChannelService().SetChannel(database.Connection, Admin, Waktu, 1, NotificationChannel.WhatsApp);
        new PembukaanHari().Jalankan(database.Connection, Tanggal);

        new PemrosesTap().Proses(database.Connection, "UID-SISWA-1", Tanggal.ToDateTime(new TimeOnly(8, 15)), modeRegistrasiAktif: false);

        Assert.Equal("6281234567890", Scalar(database, "SELECT chat_id FROM outbox WHERE jenis = 'terlambat' AND channel = 'whatsapp';"));
        Assert.Equal(0L, Scalar(database, "SELECT COUNT(*) FROM outbox WHERE chat_id = 'chat-siswa-uji';"));
    }

    private static MigratedDatabase Prepare()
    {
        var database = new MigratedDatabase();
        database.SeedSekolahUntukNotifikasi();
        SeedAccounts(database);
        return database;
    }

    // audit.akun_id references akun(id); the notification seed has no accounts.
    private static void SeedAccounts(MigratedDatabase database) => database.Execute("""
        INSERT INTO akun (id, username, sandi_hash, peran, guru_id, aktif, dibuat_pada)
        VALUES (1, 'admin', 'x', 'admin', NULL, 1, '2026-09-01T00:00:00');
        INSERT INTO akun (id, username, sandi_hash, peran, guru_id, aktif, dibuat_pada)
        VALUES (2, 'wali', 'x', 'wali_kelas', 1, 1, '2026-09-01T00:00:00');
        """);

    private static object? Scalar(MigratedDatabase database, string sql)
    {
        using var command = database.Connection.CreateCommand();
        command.CommandText = sql;
        return command.ExecuteScalar();
    }
}
