using Absensi.Data.Akun;
using Absensi.Data.Notifikasi;
using Absensi.Data.Repositori;
using Absensi.Domain.Akun;
using Absensi.Domain.Kehadiran;

namespace Absensi.Data.Tests;

// RENCANA-TUGAS.md Tugas 10 — Pairing Orang Tua/Guru (PRD §16.6, §17.7).
public sealed class LayananPairingTests
{
    private static readonly DateTime Waktu = new(2026, 9, 17, 8, 0, 0);
    private static readonly AkunTersimpan Admin = new(1, "admin1", "x", PeranAkun.Admin, null, true);

    [Fact]
    public void BuatTokenBaru_TokenBisaDicariDanBelumTertaut()
    {
        using var database = new MigratedDatabase();
        database.SeedMasterDataUntukUji();
        var layanan = new LayananPairing();

        var token = layanan.BuatTokenBaru(database.Connection, Admin, Waktu, JenisPemilikKartu.Siswa, subjekId: 1);

        Assert.Equal(6, token.Length);
        var aktif = new RepositoriPairing().CariMenungguBerdasarkanToken(database.Connection, token);
        Assert.NotNull(aktif);
        Assert.Equal(1, aktif!.SubjekId);
        Assert.Equal(JenisPemilikKartu.Siswa, aktif.SubjekTipe);
    }

    [Fact]
    public void TautkanChatId_TokenValid_StatusJadiTertautDanChatIdAktifBisaDicari()
    {
        using var database = new MigratedDatabase();
        database.SeedMasterDataUntukUji();
        var layanan = new LayananPairing();
        var token = layanan.BuatTokenBaru(database.Connection, Admin, Waktu, JenisPemilikKartu.Siswa, subjekId: 1);
        var pairing = new RepositoriPairing().CariMenungguBerdasarkanToken(database.Connection, token)!;

        var berhasil = new RepositoriPairing().TautkanChatId(database.Connection, pairing.Id, "chat-123", Waktu);

        Assert.True(berhasil);
        Assert.Equal("chat-123", new RepositoriPairing().CariChatIdAktif(database.Connection, JenisPemilikKartu.Siswa, 1));
    }

    // PRD §16.6 langkah 7: "orang tua kedua memindai QR yang sama → ditolak".
    [Fact]
    public void TautkanChatId_TokenSudahDipakai_PemindaianKeduaDitolak()
    {
        using var database = new MigratedDatabase();
        database.SeedMasterDataUntukUji();
        var layanan = new LayananPairing();
        var token = layanan.BuatTokenBaru(database.Connection, Admin, Waktu, JenisPemilikKartu.Siswa, subjekId: 1);
        var pairing = new RepositoriPairing().CariMenungguBerdasarkanToken(database.Connection, token)!;
        new RepositoriPairing().TautkanChatId(database.Connection, pairing.Id, "chat-pertama", Waktu);

        var berhasilKedua = new RepositoriPairing().TautkanChatId(database.Connection, pairing.Id, "chat-kedua", Waktu);

        Assert.False(berhasilKedua);
        Assert.Equal("chat-pertama", new RepositoriPairing().CariChatIdAktif(database.Connection, JenisPemilikKartu.Siswa, 1));
    }

    // PRD §16.6 langkah 9: "QR hilang → dibuatkan kembali (token lama mati)".
    [Fact]
    public void BuatTokenBaru_DipanggilLagi_TokenLamaMatiTidakBisaDitautkan()
    {
        using var database = new MigratedDatabase();
        database.SeedMasterDataUntukUji();
        var layanan = new LayananPairing();
        var tokenLama = layanan.BuatTokenBaru(database.Connection, Admin, Waktu, JenisPemilikKartu.Siswa, subjekId: 1);

        var tokenBaru = layanan.BuatTokenBaru(database.Connection, Admin, Waktu, JenisPemilikKartu.Siswa, subjekId: 1);

        Assert.Null(new RepositoriPairing().CariMenungguBerdasarkanToken(database.Connection, tokenLama));
        Assert.NotNull(new RepositoriPairing().CariMenungguBerdasarkanToken(database.Connection, tokenBaru));
    }

    // PRD §16.6 langkah 4: "Admin mencetak QR massal per kelas".
    [Fact]
    public void PastikanTokenUntukCetakMassal_BelumPernahAdaToken_MembuatBaru()
    {
        using var database = new MigratedDatabase();
        database.SeedMasterDataUntukUji();
        var layanan = new LayananPairing();

        var token = layanan.PastikanTokenUntukCetakMassal(database.Connection, Admin, Waktu, JenisPemilikKartu.Siswa, subjekId: 1);

        Assert.NotNull(token);
        Assert.NotNull(new RepositoriPairing().CariMenungguBerdasarkanToken(database.Connection, token!));
    }

    [Fact]
    public void PastikanTokenUntukCetakMassal_SudahAdaTokenMenunggu_MemakaiTokenYangSama()
    {
        using var database = new MigratedDatabase();
        database.SeedMasterDataUntukUji();
        var layanan = new LayananPairing();
        var tokenPertama = layanan.PastikanTokenUntukCetakMassal(database.Connection, Admin, Waktu, JenisPemilikKartu.Siswa, subjekId: 1);

        var tokenKedua = layanan.PastikanTokenUntukCetakMassal(database.Connection, Admin, Waktu, JenisPemilikKartu.Siswa, subjekId: 1);

        Assert.Equal(tokenPertama, tokenKedua);
    }

    [Fact]
    public void PastikanTokenUntukCetakMassal_SudahTertaut_Dilewati()
    {
        using var database = new MigratedDatabase();
        database.SeedMasterDataUntukUji();
        var layanan = new LayananPairing();
        var token = layanan.BuatTokenBaru(database.Connection, Admin, Waktu, JenisPemilikKartu.Siswa, subjekId: 1);
        var pairing = new RepositoriPairing().CariMenungguBerdasarkanToken(database.Connection, token)!;
        new RepositoriPairing().TautkanChatId(database.Connection, pairing.Id, "chat-1", Waktu);

        var hasil = layanan.PastikanTokenUntukCetakMassal(database.Connection, Admin, Waktu, JenisPemilikKartu.Siswa, subjekId: 1);

        Assert.Null(hasil);
    }

    [Fact]
    public void BuatUrlQr_MembentukTautanMulaiObrolanBot()
    {
        var url = LayananPairing.BuatUrlQr("BotSekolahX", "ABC123");

        Assert.Equal("https://t.me/BotSekolahX?start=ABC123", url);
    }
}
