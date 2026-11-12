using Absensi.Data.Pengaturan;
using Absensi.Data.Repositori;
using Absensi.Data.Waktu;
using Absensi.Domain.Akun;
using Absensi.Domain.Kehadiran;

namespace Absensi.Data.Tests;

// PRD §7 P-15 (jadwal berlaku mulai tanggal, versi lama tetap dipakai untuk
// tanggal lama) dan identitas sekolah pada Pengaturan (PANDUAN-UI-UX §9).
public sealed class ScheduleAndSchoolSettingsTests
{
    private static readonly DateTime Today = new(2026, 9, 17, 9, 0, 0); // Kamis
    private static readonly DateOnly NextMonday = new(2026, 9, 21);
    private static readonly AkunTersimpan Admin = new(1, "admin", "x", PeranAkun.Admin, null, true);
    private static readonly AkunTersimpan Wali = new(2, "wali", "x", PeranAkun.WaliKelas, 1, true);

    [Fact]
    public void ScheduleFrom_FutureDate_OldDatesKeepOldScheduleNewDatesUseNewOne()
    {
        using var database = Prepare();

        new LayananJadwalHari().ScheduleFrom(database.Connection, Admin, Today, 1, NextMonday, Week(mondayArrival: new TimeOnly(7, 0)));

        var repository = new RepositoriJadwalHari();
        Assert.Equal(new TimeOnly(7, 30), repository.CariUntukHari(database.Connection, 1, 1, tanggal: new DateOnly(2026, 9, 14))!.JamMasuk);
        Assert.Equal(new TimeOnly(7, 0), repository.CariUntukHari(database.Connection, 1, 1, tanggal: NextMonday)!.JamMasuk);
        Assert.Equal(new TimeOnly(7, 0), repository.CariUntukHari(database.Connection, 1, 1, tanggal: new DateOnly(2026, 9, 28))!.JamMasuk);
        // Without a date the base version (jadwal_hari) is unchanged.
        Assert.Equal(new TimeOnly(7, 30), repository.CariUntukHari(database.Connection, 1, 1)!.JamMasuk);
    }

    [Fact]
    public void ScheduleFrom_TapsUseScheduleOfTheirOwnDate()
    {
        using var database = Prepare();
        new LayananJadwalHari().ScheduleFrom(database.Connection, Admin, Today, 1, NextMonday, Week(mondayArrival: new TimeOnly(7, 0)));
        var oldMonday = new DateOnly(2026, 9, 14);
        new PembukaanHari().Jalankan(database.Connection, oldMonday);
        new PembukaanHari().Jalankan(database.Connection, NextMonday);

        new PemrosesTap().Proses(database.Connection, "UID-SISWA-1", oldMonday.ToDateTime(new TimeOnly(7, 15)), modeRegistrasiAktif: false);
        new PemrosesTap().Proses(database.Connection, "UID-SISWA-1", NextMonday.ToDateTime(new TimeOnly(7, 15)), modeRegistrasiAktif: false);

        Assert.Equal(StatusAbsensiSiswa.Hadir, new RepositoriAbsensi().CariHariIniSiswa(database.Connection, oldMonday, 1)!.Status);
        Assert.Equal(StatusAbsensiSiswa.Terlambat, new RepositoriAbsensi().CariHariIniSiswa(database.Connection, NextMonday, 1)!.Status);
    }

    [Fact]
    public void ScheduleFrom_SaturdayBecomesHoliday_CalendarChangesOnlyFromEffectiveDate()
    {
        using var database = Prepare();
        var effective = new DateOnly(2026, 10, 3);

        new LayananJadwalHari().ScheduleFrom(database.Connection, Admin, Today, 1, effective, Week(saturdaySchool: false));

        var calendar = new RepositoriKalender();
        Assert.Equal(JenisHariKalender.Sekolah, calendar.TentukanJenisHari(database.Connection, new DateOnly(2026, 9, 26), 1));
        Assert.Equal(JenisHariKalender.Libur, calendar.TentukanJenisHari(database.Connection, effective, 1));
    }

