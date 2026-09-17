using System.Net.Http.Json;
using System.Text.Json.Serialization;

namespace Absensi.Telegram;

// RENCANA-TUGAS.md Tugas 10 — HTTPS langsung ke Bot API, tanpa pustaka
// Telegram pihak ketiga (CLAUDE.md §2). Kegagalan jaringan/HTTP tidak
// pernah dilempar ke pemanggil sebagai galat fatal — PengirimOutbox dan
// PemrosesPembaruanPairing berjalan tiap siklus penjadwal, jadi kegagalan
// satu siklus cukup dicoba lagi siklus berikutnya (PRD §9.4: "internet
// putus → antre, kirim saat normal").
public sealed class KlienBotTelegram : IKlienBotTelegram
{
    private readonly HttpClient _httpClient;

    public KlienBotTelegram(HttpClient? httpClient = null)
    {
        _httpClient = httpClient ?? new HttpClient();
    }

    public async Task<bool> KirimPesanAsync(string tokenBot, string chatId, string teks)
    {
        try
        {
            using var respons = await _httpClient.PostAsJsonAsync(
                $"https://api.telegram.org/bot{tokenBot}/sendMessage",
                new PermintaanKirimPesan(chatId, teks));

            if (!respons.IsSuccessStatusCode)
            {
                return false;
            }

            var hasil = await respons.Content.ReadFromJsonAsync<ResponsTelegram>();
            return hasil?.Ok ?? false;
        }
        catch (HttpRequestException)
        {
            return false;
        }
        catch (TaskCanceledException)
        {
            return false;
        }
    }

    public async Task<IReadOnlyList<PembaruanTelegram>> AmbilPembaruanAsync(string tokenBot, long offsetTerakhir)
    {
        try
        {
            using var respons = await _httpClient.GetAsync(
                $"https://api.telegram.org/bot{tokenBot}/getUpdates?offset={offsetTerakhir}&timeout=0");

            if (!respons.IsSuccessStatusCode)
            {
                return Array.Empty<PembaruanTelegram>();
            }

            var hasil = await respons.Content.ReadFromJsonAsync<ResponsTelegramUpdates>();
            if (hasil is not { Ok: true, Result: { } update })
            {
                return Array.Empty<PembaruanTelegram>();
            }

            return update
                .Where(u => u.Message?.Text is not null)
                .Select(u => new PembaruanTelegram(u.UpdateId, u.Message!.Chat.Id.ToString(), u.Message.Text!))
                .ToList();
        }
        catch (HttpRequestException)
        {
            return Array.Empty<PembaruanTelegram>();
        }
        catch (TaskCanceledException)
        {
            return Array.Empty<PembaruanTelegram>();
        }
    }

    private sealed record PermintaanKirimPesan(
        [property: JsonPropertyName("chat_id")] string ChatId,
        [property: JsonPropertyName("text")] string Text);

    private sealed record ResponsTelegram([property: JsonPropertyName("ok")] bool Ok);

    private sealed record ResponsTelegramUpdates(
        [property: JsonPropertyName("ok")] bool Ok,
        [property: JsonPropertyName("result")] IReadOnlyList<UpdateTelegram>? Result);

    private sealed record UpdateTelegram(
        [property: JsonPropertyName("update_id")] long UpdateId,
        [property: JsonPropertyName("message")] PesanTelegram? Message);

    private sealed record PesanTelegram(
        [property: JsonPropertyName("chat")] ChatTelegram Chat,
        [property: JsonPropertyName("text")] string? Text);

    private sealed record ChatTelegram([property: JsonPropertyName("id")] long Id);
}
