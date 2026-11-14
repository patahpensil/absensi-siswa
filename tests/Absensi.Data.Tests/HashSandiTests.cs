using Absensi.Data.Akun;

namespace Absensi.Data.Tests;

public sealed class HashSandiTests
{
    [Fact]
    public void Verifikasi_SandiBenar_MengembalikanTrue()
    {
        var hash = HashSandi.Buat("Rahasia#2026");

        Assert.True(HashSandi.Verifikasi("Rahasia#2026", hash));
    }

    [Fact]
    public void Verifikasi_SandiSalah_MengembalikanFalse()
    {
        var hash = HashSandi.Buat("Rahasia#2026");

        Assert.False(HashSandi.Verifikasi("SandiLain!", hash));
    }

    [Fact]
    public void Buat_DipanggilDuaKaliDenganSandiSama_MenghasilkanHashBerbeda()
    {
        var hashPertama = HashSandi.Buat("Rahasia#2026");
        var hashKedua = HashSandi.Buat("Rahasia#2026");

        Assert.NotEqual(hashPertama, hashKedua);
    }

    [Theory]
    [InlineData("pbkdf2-sha256$abc$AAAA$AAAA")]
    [InlineData("pbkdf2-sha256$0$AAAA$AAAA")]
    [InlineData("pbkdf2-sha256$-5$AAAA$AAAA")]
    [InlineData("pbkdf2-sha256$999999999$AAAA$AAAA")]
    [InlineData("pbkdf2-sha256$100000$bukan-base64!$AAAA")]
    [InlineData("pbkdf2-sha256$100000$AAAA$bukan-base64!")]
    [InlineData("pbkdf2-sha256$100000$$")]
    [InlineData("")]
    public void Verifikasi_HashRusak_MengembalikanFalseTanpaException(string hashRusak)
    {
        Assert.False(HashSandi.Verifikasi("Rahasia#2026", hashRusak));
    }
}
