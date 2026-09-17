using Absensi.Data.Akun;

namespace Absensi.Data.Tests;

public sealed class HashSandiTests
{
    [Fact]
    public void Verifikasi_SandiBenar_MengembalikanTrue()
    {
        var hash = HashSandi.Buat("Rahasia123!");

        Assert.True(HashSandi.Verifikasi("Rahasia123!", hash));
    }

    [Fact]
    public void Verifikasi_SandiSalah_MengembalikanFalse()
    {
        var hash = HashSandi.Buat("Rahasia123!");

        Assert.False(HashSandi.Verifikasi("SandiLain!", hash));
    }

    [Fact]
    public void Buat_DipanggilDuaKaliDenganSandiSama_MenghasilkanHashBerbeda()
    {
        var hashPertama = HashSandi.Buat("Rahasia123!");
        var hashKedua = HashSandi.Buat("Rahasia123!");

        Assert.NotEqual(hashPertama, hashKedua);
    }
}
