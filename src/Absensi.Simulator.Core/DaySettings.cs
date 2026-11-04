using Absensi.Domain.Kehadiran;

namespace Absensi.Simulator.Core;

// Konfigurasi satu hari yang di aplikasi desktop dibaca dari jadwal_hari,
// kalender, pengaturan jeda tap, dan status mode registrasi.
public sealed record DaySettings(
    DateOnly Date,
    JenisHariKalender DayType,
    TimeOnly EntryTime,
    TimeOnly EntryDeadline,
    TimeOnly DepartureStart,
    TimeSpan MinimumTapInterval,
    bool RegistrationModeActive)
{
    // Isian awal mengikuti jadwal sekolah rujukan di PRD §7 (Senin–Kamis dan
    // Sabtu: batas tap masuk 12.00, pulang 13.10; Jumat: 10.00 dan 11.30;
    // Minggu libur) dan jeda tap bawaan 15 detik. Semua nilai tetap dapat
    // diubah di layar simulator, seperti di menu Pengaturan aplikasi.
    public static DaySettings DefaultFor(DateOnly date)
    {
        var isFriday = date.DayOfWeek == DayOfWeek.Friday;
        return new DaySettings(
            date,
            date.DayOfWeek == DayOfWeek.Sunday ? JenisHariKalender.Libur : JenisHariKalender.Sekolah,
            EntryTime: new TimeOnly(7, 30),
            EntryDeadline: isFriday ? new TimeOnly(10, 0) : new TimeOnly(12, 0),
            DepartureStart: isFriday ? new TimeOnly(11, 30) : new TimeOnly(13, 10),
            MinimumTapInterval: TimeSpan.FromSeconds(15),
            RegistrationModeActive: false);
    }
}
