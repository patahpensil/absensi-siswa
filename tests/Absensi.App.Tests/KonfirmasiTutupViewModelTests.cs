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

    // UAT 2026-09-22: mengonfirmasi keadaan awal dialog yang menopang
    // perbaikan layar gerbang — tombol "Tutup layar gerbang" harus lahir
    // nonaktif (Username/Sandi kosong) dan pesan galat harus lahir kosong
    // (TextBlock-nya baru tampil lewat NullKeVisibilitasConverter saat
    // PesanGalat terisi, lihat KonfirmasiTutupWindow.xaml).
    [Fact]
    public void KonfirmasiCommand_Awal_TidakBisaDieksekusi()
    {
        using var database = new MigratedDatabase();
        database.SeedSekolahUntukGerbang();
        var viewModel = new KonfirmasiTutupViewModel(database.Connection);

        Assert.False(viewModel.KonfirmasiCommand.CanExecute(null));
        Assert.Null(viewModel.PesanGalat);
    }

    [Fact]
    public void KonfirmasiCommand_HanyaUsernameTerisi_TidakBisaDieksekusi()
    {
        using var database = new MigratedDatabase();
        database.SeedSekolahUntukGerbang();
        var viewModel = new KonfirmasiTutupViewModel(database.Connection)
        {
            Username = "kepsek",
        };

        Assert.False(viewModel.KonfirmasiCommand.CanExecute(null));
    }

    [Fact]
    public void KonfirmasiCommand_UsernameDanSandiTerisi_BisaDieksekusi()
    {
        using var database = new MigratedDatabase();
        database.SeedSekolahUntukGerbang();
        var viewModel = new KonfirmasiTutupViewModel(database.Connection)
        {
            Username = "kepsek",
        };
        viewModel.AturSandi("apa-saja");

        Assert.True(viewModel.KonfirmasiCommand.CanExecute(null));
    }
}
