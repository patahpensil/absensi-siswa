using Absensi.App.Gerbang;

namespace Absensi.App.Tests;

// Koreksi Tugas 6, poin 3: dialog tutup layar gerbang dibatasi ke kepala
// sekolah dan admin — wali kelas ditolak walau sandinya benar.
public sealed class KonfirmasiTutupViewModelTests
{
    private static bool JalankanKonfirmasi(KonfirmasiTutupViewModel viewModel)
    {
        var diizinkan = false;
        viewModel.DiizinkanTutup += (_, _) => diizinkan = true;
        viewModel.KonfirmasiCommand.Execute(null);
        return diizinkan;
    }

    [Fact]
    public void Konfirmasi_KepalaSekolahSandiBenar_Diizinkan()
    {
        using var database = new MigratedDatabase();
        database.SeedSekolahUntukGerbang();
        database.TambahAkun(1, "kepsek", "kepala_sekolah", guruId: null);
        var viewModel = new KonfirmasiTutupViewModel(database.Connection)
        {
            Username = "kepsek",
        };
        viewModel.AturSandi(MigratedDatabase.SandiUji);

        Assert.True(JalankanKonfirmasi(viewModel));
        Assert.Null(viewModel.PesanGalat);
    }

    [Fact]
    public void Konfirmasi_AdminSandiBenar_Diizinkan()
    {
        using var database = new MigratedDatabase();
        database.SeedSekolahUntukGerbang();
        database.TambahAkun(1, "admin1", "admin", guruId: null);
        var viewModel = new KonfirmasiTutupViewModel(database.Connection)
        {
            Username = "admin1",
        };
        viewModel.AturSandi(MigratedDatabase.SandiUji);

        Assert.True(JalankanKonfirmasi(viewModel));
        Assert.Null(viewModel.PesanGalat);
    }

    [Fact]
    public void Konfirmasi_WaliKelasSandiBenar_TetapDitolak()
    {
        using var database = new MigratedDatabase();
        database.SeedSekolahUntukGerbang();
        database.TambahAkun(1, "wali_a", "wali_kelas", guruId: 1);
        var viewModel = new KonfirmasiTutupViewModel(database.Connection)
        {
            Username = "wali_a",
        };
        viewModel.AturSandi(MigratedDatabase.SandiUji);

        Assert.False(JalankanKonfirmasi(viewModel));
        Assert.Equal("Hanya kepala sekolah atau admin yang bisa menutup layar gerbang.", viewModel.PesanGalat);
    }

    [Fact]
    public void Konfirmasi_SandiSalah_Ditolak()
    {
        using var database = new MigratedDatabase();
        database.SeedSekolahUntukGerbang();
        database.TambahAkun(1, "kepsek", "kepala_sekolah", guruId: null);
        var viewModel = new KonfirmasiTutupViewModel(database.Connection)
        {
            Username = "kepsek",
        };
        viewModel.AturSandi("sandi-salah");

        Assert.False(JalankanKonfirmasi(viewModel));
        Assert.Equal("Username atau sandi salah.", viewModel.PesanGalat);
    }
}
