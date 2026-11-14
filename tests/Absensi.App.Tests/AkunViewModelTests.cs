using Absensi.App.Pengaturan;
using Absensi.Data.Repositori;
using Absensi.Domain.Akun;

namespace Absensi.App.Tests;

// RENCANA-TUGAS.md Tugas 14 — layar Manajemen Akun (Pengaturan → Akun).
// TambahAkun/ResetSandi membuka Window.ShowDialog() sungguhan (pola sama
// seperti OpenFileDialog di ImporSiswaWizardViewModel) — tidak diuji
// langsung di sini, hanya state yang bisa diuji tanpa membuka jendela.
public sealed class AkunViewModelTests
{
    private static readonly DateTime Waktu = new(2026, 9, 19, 8, 0, 0);
    private static readonly AkunTersimpan Kepsek = new(1, "admin1", "x", PeranAkun.KepalaSekolah, null, true);
    private static readonly AkunTersimpan WaliKelas = new(2, "wali_a", "x", PeranAkun.WaliKelas, 1, true);

    // SeedKelasUntukRegistrasiKartu sudah membuat akun id=1 "admin1"
    // (peran admin) — di sini diperlakukan sebagai Kepala Sekolah lewat
    // AkunTersimpan bikinan sendiri (pola sama seperti tes lain di
    // berkas ini) supaya BolehAkses/dropdown Peran diuji dari sudut
    // pandang kewenangan penuh, bukan bergantung id akun sungguhan.
    private static MigratedDatabase BuatDatabaseDenganWaliKelas()
    {
        var database = new MigratedDatabase();
        database.SeedKelasUntukRegistrasiKartu();
        database.Execute("INSERT INTO guru (id, nip, nama) VALUES (1, '111', 'Guru Satu');");
        database.TambahAkun(2, "wali_a", "wali_kelas", guruId: 1);
        return database;
    }

    [Fact]
    public void Konstruksi_OlehWaliKelas_TidakBolehAksesDanDaftarKosong()
    {
        using var database = BuatDatabaseDenganWaliKelas();

        var viewModel = new AkunViewModel(database.Connection, WaliKelas, new PenyediaWaktuUji(Waktu));

        Assert.False(viewModel.BolehAkses);
        Assert.Empty(viewModel.DaftarAkun);
        Assert.False(viewModel.TambahAkunCommand.CanExecute(null));
    }

    [Fact]
    public void Konstruksi_OlehKepsek_DaftarAkunTerisi()
    {
        using var database = BuatDatabaseDenganWaliKelas();

        var viewModel = new AkunViewModel(database.Connection, Kepsek, new PenyediaWaktuUji(Waktu));

        Assert.True(viewModel.BolehAkses);
        Assert.Contains(viewModel.DaftarAkun, a => a.Username == "admin1");
        Assert.Contains(viewModel.DaftarAkun, a => a.Username == "wali_a" && a.GuruTerkait == "Guru Satu");
        Assert.True(viewModel.TambahAkunCommand.CanExecute(null));
    }

    [Fact]
    public void Nonaktifkan_OlehKepsek_MenandaiTidakAktifDanMuatUlangDaftar()
    {
        using var database = BuatDatabaseDenganWaliKelas();
        var viewModel = new AkunViewModel(database.Connection, Kepsek, new PenyediaWaktuUji(Waktu));
        var waliA = viewModel.DaftarAkun.Single(a => a.Username == "wali_a");

        viewModel.NonaktifkanCommand.Execute(waliA);

        Assert.Equal("Akun wali_a dinonaktifkan.", viewModel.PesanStatus);
        Assert.False(viewModel.DaftarAkun.Single(a => a.Username == "wali_a").Aktif);
    }

    [Fact]
    public void NonaktifkanCommand_OlehWaliKelas_TidakBisaDieksekusi()
    {
        using var database = BuatDatabaseDenganWaliKelas();
        var viewModel = new AkunViewModel(database.Connection, WaliKelas, new PenyediaWaktuUji(Waktu));

        Assert.False(viewModel.NonaktifkanCommand.CanExecute(null));
        Assert.False(viewModel.ResetSandiCommand.CanExecute(null));
    }

    // P-21 (revised): the row offers Reset sandi and Nonaktifkan only when the
    // service would accept them, the same rule as the web screen.
    [Fact]
    public void CanManage_FollowsP21_ForHeadAndAdmin()
    {
        using var database = BuatDatabaseDenganWaliKelas();
        var admin = new AkunTersimpan(3, "admin2", "x", PeranAkun.Admin, null, true);

        var head = new AkunViewModel(database.Connection, Kepsek, new PenyediaWaktuUji(Waktu));
        Assert.False(head.DaftarAkun.Single(a => a.Username == "admin1").CanManage);
        Assert.True(head.DaftarAkun.Single(a => a.Username == "wali_a").CanManage);

        var byAdmin = new AkunViewModel(database.Connection, admin, new PenyediaWaktuUji(Waktu));
        Assert.False(byAdmin.DaftarAkun.Single(a => a.Username == "admin1").CanManage);
        Assert.True(byAdmin.DaftarAkun.Single(a => a.Username == "wali_a").CanManage);

        head.NonaktifkanCommand.Execute(head.DaftarAkun.Single(a => a.Username == "wali_a"));
        Assert.False(head.DaftarAkun.Single(a => a.Username == "wali_a").CanManage);
    }
}
