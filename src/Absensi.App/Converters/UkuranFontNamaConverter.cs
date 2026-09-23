using System.Globalization;
using System.Windows;
using System.Windows.Data;
using Absensi.App.Gerbang;

namespace Absensi.App.Converters;

// Layar Gerbang: ukuran font nama = ukuran dasar tingkat layar
// (TipografiGerbang.FontNama) diturunkan menurut panjang nama —
// KartuTapViewModel.UkuranFontUntukNama. values[0] = nama (string),
// values[1] = ukuran dasar (double).
public sealed class UkuranFontNamaConverter : IMultiValueConverter
{
    public object Convert(object[] values, Type targetType, object? parameter, CultureInfo culture)
    {
        var nama = values.Length > 0 ? values[0] as string : null;
        var dasar = values.Length > 1 && values[1] is double d ? d : 64;
        return KartuTapViewModel.UkuranFontUntukNama(nama, dasar);
    }

    public object[] ConvertBack(object value, Type[] targetTypes, object? parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}

// Jarak baris nama (LineHeight) = ukuran font nama efektif x faktor tingkat
// layar (TipografiGerbang.FaktorJarakBarisNama). Faktor <= 0 -> NaN =
// jarak baris bawaan font (tingkat besar, tampilan 1920x1080 tak berubah).
// values[0] = nama, values[1] = ukuran dasar, values[2] = faktor.
public sealed class JarakBarisNamaConverter : IMultiValueConverter
{
    public object Convert(object[] values, Type targetType, object? parameter, CultureInfo culture)
    {
        var nama = values.Length > 0 ? values[0] as string : null;
        var dasar = values.Length > 1 && values[1] is double d ? d : 64;
        var faktor = values.Length > 2 && values[2] is double f ? f : 0;
        return faktor <= 0 ? double.NaN : KartuTapViewModel.UkuranFontUntukNama(nama, dasar) * faktor;
    }

    public object[] ConvertBack(object value, Type[] targetTypes, object? parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}
