using Absensi.Data.Repositori;
using Absensi.Domain;
using Absensi.Domain.Notifikasi;
using Microsoft.Data.Sqlite;

namespace Absensi.Telegram;

// RENCANA-TUGAS.md Tugas 10 — pengirim outbox terpisah (PRD §9.4/§15:
// pesan tidak pernah dikirim langsung dari kode pemicunya).
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
        var antrean = _repositoriOutbox.ListQueuedForChannel(connection, NotificationChannel.Telegram);

        foreach (var baris in antrean)
        {
            var hasil = await _klien.KirimPesanAsync(tokenBot, baris.ChatId, baris.Isi);
            if (hasil == HasilKirimPesan.GagalJaringan)
            {
                // PRD §9.4 "internet putus → antre, kirim saat normal": tidak
                // ada koneksi (atau Telegram sedang bermasalah) bukan salah
                // pesan ini, jadi jatah percobaan TIDAK dikurangi dan sisa
                // antrean tidak dicoba lagi pada siklus ini (pasti gagal
                // juga, dan menunggu timeout per pesan menahan siklus).
                break;
            }

            if (hasil == HasilKirimPesan.Berhasil)
            {
                _repositoriOutbox.TandaiTerkirim(connection, baris.Id, penyediaWaktu.Sekarang);
                jumlahTerkirim++;
            }
            else if (baris.Percobaan + 1 >= BatasPercobaan)
            {
                _repositoriOutbox.TandaiGagalPermanen(connection, baris.Id, "Ditolak Telegram setelah beberapa kali percobaan.");
            }
            else
            {
                _repositoriOutbox.TandaiGagalSementara(connection, baris.Id, "Ditolak Telegram, akan dicoba lagi.");
            }

            await Task.Delay(_jedaAntarKirim);
        }

        return jumlahTerkirim;
    }
}
