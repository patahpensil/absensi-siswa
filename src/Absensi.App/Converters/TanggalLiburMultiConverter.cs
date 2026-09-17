using System.Globalization;
using System.Windows.Data;

namespace Absensi.App.Converters;

// Dipakai Kalender.CalendarDayButtonStyle (Kalender/KalenderView.xaml) untuk
// "memblok" tampilan tanggal libur (PRD §6) — nilai[0] adalah tanggal sel
// kalender (DataContext bawaan CalendarDayButton), nilai[1] adalah
// KalenderViewModel.TanggalLibur. Tidak memakai Calendar.BlackoutDates
// bawaan karena itu membuat tanggal tidak bisa dipilih lagi — padahal
// tanggal libur perlu tetap bisa dipilih ulang untuk dikembalikan jadi
// hari sekolah (MPLS/Sumatif, PRD §6).
public sealed class TanggalLiburMultiConverter : IMultiValueConverter
{
    public object Convert(object[] values, Type targetType, object? parameter, CultureInfo culture)
    {
        if (values.Length != 2 || values[0] is not DateTime tanggal || values[1] is not HashSet<DateOnly> daftarLibur)
        {
            return false;
        }

        return daftarLibur.Contains(DateOnly.FromDateTime(tanggal));
    }

    public object[] ConvertBack(object value, Type[] targetTypes, object? parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}
