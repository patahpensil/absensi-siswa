using Absensi.Domain.Kehadiran;

namespace Absensi.Simulator.Core;

public sealed record BoundaryScenarioResult(string Name, string Expected, string Actual)
{
    public bool Passed => Expected == Actual;
}

// Kasus batas wajib CLAUDE.md §5, dijalankan lewat SimulationSession dengan
// konfigurasi sekolah rujukan (Kamis 17 Sept 2026 dan Jumat 18 Sept 2026).
public static class BoundaryScenarios
{
    private static readonly DateOnly Thursday = new(2026, 9, 17);
    private static readonly DateOnly Friday = new(2026, 9, 18);

    public static IReadOnlyList<BoundaryScenarioResult> RunAll() => new[]
    {
        FirstStudentTap("tap 07.29 → hadir", Thursday, new TimeOnly(7, 29), "Jam masuk tercatat: Hadir"),
        FirstStudentTap("tap 07.30.00 → hadir", Thursday, new TimeOnly(7, 30, 0), "Jam masuk tercatat: Hadir"),
        FirstStudentTap("tap 07.30.01 → terlambat", Thursday, new TimeOnly(7, 30, 1), "Jam masuk tercatat: Terlambat"),
        FirstStudentTap("tap 07.31 → terlambat", Thursday, new TimeOnly(7, 31), "Jam masuk tercatat: Terlambat"),
        SecondStudentTap("tap kedua 10 detik → ditolak", new TimeOnly(7, 0, 0), new TimeOnly(7, 0, 10), "Ditolak: tap terlalu cepat setelah tap sebelumnya"),
        SecondStudentTap("tap kedua 20 detik → diabaikan", new TimeOnly(7, 0, 0), new TimeOnly(7, 0, 20), "Diabaikan: sudah tap masuk, belum jam pulang"),
        FirstStudentTap("tap 12.01 tanpa masuk → ditolak", Thursday, new TimeOnly(12, 1), "Ditolak: lewat batas akhir tap masuk"),
        SecondStudentTap("tap 13.15 sudah masuk → jam pulang", new TimeOnly(7, 0), new TimeOnly(13, 15), "Jam pulang tercatat"),
        FirstStudentTap("Jumat tap 10.30 → ditolak", Friday, new TimeOnly(10, 30), "Ditolak: lewat batas akhir tap masuk"),
        SecondStudentTap("Jumat tap 11.35 → jam pulang", new TimeOnly(7, 0), new TimeOnly(11, 35), "Jam pulang tercatat", Friday),
        Holiday(),
        TeacherEntryOnly(),
        StudentOnLeave(),
    };

    private static SimulationSession NewSession(DateOnly date) => SimulationSession.CreateWithSampleData(date);

    private static BoundaryScenarioResult FirstStudentTap(string name, DateOnly date, TimeOnly time, string expected) =>
        new(name, expected, NewSession(date).Tap("SISWA-001", time).Description);

    private static BoundaryScenarioResult SecondStudentTap(string name, TimeOnly first, TimeOnly second, string expected, DateOnly? date = null)
    {
        var session = NewSession(date ?? Thursday);
        session.Tap("SISWA-001", first);
        return new(name, expected, session.Tap("SISWA-001", second).Description);
    }

    private static BoundaryScenarioResult Holiday()
    {
        var session = NewSession(Thursday);
        session.Settings = session.Settings with { DayType = JenisHariKalender.Libur };
        return new("tanggal libur → semua tap ditolak", "Ditolak: tanggal libur", session.Tap("SISWA-001", new TimeOnly(7, 0)).Description);
    }

    private static BoundaryScenarioResult TeacherEntryOnly()
    {
        var session = NewSession(Thursday);
        session.Tap("GURU-001", new TimeOnly(7, 0));
        session.FinishDay();
        return new("guru tap masuk saja → tidak lengkap", "Tidak lengkap", session.FindPerson("GURU-001")!.StatusText);
    }

    private static BoundaryScenarioResult StudentOnLeave()
    {
        var session = NewSession(Thursday);
        session.CorrectStudentStatus("SISWA-001", StatusAbsensiSiswa.Izin);
        var entry = session.Tap("SISWA-001", new TimeOnly(7, 0));
        var person = session.FindPerson("SISWA-001")!;
        var actual = $"{entry.Description}; jam masuk {(person.EntryTime is null ? "kosong" : "terisi")}";
        return new("siswa Izin tap → status dan jam tetap", "Tidak berubah: tetap Izin; jam masuk kosong", actual);
    }
}
