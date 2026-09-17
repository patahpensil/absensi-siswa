using Absensi.Domain.Akun;

namespace Absensi.Domain.Tests;

public sealed class PenentuAksesKelasTests
{
    private readonly PenentuAksesKelas _penentu = new();

    [Fact]
    public void KepalaSekolah_BolehKelasApaPun()
    {
        Assert.True(_penentu.BolehMenyentuh(PeranAkun.KepalaSekolah, guruIdAkun: null, waliGuruIdKelas: 99));
    }

    [Fact]
    public void Admin_BolehKelasApaPun()
    {
        Assert.True(_penentu.BolehMenyentuh(PeranAkun.Admin, guruIdAkun: null, waliGuruIdKelas: 99));
    }

    [Fact]
    public void WaliKelas_KelasSendiri_Boleh()
    {
        Assert.True(_penentu.BolehMenyentuh(PeranAkun.WaliKelas, guruIdAkun: 5, waliGuruIdKelas: 5));
    }

    [Fact]
    public void WaliKelas_KelasLain_Ditolak()
    {
        Assert.False(_penentu.BolehMenyentuh(PeranAkun.WaliKelas, guruIdAkun: 5, waliGuruIdKelas: 6));
    }

    [Fact]
    public void WaliKelas_TanpaGuruIdTertaut_Ditolak()
    {
        Assert.False(_penentu.BolehMenyentuh(PeranAkun.WaliKelas, guruIdAkun: null, waliGuruIdKelas: 5));
    }
}
