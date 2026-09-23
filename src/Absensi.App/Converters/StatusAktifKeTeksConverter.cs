using System.Globalization;
using System.Windows.Data;

namespace Absensi.App.Converters;

// Kolom Status di tabel Data Guru: dulu mengikat langsung ke
// AlasanNonaktif (kosong untuk guru aktif, "pensiun"/"pindah" mentah untuk
// yang tidak) sehingga kolom hampir selalu kosong. Sekarang teks status
// yang bisa dibaca TU: "Aktif" untuk guru aktif; untuk guru tidak aktif
// alasan yang tersimpan dengan huruf awal kapital ("Pensiun", "Pindah"),
// atau "Nonaktif" bila alasan kosong. Nilai Boolean tidak pernah ditampilkan
// mentah. values[0] = Aktif (bool), values[1] = AlasanNonaktif (string?).
public sealed class StatusAktifKeTeksConverter : IMultiValueConverter
{
    public object Convert(object[] values, Type targetType, object? parameter, CultureInfo culture)
    {
        var aktif = values.Length > 0 && values[0] is true;
        if (aktif)
        {
            return "Aktif";
        }

        var alasan = values.Length > 1 ? (values[1] as string)?.Trim() : null;
        return string.IsNullOrEmpty(alasan)
            ? "Nonaktif"
            : char.ToUpper(alasan[0], culture) + alasan[1..];
    }

    public object[] ConvertBack(object value, Type[] targetTypes, object? parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}
