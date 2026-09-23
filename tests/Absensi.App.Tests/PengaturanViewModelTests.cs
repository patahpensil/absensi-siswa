using System.IO;
using Absensi.App.Pengaturan;
using Absensi.Data;
using Absensi.Data.Backup;
using Absensi.Data.Repositori;
using Absensi.Domain.Akun;
using Absensi.Domain.Kehadiran;
using Microsoft.Data.Sqlite;

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

        // Sejak jam wajib untuk hari sekolah, seluruh baris HariSekolah=true
        // (indeks 0-5, bawaan MuatJadwal saat belum pernah disimpan) harus
        // diisi — bukan cuma Jumat seperti sebelumnya.
        foreach (var indeks in new[] { 0, 1, 2, 3, 5 })
        {
            viewModel.JadwalHarian[indeks].JamMasuk = "07:30";
            viewModel.JadwalHarian[indeks].JamRekap = "08:00";
            viewModel.JadwalHarian[indeks].BatasTapMasuk = "12:00";
            viewModel.JadwalHarian[indeks].MulaiPulang = "13:10";
        }

        viewModel.JadwalHarian[4].JamMasuk = "07:30";
        viewModel.JadwalHarian[4].JamRekap = "08:00";
        viewModel.JadwalHarian[4].BatasTapMasuk = "10:00";
        viewModel.JadwalHarian[4].MulaiPulang = "11:30";

        viewModel.SimpanJadwalCommand.Execute(null);

        Assert.Equal("Jadwal harian tersimpan.", viewModel.PesanStatus);
        Assert.Null(viewModel.PesanGalat);

        var tersimpan = new RepositoriJadwalHari().DaftarUntukTahunAjaran(database.Connection, tahunAjaranId: 1);
        var jumat = tersimpan.Single(h => h.Hari == 5);
        Assert.Equal(new TimeOnly(10, 0), jumat.BatasTapMasuk);
    }

    // UAT 2026-09-23: sebelumnya kosong diterima sebagai valid untuk hari
    // sekolah, dikombinasikan dengan grid yang saat itu tidak bisa diedit
    // sama sekali — Simpan menulis NULL ke jadwal_hari sambil TETAP
    // menampilkan "Jadwal harian tersimpan." seolah berhasil. Test ini
    // mengunci: hari sekolah dengan jam kosong ditolak, pesan menyebut hari
    // DAN kolom yang salah, pesan sukses TIDAK tampil, dan tidak ada baris
    // yang tertulis ke database sama sekali.
    [Fact]
    public void SimpanJadwalCommand_HariSekolahJamKosong_DitolakSebutkanHariDanKolomTanpaPesanSukses()
    {
        using var database = new MigratedDatabase();
        database.SeedKelasUntukRegistrasiKartu();
        var viewModel = BuatViewModel(database);
        // Senin (indeks 0) dibiarkan kosong — HariSekolah bawaan true saat
        // belum pernah disimpan (RepositoriJadwalHari.DaftarUntukTahunAjaran).

        viewModel.SimpanJadwalCommand.Execute(null);

        Assert.Null(viewModel.PesanStatus);
        Assert.NotEqual("Jadwal harian tersimpan.", viewModel.PesanStatus);
        Assert.Contains("Senin", viewModel.PesanGalat);
        Assert.Contains("jam masuk", viewModel.PesanGalat);
        Assert.Contains("wajib diisi", viewModel.PesanGalat);

        using var command = database.Connection.CreateCommand();
        command.CommandText = "SELECT COUNT(*) FROM jadwal_hari WHERE tahun_ajaran_id = 1;";
        Assert.Equal(0, Convert.ToInt32(command.ExecuteScalar()));
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

    // Audit Bug #11 (2026-09-23): pada instalasi yang sudah lewat wizard
    // (tidak bisa dijalankan ulang), Pengaturan → Jadwal Harian adalah
    // SATU-SATUNYA jalur mengisi jam yang hilang. Test ini membuktikan
    // jalur itu benar ujung ke ujung memakai DATABASE TEST TERPISAH (bukan
    // UAT): isi jadwal penuh satu minggu sesuai PRD §7 lewat
    // PengaturanViewModel → Simpan → tutup KONEKSI SEPENUHNYA (bukan cuma
    // ViewModel baru di koneksi yang sama) → baca ulang lewat KONEKSI BARU
    // ke berkas yang sama (pola sama seperti durabilitas
    // WizardInstalasiViewModelTests) → lalu buktikan PemrosesTap sungguhan
    // TIDAK LAGI menolak "JadwalBelumDiatur" untuk tap masuk pagi, begitu
    // jadwal ini tersimpan.
    [Fact]
    public void SimpanJadwalCommand_JadwalPenuhSatuMinggu_TersimpanUntukTahunAjaranAktifBertahanSetelahDibukaUlangDanMengizinkanTapMasuk()
    {
        using var database = new MigratedDatabase();
        database.SeedKelasUntukRegistrasiKartu();
        var viewModel = BuatViewModel(database);

        // PRD §7: Senin-Kamis & Sabtu (indeks 0,1,2,3,5) sama; Jumat
        // (indeks 4) beda batas tap dan mulai pulang; Minggu (indeks 6)
        // tetap kosong — sudah HariSekolah=false bawaan, tidak disentuh.
        foreach (var indeks in new[] { 0, 1, 2, 3, 5 })
        {
            viewModel.JadwalHarian[indeks].JamMasuk = "07:30";
            viewModel.JadwalHarian[indeks].JamRekap = "08:00";
            viewModel.JadwalHarian[indeks].BatasTapMasuk = "12:00";
            viewModel.JadwalHarian[indeks].MulaiPulang = "13:10";
        }

        viewModel.JadwalHarian[4].JamMasuk = "07:30";
        viewModel.JadwalHarian[4].JamRekap = "08:00";
        viewModel.JadwalHarian[4].BatasTapMasuk = "10:00";
        viewModel.JadwalHarian[4].MulaiPulang = "11:30";

        viewModel.SimpanJadwalCommand.Execute(null);

        Assert.Equal("Jadwal harian tersimpan.", viewModel.PesanStatus);
        Assert.Null(viewModel.PesanGalat);

        var pathBerkas = database.PathBerkas;
        database.Connection.Dispose();
        SqliteConnection.ClearAllPools();

        using var koneksiBaru = SqliteConnectionFactory.Open(pathBerkas);
        var repositoriJadwal = new RepositoriJadwalHari();

        foreach (var hari in new[] { 1, 2, 3, 4, 6 })
        {
            var baris = repositoriJadwal.CariUntukHari(koneksiBaru, tahunAjaranId: 1, hari);
            Assert.NotNull(baris);
            Assert.True(baris!.HariSekolah);
            Assert.Equal(new TimeOnly(7, 30), baris.JamMasuk);
            Assert.Equal(new TimeOnly(8, 0), baris.JamRekap);
            Assert.Equal(new TimeOnly(12, 0), baris.BatasTapMasuk);
            Assert.Equal(new TimeOnly(13, 10), baris.MulaiPulang);
        }

        var jumat = repositoriJadwal.CariUntukHari(koneksiBaru, tahunAjaranId: 1, hari: 5);
        Assert.NotNull(jumat);
        Assert.Equal(new TimeOnly(10, 0), jumat!.BatasTapMasuk);
        Assert.Equal(new TimeOnly(11, 30), jumat.MulaiPulang);

        var minggu = repositoriJadwal.CariUntukHari(koneksiBaru, tahunAjaranId: 1, hari: 7);
        Assert.NotNull(minggu);
        Assert.False(minggu!.HariSekolah);
        Assert.Null(minggu.JamMasuk);

        // 2026-09-17 adalah Kamis (hari=4) — jadwal itu barusan diisi.
        // "UID-SISWA-LAMA" (siswa id 1, "Siswa Sudah Kartu") sudah aktif
        // sejak SeedKelasUntukRegistrasiKartu, belum ada catatan absensi
        // hari ini sama sekali (persis situasi tap pertama pagi hari).
        var keputusan = new PemrosesTap().Proses(koneksiBaru, "UID-SISWA-LAMA", new DateTime(2026, 9, 17, 7, 29, 0), modeRegistrasiAktif: false);

        Assert.Equal(JenisTindakanTap.IsiJamMasuk, keputusan.Tindakan);
        Assert.Equal(StatusAbsensiSiswa.Hadir, keputusan.StatusSiswaBaru);
        Assert.NotEqual(AlasanTap.JadwalBelumDiatur, keputusan.Alasan);
    }

    // PRD §7: hari NON-sekolah tetap boleh kosong — hanya hari sekolah
    // yang wajib diisi. Baris lain (Senin-Sabtu) diisi penuh di sini supaya
    // test ini murni mengisolasi pengecualian Minggu, bukan ikut gagal
    // karena baris lain kosong.
    [Fact]
    public void SimpanJadwalCommand_HariNonSekolahJamKosong_TetapDiizinkan()
    {
        using var database = new MigratedDatabase();
        database.SeedKelasUntukRegistrasiKartu();
        var viewModel = BuatViewModel(database);
        foreach (var indeks in new[] { 0, 1, 2, 3, 4, 5 })
        {
            viewModel.JadwalHarian[indeks].JamMasuk = "07:30";
            viewModel.JadwalHarian[indeks].JamRekap = "08:00";
            viewModel.JadwalHarian[indeks].BatasTapMasuk = "12:00";
            viewModel.JadwalHarian[indeks].MulaiPulang = "13:10";
        }

        viewModel.JadwalHarian[6].HariSekolah = false;
        viewModel.JadwalHarian[6].JamMasuk = null;

        viewModel.SimpanJadwalCommand.Execute(null);

        Assert.Equal("Jadwal harian tersimpan.", viewModel.PesanStatus);
        Assert.Null(viewModel.PesanGalat);

        var minggu = new RepositoriJadwalHari().DaftarUntukTahunAjaran(database.Connection, tahunAjaranId: 1).Single(h => h.Hari == 7);
        Assert.False(minggu.HariSekolah);
        Assert.Null(minggu.JamMasuk);
    }

    // Uncentang "Hari sekolah" pada baris yang bawaannya sekolah (mis.
    // Senin) juga harus melepaskan kewajiban jam — validasi mengikuti
    // checkbox baris itu sendiri, bukan daftar hari hardcoded, supaya
    // kalender per-sekolah yang berbeda dari bawaan PRD §7 tidak ikut
    // terhalang.
    [Fact]
    public void SimpanJadwalCommand_HariSekolahDiuncentangJamKosong_TetapDiizinkan()
    {
        using var database = new MigratedDatabase();
        database.SeedKelasUntukRegistrasiKartu();
        var viewModel = BuatViewModel(database);
        foreach (var indeks in new[] { 1, 2, 3, 4, 5 })
        {
            viewModel.JadwalHarian[indeks].JamMasuk = "07:30";
            viewModel.JadwalHarian[indeks].JamRekap = "08:00";
            viewModel.JadwalHarian[indeks].BatasTapMasuk = "12:00";
            viewModel.JadwalHarian[indeks].MulaiPulang = "13:10";
        }

        viewModel.JadwalHarian[0].HariSekolah = false;
        viewModel.JadwalHarian[0].JamMasuk = null;

        viewModel.SimpanJadwalCommand.Execute(null);

        Assert.Equal("Jadwal harian tersimpan.", viewModel.PesanStatus);
        Assert.Null(viewModel.PesanGalat);
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
