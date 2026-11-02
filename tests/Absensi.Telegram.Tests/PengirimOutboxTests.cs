namespace Absensi.Telegram.Tests;

// RENCANA-TUGAS.md Tugas 10, "Selesai bila": mematikan internet tidak
// menghilangkan pesan; menyalakan kembali mengirim antrean.
public sealed class PengirimOutboxTests
{
    private static readonly DateTime Waktu = new(2026, 9, 17, 8, 5, 0);

    [Fact]
    public async Task KirimAntreanAsync_TanpaTokenBot_TidakMengirimApaPun()
    {
        using var database = new MigratedDatabase();
        database.TambahOutboxAntre("chat-1");
        var klien = new KlienBotTelegramPalsu();
        var pengirim = new PengirimOutbox(klien, TimeSpan.Zero);

        var jumlah = await pengirim.KirimAntreanAsync(database.Connection, new PenyediaWaktuTetap(Waktu));

        Assert.Equal(0, jumlah);
        Assert.Empty(klien.PesanTerkirim);
    }

    [Fact]
    public async Task KirimAntreanAsync_DuaBarisAntre_KeduanyaTerkirimDanDitandai()
    {
        using var database = new MigratedDatabase();
        database.TulisPengaturan("token_bot", "TOKEN-UJI");
        database.TambahOutboxAntre("chat-1", jenis: "hadir");
        database.TambahOutboxAntre("chat-2", jenis: "terlambat");
        var klien = new KlienBotTelegramPalsu();
        var pengirim = new PengirimOutbox(klien, TimeSpan.Zero);

        var jumlah = await pengirim.KirimAntreanAsync(database.Connection, new PenyediaWaktuTetap(Waktu));

        Assert.Equal(2, jumlah);
        Assert.Equal(2, klien.PesanTerkirim.Count);

        using var command = database.Connection.CreateCommand();
        command.CommandText = "SELECT COUNT(*) FROM outbox WHERE status = 'terkirim';";
        Assert.Equal(2, Convert.ToInt32(command.ExecuteScalar()));
    }

    [Fact]
    public async Task KirimAntreanAsync_GagalSekali_TetapAntreDenganPercobaanBertambah()
    {
        using var database = new MigratedDatabase();
        database.TulisPengaturan("token_bot", "TOKEN-UJI");
        database.TambahOutboxAntre("chat-gagal");
        var klien = new KlienBotTelegramPalsu();
        klien.ChatIdGagal.Add("chat-gagal");
        var pengirim = new PengirimOutbox(klien, TimeSpan.Zero);

        await pengirim.KirimAntreanAsync(database.Connection, new PenyediaWaktuTetap(Waktu));

        using var command = database.Connection.CreateCommand();
        command.CommandText = "SELECT status, percobaan FROM outbox WHERE chat_id = 'chat-gagal';";
        using var reader = command.ExecuteReader();
        Assert.True(reader.Read());
        Assert.Equal("antre", reader.GetString(0));
        Assert.Equal(1, reader.GetInt32(1));
    }

    [Fact]
    public async Task KirimAntreanAsync_GagalBerkaliKali_AkhirnyaDitandaiGagalPermanen()
    {
        using var database = new MigratedDatabase();
        database.TulisPengaturan("token_bot", "TOKEN-UJI");
        database.TambahOutboxAntre("chat-gagal-total");
        var klien = new KlienBotTelegramPalsu();
        klien.ChatIdGagal.Add("chat-gagal-total");
        var pengirim = new PengirimOutbox(klien, TimeSpan.Zero);

        for (var i = 0; i < 5; i++)
        {
            await pengirim.KirimAntreanAsync(database.Connection, new PenyediaWaktuTetap(Waktu));
        }

        using var command = database.Connection.CreateCommand();
        command.CommandText = "SELECT status FROM outbox WHERE chat_id = 'chat-gagal-total';";
        Assert.Equal("gagal", command.ExecuteScalar());
    }

    [Fact]
    public async Task KirimAntreanAsync_InternetMati_TidakMenghabiskanJatahPercobaanDanBerhentiSiklus()
    {
        using var database = new MigratedDatabase();
        database.TulisPengaturan("token_bot", "TOKEN-UJI");
        database.TambahOutboxAntre("chat-a");
        database.TambahOutboxAntre("chat-b", jenis: "alpa");
        var klien = new KlienBotTelegramPalsu { JaringanMati = true };
        var pengirim = new PengirimOutbox(klien, TimeSpan.Zero);

        // Jauh melebihi BatasPercobaan: offline berjam-jam tidak boleh
        // membuat antrean 'gagal' permanen (PRD §9.4).
        for (var i = 0; i < 50; i++)
        {
            await pengirim.KirimAntreanAsync(database.Connection, new PenyediaWaktuTetap(Waktu));
        }

        using var command = database.Connection.CreateCommand();
        command.CommandText = "SELECT COUNT(*) FROM outbox WHERE status = 'antre' AND percobaan = 0;";
        Assert.Equal(2L, command.ExecuteScalar());
        // Satu panggilan per siklus: begitu jaringan terdeteksi mati, sisa antrean tidak dicoba.
        Assert.Equal(50, klien.JumlahPanggilanKirim);
    }

    [Fact]
    public async Task KirimAntreanAsync_InternetKembali_PesanYangTertundaTerkirim()
    {
        using var database = new MigratedDatabase();
        database.TulisPengaturan("token_bot", "TOKEN-UJI");
        database.TambahOutboxAntre("chat-a");
        var klien = new KlienBotTelegramPalsu { JaringanMati = true };
        var pengirim = new PengirimOutbox(klien, TimeSpan.Zero);
        for (var i = 0; i < 10; i++)
        {
            await pengirim.KirimAntreanAsync(database.Connection, new PenyediaWaktuTetap(Waktu));
        }

        klien.JaringanMati = false;
        var terkirim = await pengirim.KirimAntreanAsync(database.Connection, new PenyediaWaktuTetap(Waktu));

        Assert.Equal(1, terkirim);
    }
}
