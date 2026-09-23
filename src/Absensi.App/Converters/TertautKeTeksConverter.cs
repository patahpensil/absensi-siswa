using System.Globalization;
using System.Windows.Data;

namespace Absensi.App.Converters;

// Kolom "Tertaut" (pairing Telegram) di menu Notifikasi: sebelumnya menampilkan
// nilai Boolean mentah "True"/"False". Sekarang "Tertaut" / "Belum".
public sealed class TertautKeTeksConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        value is true ? "Tertaut" : "Belum";

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}
