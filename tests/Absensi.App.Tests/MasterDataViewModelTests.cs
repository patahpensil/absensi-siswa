using Absensi.App;
using Absensi.App.MasterData;
using Absensi.Data.Akun;
using Absensi.Data.Repositori;
using Absensi.Domain.Akun;

namespace Absensi.App.Tests;

// Tugas 7: hanya jalur yang tidak membuka dialog WPF (ShowDialog) yang
// diuji otomatis di sini — Impor Excel/Foto dan Nonaktifkan butuh jendela
// nyata, sama seperti keterbatasan pengujian layar gerbang di Tugas 6.
public sealed class MasterDataViewModelTests
{
    private static readonly DateTime Waktu = new(2026, 9, 17, 8, 0, 0);
    private static readonly AkunTersimpan Admin = new(1, "admin1", "x", PeranAkun.Admin, null, true);

    [Fact]
    public void SiswaViewModel_Tambah_MunculDiDaftar()
    {
        using var database = new MigratedDatabase();
        database.SeedSekolahUntukGerbang();
        database.TambahAkun(1, "admin1", "admin", guruId: null);
        var viewModel = new SiswaViewModel(database.Connection, Admin, new PenyediaWaktuUji(Waktu), new StatusModeRegistrasi(), tahunAjaranId: 1);
        var jumlahAwal = viewModel.Daftar.Count;

        viewModel.Nisn = "0099999999";
        viewModel.Nama = "Siswa Baru";
        viewModel.JenisKelamin = "L";
        viewModel.KelasTerpilih = viewModel.DaftarKelas.Single();
        viewModel.SimpanCommand.Execute(null);

        Assert.Equal(jumlahAwal + 1, viewModel.Daftar.Count);
        Assert.Contains(viewModel.Daftar, s => s.Nama == "Siswa Baru");
        Assert.Equal(string.Empty, viewModel.Nama);
    }

    [Fact]
    public void SiswaViewModel_TanpaKelasTerpilih_SimpanCommandTidakBisaDieksekusi()
    {
        using var database = new MigratedDatabase();
        database.SeedSekolahUntukGerbang();
        var viewModel = new SiswaViewModel(database.Connection, Admin, new PenyediaWaktuUji(Waktu), new StatusModeRegistrasi(), tahunAjaranId: 1)
        {
            Nisn = "0099999999",
            Nama = "Siswa Baru",
        };

        Assert.False(viewModel.SimpanCommand.CanExecute(null));
    }

    [Fact]
    public void GuruViewModel_Tambah_MunculDiDaftar()
    {
        using var database = new MigratedDatabase();
        database.SeedSekolahUntukGerbang();
        database.TambahAkun(1, "admin1", "admin", guruId: null);
        var viewModel = new GuruViewModel(database.Connection, Admin, new PenyediaWaktuUji(Waktu), new StatusModeRegistrasi(), tahunAjaranId: 1);
        var jumlahAwal = viewModel.Daftar.Count;

        viewModel.Nip = "999999";
        viewModel.Nama = "Guru Baru";
        viewModel.SimpanCommand.Execute(null);

        Assert.Equal(jumlahAwal + 1, viewModel.Daftar.Count);
        Assert.Contains(viewModel.Daftar, g => g.Nama == "Guru Baru");
    }

    [Fact]
    public void KelasViewModel_TambahLaluUbah_DaftarMencerminkanPerubahan()
    {
        using var database = new MigratedDatabase();
        database.SeedSekolahUntukGerbang();
        database.TambahAkun(1, "admin1", "admin", guruId: null);
        var viewModel = new KelasViewModel(database.Connection, Admin, new PenyediaWaktuUji(Waktu), tahunAjaranId: 1);

        viewModel.Nama = "9Z";
        viewModel.Tingkat = 9;
        viewModel.SimpanCommand.Execute(null);

        var baru = Assert.Single(viewModel.Daftar, k => k.Nama == "9Z");

        viewModel.EditCommand.Execute(baru);
        viewModel.Nama = "9Y";
        viewModel.SimpanCommand.Execute(null);

        Assert.Contains(viewModel.Daftar, k => k.Nama == "9Y");
        Assert.DoesNotContain(viewModel.Daftar, k => k.Nama == "9Z");
    }
}