    [Theory]
    [InlineData(2026, 9, 17)]
    [InlineData(2026, 9, 1)]
    [InlineData(2027, 1, 4)]
    public void ScheduleFrom_TodayPastOrOutsidePeriod_RejectedWithoutChange(int year, int month, int day)
    {
        using var database = Prepare();

        Assert.Throws<InvalidOperationException>(() =>
            new LayananJadwalHari().ScheduleFrom(database.Connection, Admin, Today, 1, new DateOnly(year, month, day), Week(mondayArrival: new TimeOnly(7, 0))));

        Assert.Empty(new RepositoriJadwalHari().ListChanges(database.Connection, 1));
    }

    [Fact]
    public void ScheduleFrom_HomeroomTeacher_Rejected()
    {
        using var database = Prepare();

        Assert.Throws<InvalidOperationException>(() =>
            new LayananJadwalHari().ScheduleFrom(database.Connection, Wali, Today, 1, NextMonday, Week()));
    }

    [Fact]
    public void ScheduleFrom_TimesOutOfOrder_RejectedWithDayName()
    {
        using var database = Prepare();

        var error = Assert.Throws<InvalidOperationException>(() =>
            new LayananJadwalHari().ScheduleFrom(database.Connection, Admin, Today, 1, NextMonday, Week(mondayArrival: new TimeOnly(9, 0))));

        Assert.StartsWith("Senin", error.Message);
    }

    [Fact]
    public void ScheduleFrom_WritesOldAndNewScheduleToAudit()
    {
        using var database = Prepare();

        new LayananJadwalHari().ScheduleFrom(database.Connection, Admin, Today, 1, NextMonday, Week(mondayArrival: new TimeOnly(7, 0)));

        var newValue = (string)Scalar(database, "SELECT nilai_baru FROM audit WHERE aksi = 'jadwal_berlaku_mulai';")!;
        Assert.Contains("berlaku_mulai=2026-09-21", newValue);
        Assert.Contains("Senin=hadir 07:00", newValue);
        Assert.Contains("Senin=hadir 07:30", (string)Scalar(database, "SELECT nilai_lama FROM audit WHERE aksi = 'jadwal_berlaku_mulai';")!);
    }

    [Fact]
    public void NewAcademicPeriod_CopiesScheduleInForceAtItsStart()
    {
        using var database = Prepare();
        new LayananJadwalHari().ScheduleFrom(database.Connection, Admin, Today, 1, NextMonday, Week(mondayArrival: new TimeOnly(7, 0)));

        var nextId = new Absensi.Data.MasterData.AcademicPeriodService().Prepare(
            database.Connection, Admin, Today, "2026/2027", "genap", new DateOnly(2027, 1, 4), new DateOnly(2027, 6, 30));

        Assert.Equal(new TimeOnly(7, 0), new RepositoriJadwalHari().CariUntukHari(database.Connection, nextId, 1)!.JamMasuk);
    }

    [Fact]
    public void SchoolIdentity_Update_StoredAndAudited()
    {
        using var database = Prepare();

        new SchoolIdentityService().Update(database.Connection, Admin, Today, new SchoolIdentity(" SMP Uji Baru (contoh) ", "12345678", "Jl. Contoh No. 1"));

        var identity = new RepositoriSekolah().FindIdentity(database.Connection)!;
        Assert.Equal(new SchoolIdentity("SMP Uji Baru (contoh)", "12345678", "Jl. Contoh No. 1"), identity);
        Assert.Contains("nama=SMP Uji Baru (contoh)", (string)Scalar(database, "SELECT nilai_baru FROM audit WHERE aksi = 'ubah_identitas_sekolah';")!);
    }

    [Theory]
    [InlineData("", null)]
    [InlineData("SMP Uji", "1234")]
    [InlineData("SMP Uji", "1234567A")]
    public void SchoolIdentity_InvalidValues_Rejected(string name, string? npsn)
    {
        using var database = Prepare();

        Assert.Throws<InvalidOperationException>(() =>
            new SchoolIdentityService().Update(database.Connection, Admin, Today, new SchoolIdentity(name, npsn, null)));

        Assert.Equal("SMP Uji (contoh)", new RepositoriSekolah().FindIdentity(database.Connection)!.Name);
    }

