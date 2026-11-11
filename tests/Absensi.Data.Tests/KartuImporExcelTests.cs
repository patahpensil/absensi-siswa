using Absensi.Data.MasterData;
using Absensi.Data.Repositori;
using Absensi.Domain.Akun;
using Absensi.Domain.Kehadiran;
using ClosedXML.Excel;

namespace Absensi.Data.Tests;

public sealed class KartuImporExcelTests
{
    private static readonly AkunTersimpan Admin = new(1, "admin1", "x", PeranAkun.Admin, null, true);
    private static readonly DateTime Waktu = new(2026, 9, 17, 8, 0, 0);

    [Fact]
    public void ImporUlangSiswa_MempertahankanKartuDanNolDiDepan()
    {
        using var database = new MigratedDatabase();
        database.SeedMasterDataUntukUji();
        var impor = new ImporSiswaExcel();
        using var denganKartu = BuatBerkas(impor.BuatTemplate(),
            ["0012345678", "Siswa Uji", "L", "", "", "", "7A", "", "", "0000012345"]);

        var analisis = impor.Analisis(database.Connection, 1, denganKartu);
        Assert.False(analisis.AdaKesalahan);
        Assert.Equal("0000012345", Assert.Single(analisis.Baris).NoKartu);
        var hasil = impor.Impor(database.Connection, Admin, Waktu, 1, analisis.Baris);
        Assert.Equal(1, hasil.Ditambah);
        Assert.Equal(1, hasil.KartuDitautkan);

        denganKartu.Position = 0;
        var sama = impor.Analisis(database.Connection, 1, denganKartu);
        var ulang = impor.Impor(database.Connection, Admin, Waktu, 1, sama.Baris);
        Assert.Equal(0, ulang.KartuDitautkan);
        Assert.Equal(1, ulang.KartuTetap);

        using var tanpaKartu = BuatBerkas(impor.BuatTemplate(),
            ["0012345678", "Siswa Diperbarui", "L", "", "", "", "7A", "", "", ""]);
        var tanpaUid = impor.Analisis(database.Connection, 1, tanpaKartu);
        var diperbarui = impor.Impor(database.Connection, Admin, Waktu, 1, tanpaUid.Baris);
        Assert.Equal(1, diperbarui.KartuTetap);

        using var kartuLain = BuatBerkas(impor.BuatTemplate(),
            ["0012345678", "Siswa Diperbarui", "L", "", "", "", "7A", "", "", "0000019999"]);
        var konflik = impor.Analisis(database.Connection, 1, kartuLain);
        Assert.Contains("kartu aktif", Assert.Single(konflik.Baris).TeksKesalahan);
        var kartu = new RepositoriKartu().CariAktifBerdasarkanUid(database.Connection, "0000012345");
        Assert.Equal(JenisPemilikKartu.Siswa, kartu?.PemilikTipe);
    }

    [Fact]
    public void ImporGuru_KonflikSetelahPratinjauMembatalkanSemuaPerubahanDanAudit()
    {
        using var database = new MigratedDatabase();
        database.SeedMasterDataUntukUji();
        var impor = new ImporGuruExcel();
        using var workbook = BuatBerkas(impor.BuatTemplate(),
            ["Guru A", "000001", "", "", "0000022221"],
            ["Guru B", "000002", "", "", "0000022222"]);
        var analisis = impor.Analisis(database.Connection, workbook);
        Assert.False(analisis.AdaKesalahan);

        var kartu = new RepositoriKartu();
        using (var tx = database.Connection.BeginTransaction())
        {
            kartu.Daftarkan(database.Connection, tx, "0000022222", JenisPemilikKartu.Siswa, 99, Waktu);
            tx.Commit();
        }

        Assert.Throws<InvalidOperationException>(() => impor.Impor(database.Connection, Admin, Waktu, analisis.Baris));
        Assert.Null(new RepositoriGuru().CariIdBerdasarkanNip(database.Connection, "000001"));
        Assert.Null(new RepositoriGuru().CariIdBerdasarkanNip(database.Connection, "000002"));
        Assert.Null(kartu.CariAktifBerdasarkanUid(database.Connection, "0000022221"));
        using var audit = database.Connection.CreateCommand();
        audit.CommandText = "SELECT COUNT(*) FROM audit WHERE aksi = 'impor_guru';";
        Assert.Equal(0L, audit.ExecuteScalar());
    }

    private static MemoryStream BuatBerkas(byte[] template, params string[][] rows)
    {
        using var input = new MemoryStream(template);
        using var workbook = new XLWorkbook(input);
        var worksheet = workbook.Worksheet(1);
        for (var row = 0; row < rows.Length; row++)
        {
            for (var column = 0; column < rows[row].Length; column++)
            {
                worksheet.Cell(row + 2, column + 1).SetValue(rows[row][column]);
            }
        }

        var output = new MemoryStream();
        workbook.SaveAs(output);
        output.Position = 0;
        return output;
    }
}
