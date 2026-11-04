using Absensi.Domain.Kehadiran;
using Absensi.Simulator.Core;

namespace Absensi.Simulator.Core.Tests;

// Simulator browser harus memberi keputusan yang sama dengan aturan
// Absensi.Domain dan menerapkannya ke keadaan hari seperti PemrosesTap.
public sealed class SimulationSessionTests
{
    private static readonly DateOnly Thursday = new(2026, 9, 17);

    [Fact]
    public void BoundaryScenarios_AllRequiredCases_Pass()
    {
        var failed = BoundaryScenarios.RunAll().Where(r => !r.Passed).Select(r => $"{r.Name}: {r.Actual}").ToList();

        Assert.Empty(failed);
    }

    [Fact]
    public void DefaultFor_FridayAndSunday_FollowReferenceSchedule()
    {
        var friday = DaySettings.DefaultFor(new DateOnly(2026, 9, 18));
        var sunday = DaySettings.DefaultFor(new DateOnly(2026, 9, 20));

        Assert.Equal(new TimeOnly(10, 0), friday.EntryDeadline);
        Assert.Equal(new TimeOnly(11, 30), friday.DepartureStart);
        Assert.Equal(JenisHariKalender.Libur, sunday.DayType);
    }

    [Fact]
    public void Tap_UnregisteredCard_RejectedAndLogged()
    {
        var session = SimulationSession.CreateWithSampleData(Thursday);

        var entry = session.Tap("TIDAK-ADA", new TimeOnly(7, 0));

        Assert.Equal(JenisTindakanTap.Ditolak, entry.Decision.Tindakan);
        Assert.Equal(AlasanTap.KartuTidakTerdaftar, entry.Decision.Alasan);
        Assert.Single(session.Log);
    }

    [Fact]
    public void Tap_InactiveStudent_RejectedWithoutEntryTime()
    {
        var session = SimulationSession.CreateWithSampleData(Thursday);

        var entry = session.Tap("SISWA-004", new TimeOnly(7, 0));

        Assert.Equal(AlasanTap.PemilikTidakAktif, entry.Decision.Alasan);
        Assert.Null(session.FindPerson("SISWA-004")!.EntryTime);
    }

    [Fact]
    public void Tap_RegistrationModeActive_DoesNotChangeAttendance()
    {
        var session = SimulationSession.CreateWithSampleData(Thursday);
        session.Settings = session.Settings with { RegistrationModeActive = true };

        var entry = session.Tap("SISWA-001", new TimeOnly(7, 0));

        var person = session.FindPerson("SISWA-001")!;
        Assert.Equal(JenisTindakanTap.Registrasi, entry.Decision.Tindakan);
        Assert.Equal(StatusAbsensiSiswa.Alpa, person.StudentStatus);
        Assert.Null(person.EntryTime);
        Assert.Null(person.LastTap);
    }

    [Fact]
    public void FinishDay_StudentWithoutDepartureTap_BecomesTruantAndTeacherWithPresetUnchanged()
    {
        var session = SimulationSession.CreateWithSampleData(Thursday);
        session.Tap("SISWA-001", new TimeOnly(7, 10));
        session.SetTeacherPresetStatus("GURU-002", StatusAbsensiGuru.DinasLuar);
        session.Tap("GURU-002", new TimeOnly(7, 5));

        var changes = session.FinishDay();

        Assert.Equal(StatusAbsensiSiswa.Bolos, session.FindPerson("SISWA-001")!.StudentStatus);
        Assert.Equal("Dinas luar", session.FindPerson("GURU-002")!.StatusText);
        Assert.Contains(changes, c => c.StartsWith("Ani (contoh)"));
    }

    [Fact]
    public void ResetDay_ClearsStatusesTimesAndLog()
    {
        var session = SimulationSession.CreateWithSampleData(Thursday);
        session.Tap("SISWA-001", new TimeOnly(7, 10));

        session.ResetDay();

        var person = session.FindPerson("SISWA-001")!;
        Assert.Equal(StatusAbsensiSiswa.Alpa, person.StudentStatus);
        Assert.Null(person.EntryTime);
        Assert.Empty(session.Log);
    }

    [Fact]
    public void CorrectStudentStatus_TeacherCard_Rejected()
    {
        var session = SimulationSession.CreateWithSampleData(Thursday);

        Assert.Throws<InvalidOperationException>(() => session.CorrectStudentStatus("GURU-001", StatusAbsensiSiswa.Izin));
    }
}