    [Fact]
    public void SchoolIdentity_HomeroomTeacher_Rejected()
    {
        using var database = Prepare();

        Assert.Throws<InvalidOperationException>(() =>
            new SchoolIdentityService().Update(database.Connection, Wali, Today, new SchoolIdentity("SMP Lain", null, null)));
    }

    [Fact]
    public void SchoolLogo_Set_StoredAndAudited()
    {
        using var database = Prepare();

        new SchoolIdentityService().SetLogo(database.Connection, Admin, Today, "logo-sekolah.png");

        Assert.Equal("logo-sekolah.png", new RepositoriSekolah().CariSatuSatunya(database.Connection)!.LogoPath);
        Assert.Contains("logo=logo-sekolah.png", (string)Scalar(database, "SELECT nilai_baru FROM audit WHERE aksi = 'ubah_logo_sekolah';")!);
    }

    [Theory]
    [InlineData("../logo.png")]
    [InlineData("logo.exe")]
    [InlineData("folder/logo.png")]
    [InlineData("")]
    public void SchoolLogo_UnsafeName_Rejected(string fileName)
    {
        using var database = Prepare();

        Assert.Throws<InvalidOperationException>(() => new SchoolIdentityService().SetLogo(database.Connection, Admin, Today, fileName));
        Assert.Null(new RepositoriSekolah().CariSatuSatunya(database.Connection)!.LogoPath);
    }

    [Fact]
    public void SchoolLogo_HomeroomTeacher_Rejected()
    {
        using var database = Prepare();

        Assert.Throws<InvalidOperationException>(() => new SchoolIdentityService().SetLogo(database.Connection, Wali, Today, "logo-sekolah.png"));
    }

    private static MigratedDatabase Prepare()
    {
        var database = new MigratedDatabase();
        database.SeedSekolahUntukTap();
        database.Execute("""
            INSERT OR IGNORE INTO akun (id, username, sandi_hash, peran, guru_id, aktif, dibuat_pada)
            VALUES (1, 'admin', 'x', 'admin', NULL, 1, '2026-09-01T00:00:00');
            INSERT OR IGNORE INTO akun (id, username, sandi_hash, peran, guru_id, aktif, dibuat_pada)
            VALUES (2, 'wali', 'x', 'wali_kelas', 1, 1, '2026-09-01T00:00:00');
            INSERT OR IGNORE INTO sekolah (id, nama) VALUES (1, 'SMP Uji (contoh)');
            """);
        return database;
    }

    private static List<JadwalHariBaris> Week(TimeOnly? mondayArrival = null, bool saturdaySchool = true) =>
        Enumerable.Range(1, 7).Select(day => day switch
        {
            7 => new JadwalHariBaris(7, false, null, null, null, null),
            6 when !saturdaySchool => new JadwalHariBaris(6, false, null, null, null, null),
            5 => SchoolDay(5, new TimeOnly(7, 30), new TimeOnly(10, 0), new TimeOnly(11, 30)),
            1 => SchoolDay(1, mondayArrival ?? new TimeOnly(7, 30), new TimeOnly(12, 0), new TimeOnly(13, 10)),
            _ => SchoolDay(day, new TimeOnly(7, 30), new TimeOnly(12, 0), new TimeOnly(13, 10)),
        }).ToList();

    private static JadwalHariBaris SchoolDay(int day, TimeOnly arrival, TimeOnly deadline, TimeOnly departure) =>
        new(day, true, arrival, new TimeOnly(8, 10), deadline, departure, new TimeOnly(8, 0));

    private static object? Scalar(MigratedDatabase database, string sql)
    {
        using var command = database.Connection.CreateCommand();
        command.CommandText = sql;
        return command.ExecuteScalar();
    }
}
