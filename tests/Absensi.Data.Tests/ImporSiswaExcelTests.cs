using Absensi.Data.Akun;
using Absensi.Data.MasterData;
using Absensi.Data.Repositori;
using Absensi.Domain.Akun;
using ClosedXML.Excel;

namespace Absensi.Data.Tests;

public sealed class ImporSiswaExcelTests
{
    private static readonly DateTime Waktu = new(2026, 9, 17, 8, 0, 0);
    private static readonly string[] KolomBaku =
    {
        "NISN", "NAMA_LENGKAP", "JENIS_KELAMIN", "TEMPAT_LAHIR",
        "TANGGAL_LAHIR", "ALAMAT", "KELAS", "NAMA_ORANG_TUA", "NO_HP_ORANG_TUA",
    };

    private readonly ImporSiswaExcel _importir = new();

    private static string BuatBerkas(params object[][] baris)
    {
        var path = Path.Combine(Path.GetTempPath(), $"impor-siswa-uji-{Guid.NewGuid():N}.xlsx");
        using var workbook = new XLWorkbook();
        var worksheet = workbook.Worksheets.Add("Siswa");

        for (var kolom = 0; kolom < KolomBaku.Length; kolom++)
        {
            worksheet.Cell(1, kolom + 1).SetValue(KolomBaku[kolom]);
        }

        for (var baris_ = 0; baris_ < baris.Length; baris_++)
        {
            for (var kolom = 0; kolom < baris[baris_].Length; kolom++)
            {
                var nilai = baris[baris_][kolom];
                var cell = worksheet.Cell(baris_ + 2, kolom + 1);
                if (nilai is string teks)
                {
                    cell.SetValue(teks);
                }
            }
        }

        workbook.SaveAs(path);
        return path;
    }

    private static object[] BarisSiswaValid(string nisn, string nama, string kelas = "7A") =>
        new object[] { nisn, nama, "L", "Makassar", "2013-01-01", "Jl. Contoh", kelas, "Budi", "081234567890" };

