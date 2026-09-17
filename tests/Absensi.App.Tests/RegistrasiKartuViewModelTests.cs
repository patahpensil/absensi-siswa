using Absensi.App.RegistrasiKartu;
using Absensi.Data.Repositori;
using Absensi.Domain.Akun;

namespace Absensi.App.Tests;

// RENCANA-TUGAS.md Tugas 8, "Selesai bila": tidak ada satu pun baris
// absensi tertulis selama mode registrasi aktif — dicek lewat
// GerbangViewModelTests. Di sini: kursor per kelas, penolakan UID
// lintas kelas/master, duplikat dalam sesi yang sama, dan simpan atomik
// (PRD §16.5, dikonfirmasi pemilik proyek 2026-09-18).
public sealed class RegistrasiKartuViewModelTests
{
    private static readonly DateTime Waktu = new(2026, 9, 17, 8, 0, 0);
    private static readonly AkunTersimpan Admin = new(1, "admin1", "x", PeranAkun.Admin, null, true);

    private static RegistrasiKartuViewModel BuatViewModel(MigratedDatabase database, StatusModeRegistrasi? status = null) =>
        new(database.Connection, Admin, new PenyediaWaktuUji(Waktu), status ?? new StatusModeRegistrasi(), tahunAjaranId: 1);

    [Fact]
    public void Konstruksi_MengaktifkanModeRegistrasi()
    {
        using var database = new MigratedDatabase();
        database.SeedKelasUntukRegistrasiKartu();
        var status = new StatusModeRegistrasi();

        BuatViewModel(database, status);

        Assert.True(status.Aktif);
    }

    [Fact]
    public void KelasTerpilih_LompatKeSiswaPertamaYangBelumPunyaKartu()
    {
        using var database = new MigratedDatabase();
        database.SeedKelasUntukRegistrasiKartu();
        var viewModel = BuatViewModel(database);

        viewModel.KelasTerpilih = viewModel.DaftarKelas.Single(k => k.Nama == "7A");

        // Diurutkan per nama (RepositoriSiswa.DaftarUntukTahunAjaran, sama
        // seperti layar Siswa) — "Dua" < "Satu" secara alfabetis.
        Assert.Equal("Siswa Belum Dua", viewModel.SiswaSekarang?.Nama);
        Assert.Equal("7A: 1 dari 3 terdaftar", viewModel.ProgresTeks);
    }

    [Fact]
    public void TapUid_UidBebas_TerdaftarDanKursorLompatKeBerikutnya()
    {
        using var database = new MigratedDatabase();
        database.SeedKelasUntukRegistrasiKartu();
        var viewModel = BuatViewModel(database);
        viewModel.KelasTerpilih = viewModel.DaftarKelas.Single(k => k.Nama == "7A");

        viewModel.TapUid("UID-BARU-1");

        Assert.Equal("Siswa Belum Satu", viewModel.SiswaSekarang?.Nama);
        Assert.Equal("7A: 2 dari 3 terdaftar", viewModel.ProgresTeks);
        Assert.Null(viewModel.PesanGalat);
        Assert.True(viewModel.SimpanCommand.CanExecute(null));
    }

    [Fact]
    public void TapUid_UidSudahDipakaiKelasLain_DitolakDenganNamaDanKelasPemilik()
    {
        using var database = new MigratedDatabase();
        database.SeedKelasUntukRegistrasiKartu();
        var viewModel = BuatViewModel(database);
        viewModel.KelasTerpilih = viewModel.DaftarKelas.Single(k => k.Nama == "7A");
        var siswaSebelumTap = viewModel.SiswaSekarang;

        viewModel.TapUid("UID-KELAS-LAIN");

        Assert.Equal("Kartu ini sudah dipakai Siswa Kelas Lain di kelas 7B.", viewModel.PesanGalat);
        Assert.Equal(siswaSebelumTap, viewModel.SiswaSekarang);
        Assert.False(viewModel.SimpanCommand.CanExecute(null));
    }

    [Fact]
    public void TapUid_UidSudahDipakaiSesiIni_DitolakTanpaMenyentuhDatabase()
    {
        using var database = new MigratedDatabase();
        database.SeedKelasUntukRegistrasiKartu();
        var viewModel = BuatViewModel(database);
        viewModel.KelasTerpilih = viewModel.DaftarKelas.Single(k => k.Nama == "7A");

        viewModel.TapUid("UID-BARU-1");
        var siswaSetelahTapPertama = viewModel.SiswaSekarang;
        viewModel.TapUid("UID-BARU-1");

        Assert.Equal("Kartu ini sudah dipakai Siswa Belum Dua pada sesi ini.", viewModel.PesanGalat);
        Assert.Equal(siswaSetelahTapPertama, viewModel.SiswaSekarang);
    }

    [Fact]
    public void Simpan_MenyimpanSeluruhSesiDanMematikanModeRegistrasi()
    {
        using var database = new MigratedDatabase();
        database.SeedKelasUntukRegistrasiKartu();
        var status = new StatusModeRegistrasi();
        var viewModel = BuatViewModel(database, status);
        viewModel.KelasTerpilih = viewModel.DaftarKelas.Single(k => k.Nama == "7A");
        viewModel.TapUid("UID-BARU-1");
        viewModel.TapUid("UID-BARU-2");

        var selesaiDipicu = false;
        viewModel.Selesai += (_, _) => selesaiDipicu = true;
        viewModel.SimpanCommand.Execute(null);
        viewModel.MatikanModeRegistrasi();

        Assert.True(selesaiDipicu);
        Assert.False(status.Aktif);

        using var command = database.Connection.CreateCommand();
        command.CommandText = "SELECT COUNT(*) FROM kartu WHERE uid IN ('UID-BARU-1', 'UID-BARU-2') AND aktif = 1;";
        Assert.Equal(2, Convert.ToInt32(command.ExecuteScalar()));
    }

    [Fact]
    public void Batal_TidakMenyimpanApaPunKeDatabase()
    {
        using var database = new MigratedDatabase();
        database.SeedKelasUntukRegistrasiKartu();
        var viewModel = BuatViewModel(database);
        viewModel.KelasTerpilih = viewModel.DaftarKelas.Single(k => k.Nama == "7A");
        viewModel.TapUid("UID-BARU-1");

        viewModel.Batal();

        using var command = database.Connection.CreateCommand();
        command.CommandText = "SELECT COUNT(*) FROM kartu WHERE uid = 'UID-BARU-1';";
        Assert.Equal(0, Convert.ToInt32(command.ExecuteScalar()));
    }
}
