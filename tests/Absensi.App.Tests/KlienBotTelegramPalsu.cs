using Absensi.Telegram;

namespace Absensi.App.Tests;

// Pengganti IKlienBotTelegram untuk pengujian Penjadwal — tidak pernah
// menyentuh jaringan sungguhan.
internal sealed class KlienBotTelegramPalsu : IKlienBotTelegram
{
    public List<(string ChatId, string Teks)> PesanTerkirim { get; } = new();

    public Task<bool> KirimPesanAsync(string tokenBot, string chatId, string teks)
    {
        PesanTerkirim.Add((chatId, teks));
        return Task.FromResult(true);
    }

    public Task<IReadOnlyList<PembaruanTelegram>> AmbilPembaruanAsync(string tokenBot, long offsetTerakhir) =>
        Task.FromResult<IReadOnlyList<PembaruanTelegram>>(Array.Empty<PembaruanTelegram>());
}
