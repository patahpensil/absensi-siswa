using Absensi.App.Gerbang;

namespace Absensi.App.Tests;

// Koreksi Tugas 6, poin 1: header layar gerbang (nama sekolah + logo,
// PRD §14.1) diambil saat CacheTampilanGerbang.Muat().
public sealed class CacheTampilanGerbangTests
{
    [Fact]
    public void Muat_TanpaBarisSekolah_NamaSekolahFallbackKePlaceholder()
    {
        using var database = new MigratedDatabase();
        database.SeedSekolahUntukGerbang();

        var cache = CacheTampilanGerbang.Muat(database.Connection);

        Assert.Equal("Nama sekolah belum diatur", cache.NamaSekolah);
        Assert.Null(cache.Logo);
    }

    [Fact]
    public void Muat_DenganBarisSekolah_NamaSekolahTerisi()
    {
        using var database = new MigratedDatabase();
        database.SeedSekolahUntukGerbang();
        database.TambahSekolah("SMP Negeri Uji Coba");

        var cache = CacheTampilanGerbang.Muat(database.Connection);

        Assert.Equal("SMP Negeri Uji Coba", cache.NamaSekolah);
    }

    [Fact]
    public void Muat_LogoPathTidakAda_LogoNullBukanGalat()
    {
        using var database = new MigratedDatabase();
        database.SeedSekolahUntukGerbang();
        database.TambahSekolah("SMP Negeri Uji Coba");
        database.Execute("UPDATE sekolah SET logo_path = 'berkas-tidak-ada.png' WHERE id = 1;");

        var cache = CacheTampilanGerbang.Muat(database.Connection);

        Assert.Null(cache.Logo);
    }
}
