using System.IO;
using Absensi.App.MasterData;
using Absensi.App.Pengaturan;
using Absensi.Data.Akun;
using Absensi.Data.Backup;
using Absensi.Data.Repositori;
using Absensi.Domain.Akun;
using ClosedXML.Excel;

namespace Absensi.App.Tests;

// Audit panel 2026-09-24: setiap aksi yang memberi hasil harus menampilkan pesan
// berhasil/gagal yang sesuai hasil sebenarnya, tanpa membocorkan kredensial atau
// teks teknis, dan tanpa membiarkan klik berulang menggandakan pekerjaan.
public sealed class PanelUmpanBalikTests
{
    private static readonly DateTime Waktu = new(2026, 9, 24, 8, 0, 0);
    private static readonly AkunTersimpan Admin = new(1, "admin1", "x", PeranAkun.Admin, null, true);

    private static void Seed(MigratedDatabase database)
    {
        database.Execute("""
            INSERT INTO tahun_ajaran (id, nama, semester, tgl_mulai, tgl_selesai, aktif)
            VALUES (1, '2026/2027', 'ganjil', '2026-07-01', '2026-12-19', 1);
            INSERT INTO kelas (id, tahun_ajaran_id, nama, tingkat) VALUES (1, 1, '7A', 7);
            """);
        database.TambahAkun(1, "admin1", "admin", guruId: null);
    }

    private static object? Scalar(MigratedDatabase database, string sql)
    {
        using var cmd = database.Connection.CreateCommand();
        cmd.CommandText = sql;
        return cmd.ExecuteScalar();
    }

    private static PengaturanViewModel BuatPengaturan(MigratedDatabase database) => new(
        database.Connection, Admin, new PenyediaWaktuUji(Waktu), 1,
        new LayananBackup(
            Path.Combine(Path.GetTempPath(), "b" + Guid.NewGuid().ToString("N")),
            new PenyimpanCloudBackupLokal(Path.Combine(Path.GetTempPath(), "c" + Guid.NewGuid().ToString("N")))));

    // ---------- Pengaturan → Bot Telegram ----------

    [Fact]
    public void Pengaturan_TokenBotFormatSalah_PesanGalatTanpaMengulangTokenDanTidakTersimpan()
    {
        using var database = new MigratedDatabase();
        Seed(database);
        var vm = BuatPengaturan(database);
        vm.TokenBot = "rahasia-bukan-token";
        vm.UsernameBot = "bot_uji";

        vm.SimpanBotCommand.Execute(null);

        Assert.NotNull(vm.PesanGalatBot);
        Assert.DoesNotContain("rahasia-bukan-token", vm.PesanGalatBot);
        Assert.Null(vm.PesanStatusBot);
        Assert.Equal(0L, Scalar(database, "SELECT COUNT(*) FROM pengaturan WHERE kunci = 'token_bot'"));
    }

    [Fact]
    public void Pengaturan_TokenDanUsernameSah_TersimpanDenganPesanBerhasil()
    {
        using var database = new MigratedDatabase();
        Seed(database);
        var vm = BuatPengaturan(database);
        vm.TokenBot = "  123456:ABC-def_GHI  ";
        vm.UsernameBot = "@sekolah_bot";

        vm.SimpanBotCommand.Execute(null);

        Assert.Null(vm.PesanGalatBot);
        Assert.NotNull(vm.PesanStatusBot);
        Assert.Equal("123456:ABC-def_GHI", vm.TokenBot);
        Assert.Equal("sekolah_bot", vm.UsernameBot);
    }

    [Fact]
    public void Pengaturan_UsernameBotTidakSah_PesanGalat()
    {
        using var database = new MigratedDatabase();
        Seed(database);
        var vm = BuatPengaturan(database);
        vm.TokenBot = "123456:ABC";
        vm.UsernameBot = "nama bot dengan spasi";

        vm.SimpanBotCommand.Execute(null);

        Assert.NotNull(vm.PesanGalatBot);
        Assert.Null(vm.PesanStatusBot);
    }

    // ---------- Kelas ----------

    [Fact]
    public void Kelas_NamaGanda_PesanGalatDanTidakAdaBarisBaru()
    {
        using var database = new MigratedDatabase();
        Seed(database);
        var vm = new KelasViewModel(database.Connection, Admin, new PenyediaWaktuUji(Waktu), 1) { Nama = "7A", Tingkat = 7 };

        vm.SimpanCommand.Execute(null);

        Assert.NotNull(vm.PesanGalat);
        Assert.Contains("7A", vm.PesanGalat);
        Assert.Null(vm.PesanStatus);
        Assert.Equal(1L, Scalar(database, "SELECT COUNT(*) FROM kelas"));
    }

    [Fact]
    public void Kelas_NamaBaru_TersimpanDenganPesanBerhasil()
    {
        using var database = new MigratedDatabase();
        Seed(database);
        var vm = new KelasViewModel(database.Connection, Admin, new PenyediaWaktuUji(Waktu), 1) { Nama = "  7B  ", Tingkat = 7 };

        vm.SimpanCommand.Execute(null);

        Assert.Null(vm.PesanGalat);
        Assert.Contains("7B", vm.PesanStatus);
        Assert.Equal(2L, Scalar(database, "SELECT COUNT(*) FROM kelas"));
        Assert.Equal(1L, Scalar(database, "SELECT COUNT(*) FROM kelas WHERE nama = '7B'"));
    }

    // ---------- Siswa ----------

