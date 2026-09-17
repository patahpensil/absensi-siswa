namespace Absensi.Telegram.Tests;

// Pengganti IKlienBotTelegram untuk pengujian — tidak pernah menyentuh
// jaringan sungguhan.
internal sealed class KlienBotTelegramPalsu : IKlienBotTelegram
{
    private readonly Queue<PembaruanTelegram> _pembaruanAntre = new();

    public List<(string ChatId, string Teks)> PesanTerkirim { get; } = new();

    public HashSet<string> ChatIdGagal { get; } = new();

    public int JumlahGagalBerturutan { get; set; }

    public void AntrekanPembaruan(PembaruanTelegram pembaruan) => _pembaruanAntre.Enqueue(pembaruan);

    public Task<bool> KirimPesanAsync(string tokenBot, string chatId, string teks)
    {
        if (ChatIdGagal.Contains(chatId))
        {
            return Task.FromResult(false);
        }

        PesanTerkirim.Add((chatId, teks));
        return Task.FromResult(true);
    }

    public Task<IReadOnlyList<PembaruanTelegram>> AmbilPembaruanAsync(string tokenBot, long offsetTerakhir)
    {
        var hasil = _pembaruanAntre.ToList();
        _pembaruanAntre.Clear();
        return Task.FromResult<IReadOnlyList<PembaruanTelegram>>(hasil);
    }
}
