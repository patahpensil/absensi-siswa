using System.Net.Http.Json;
using System.Text.Json.Serialization;
using Absensi.Data.Diagnostik;

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

    public async Task<HasilKirimPesan> KirimPesanAsync(string tokenBot, string chatId, string teks)
    {
        try
        {
            using var respons = await _httpClient.PostAsJsonAsync(
                $"https://api.telegram.org/bot{tokenBot}/sendMessage",
                new PermintaanKirimPesan(chatId, teks));

            if (!respons.IsSuccessStatusCode)
            {
                // 429 (terlalu cepat) dan 5xx adalah masalah sementara di sisi
                // Telegram — bukan salah pesan/chat/token, jadi tidak boleh
                // menghabiskan jatah percobaan. 4xx lain (400/401/403/404)
                // adalah penolakan nyata.
                var kode = (int)respons.StatusCode;
                return kode == 429 || kode >= 500 ? HasilKirimPesan.GagalJaringan : HasilKirimPesan.Ditolak;
            }

            var hasil = await respons.Content.ReadFromJsonAsync<ResponsTelegram>();
            return hasil?.Ok ?? false ? HasilKirimPesan.Berhasil : HasilKirimPesan.Ditolak;
        }
        catch (HttpRequestException galat)
        {
            // TODO-VERSI-BERIKUTNYA.md A.3.1 / docs/INVESTIGASI-CRASH-2026-09-21.md
            // §4.2: dulu ditelan tanpa jejak sama sekali — kegagalan yang
            // MENETAP (mis. token salah permanen) tidak pernah kelihatan.
            // Level ERROR (bukan FATAL): kegagalan satu kali kirim tidak
            // menghentikan aplikasi, PRD §9.4 tetap "antre, coba lagi".
            PencatatGalat.Catat(galat, sumber: "KlienBotTelegram.KirimPesanAsync", level: "ERROR");
            return HasilKirimPesan.GagalJaringan;
        }
        catch (TaskCanceledException galat)
        {
            PencatatGalat.Catat(galat, sumber: "KlienBotTelegram.KirimPesanAsync", level: "ERROR");
            return HasilKirimPesan.GagalJaringan;
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
        catch (HttpRequestException galat)
        {
            PencatatGalat.Catat(galat, sumber: "KlienBotTelegram.AmbilPembaruanAsync", level: "ERROR");
            return Array.Empty<PembaruanTelegram>();
        }
        catch (TaskCanceledException galat)
        {
            PencatatGalat.Catat(galat, sumber: "KlienBotTelegram.AmbilPembaruanAsync", level: "ERROR");
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
