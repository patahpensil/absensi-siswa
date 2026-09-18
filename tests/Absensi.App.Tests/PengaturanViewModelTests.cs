using System.IO;
using Absensi.App.Pengaturan;
using Absensi.Data.Backup;
using Absensi.Data.Repositori;
using Absensi.Domain.Akun;

namespace Absensi.App.Tests;

// RENCANA-TUGAS.md Tugas 9 — bagian jadwal harian dari layar Pengaturan (PRD §7).
public sealed class PengaturanViewModelTests
{
    private static readonly DateTime Waktu = new(2026, 9, 18, 8, 0, 0);
    private static readonly AkunTersimpan Admin = new(1, "admin1", "x", PeranAkun.Admin, null, true);
    private static readonly AkunTersimpan WaliKelas = new(2, "wali_a", "x", PeranAkun.WaliKelas, 1, true);

    // Backup (RENCANA-TUGAS.md Tugas 13) bukan fokus pengujian di berkas
    // ini — folder sementara acak supaya tidak saling bertabrakan.
    private static LayananBackup BuatLayananBackup() => new(
        Path.Combine(Path.GetTempPath(), $"absensi-test-backup-{Guid.NewGuid():N}"),
        new PenyimpanCloudBackupLokal(Path.Combine(Path.GetTempPath(), $"absensi-test-cloud-{Guid.NewGuid():N}")));

    private static PengaturanViewModel BuatViewModel(MigratedDatabase database, AkunTersimpan? akun = null) =>
        new(database.Connection, akun ?? Admin, new PenyediaWaktuUji(Waktu), tahunAjaranId: 1, BuatLayananBackup());

    // PRD §3: pengaturan adalah kewenangan penuh — wali kelas tidak bisa
    // menyimpan jadwal harian.
    [Fact]
    public void WaliKelas_SimpanJadwalCommandTidakBisaDieksekusi()
    {
        using var database = new MigratedDatabase();
        database.SeedKelasUntukRegistrasiKartu();
        var viewModel = BuatViewModel(database, WaliKelas);

        Assert.False(viewModel.SimpanJadwalCommand.CanExecute(null));
        Assert.False(viewModel.BolehMengubah);
    }

    [Fact]
    public void Konstruksi_BelumPernahDisimpan_TujuhBarisDenganNamaHariBenar()
    {
        using var database = new MigratedDatabase();
        database.SeedKelasUntukRegistrasiKartu();
        var viewModel = BuatViewModel(database);

        Assert.Equal(7, viewModel.JadwalHarian.Count);
        Assert.Equal("Senin", viewModel.JadwalHarian[0].NamaHari);
        Assert.Equal("Minggu", viewModel.JadwalHarian[6].NamaHari);
        Assert.False(viewModel.JadwalHarian[6].HariSekolah);
    }

    [Fact]
    public void SimpanJadwalCommand_NilaiValid_Tersimpan()
    {
        using var database = new MigratedDatabase();
        database.SeedKelasUntukRegistrasiKartu();
        var viewModel = BuatViewModel(database);
        viewModel.JadwalHarian[4].BatasTapMasuk = "10:00";
        viewModel.JadwalHarian[4].MulaiPulang = "11:30";

        viewModel.SimpanJadwalCommand.Execute(null);

        Assert.Equal("Jadwal harian tersimpan.", viewModel.PesanStatus);
        Assert.Null(viewModel.PesanGalat);

        var tersimpan = new RepositoriJadwalHari().DaftarUntukTahunAjaran(database.Connection, tahunAjaranId: 1);
        var jumat = tersimpan.Single(h => h.Hari == 5);
        Assert.Equal(new TimeOnly(10, 0), jumat.BatasTapMasuk);
    }

    [Fact]
    public void SimpanJadwalCommand_FormatJamTidakValid_DitolakDenganPesanGalatMenyebutHari()
    {
        using var database = new MigratedDatabase();
        database.SeedKelasUntukRegistrasiKartu();
        var viewModel = BuatViewModel(database);
        viewModel.JadwalHarian[0].JamMasuk = "bukan-jam";

        viewModel.SimpanJadwalCommand.Execute(null);

        Assert.Contains("Senin", viewModel.PesanGalat);
        Assert.Null(viewModel.PesanStatus);

        using var command = database.Connection.CreateCommand();
        command.CommandText = "SELECT COUNT(*) FROM jadwal_hari WHERE tahun_ajaran_id = 1;";
        Assert.Equal(0, Convert.ToInt32(command.ExecuteScalar()));
    }

    [Fact]
    public void SimpanJadwalCommand_JamKosong_Diizinkan()
    {
        using var database = new MigratedDatabase();
        database.SeedKelasUntukRegistrasiKartu();
        var viewModel = BuatViewModel(database);
        viewModel.JadwalHarian[6].HariSekolah = false;
        viewModel.JadwalHarian[6].JamMasuk = null;

        viewModel.SimpanJadwalCommand.Execute(null);

        Assert.Equal("Jadwal harian tersimpan.", viewModel.PesanStatus);
    }

    // RENCANA-TUGAS.md Tugas 10 — PRD §16.6 langkah 2.
    [Fact]
    public void SimpanBotCommand_TersimpanDanBisaDibacaUlang()
    {
        using var database = new MigratedDatabase();
        database.SeedKelasUntukRegistrasiKartu();
        var viewModel = BuatViewModel(database);
        viewModel.TokenBot = "123456:ABCDEF";
        viewModel.UsernameBot = "BotSekolahX";

        viewModel.SimpanBotCommand.Execute(null);

        Assert.Equal("Pengaturan bot Telegram tersimpan.", viewModel.PesanStatusBot);
        var viewModelBaru = BuatViewModel(database);
        Assert.Equal("123456:ABCDEF", viewModelBaru.TokenBot);
        Assert.Equal("BotSekolahX", viewModelBaru.UsernameBot);
    }

    [Fact]
    public void WaliKelas_SimpanBotCommandTidakBisaDieksekusi()
    {
        using var database = new MigratedDatabase();
        database.SeedKelasUntukRegistrasiKartu();
        var viewModel = BuatViewModel(database, WaliKelas);

        Assert.False(viewModel.SimpanBotCommand.CanExecute(null));
    }

    // RENCANA-TUGAS.md Tugas 13 — PRD §18.4 langkah 3: daftar menampilkan
    // tanggal dan hari data yang akan hilang untuk tiap berkas.
    [Fact]
    public async Task Konstruksi_AdaBerkasBackupLokal_TampilDenganHariDataHilangBenar()
    {
        using var database = new MigratedDatabase();
        database.SeedKelasUntukRegistrasiKartu();
        var layananBackup = BuatLayananBackup();
        await layananBackup.JalankanAsync(database.Connection, new DateTime(2026, 9, 10, 6, 0, 0), kunciLisensiAktif: null);

        var viewModel = new PengaturanViewModel(database.Connection, Admin, new PenyediaWaktuUji(Waktu), tahunAjaranId: 1, layananBackup);

        Assert.Single(viewModel.DaftarBackup);
        Assert.Equal(8, viewModel.DaftarBackup[0].HariDataHilang);
    }

    [Fact]
    public void WaliKelas_RestoreCommandTidakBisaDieksekusi()
    {
        using var database = new MigratedDatabase();
        database.SeedKelasUntukRegistrasiKartu();
        var viewModel = BuatViewModel(database, WaliKelas);

        Assert.False(viewModel.RestoreCommand.CanExecute(null));
    }
}
