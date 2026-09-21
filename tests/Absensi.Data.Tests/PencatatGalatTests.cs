using System.Text.RegularExpressions;
using Absensi.Data.Diagnostik;

namespace Absensi.Data.Tests;

// docs/INVESTIGASI-CRASH-2026-09-21.md §4.8 — pengujian PencatatGalat
// terisolasi. TIDAK menguji lewat .exe Release sungguhan (keterbatasan yang
// sama dijelaskan PANDUAN-TESTER.md baris 3) — itu dibuktikan manual di
// Tahap C, dicatat sebagai skenario baru PANDUAN-TESTER.md.
public sealed class PencatatGalatTests : IDisposable
{
    private readonly string _folderLog = Path.Combine(Path.GetTempPath(), $"absensi-log-{Guid.NewGuid():N}");

    [Fact]
    public void Catat_FolderBelumAda_DibuatSendiriDanBerkasHariIniMuncul()
    {
        Assert.False(Directory.Exists(_folderLog));

        PencatatGalat.Catat(new InvalidOperationException("uji"), sumber: "UjiFolder", folderLog: _folderLog);

        Assert.True(Directory.Exists(_folderLog));
        var namaBerkasHariIni = $"{DateTimeOffset.Now:yyyyMMdd}.log";
        Assert.True(File.Exists(Path.Combine(_folderLog, namaBerkasHariIni)));
    }

    [Fact]
    public void Catat_MenulisBaris_FormatMemuatWaktuLevelSumberDanTipeException()
    {
        PencatatGalat.Catat(new InvalidOperationException("uji format"), sumber: "UjiFormat", level: "ERROR", folderLog: _folderLog);

        var isi = BacaBerkasHariIni();

        Assert.Contains("[ERROR]", isi);
        Assert.Contains("[UjiFormat]", isi);
        Assert.Contains(typeof(InvalidOperationException).FullName!, isi);
        Assert.Contains("--- akhir entri ---", isi);
    }

    [Fact]
    public void Catat_ExceptionDenganSourceAbsensiData_PesanDisembunyikanBukanTeksAsli()
    {
        var exception = new InvalidOperationException("Siswa Budi Santoso dengan NISN 1234567890 tidak ditemukan.")
        {
            Source = "Absensi.Data",
        };

        PencatatGalat.Catat(exception, sumber: "UjiSembunyi", folderLog: _folderLog);

        var isi = BacaBerkasHariIni();

        Assert.DoesNotContain("Budi Santoso", isi);
        Assert.DoesNotContain("1234567890", isi);
        Assert.Contains("disembunyikan", isi);
    }

    [Fact]
    public void Catat_ExceptionDenganSourceBcl_PesanTetapDicatatApaAdanya()
    {
        var exception = new InvalidOperationException("database is locked") { Source = "Microsoft.Data.Sqlite" };

        PencatatGalat.Catat(exception, sumber: "UjiBcl", folderLog: _folderLog);

        var isi = BacaBerkasHariIni();

        Assert.Contains("database is locked", isi);
    }

    [Theory]
    [InlineData("Data Source=C:\\rahasia\\absensi.db;Password=sandi123;", "Password=<redaksi>")]
    [InlineData("Token bot: 123456789:AAHk9xLoremIpsumTokenPalsuPanjangSekali", "<token-redaksi>")]
    [InlineData("NISN pemohon 9876543210 bermasalah", "<angka-redaksi>")]
    public void Catat_PesanBclDenganPolaSensitif_BagianYangCocokDiredaksi(string pesanAsli, string potonganDiharapkan)
    {
        var exception = new HttpRequestException(pesanAsli) { Source = "System.Net.Http" };

        PencatatGalat.Catat(exception, sumber: "UjiPola", folderLog: _folderLog);

        var isi = BacaBerkasHariIni();

        Assert.Contains(potonganDiharapkan, isi);
    }

    [Fact]
    public void Catat_StackTraceMengandungPathAkunWindows_PathDiredaksi()
    {
        var exception = new PengecualianJejakPalsu(@"   at Absensi.App.Contoh() in C:\Users\budi123\repo\Contoh.cs:line 42");

        PencatatGalat.Catat(exception, sumber: "UjiJejak", folderLog: _folderLog);

        var isi = BacaBerkasHariIni();

        Assert.DoesNotContain("budi123", isi);
        Assert.Contains("<profil-pengguna>", isi);
    }

