using Absensi.Data.Lisensi;

namespace Absensi.Data.Tests;

public sealed class LayananLisensiLokalTests
{
    private readonly LayananLisensiLokal _layanan = new();

    [Fact]
    public void Aktifkan_KunciDiisi_Berhasil()
    {
        var hasil = _layanan.Aktifkan("SEKOLAH-001-ABCD");

        Assert.True(hasil.Berhasil);
        Assert.Null(hasil.PesanGalat);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(null)]
    public void Aktifkan_KunciKosong_Ditolak(string? kunci)
    {
        var hasil = _layanan.Aktifkan(kunci!);

        Assert.False(hasil.Berhasil);
        Assert.NotNull(hasil.PesanGalat);
    }
}
