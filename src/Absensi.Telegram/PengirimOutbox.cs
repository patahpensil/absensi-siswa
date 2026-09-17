using Absensi.Data.Repositori;
using Absensi.Domain;
using Microsoft.Data.Sqlite;

namespace Absensi.Telegram;

// RENCANA-TUGAS.md Tugas 10 — pengirim outbox terpisah (CLAUDE.md §4.4
// aturan 15: pesan tidak pernah dikirim langsung dari kode pemicunya).
// Dipanggil berkala oleh penjadwal (Absensi.App) — membaca antrean,
// menghormati batas kirim Telegram (jeda antar pesan), mengulang saat
// gagal, menandai terkirim.
public sealed class PengirimOutbox
{
    // Bot API mengizinkan sekitar 1 pesan/detik per chat — jeda ini
    // sekadar konservatif, bukan nilai resmi dari Telegram. Bisa
    // ditimpa (mis. TimeSpan.Zero) untuk pengujian.
    private static readonly TimeSpan JedaAntarKirimBawaan = TimeSpan.FromSeconds(1);
    private const int BatasPercobaan = 5;

    private readonly IKlienBotTelegram _klien;
    private readonly TimeSpan _jedaAntarKirim;
    private readonly RepositoriOutbox _repositoriOutbox = new();
    private readonly RepositoriPengaturan _repositoriPengaturan = new();

    public PengirimOutbox(IKlienBotTelegram klien, TimeSpan? jedaAntarKirim = null)
    {
        _klien = klien;
        _jedaAntarKirim = jedaAntarKirim ?? JedaAntarKirimBawaan;
    }

    public async Task<int> KirimAntreanAsync(SqliteConnection connection, IPenyediaWaktu penyediaWaktu)
    {
        var tokenBot = _repositoriPengaturan.BacaString(connection, "token_bot");
        if (string.IsNullOrWhiteSpace(tokenBot))
        {
            // PRD §16.6 langkah 2 belum dilakukan admin — tidak ada bot
            // untuk mengirim, bukan galat.
            return 0;
        }

        var jumlahTerkirim = 0;
        var antrean = _repositoriOutbox.DaftarAntre(connection);

        foreach (var baris in antrean)
        {
            var berhasil = await _klien.KirimPesanAsync(tokenBot, baris.ChatId, baris.Isi);
            if (berhasil)
            {
                _repositoriOutbox.TandaiTerkirim(connection, baris.Id, penyediaWaktu.Sekarang);
                jumlahTerkirim++;
            }
            else if (baris.Percobaan + 1 >= BatasPercobaan)
            {
                _repositoriOutbox.TandaiGagalPermanen(connection, baris.Id, "Gagal mengirim setelah beberapa kali percobaan.");
            }
            else
            {
                _repositoriOutbox.TandaiGagalSementara(connection, baris.Id, "Gagal mengirim, akan dicoba lagi.");
            }

            await Task.Delay(_jedaAntarKirim);
        }

        return jumlahTerkirim;
    }
}
