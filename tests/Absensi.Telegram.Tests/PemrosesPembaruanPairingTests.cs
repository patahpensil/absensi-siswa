namespace Absensi.Telegram.Tests;

// RENCANA-TUGAS.md Tugas 10 — sisi penerima pairing (PRD §16.6 langkah 6).
public sealed class PemrosesPembaruanPairingTests
{
    private static readonly DateTime Waktu = new(2026, 9, 17, 8, 5, 0);

    [Fact]
    public async Task ProsesPembaruanAsync_StartDenganTokenValid_MenautkanChatId()
    {
        using var database = new MigratedDatabase();
        database.TulisPengaturan("token_bot", "TOKEN-UJI");
        database.TambahPairingMenunggu("ABC123");
        var klien = new KlienBotTelegramPalsu();
        klien.AntrekanPembaruan(new PembaruanTelegram(1, "chat-baru", "/start ABC123"));
        var pemroses = new PemrosesPembaruanPairing(klien);

        var jumlah = await pemroses.ProsesPembaruanAsync(database.Connection, new PenyediaWaktuTetap(Waktu));

        Assert.Equal(1, jumlah);
        using var command = database.Connection.CreateCommand();
        command.CommandText = "SELECT status, chat_id FROM pairing WHERE token = 'ABC123';";
        using var reader = command.ExecuteReader();
        Assert.True(reader.Read());
        Assert.Equal("tertaut", reader.GetString(0));
        Assert.Equal("chat-baru", reader.GetString(1));
    }

    // PRD §16.6 langkah 7: "orang tua kedua memindai QR yang sama → ditolak".
    [Fact]
    public async Task ProsesPembaruanAsync_TokenSudahDipakai_TidakMenautkanChatKedua()
    {
        using var database = new MigratedDatabase();
        database.TulisPengaturan("token_bot", "TOKEN-UJI");
        database.TambahPairingMenunggu("ABC123");
        var klien = new KlienBotTelegramPalsu();
        var pemroses = new PemrosesPembaruanPairing(klien);
        klien.AntrekanPembaruan(new PembaruanTelegram(1, "chat-pertama", "/start ABC123"));
        await pemroses.ProsesPembaruanAsync(database.Connection, new PenyediaWaktuTetap(Waktu));

        klien.AntrekanPembaruan(new PembaruanTelegram(2, "chat-kedua", "/start ABC123"));
        var jumlah = await pemroses.ProsesPembaruanAsync(database.Connection, new PenyediaWaktuTetap(Waktu));

        Assert.Equal(0, jumlah);
        using var command = database.Connection.CreateCommand();
        command.CommandText = "SELECT chat_id FROM pairing WHERE token = 'ABC123';";
        Assert.Equal("chat-pertama", command.ExecuteScalar());
    }

    [Fact]
    public async Task ProsesPembaruanAsync_TokenTidakDikenal_Diabaikan()
    {
        using var database = new MigratedDatabase();
        database.TulisPengaturan("token_bot", "TOKEN-UJI");
        var klien = new KlienBotTelegramPalsu();
        klien.AntrekanPembaruan(new PembaruanTelegram(1, "chat-x", "/start TIDAKADA"));
        var pemroses = new PemrosesPembaruanPairing(klien);

        var jumlah = await pemroses.ProsesPembaruanAsync(database.Connection, new PenyediaWaktuTetap(Waktu));

        Assert.Equal(0, jumlah);
    }

    [Fact]
    public async Task ProsesPembaruanAsync_TanpaTokenBot_TidakMemanggilAmbilPembaruan()
    {
        using var database = new MigratedDatabase();
        database.TambahPairingMenunggu("ABC123");
        var klien = new KlienBotTelegramPalsu();
        klien.AntrekanPembaruan(new PembaruanTelegram(1, "chat-baru", "/start ABC123"));
        var pemroses = new PemrosesPembaruanPairing(klien);

        var jumlah = await pemroses.ProsesPembaruanAsync(database.Connection, new PenyediaWaktuTetap(Waktu));

        Assert.Equal(0, jumlah);
    }
}
