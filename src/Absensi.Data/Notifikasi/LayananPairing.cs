using Absensi.Data.Akun;
using Absensi.Data.Repositori;
using Absensi.Domain.Kehadiran;
using Microsoft.Data.Sqlite;

namespace Absensi.Data.Notifikasi;

// RENCANA-TUGAS.md Tugas 10 — Pairing Orang Tua/Guru (PRD §16.6, §17.7).
// Token acak 6 karakter sekali pakai; QR-nya berisi tautan mulai obrolan
// bot (langkah 4). Setiap perubahan lewat JalurPerubahanData (PRD
// §12) — kecuali penautan chat_id, yang dipicu Telegram sendiri
// (lihat RepositoriPairing.TautkanChatId).
public sealed class LayananPairing
{
    // Menghindari huruf/angka yang gampang keliru dibaca dari kertas
    // cetakan (0/O, 1/I) — bukan aturan PRD, murni kenyamanan operator.
    private const string AbjadToken = "ABCDEFGHJKLMNPQRSTUVWXYZ23456789";
    private const int PanjangToken = 6;

    private readonly RepositoriPairing _repositoriPairing = new();
    private readonly JalurPerubahanData _jalurPerubahanData = new();

    // PRD §16.6 langkah 3 (token baru) dan langkah 9 (QR hilang, token
    // lama mati) memakai jalur yang sama — token lama (kalau ada) selalu
    // dinonaktifkan lebih dulu, satu transaksi.
    public string BuatTokenBaru(SqliteConnection connection, AkunTersimpan akun, DateTime waktu, JenisPemilikKartu subjekTipe, int subjekId)
    {
        AuthorizationGuard.EnsureFullAuthority(akun, "membuat token pairing");

        var token = TokenBaru();

        _jalurPerubahanData.Jalankan(
            connection,
            akun,
            waktu,
            aksi: "buat_token_pairing",
            entitas: "pairing",
            perubahan: transaction =>
            {
                _repositoriPairing.BatalkanAktifUntukSubjek(connection, transaction, subjekTipe, subjekId);

                // Tabrakan token acak (kolom token UNIQUE) sangat jarang
                // pada abjad+panjang ini, tapi dicoba ulang beberapa kali
                // alih-alih membiarkan SqliteException naik ke pemanggil.
                for (var percobaan = 0; percobaan < 5; percobaan++)
                {
                    try
                    {
                        _repositoriPairing.BuatToken(connection, transaction, subjekTipe, subjekId, token, waktu);
                        return null;
                    }
                    catch (SqliteException) when (percobaan < 4)
                    {
                        token = TokenBaru();
                    }
                }

                return null;
            },
            nilaiBaru: token);

        return token;
    }

    // PRD §16.6 langkah 4: "Admin mencetak QR massal per kelas". Berbeda
    // dari BuatTokenBaru — token yang sudah tercetak tapi belum dipindai
    // dipakai ulang (bukan diganti tiap kali tombol cetak ditekan), dan
    // siswa yang SUDAH tertaut dilewati sama sekali (mencetak ulang
    // otomatis memutus tautan yang sudah berhasil bukan tujuan tombol
    // ini — beda kasus dari "QR hilang" yang memang minta token baru
    // lewat BuatTokenBaru). Null berarti dilewati (sudah tertaut).
    public string? PastikanTokenUntukCetakMassal(SqliteConnection connection, AkunTersimpan akun, DateTime waktu, JenisPemilikKartu subjekTipe, int subjekId)
    {
        AuthorizationGuard.EnsureFullAuthority(akun, "mencetak QR pairing");

        if (_repositoriPairing.CariChatIdAktif(connection, subjekTipe, subjekId) is not null)
        {
            return null;
        }

        if (_repositoriPairing.CariTokenMenungguUntukSubjek(connection, subjekTipe, subjekId) is { } tokenAda)
        {
            return tokenAda;
        }

        return BuatTokenBaru(connection, akun, waktu, subjekTipe, subjekId);
    }

    // PRD §16.6: "Isi QR: t.me/<BotSekolah>?start=<token siswa>" — memakai
    // skema https:// eksplisit di sini (bukan hanya "t.me/...") supaya
    // pemindai kamera HP mana pun mengenalinya sebagai tautan, bukan teks
    // biasa; isi tautannya tetap identik dengan yang diminta PRD.
    public static string BuatUrlQr(string usernameBot, string token) => $"https://t.me/{usernameBot}?start={token}";

    private static string TokenBaru()
    {
        Span<char> buffer = stackalloc char[PanjangToken];
        for (var i = 0; i < PanjangToken; i++)
        {
            buffer[i] = AbjadToken[Random.Shared.Next(AbjadToken.Length)];
        }

        return new string(buffer);
    }
}
