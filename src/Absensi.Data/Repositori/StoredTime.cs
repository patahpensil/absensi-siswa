using System.Globalization;

namespace Absensi.Data.Repositori;

// Clock-time text stored in SQLite (jadwal_hari, absensi_siswa, absensi_guru).
// Always written with InvariantCulture: in a custom .NET format ':' means
// "the current culture's time separator", which is '.' for id-ID, so a
// school PC or browser set to Indonesian would otherwise store "07.30".
// Reading accepts the canonical "HH:mm[:ss]" plus the legacy "HH.mm[.ss]"
// that such machines may already have written.
internal static class StoredTime
{
    private static readonly string[] ReadFormats = ["HH:mm:ss", "HH:mm", "HH.mm.ss", "HH.mm"];

    public static string Minutes(TimeOnly time) => time.ToString("HH:mm", CultureInfo.InvariantCulture);

    public static string Seconds(TimeOnly time) => time.ToString("HH:mm:ss", CultureInfo.InvariantCulture);

    public static TimeOnly Parse(string text) =>
        TimeOnly.ParseExact(text.Trim(), ReadFormats, CultureInfo.InvariantCulture, DateTimeStyles.None);
}
