using System.Globalization;
using System.Windows.Data;

namespace Absensi.App.Converters;

// UAT Bug #11 (2026-09-23): kolom Kartu panel Siswa dulu menampilkan
// True/False (Boolean.ToString() mentah) — sekarang menampilkan UID
// sebenarnya kalau siswa sudah punya kartu aktif, atau "Belum terdaftar"
// kalau belum. SiswaDaftar.KartuUid null berarti belum terdaftar.
public sealed class KartuUidKeTeksConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        value is string uid && !string.IsNullOrWhiteSpace(uid) ? uid : "Belum terdaftar";

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}