    [Fact]
    public void Catat_InnerExceptionIkutDicatatDenganAturanSanitasiSendiri()
    {
        // Exception(string, Exception) bawaan .NET TIDAK menyalin teks
        // InnerException ke Message miliknya sendiri (beda dari
        // AggregateException yang justru menyusunnya — lihat keterbatasan
        // yang dicatat di laporan Tahap B/C soal itu). Jadi di sini
        // memang seharusnya "Siti" HANYA muncul lewat baris InnerException
        // yang disaring aturannya sendiri (disembunyikan, karena Source-nya
        // Absensi.Data), bukan lewat Message luar.
        var dalam = new InvalidOperationException("Guru bernama Siti mengalami galat.") { Source = "Absensi.Data" };
        var luar = new InvalidOperationException("Galat gabungan", dalam) { Source = "System.Threading.Tasks" };

        PencatatGalat.Catat(luar, sumber: "UjiInner", folderLog: _folderLog);

        var isi = BacaBerkasHariIni();

        Assert.Contains("Galat gabungan", isi);
        Assert.DoesNotContain("Siti", isi);
        Assert.Contains("disembunyikan", isi);
    }

    [Fact]
    public void Catat_AggregateExceptionDenganInnerSensitif_MessageGabunganTidakBocorTapiInnerNonSensitifTetapTampil()
    {
        // AggregateException.Message BAWAAN .NET menyusun ulang teks
        // SELURUH InnerExceptions ke Message miliknya sendiri -- kalau
        // PencatatGalat naif memakai exception.Message untuk AggregateException,
        // "Siti Aminah" akan bocor lewat Message si pembungkus walau
        // Source pembungkusnya sendiri bukan Absensi.Data/Domain.
        var dalamSensitif = new InvalidOperationException("Guru bernama Siti Aminah bermasalah.") { Source = "Absensi.Data" };
        var dalamBiasa = new IOException("Berkas konfigurasi tidak ditemukan") { Source = "System.IO.FileSystem" };
        var gabungan = new AggregateException("Galat gabungan banyak tugas", dalamSensitif, dalamBiasa);

        PencatatGalat.Catat(gabungan, sumber: "UjiAggregate", folderLog: _folderLog);

        var isi = BacaBerkasHariIni();

        Assert.DoesNotContain("Siti Aminah", isi);
        Assert.Contains("Berkas konfigurasi tidak ditemukan", isi);
        Assert.Contains(typeof(AggregateException).FullName!, isi);
        Assert.Contains("2 InnerException", isi);
        Assert.Contains("disembunyikan", isi);
    }

    [Fact]
    public void Catat_AggregateExceptionMelebihiBatasTotal_DipotongDenganPenanda()
    {
        var innerBanyak = Enumerable.Range(0, 25)
            .Select(i => (Exception)new InvalidOperationException($"galat ke-{i}") { Source = "System.Threading.Tasks" })
            .ToArray();
        var gabungan = new AggregateException(innerBanyak);

        PencatatGalat.Catat(gabungan, sumber: "UjiAggregateBanyak", folderLog: _folderLog);

        var isi = BacaBerkasHariIni();

        Assert.Contains("dipotong", isi);
    }

    [Fact]
    public void Catat_KegagalanMenulisUtamaDanCadangan_TetapTidakMelempar()
    {
        Directory.CreateDirectory(_folderLog);
        var namaBerkasHariIni = $"{DateTimeOffset.Now:yyyyMMdd}.log";

        // Jadikan target berkas log utama DAN berkas cadangan sama-sama
        // sebuah DIREKTORI (bukan berkas) — memaksa KEDUA jalur penulisan
        // gagal karena alasan berbeda dari uji "folderLog sebagai berkas"
        // di bawah, supaya kegagalan jalur fallback teruji eksplisit dan
        // terpisah (§D.2).
        Directory.CreateDirectory(Path.Combine(_folderLog, namaBerkasHariIni));
        Directory.CreateDirectory(Path.Combine(_folderLog, "_gagal-tulis.log"));

        PencatatGalat.Catat(new InvalidOperationException("uji ganda gagal"), sumber: "UjiGagalGanda", folderLog: _folderLog);

        // Tidak ada Assert.Throws: lulus berarti TIDAK melempar walau baik
        // jalur utama MAUPUN jalur cadangan sama-sama gagal ditulis.
    }

