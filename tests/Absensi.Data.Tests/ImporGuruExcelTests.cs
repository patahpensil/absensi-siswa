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

    // A-12 (Master Bug & UI/UX Register, docs/PRD.md §16.2 langkah 3):
    // template Excel siap-isi, header dari ImporGuruExcel.KolomBaku --
    // assert di bawah membandingkan ke KolomBaku itu sendiri (sumber
    // kebenaran), bukan menyalin ulang daftar kolom sebagai literal baru.

    [Fact]
    public void BuatTemplate_HasilTidakKosong()
    {
        var isi = _importir.BuatTemplate();

        Assert.NotEmpty(isi);
    }

    [Fact]
    public void BuatTemplate_BisaDibukaUlangSebagaiWorkbookValid()
    {
        var isi = _importir.BuatTemplate();

        using var stream = new MemoryStream(isi);
        using var workbook = new XLWorkbook(stream);

        Assert.Single(workbook.Worksheets);
    }

    [Fact]
    public void BuatTemplate_SatuWorksheetBernamaGuru()
    {
        var isi = _importir.BuatTemplate();

        using var stream = new MemoryStream(isi);
        using var workbook = new XLWorkbook(stream);
        var worksheet = Assert.Single(workbook.Worksheets);

        Assert.Equal("Guru", worksheet.Name);
    }

    [Fact]
    public void BuatTemplate_HeaderSamaPersisKolomBaku()
    {
        var isi = _importir.BuatTemplate();

        using var stream = new MemoryStream(isi);
        using var workbook = new XLWorkbook(stream);
        var worksheet = workbook.Worksheets.Single();

        var headerAktual = Enumerable.Range(1, ImporGuruExcel.KolomBaku.Count)
            .Select(kolom => worksheet.Cell(1, kolom).GetString())
            .ToArray();

        Assert.Equal(ImporGuruExcel.KolomBaku, headerAktual);
    }

    // A-12: Array.AsReadOnly (bukan sekadar deklarasi tipe IReadOnlyList di
    // atas array mentah) supaya objek RUNTIME KolomBaku sendiri menolak
    // mutasi -- deklarasi tipe saja hanya membatasi di compile time.
    [Fact]
    public void KolomBaku_TidakBisaDimutasiSecaraRuntime()
    {
        var asli = ImporGuruExcel.KolomBaku.ToArray();

        Assert.IsNotType<string[]>(ImporGuruExcel.KolomBaku);

        var sebagaiIList = (IList<string>)ImporGuruExcel.KolomBaku;
        Assert.True(sebagaiIList.IsReadOnly);

        Assert.Throws<NotSupportedException>(() => sebagaiIList[0] = "DIUBAH");
        Assert.Throws<NotSupportedException>(() => sebagaiIList.Add("KOLOM_BARU"));
        Assert.Throws<NotSupportedException>(() => sebagaiIList.RemoveAt(0));

        Assert.Equal(asli, ImporGuruExcel.KolomBaku);

        using var stream = new MemoryStream(_importir.BuatTemplate());
        using var workbook = new XLWorkbook(stream);
        var worksheet = workbook.Worksheets.Single();
        var headerTemplate = Enumerable.Range(1, ImporGuruExcel.KolomBaku.Count)
            .Select(kolom => worksheet.Cell(1, kolom).GetString())
            .ToArray();

        Assert.Equal(asli, headerTemplate);
    }

    [Fact]
    public void BuatTemplate_TidakAdaBarisDataContoh()
    {
        var isi = _importir.BuatTemplate();

        using var stream = new MemoryStream(isi);
        using var workbook = new XLWorkbook(stream);
        var worksheet = workbook.Worksheets.Single();

        Assert.Equal(1, worksheet.LastRowUsed()?.RowNumber() ?? 1);
    }

    [Fact]
    public void BuatTemplate_LolosValidasiHeaderAnalisisTanpaFormatException()
    {
        using var database = new MigratedDatabase();
        var isi = _importir.BuatTemplate();
        var path = Path.Combine(Path.GetTempPath(), $"template-guru-uji-{Guid.NewGuid():N}.xlsx");
        File.WriteAllBytes(path, isi);

        try
        {
            var exception = Record.Exception(() => _importir.Analisis(database.Connection, path));

            Assert.Null(exception);
        }
        finally
        {
            File.Delete(path);
        }
    }
}
