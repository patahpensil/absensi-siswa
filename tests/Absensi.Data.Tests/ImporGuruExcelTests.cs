using Absensi.Data.Akun;
using Absensi.Data.MasterData;
using Absensi.Data.Repositori;
using Absensi.Domain.Akun;
using ClosedXML.Excel;

namespace Absensi.Data.Tests;

public sealed class ImporGuruExcelTests
{
    private static readonly DateTime Waktu = new(2026, 9, 17, 8, 0, 0);
    private static readonly string[] KolomBaku = { "NAMA", "NIP", "PANGKAT", "JABATAN" };

    private readonly ImporGuruExcel _importir = new();

    private static string BuatBerkas(params string[][] baris)
    {
        var path = Path.Combine(Path.GetTempPath(), $"impor-guru-uji-{Guid.NewGuid():N}.xlsx");
        using var workbook = new XLWorkbook();
        var worksheet = workbook.Worksheets.Add("Guru");

        for (var kolom = 0; kolom < KolomBaku.Length; kolom++)
        {
            worksheet.Cell(1, kolom + 1).SetValue(KolomBaku[kolom]);
        }

        for (var b = 0; b < baris.Length; b++)
        {
            for (var kolom = 0; kolom < baris[b].Length; kolom++)
            {
                worksheet.Cell(b + 2, kolom + 1).SetValue(baris[b][kolom]);
            }
        }

        workbook.SaveAs(path);
        return path;
    }

    [Fact]
    public void Analisis_BerkasValid_TidakAdaKesalahan()
    {
        using var database = new MigratedDatabase();
        var path = BuatBerkas(
            new[] { "Ahmad Guru", "111", "Pembina", "Guru Kelas" },
            new[] { "Budi Guru", "222", "Penata", "Guru Mapel" });

        try
        {
            var hasil = _importir.Analisis(database.Connection, path);

            Assert.False(hasil.AdaKesalahan);
            Assert.Equal(2, hasil.Baris.Count);
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public void Analisis_CampuranBaruDiperbaruiDanBermasalah_HitunganPratinjauBenar()
    {
        using var database = new MigratedDatabase();
        database.SeedMasterDataUntukUji();
        var akun = new AkunTersimpan(1, "admin1", "x", PeranAkun.Admin, null, true);

        var pathAwal = BuatBerkas(new[] { "Ahmad Guru", "111", "", "" });
        try
        {
            var analisisAwal = _importir.Analisis(database.Connection, pathAwal);
            _importir.Impor(database.Connection, akun, Waktu, analisisAwal.Baris);
        }
        finally
        {
            File.Delete(pathAwal);
        }

        var path = BuatBerkas(
            new[] { "Ahmad Guru Diperbarui", "111", "", "" },
            new[] { "Budi Guru Baru", "222", "", "" },
            new[] { "Siswa Rusak", "", "", "" });

        try
        {
            var hasil = _importir.Analisis(database.Connection, path);

            Assert.Equal(1, hasil.JumlahBaru);
            Assert.Equal(1, hasil.JumlahDiperbarui);
            Assert.Equal(1, hasil.JumlahBermasalah);
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public void Analisis_NipDuplikatDalamBerkas_BarisKeduaGagal()
    {
        using var database = new MigratedDatabase();
        var path = BuatBerkas(
            new[] { "Ahmad Guru", "111", "", "" },
            new[] { "Ahmad Palsu", "111", "", "" });

        try
        {
            var hasil = _importir.Analisis(database.Connection, path);

            Assert.True(hasil.Baris[0].Sah);
            Assert.False(hasil.Baris[1].Sah);
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public void Impor_DipanggilDuaKaliBerkasSama_TidakMenggandakanData()
    {
        using var database = new MigratedDatabase();
        database.SeedMasterDataUntukUji();
        var akun = new AkunTersimpan(1, "admin1", "x", PeranAkun.Admin, null, true);
        var path = BuatBerkas(new[] { "Ahmad Guru", "111", "Pembina", "Guru Kelas" });

        try
        {
            var analisis1 = _importir.Analisis(database.Connection, path);
            var hasil1 = _importir.Impor(database.Connection, akun, Waktu, analisis1.Baris);
            Assert.Equal(1, hasil1.Ditambah);

            var analisis2 = _importir.Analisis(database.Connection, path);
            var hasil2 = _importir.Impor(database.Connection, akun, Waktu, analisis2.Baris);
            Assert.Equal(0, hasil2.Ditambah);
            Assert.Equal(1, hasil2.Diperbarui);

            using var command = database.Connection.CreateCommand();
            command.CommandText = "SELECT COUNT(*) FROM guru;";
            Assert.Equal(1, Convert.ToInt32(command.ExecuteScalar()));
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public void Impor_MasihAdaBarisBermasalah_TidakMenyimpanApaPun()
    {
        using var database = new MigratedDatabase();
        database.SeedMasterDataUntukUji();
        var akun = new AkunTersimpan(1, "admin1", "x", PeranAkun.Admin, null, true);
        var path = BuatBerkas(
            new[] { "Ahmad Guru", "111", "", "" },
            new[] { "", "222", "", "" });

        try
        {
            var analisis = _importir.Analisis(database.Connection, path);
            Assert.True(analisis.AdaKesalahan);

            Assert.Throws<InvalidOperationException>(() =>
                _importir.Impor(database.Connection, akun, Waktu, analisis.Baris));

            using var command = database.Connection.CreateCommand();
            command.CommandText = "SELECT COUNT(*) FROM guru;";
            Assert.Equal(0, Convert.ToInt32(command.ExecuteScalar()));
        }
        finally
        {
            File.Delete(path);
        }
    }
}