    [Fact]
    public void Siswa_NisnBukanSepuluhAngka_PesanGalat()
    {
        using var database = new MigratedDatabase();
        Seed(database);
        var vm = new SiswaViewModel(database.Connection, Admin, new PenyediaWaktuUji(Waktu), new StatusModeRegistrasi(), tahunAjaranId: 1);
        vm.KelasTerpilih = vm.DaftarKelas.Single();
        vm.Nisn = "12ab";
        vm.Nama = "Uji";

        vm.SimpanCommand.Execute(null);

        Assert.Contains("NISN", vm.PesanGalat);
        Assert.Equal(0L, Scalar(database, "SELECT COUNT(*) FROM siswa"));
    }

    [Fact]
    public void Siswa_NisnGanda_PesanMenyebutNamaPemilikTanpaMenggandakan()
    {
        using var database = new MigratedDatabase();
        Seed(database);
        database.Execute("INSERT INTO siswa (id, nisn, nama, jenis_kelamin, aktif) VALUES (1, '0012345678', 'Ahmad Fauzi', 'L', 1);");
        var vm = new SiswaViewModel(database.Connection, Admin, new PenyediaWaktuUji(Waktu), new StatusModeRegistrasi(), tahunAjaranId: 1);
        vm.KelasTerpilih = vm.DaftarKelas.Single();
        vm.Nisn = "0012345678";
        vm.Nama = "Orang Lain";

        vm.SimpanCommand.Execute(null);

        Assert.Contains("Ahmad Fauzi", vm.PesanGalat);
        Assert.Equal(1L, Scalar(database, "SELECT COUNT(*) FROM siswa"));
    }

    [Fact]
    public void Siswa_DataSah_TersimpanNolDiDepanBertahanDanPesanBerhasil()
    {
        using var database = new MigratedDatabase();
        Seed(database);
        var vm = new SiswaViewModel(database.Connection, Admin, new PenyediaWaktuUji(Waktu), new StatusModeRegistrasi(), tahunAjaranId: 1);
        vm.KelasTerpilih = vm.DaftarKelas.Single();
        vm.Nisn = "0012345678";
        vm.Nama = "  Siti Aminah  ";
        vm.JenisKelamin = "P";

        vm.SimpanCommand.Execute(null);

        Assert.Null(vm.PesanGalat);
        Assert.Contains("Siti Aminah", vm.PesanStatus);
        Assert.Equal("Siti Aminah", Scalar(database, "SELECT nama FROM siswa WHERE nisn = '0012345678'")?.ToString());
    }

    // ---------- Wizard impor: klik berulang & kegagalan ----------

    private static string BuatBerkas(params (string Nisn, string Nama)[] baris)
    {
        var path = Path.Combine(Path.GetTempPath(), "impor-" + Guid.NewGuid().ToString("N") + ".xlsx");
        using var buku = new XLWorkbook();
        var sheet = buku.AddWorksheet("Siswa");
        var kolom = new[] { "NISN", "NAMA_LENGKAP", "JENIS_KELAMIN", "TEMPAT_LAHIR", "TANGGAL_LAHIR", "ALAMAT", "KELAS", "NAMA_ORANG_TUA", "NO_HP_ORANG_TUA" };
        for (var i = 0; i < kolom.Length; i++)
        {
            sheet.Cell(1, i + 1).Value = kolom[i];
        }

        for (var r = 0; r < baris.Length; r++)
        {
            sheet.Cell(r + 2, 1).SetValue(baris[r].Nisn);
            sheet.Cell(r + 2, 2).Value = baris[r].Nama;
            sheet.Cell(r + 2, 3).Value = "L";
            sheet.Cell(r + 2, 7).Value = "7A";
        }

        buku.SaveAs(path);
        return path;
    }

    [Fact]
    public void ImporSiswaWizard_SetelahBerhasil_TombolNonaktifDanKlikUlangTidakMengulang()
    {
        using var database = new MigratedDatabase();
        Seed(database);
        var vm = new ImporSiswaWizardViewModel(database.Connection, Admin, new PenyediaWaktuUji(Waktu), tahunAjaranId: 1);
        var path = BuatBerkas(("0012345678", "Ahmad Fauzi"));
        try
        {
            vm.AnalisisBerkas(path);
            Assert.True(vm.ImporCommand.CanExecute(null));

            vm.ImporCommand.Execute(null);

            Assert.NotNull(vm.HasilImpor);
            Assert.False(vm.ImporCommand.CanExecute(null));
            Assert.Equal("Sudah diimpor", vm.TeksTombolImpor);
            Assert.Equal(1L, Scalar(database, "SELECT COUNT(*) FROM siswa"));

            // Memilih berkas lagi memulai ulang alur: tombol aktif kembali.
            vm.AnalisisBerkas(path);
            Assert.True(vm.ImporCommand.CanExecute(null));
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public void ImporSiswaWizard_DatabaseGagal_PesanAmanTanpaTeksTeknisDanTombolTetapAktif()
    {
        using var database = new MigratedDatabase();
        Seed(database);
        var vm = new ImporSiswaWizardViewModel(database.Connection, Admin, new PenyediaWaktuUji(Waktu), tahunAjaranId: 1);
        var path = BuatBerkas(("0012345678", "Ahmad Fauzi"));
        try
        {
            vm.AnalisisBerkas(path);
            // Simulasi kegagalan penyimpanan: tabel tujuan hilang setelah analisis.
            database.Execute("DROP TABLE siswa_kelas;");

            vm.ImporCommand.Execute(null);

            Assert.Null(vm.HasilImpor);
            Assert.StartsWith("Impor gagal", vm.PesanGalat);
            Assert.DoesNotContain("SQLite", vm.PesanGalat, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("siswa_kelas", vm.PesanGalat);
            Assert.True(vm.ImporCommand.CanExecute(null));
        }
        finally
        {
            File.Delete(path);
        }
    }
}
