using System.Security.Cryptography;

namespace Absensi.Data.Akun;

// PBKDF2-HMAC-SHA256 bawaan .NET (System.Security.Cryptography) — tidak ada
// pustaka pihak ketiga yang ditambahkan (CLAUDE.md §2). Format tersimpan
// "pbkdf2-sha256$<iterasi>$<salt base64>$<hash base64>" bersifat
// self-describing supaya jumlah iterasi bisa dinaikkan nanti tanpa merusak
// hash lama yang sudah tersimpan.
public static class HashSandi
{
    private const string Algoritme = "pbkdf2-sha256";
    private const int JumlahIterasiBawaan = 100_000;
    private const int PanjangSaltByte = 16;
    private const int PanjangHashByte = 32;

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

        var iterasi = int.Parse(bagian[1]);
        var salt = Convert.FromBase64String(bagian[2]);
        var hashDiharapkan = Convert.FromBase64String(bagian[3]);

        var hashDihitung = Rfc2898DeriveBytes.Pbkdf2(sandi, salt, iterasi, HashAlgorithmName.SHA256, hashDiharapkan.Length);

        return CryptographicOperations.FixedTimeEquals(hashDihitung, hashDiharapkan);
    }
}
