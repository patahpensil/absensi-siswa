using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace Absensi.App.Converters;

// Menyembunyikan elemen (mis. pesan galat, hasil impor) saat nilai yang
// di-bind null atau string kosong; nilai bukan-string dianggap "ada" bila
// tidak null.
public sealed class NullKeVisibilitasConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is string teks)
        {
            return string.IsNullOrEmpty(teks) ? Visibility.Collapsed : Visibility.Visible;
        }

        return value is null ? Visibility.Collapsed : Visibility.Visible;
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}
