using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace Absensi.App.Converters;

// Menyembunyikan elemen (mis. pesan galat) saat nilai yang di-bind null
// atau string kosong.
public sealed class NullKeVisibilitasConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        string.IsNullOrEmpty(value as string) ? Visibility.Collapsed : Visibility.Visible;

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}
