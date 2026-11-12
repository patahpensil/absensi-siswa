using Absensi.Data.Koreksi;
using Absensi.Data.Repositori;
using Absensi.Domain.Kehadiran;

namespace Absensi.Data.Tests;

// T3 (keputusan pemilik 12 November 2026): hari sekolah lampau yang tidak
// difinalisasi karena PC mati diberi aturan akhir hari saat aplikasi dibuka,
// tanpa notifikasi.
public sealed class MissedDayFinalizationTests
{
    private static readonly DateOnly Thursday = new(2026, 9, 17);
    private static readonly DateOnly Friday = new(2026, 9, 18);

    [Fact]
    public void OpeningNextDay_StudentWithoutDeparture_BecomesBolosAndTeacherIncomplete_WithoutNotification()
    {
        using var database = TappedInOnThursday();
        var outboxBefore = Count(database, "SELECT COUNT(*) FROM outbox;");

        new PembukaanHari().Jalankan(database.Connection, Friday);

        Assert.Equal(StatusAbsensiSiswa.Bolos, new RepositoriAbsensi().CariHariIniSiswa(database.Connection, Thursday, 1)!.Status);
        Assert.Equal(StatusAbsensiGuru.TidakLengkap, new RepositoriAbsensi().CariHariIniGuru(database.Connection, Thursday, 1)!.Status);
        Assert.Equal(outboxBefore, Count(database, "SELECT COUNT(*) FROM outbox;"));
        Assert.False(new LocalAttendanceClosure().IsClosed(database.Connection, Thursday));
    }

    [Fact]
    public void FinalizeMissedDays_SecondRun_ChangesNothing()
    {
        using var database = TappedInOnThursday();

        Assert.Equal(2, new LocalAttendanceClosure().FinalizeMissedDays(database.Connection, Friday));
        Assert.Equal(0, new LocalAttendanceClosure().FinalizeMissedDays(database.Connection, Friday));
    }

    [Fact]
    public void FinalizeMissedDays_Today_NotTouched()
    {
        using var database = TappedInOnThursday();

        new LocalAttendanceClosure().FinalizeMissedDays(database.Connection, Thursday);

        Assert.Equal(StatusAbsensiSiswa.Terlambat, new RepositoriAbsensi().CariHariIniSiswa(database.Connection, Thursday, 1)!.Status);
    }

    [Fact]
    public void FinalizeMissedDays_CorrectedStatus_Kept()
    {
        using var database = TappedInOnThursday();
        database.Execute("UPDATE absensi_siswa SET sumber = 'koreksi' WHERE tanggal = '2026-09-17' AND siswa_id = 1;");

        new LocalAttendanceClosure().FinalizeMissedDays(database.Connection, Friday);

        Assert.Equal(StatusAbsensiSiswa.Terlambat, new RepositoriAbsensi().CariHariIniSiswa(database.Connection, Thursday, 1)!.Status);
    }

    [Fact]
    public void FinalizeMissedDays_DepartureTapped_StaysPresent()
    {
        using var database = TappedInOnThursday();
        new PemrosesTap().Proses(database.Connection, "UID-SISWA-1", Thursday.ToDateTime(new TimeOnly(13, 20)), modeRegistrasiAktif: false);

        new LocalAttendanceClosure().FinalizeMissedDays(database.Connection, Friday);

        Assert.Equal(StatusAbsensiSiswa.Terlambat, new RepositoriAbsensi().CariHariIniSiswa(database.Connection, Thursday, 1)!.Status);
    }

    private static MigratedDatabase TappedInOnThursday()
    {
        var database = new MigratedDatabase();
        database.SeedSekolahUntukTap();
        new PembukaanHari().Jalankan(database.Connection, Thursday);
        new PemrosesTap().Proses(database.Connection, "UID-SISWA-1", Thursday.ToDateTime(new TimeOnly(7, 40)), modeRegistrasiAktif: false);
        new PemrosesTap().Proses(database.Connection, "UID-GURU-1", Thursday.ToDateTime(new TimeOnly(7, 10)), modeRegistrasiAktif: false);
        return database;
    }

    private static long Count(MigratedDatabase database, string sql)
    {
        using var command = database.Connection.CreateCommand();
        command.CommandText = sql;
        return (long)command.ExecuteScalar()!;
    }
}
