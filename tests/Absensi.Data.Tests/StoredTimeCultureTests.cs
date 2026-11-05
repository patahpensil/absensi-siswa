using System.Globalization;
using Absensi.Data.Repositori;

namespace Absensi.Data.Tests;

// Clock times in SQLite must not depend on the device language. id-ID uses
// '.' as its time separator, so a culture-sensitive "HH:mm" would store
// "07.30" on an Indonesian PC or browser and "07:30" elsewhere.
public sealed class StoredTimeCultureTests
{
    private static T WithCulture<T>(string name, Func<T> action)
    {
        var previous = CultureInfo.CurrentCulture;
        CultureInfo.CurrentCulture = new CultureInfo(name);
        try { return action(); }
        finally { CultureInfo.CurrentCulture = previous; }
    }

    private static string? ScalarText(MigratedDatabase database, string sql)
    {
        using var command = database.Connection.CreateCommand();
        command.CommandText = sql;
        return command.ExecuteScalar() as string;
    }

    [Fact]
    public void IndonesianCulture_SeparatorIsDot_SoTheTestReproducesTheRisk()
    {
        Assert.Equal("07.30", WithCulture("id-ID", () => new TimeOnly(7, 30).ToString("HH:mm")));
    }

    [Fact]
    public void ScheduleSavedUnderIndonesianCulture_StoredWithColonAndReadBackUnderAnotherCulture()
    {
        using var database = new MigratedDatabase();
        database.Execute("""
            INSERT INTO tahun_ajaran (id, nama, semester, tgl_mulai, tgl_selesai, aktif)
            VALUES (1, '2026/2027', 'ganjil', '2026-07-01', '2026-12-19', 1);
            """);
        var repository = new RepositoriJadwalHari();

        WithCulture("id-ID", () =>
        {
            using var transaction = database.Connection.BeginTransaction();
            repository.Perbarui(database.Connection, transaction, tahunAjaranId: 1, new JadwalHariBaris(
                4, true, new TimeOnly(7, 30), new TimeOnly(8, 10), new TimeOnly(12, 0), new TimeOnly(13, 10), new TimeOnly(8, 0)));
            transaction.Commit();
            return 0;
        });

        Assert.Equal("07:30", ScalarText(database, "SELECT jam_masuk FROM jadwal_hari WHERE hari = 4;"));
        Assert.Equal("13:10", ScalarText(database, "SELECT mulai_pulang FROM jadwal_hari WHERE hari = 4;"));

        var row = WithCulture("en-US", () => repository.CariUntukHari(database.Connection, tahunAjaranId: 1, hari: 4));
        Assert.Equal(new TimeOnly(7, 30), row!.JamMasuk);
        Assert.Equal(new TimeOnly(8, 0), row.NotTappedListTime);
    }

    [Fact]
    public void LegacyDotSeparatedTimes_ReadUnderAnyCulture()
    {
        using var database = new MigratedDatabase();
        database.SeedBaselineReferenceRows();
        database.Execute("""
            INSERT INTO jadwal_hari (tahun_ajaran_id, hari, hari_sekolah, jam_masuk, jam_rekap, batas_tap_masuk, mulai_pulang)
            VALUES (1, 4, 1, '07.30', '08.10', '12.00', '13.10');
            INSERT INTO absensi_siswa (tanggal, siswa_id, kelas_id, status, jam_masuk, jam_pulang, sumber)
            VALUES ('2026-09-17', 1, 1, 'terlambat', '07.30.05', '13.10', 'tap');
            """);

        foreach (var culture in new[] { "en-US", "id-ID", "" })
        {
            var schedule = WithCulture(culture, () => new RepositoriJadwalHari().CariUntukHari(database.Connection, 1, 4));
            Assert.Equal(new TimeOnly(7, 30), schedule!.JamMasuk);
            Assert.Equal(new TimeOnly(8, 10), schedule.JamRekap);

            var attendance = WithCulture(culture, () => new RepositoriAbsensi()
                .DaftarSiswaUntukRentang(database.Connection, new DateOnly(2026, 9, 17), new DateOnly(2026, 9, 17), kelasId: null)
                .Single());
            Assert.Equal(new TimeOnly(7, 30, 5), attendance.JamMasuk);
            Assert.Equal(new TimeOnly(13, 10), attendance.JamPulang);
        }
    }

    [Theory]
    [InlineData("07:30", 7, 30, 0)]
    [InlineData("07:30:01", 7, 30, 1)]
    [InlineData("07.30", 7, 30, 0)]
    [InlineData("07.30.01", 7, 30, 1)]
    public void StoredTime_AcceptsCanonicalAndLegacyText(string text, int hour, int minute, int second)
    {
        Assert.Equal(new TimeOnly(hour, minute, second), StoredTime.Parse(text));
    }

    [Fact]
    public void StoredTime_WritesColonUnderIndonesianCulture()
    {
        Assert.Equal("07:30:01", WithCulture("id-ID", () => StoredTime.Seconds(new TimeOnly(7, 30, 1))));
        Assert.Equal("07:30", WithCulture("id-ID", () => StoredTime.Minutes(new TimeOnly(7, 30))));
    }
}