    [Fact]
    public void Analisis_BerkasValid_TidakAdaKesalahan()
    {
        using var database = new MigratedDatabase();
        database.SeedMasterDataUntukUji();
        var path = BuatBerkas(
            BarisSiswaValid("0012345678", "Ahmad Fauzi"),
            BarisSiswaValid("0012345679", "Budi Santoso"));

        try
        {
            var hasil = _importir.Analisis(database.Connection, tahunAjaranId: 1, path);

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

        // Impor pertama: satu siswa masuk sebagai data yang sudah ada.
        var pathAwal = BuatBerkas(BarisSiswaValid("0012345678", "Ahmad Fauzi"));
        try
        {
            var analisisAwal = _importir.Analisis(database.Connection, tahunAjaranId: 1, pathAwal);
            _importir.Impor(database.Connection, akun, Waktu, tahunAjaranId: 1, analisisAwal.Baris);
        }
        finally
        {
            File.Delete(pathAwal);
        }

        // Berkas kedua: satu baris sudah ada (diperbarui), satu baru, satu bermasalah.
        var path = BuatBerkas(
            BarisSiswaValid("0012345678", "Ahmad Fauzi Diperbarui"),
            BarisSiswaValid("0012345679", "Budi Baru"),
            new object[] { "123", "Siswa Rusak", "L", "", "", "", "7A", "", "" });

        try
        {
            var hasil = _importir.Analisis(database.Connection, tahunAjaranId: 1, path);

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
    public void Analisis_NisnKurangDariSepuluhDigit_AdaKesalahan()
    {
        using var database = new MigratedDatabase();
        database.SeedMasterDataUntukUji();
        var path = BuatBerkas(BarisSiswaValid("123", "Ahmad Fauzi"));

        try
        {
            var hasil = _importir.Analisis(database.Connection, tahunAjaranId: 1, path);

            Assert.True(hasil.AdaKesalahan);
            Assert.Contains(hasil.Baris[0].Kesalahan, k => k.Contains("10 digit"));
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public void Analisis_NisnDuplikatDalamBerkas_AdaKesalahanPadaBarisKedua()
    {
        using var database = new MigratedDatabase();
        database.SeedMasterDataUntukUji();
        var path = BuatBerkas(
            BarisSiswaValid("0012345678", "Ahmad Fauzi"),
            BarisSiswaValid("0012345678", "Ahmad Palsu"));

        try
        {
            var hasil = _importir.Analisis(database.Connection, tahunAjaranId: 1, path);

            Assert.True(hasil.Baris[0].Sah);
            Assert.False(hasil.Baris[1].Sah);
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public void Analisis_KelasTidakDitemukan_AdaKesalahan()
    {
        using var database = new MigratedDatabase();
        database.SeedMasterDataUntukUji();
        var path = BuatBerkas(BarisSiswaValid("0012345678", "Ahmad Fauzi", kelas: "9Z"));

        try
        {
            var hasil = _importir.Analisis(database.Connection, tahunAjaranId: 1, path);

            Assert.True(hasil.AdaKesalahan);
            Assert.Contains(hasil.Baris[0].Kesalahan, k => k.Contains("tidak ditemukan"));
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public void Analisis_JenisKelaminSelainLatauP_AdaKesalahan()
    {
        using var database = new MigratedDatabase();
        database.SeedMasterDataUntukUji();
        var path = BuatBerkas(new object[]
        {
            "0012345678", "Ahmad Fauzi", "X", "Makassar", "2013-01-01", "Jl. Contoh", "7A", "Budi", "081234567890",
        });

        try
        {
            var hasil = _importir.Analisis(database.Connection, tahunAjaranId: 1, path);

            Assert.True(hasil.AdaKesalahan);
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public void Impor_BerkasValid_MenambahSiswaBaruDanMenyimpanNisnSebagaiTeksApaAdanya()
    {
        using var database = new MigratedDatabase();
        database.SeedMasterDataUntukUji();
        var akun = new AkunTersimpan(1, "admin1", "x", PeranAkun.Admin, null, true);
        var path = BuatBerkas(
            BarisSiswaValid("0012345678", "Ahmad Fauzi"),
            BarisSiswaValid("0012345679", "Budi Santoso"));

        try
        {
            var analisis = _importir.Analisis(database.Connection, tahunAjaranId: 1, path);
            var hasil = _importir.Impor(database.Connection, akun, Waktu, tahunAjaranId: 1, analisis.Baris);

            Assert.Equal(2, hasil.Ditambah);
            Assert.Equal(0, hasil.Diperbarui);

            var idSiswa = new RepositoriSiswa().CariIdBerdasarkanNisn(database.Connection, "0012345678");
            Assert.NotNull(idSiswa);
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public void Impor_DipanggilDuaKaliDenganBerkasSama_TidakMenggandakanData()
    {
        using var database = new MigratedDatabase();
        database.SeedMasterDataUntukUji();
        var akun = new AkunTersimpan(1, "admin1", "x", PeranAkun.Admin, null, true);
        var path = BuatBerkas(BarisSiswaValid("0012345678", "Ahmad Fauzi"));

        try
        {
            var analisis = _importir.Analisis(database.Connection, tahunAjaranId: 1, path);
            _importir.Impor(database.Connection, akun, Waktu, tahunAjaranId: 1, analisis.Baris);

            var analisisKedua = _importir.Analisis(database.Connection, tahunAjaranId: 1, path);
            var hasilKedua = _importir.Impor(database.Connection, akun, Waktu, tahunAjaranId: 1, analisisKedua.Baris);

            Assert.Equal(0, hasilKedua.Ditambah);
            Assert.Equal(1, hasilKedua.Diperbarui);

            using var command = database.Connection.CreateCommand();
            command.CommandText = "SELECT COUNT(*) FROM siswa;";
            Assert.Equal(1, Convert.ToInt32(command.ExecuteScalar()));
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public void Impor_MasihAdaBarisBermasalah_MelemparGalatDanTidakMenyimpanApaPun()
    {
        using var database = new MigratedDatabase();
        database.SeedMasterDataUntukUji();
        var akun = new AkunTersimpan(1, "admin1", "x", PeranAkun.Admin, null, true);
        var path = BuatBerkas(
            BarisSiswaValid("0012345678", "Ahmad Fauzi"),
            new object[] { "123", "Siswa Rusak", "L", "", "", "", "7A", "", "" });

        try
        {
            var analisis = _importir.Analisis(database.Connection, tahunAjaranId: 1, path);
            Assert.True(analisis.AdaKesalahan);

            Assert.Throws<InvalidOperationException>(() =>
                _importir.Impor(database.Connection, akun, Waktu, tahunAjaranId: 1, analisis.Baris));

            using var command = database.Connection.CreateCommand();
            command.CommandText = "SELECT COUNT(*) FROM siswa;";
            Assert.Equal(0, Convert.ToInt32(command.ExecuteScalar()));

            using var commandAudit = database.Connection.CreateCommand();
            commandAudit.CommandText = "SELECT COUNT(*) FROM audit;";
            Assert.Equal(0, Convert.ToInt32(commandAudit.ExecuteScalar()));
        }
        finally
        {
            File.Delete(path);
        }
    }

    // A-12 (Master Bug & UI/UX Register, docs/PRD.md §16.2 langkah 3):
    // template Excel siap-isi, header dari ImporSiswaExcel.KolomBaku --
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
    public void BuatTemplate_SatuWorksheetBernamaSiswa()
    {
        var isi = _importir.BuatTemplate();

        using var stream = new MemoryStream(isi);
        using var workbook = new XLWorkbook(stream);
        var worksheet = Assert.Single(workbook.Worksheets);

        Assert.Equal("Siswa", worksheet.Name);
    }

    [Fact]
    public void BuatTemplate_HeaderSamaPersisKolomBaku()
    {
        var isi = _importir.BuatTemplate();

        using var stream = new MemoryStream(isi);
        using var workbook = new XLWorkbook(stream);
        var worksheet = workbook.Worksheets.Single();

        var headerAktual = Enumerable.Range(1, ImporSiswaExcel.KolomBaku.Count)
            .Select(kolom => worksheet.Cell(1, kolom).GetString())
            .ToArray();

        Assert.Equal(ImporSiswaExcel.KolomBaku, headerAktual);
    }

    // A-12: Array.AsReadOnly (bukan sekadar deklarasi tipe IReadOnlyList di
    // atas array mentah) supaya objek RUNTIME KolomBaku sendiri menolak
    // mutasi -- deklarasi tipe saja hanya membatasi di compile time.
    [Fact]
    public void KolomBaku_TidakBisaDimutasiSecaraRuntime()
    {
        var asli = ImporSiswaExcel.KolomBaku.ToArray();

        Assert.IsNotType<string[]>(ImporSiswaExcel.KolomBaku);

        var sebagaiIList = (IList<string>)ImporSiswaExcel.KolomBaku;
        Assert.True(sebagaiIList.IsReadOnly);

        Assert.Throws<NotSupportedException>(() => sebagaiIList[0] = "DIUBAH");
        Assert.Throws<NotSupportedException>(() => sebagaiIList.Add("KOLOM_BARU"));
        Assert.Throws<NotSupportedException>(() => sebagaiIList.RemoveAt(0));

        Assert.Equal(asli, ImporSiswaExcel.KolomBaku);

        using var stream = new MemoryStream(_importir.BuatTemplate());
        using var workbook = new XLWorkbook(stream);
        var worksheet = workbook.Worksheets.Single();
        var headerTemplate = Enumerable.Range(1, ImporSiswaExcel.KolomBaku.Count)
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
        var path = Path.Combine(Path.GetTempPath(), $"template-siswa-uji-{Guid.NewGuid():N}.xlsx");
        File.WriteAllBytes(path, isi);

        try
        {
            var exception = Record.Exception(() => _importir.Analisis(database.Connection, tahunAjaranId: 1, path));

            Assert.Null(exception);
        }
        finally
        {
            File.Delete(path);
        }
    }
}
