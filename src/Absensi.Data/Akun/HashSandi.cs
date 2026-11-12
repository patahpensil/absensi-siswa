using System.Security.Cryptography;

namespace Absensi.Data.Akun;

// PBKDF2-HMAC-SHA256 bawaan .NET (System.Security.Cryptography) — tidak ada
// pustaka pihak ketiga yang ditambahkan (PRD §24.3). Format tersimpan
// "pbkdf2-sha256$<iterasi>$<salt base64>$<hash base64>" bersifat
// self-describing supaya jumlah iterasi bisa dinaikkan nanti tanpa merusak
// hash lama yang sudah tersimpan.
public static class HashSandi
{
    private const string Algoritme = "pbkdf2-sha256";
    private const int JumlahIterasiBawaan = 100_000;
    private const int PanjangSaltByte = 16;
    private const int PanjangHashByte = 32;
    private const int BatasIterasi = 10_000_000;

    public static string Buat(string sandi)
    {
        var salt = RandomNumberGenerator.GetBytes(PanjangSaltByte);
        var hash = Rfc2898DeriveBytes.Pbkdf2(sandi, salt, JumlahIterasiBawaan, HashAlgorithmName.SHA256, PanjangHashByte);

        return $"{Algoritme}${JumlahIterasiBawaan}${Convert.ToBase64String(salt)}${Convert.ToBase64String(hash)}";
    }

    public static bool Verifikasi(string sandi, string hashTersimpan)
    {
        var bagian = hashTersimpan.Split('$');
        if (bagian.Length != 4 || bagian[0] != Algoritme)
        {
            return false;
        }

        // Hash rusak atau tidak wajar (data dipulihkan dari backup lama, diubah
        // manual) ditolak sebagai sandi salah, bukan exception yang
        // menghentikan login. Batas atas iterasi mencegah nilai raksasa
        // membuat login macet.
        if (!int.TryParse(bagian[1], System.Globalization.NumberStyles.None, System.Globalization.CultureInfo.InvariantCulture, out var iterasi)
            || iterasi is < 1 or > BatasIterasi)
        {
            return false;
        }

        byte[] salt, hashDiharapkan;
        try
        {
            salt = Convert.FromBase64String(bagian[2]);
            hashDiharapkan = Convert.FromBase64String(bagian[3]);
        }
        catch (FormatException)
        {
            return false;
        }

        if (salt.Length == 0 || hashDiharapkan.Length == 0)
        {
            return false;
        }

        var hashDihitung = Rfc2898DeriveBytes.Pbkdf2(sandi, salt, iterasi, HashAlgorithmName.SHA256, hashDiharapkan.Length);

        return CryptographicOperations.FixedTimeEquals(hashDihitung, hashDiharapkan);
    }
}
