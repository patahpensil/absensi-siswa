using Absensi.Data.Repositori;
using Absensi.Domain;
using Microsoft.Data.Sqlite;

namespace Absensi.Telegram;

// RENCANA-TUGAS.md Tugas 10 — sisi penerima pairing (PRD §16.6 langkah 6):
// mencocokkan token dari pesan "/start <token>" dengan pairing yang masih
// menunggu, menyimpan chat_id. Offset getUpdates sengaja hanya di memori
// (bukan disimpan ke database) — aman dipanggil ulang dari offset lama
// setelah restart karena penautan sendiri idempoten (RepositoriPairing
// .TautkanChatId hanya berhasil sekali per pairing, PRD §16.6 langkah 7).
public sealed class PemrosesPembaruanPairing
{
    private readonly IKlienBotTelegram _klien;
    private readonly RepositoriPengaturan _repositoriPengaturan = new();
    private readonly RepositoriPairing _repositoriPairing = new();

    private long _offsetTerakhir;

    public PemrosesPembaruanPairing(IKlienBotTelegram klien) => _klien = klien;

    public async Task<int> ProsesPembaruanAsync(SqliteConnection connection, IPenyediaWaktu penyediaWaktu)
    {
        var tokenBot = _repositoriPengaturan.BacaString(connection, "token_bot");
        if (string.IsNullOrWhiteSpace(tokenBot))
        {
            return 0;
        }

        var pembaruan = await _klien.AmbilPembaruanAsync(tokenBot, _offsetTerakhir);
        var jumlahTertaut = 0;

        foreach (var update in pembaruan)
        {
            _offsetTerakhir = update.UpdateId + 1;

            if (EkstrakToken(update.Teks) is not { } token)
            {
                continue;
            }

            if (_repositoriPairing.CariMenungguBerdasarkanToken(connection, token) is not { } pairing)
            {
                // Token tidak dikenal, atau sudah dipakai orang lain lebih
                // dulu — PRD §16.6 langkah 7: "orang tua kedua ... ditolak".
                continue;
            }

            if (_repositoriPairing.TautkanChatId(connection, pairing.Id, update.ChatId, penyediaWaktu.Sekarang))
            {
                jumlahTertaut++;
            }
        }

        return jumlahTertaut;
    }

    private static string? EkstrakToken(string teks)
    {
        const string awalan = "/start ";
        return teks.StartsWith(awalan, StringComparison.Ordinal) ? teks[awalan.Length..].Trim() : null;
    }
}