    [Fact]
    public void TulisCadangan_MelewatiBatasUkuran_DitimpaBukanTerusTumbuh()
    {
        Directory.CreateDirectory(_folderLog);
        var namaBerkasHariIni = $"{DateTimeOffset.Now:yyyyMMdd}.log";
        Directory.CreateDirectory(Path.Combine(_folderLog, namaBerkasHariIni)); // paksa gagal tulis utama saja

        var pathCadangan = Path.Combine(_folderLog, "_gagal-tulis.log");
        File.WriteAllText(pathCadangan, new string('x', 1024 * 1024 + 10));
        var ukuranSebelum = new FileInfo(pathCadangan).Length;
        Assert.True(ukuranSebelum >= 1024 * 1024);

        PencatatGalat.Catat(new InvalidOperationException("pemicu cadangan"), sumber: "UjiBatasCadangan", folderLog: _folderLog);

        var ukuranSesudah = new FileInfo(pathCadangan).Length;
        Assert.True(ukuranSesudah < ukuranSebelum, "berkas cadangan seharusnya ditimpa (direset) begitu melewati batas, bukan terus bertambah besar");
    }

    [Fact]
    public void Catat_TidakPernahMelemparWalauFolderLogTidakBisaDitulis()
    {
        // §4.7.3: folderLog sengaja dibuat menunjuk ke sebuah BERKAS
        // (bukan folder) supaya Directory.CreateDirectory pasti gagal —
        // cara yang portabel untuk memaksa kegagalan tulis tanpa
        // bergantung ACL/izin OS yang rapuh di lingkungan pengujian.
        var pathBerupaBerkas = Path.Combine(Path.GetTempPath(), $"absensi-log-sebagai-berkas-{Guid.NewGuid():N}");
        File.WriteAllText(pathBerupaBerkas, "bukan folder");

        try
        {
            for (var i = 0; i < 5; i++)
            {
                PencatatGalat.Catat(new InvalidOperationException($"percobaan {i}"), sumber: "UjiGagalTulis", folderLog: pathBerupaBerkas);
            }
        }
        finally
        {
            File.Delete(pathBerupaBerkas);
        }

        // Tidak ada Assert.Throws di sini dengan sengaja: kalau baris di
        // atas melempar, xUnit sudah menandai pengujian ini gagal dengan
        // sendirinya. Lulus berarti 5 percobaan berturut-turut TIDAK
        // pernah melempar dan TIDAK rekursi/stack overflow.
    }

    [Fact]
    public void Catat_RotasiHanyaMenyisakan30BerkasResmiTerbaru_BerkasAsingTidakTersentuh()
    {
        Directory.CreateDirectory(_folderLog);

        for (var i = 0; i < 35; i++)
        {
            var namaBerkas = $"{new DateTime(2000, 1, 1).AddDays(i):yyyyMMdd}.log";
            File.WriteAllText(Path.Combine(_folderLog, namaBerkas), "entri lama");
        }

        var asingTxt = Path.Combine(_folderLog, "catatan-manual.txt");
        var asingLogSalahPola = Path.Combine(_folderLog, "20200101.txt");
        File.WriteAllText(asingTxt, "jangan dihapus");
        File.WriteAllText(asingLogSalahPola, "jangan dihapus juga");

        PencatatGalat.Catat(new InvalidOperationException("pemicu rotasi"), sumber: "UjiRotasi", folderLog: _folderLog);

        var berkasResmiTersisa = Directory.GetFiles(_folderLog)
            .Select(Path.GetFileName)
            .Where(nama => Regex.IsMatch(nama!, @"^\d{8}\.log$"))
            .ToList();

        Assert.Equal(30, berkasResmiTersisa.Count);
        Assert.True(File.Exists(asingTxt));
        Assert.True(File.Exists(asingLogSalahPola));
        Assert.Equal("jangan dihapus", File.ReadAllText(asingTxt));

        var namaBerkasHariIni = $"{DateTimeOffset.Now:yyyyMMdd}.log";
        Assert.Contains(namaBerkasHariIni, berkasResmiTersisa);
    }

    private string BacaBerkasHariIni() =>
        File.ReadAllText(Path.Combine(_folderLog, $"{DateTimeOffset.Now:yyyyMMdd}.log"));

    public void Dispose()
    {
        if (Directory.Exists(_folderLog))
        {
            Directory.Delete(_folderLog, recursive: true);
        }
    }

    private sealed class PengecualianJejakPalsu : Exception
    {
        public override string StackTrace { get; }

        public PengecualianJejakPalsu(string jejakPalsu) => StackTrace = jejakPalsu;
    }
}
