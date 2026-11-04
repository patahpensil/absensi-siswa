using System.Security.Cryptography;
using System.Text;

namespace Absensi.Data.Backup;

// PRD §18.2: "Berkas dienkripsi sebelum diunggah, karena memuat data ribuan
// siswa". Kunci enkripsi diturunkan dari kunci lisensi sekolah lewat
// PBKDF2 (pola sama seperti Absensi.Data.Akun.HashSandi) — bukan nilai
// tertanam di kode (PRD §1/§15), dan sekaligus mengikuti PRD
// §18.3: "kunci lisensi sekolah menjadi penanda backup mana yang miliknya".
public static class EnkripsiBackup
{
    private const int JumlahIterasi = 100_000;
    private const int PanjangKunciByte = 32;

    // Garam tetap: bukan rahasia, sekadar pemisah domain PBKDF2 (supaya
    // turunan kunci backup tidak kebetulan sama dengan turunan lain dari
    // string yang sama). Keamanan bergantung pada kerahasiaan kunci
    // lisensi, bukan pada nilai garam ini.
    private static readonly byte[] Garam = Encoding.UTF8.GetBytes("absensi-backup-v1");

    public static byte[] Enkripsi(byte[] data, string kunciLisensi)
    {
        var kunci = TurunkanKunci(kunciLisensi);
        var nonce = RandomNumberGenerator.GetBytes(AesGcm.NonceByteSizes.MaxSize);
        var tag = new byte[AesGcm.TagByteSizes.MaxSize];
        var cipherText = new byte[data.Length];

        using var aes = new AesGcm(kunci, tag.Length);
        aes.Encrypt(nonce, data, cipherText, tag);

        var paket = new byte[nonce.Length + tag.Length + cipherText.Length];
        Buffer.BlockCopy(nonce, 0, paket, 0, nonce.Length);
        Buffer.BlockCopy(tag, 0, paket, nonce.Length, tag.Length);
        Buffer.BlockCopy(cipherText, 0, paket, nonce.Length + tag.Length, cipherText.Length);
        return paket;
    }

    public static byte[] Dekripsi(byte[] paket, string kunciLisensi)
    {
        var kunci = TurunkanKunci(kunciLisensi);
        var panjangNonce = AesGcm.NonceByteSizes.MaxSize;
        var panjangTag = AesGcm.TagByteSizes.MaxSize;

        var nonce = paket[..panjangNonce];
        var tag = paket[panjangNonce..(panjangNonce + panjangTag)];
        var cipherText = paket[(panjangNonce + panjangTag)..];
        var plainText = new byte[cipherText.Length];

        using var aes = new AesGcm(kunci, panjangTag);
        aes.Decrypt(nonce, cipherText, tag, plainText);
        return plainText;
    }

    private static byte[] TurunkanKunci(string kunciLisensi) =>
        Rfc2898DeriveBytes.Pbkdf2(kunciLisensi, Garam, JumlahIterasi, HashAlgorithmName.SHA256, PanjangKunciByte);
}
