namespace Absensi.App.Tests;

// RENCANA-TUGAS.md Tugas 10 — Penjadwal (PRD §9.2 titik pengiriman batch,
// §9.4 toleransi 15 menit + tanggal diblok).
public sealed class PenjadwalTests
{
    private static readonly DateOnly HariKamis = new(2026, 9, 17);

    private static DateTime Waktu(int jam, int menit) => HariKamis.ToDateTime(new TimeOnly(jam, menit));

    private static void SeedPairingSiswaSatu(MigratedDatabase database) => database.Execute("""
        INSERT INTO pairing (subjek_tipe, subjek_id, token, status, chat_id, dibuat_pada, tertaut_pada)
        VALUES ('siswa', 1, 'TOKUJI', 'tertaut', 'chat-siswa-uji', '2026-09-01T00:00:00', '2026-09-01T00:00:00');
        """);

    [Fact]
    public async Task DetakAsync_SebelumJamBatasAkhir_TidakMengantreNotifikasiAlpa()
    {
        using var database = new MigratedDatabase();
        database.SeedSekolahUntukGerbang();
        SeedPairingSiswaSatu(database);
        var waktu = new PenyediaWaktuUji(Waktu(11, 55));
        var penjadwal = new Penjadwal(database.Connection, waktu, new KlienBotTelegramPalsu());

        await penjadwal.DetakAsync();

        using var command = database.Connection.CreateCommand();
        command.CommandText = "SELECT COUNT(*) FROM outbox WHERE chat_id = 'chat-siswa-uji';";
        Assert.Equal(0, Convert.ToInt32(command.ExecuteScalar()));
    }

    [Fact]
    public async Task DetakAsync_DalamJendelaToleransi_MengantreNotifikasiAlpa()
    {
        using var database = new MigratedDatabase();
        database.SeedSekolahUntukGerbang();
        SeedPairingSiswaSatu(database);
        var waktu = new PenyediaWaktuUji(Waktu(12, 5));
        var penjadwal = new Penjadwal(database.Connection, waktu, new KlienBotTelegramPalsu());

        await penjadwal.DetakAsync();

        using var command = database.Connection.CreateCommand();
        command.CommandText = "SELECT COUNT(*) FROM outbox WHERE chat_id = 'chat-siswa-uji' AND jenis = 'alpa';";
        Assert.Equal(1, Convert.ToInt32(command.ExecuteScalar()));
    }

    // PRD §9.4: "aplikasi menyala > 15 menit setelah jadwal → jadwal DIBUANG".
    [Fact]
    public async Task DetakAsync_LewatJendelaToleransi_JadwalDibuangTidakDiantre()
    {
        using var database = new MigratedDatabase();
        database.SeedSekolahUntukGerbang();
        SeedPairingSiswaSatu(database);
        var waktu = new PenyediaWaktuUji(Waktu(12, 20));
        var penjadwal = new Penjadwal(database.Connection, waktu, new KlienBotTelegramPalsu());

        await penjadwal.DetakAsync();

        using var command = database.Connection.CreateCommand();
        command.CommandText = "SELECT COUNT(*) FROM outbox WHERE chat_id = 'chat-siswa-uji';";
        Assert.Equal(0, Convert.ToInt32(command.ExecuteScalar()));
    }

    [Fact]
    public async Task DetakAsync_DetakBerulangDalamHariSama_HanyaMengantreSekali()
    {
        using var database = new MigratedDatabase();
        database.SeedSekolahUntukGerbang();
        SeedPairingSiswaSatu(database);
        var waktu = new PenyediaWaktuUji(Waktu(12, 5));
        var penjadwal = new Penjadwal(database.Connection, waktu, new KlienBotTelegramPalsu());

        await penjadwal.DetakAsync();
        waktu.Sekarang = Waktu(12, 6);
        await penjadwal.DetakAsync();

        using var command = database.Connection.CreateCommand();
        command.CommandText = "SELECT COUNT(*) FROM outbox WHERE chat_id = 'chat-siswa-uji';";
        Assert.Equal(1, Convert.ToInt32(command.ExecuteScalar()));
    }

    // PRD §9.4: "Tanggal diblok → seluruh jadwal dibatalkan".
    [Fact]
    public async Task DetakAsync_TanggalLibur_TidakMengantreApaPun()
    {
        using var database = new MigratedDatabase();
        database.SeedSekolahUntukGerbang();
        SeedPairingSiswaSatu(database);
        database.Execute("""
            INSERT INTO kalender (tanggal, jenis, keterangan, dibuat_pada)
            VALUES ('2026-09-17', 'libur', 'Libur uji', '2026-09-01T00:00:00');
            """);
        var waktu = new PenyediaWaktuUji(Waktu(12, 5));
        var penjadwal = new Penjadwal(database.Connection, waktu, new KlienBotTelegramPalsu());

        await penjadwal.DetakAsync();

        using var command = database.Connection.CreateCommand();
        command.CommandText = "SELECT COUNT(*) FROM outbox;";
        Assert.Equal(0, Convert.ToInt32(command.ExecuteScalar()));
    }
}
