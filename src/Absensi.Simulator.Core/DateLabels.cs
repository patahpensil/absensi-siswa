namespace Absensi.Simulator.Core;

// Nama hari dan bulan berbahasa Indonesia tanpa bergantung pada data budaya
// (simulator browser berjalan dengan InvariantGlobalization).
public static class DateLabels
{
    private static readonly string[] DayNames = { "Minggu", "Senin", "Selasa", "Rabu", "Kamis", "Jumat", "Sabtu" };

    private static readonly string[] MonthNames =
    {
        "Januari", "Februari", "Maret", "April", "Mei", "Juni",
        "Juli", "Agustus", "September", "Oktober", "November", "Desember",
    };

    public static string Indonesian(DateOnly date) =>
        $"{DayNames[(int)date.DayOfWeek]}, {date.Day} {MonthNames[date.Month - 1]} {date.Year}";
}
