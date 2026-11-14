using Absensi.Domain.Kehadiran;

namespace Absensi.Data.Tests;

// Simulates an application that stays open from Thursday into Friday without
// being reloaded (PRD §16.7).
public sealed class CurrentDayGuardTests
{
    private static readonly DateOnly Thursday = new(2026, 9, 17);
    private static readonly DateOnly Friday = new(2026, 9, 18);
    private static readonly DateTime FridayMorning = Friday.ToDateTime(new TimeOnly(7, 0));

    [Fact]
    public void TapAfterMidnight_WithoutOpeningTheDay_IsNotStored()
    {
        using var database = new MigratedDatabase();
        database.SeedSekolahUntukTap();
        new PembukaanHari().Jalankan(database.Connection, Thursday);

        new PemrosesTap().Proses(database.Connection, "UID-SISWA-1", FridayMorning, modeRegistrasiAktif: false);

        Assert.Equal(0, CountStudentRows(database, Friday));
    }

    [Fact]
    public void TapAfterMidnight_AfterEnsureOpened_IsStored()
    {
        using var database = new MigratedDatabase();
        database.SeedSekolahUntukTap();
        var guard = new CurrentDayGuard();
        guard.Open(database.Connection, Thursday);

        Assert.True(guard.EnsureOpened(database.Connection, Friday));
        var decision = new PemrosesTap().Proses(database.Connection, "UID-SISWA-1", FridayMorning, modeRegistrasiAktif: false);

        Assert.Equal(JenisTindakanTap.IsiJamMasuk, decision.Tindakan);
        Assert.Equal("hadir", StudentStatus(database, Friday));
        Assert.Equal(Friday, guard.OpenedDay);
    }

    [Fact]
    public void EnsureOpened_SameDay_DoesNotRunAgain()
    {
        using var database = new MigratedDatabase();
        database.SeedSekolahUntukTap();
        var guard = new CurrentDayGuard();

        Assert.True(guard.EnsureOpened(database.Connection, Thursday));
        database.Execute("DELETE FROM absensi_siswa;");

        Assert.False(guard.EnsureOpened(database.Connection, Thursday));
        Assert.Equal(0, CountStudentRows(database, Thursday));
    }

    [Fact]
    public void EnsureOpened_AfterForget_RunsAgain()
    {
        using var database = new MigratedDatabase();
        database.SeedSekolahUntukTap();
        var guard = new CurrentDayGuard();
        guard.EnsureOpened(database.Connection, Thursday);
        database.Execute("DELETE FROM absensi_siswa;");

        guard.Forget();

        Assert.True(guard.EnsureOpened(database.Connection, Thursday));
        Assert.Equal(1, CountStudentRows(database, Thursday));
    }

    [Fact]
    public void EnsureOpened_WithoutActiveAcademicPeriod_DoesNothing()
    {
        using var database = new MigratedDatabase();
        var guard = new CurrentDayGuard();

        Assert.False(guard.EnsureOpened(database.Connection, Thursday));
        Assert.Null(guard.OpenedDay);
    }

    private static int CountStudentRows(MigratedDatabase database, DateOnly day)
    {
        using var command = database.Connection.CreateCommand();
        command.CommandText = "SELECT COUNT(*) FROM absensi_siswa WHERE tanggal = $day;";
        command.Parameters.AddWithValue("$day", day.ToString("yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture));
        return Convert.ToInt32(command.ExecuteScalar(), System.Globalization.CultureInfo.InvariantCulture);
    }

    private static string? StudentStatus(MigratedDatabase database, DateOnly day)
    {
        using var command = database.Connection.CreateCommand();
        command.CommandText = "SELECT status FROM absensi_siswa WHERE tanggal = $day AND siswa_id = 1;";
        command.Parameters.AddWithValue("$day", day.ToString("yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture));
        return command.ExecuteScalar() as string;
    }
}
