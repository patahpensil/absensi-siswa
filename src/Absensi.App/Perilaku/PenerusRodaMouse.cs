using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;

namespace Absensi.App.Perilaku;

// Laporan pemilik (2026-09-24): scroll di Pengaturan "kadang berfungsi dan
// kadang tidak". Akar masalah (direproduksi di PengaturanScrollTests dengan
// roda mouse yang dikirim ke elemen hasil hit-test): DataGrid punya
// ScrollViewer internal (DG_ScrollViewer) yang SELALU menandai MouseWheel
// sebagai Handled, walau isinya tidak bisa digulir (tabel yang tumbuh
// setinggi barisnya, mis. Jadwal Harian). Selama kursor berada di atas
// tabel itu, roda mati total dan ScrollViewer halaman yang membungkusnya
// tidak pernah menerima peristiwanya — di area lain halaman menggulir
// normal, sehingga tampak "kadang".
//
// Perbaikan: class handler PreviewMouseWheel (tunneling — berjalan sebelum
// kontrol anak) untuk DataGrid, TextBox, dan ComboBox. Bila kontrol itu
// sendiri tidak bisa menggulir ke arah roda (tabel tanpa luapan, sudah di
// ujung, kolom isian satu baris, ComboBox tertutup), gulirannya
// DITERUSKAN langsung ke ScrollViewer induk terdekat yang masih bisa
// bergerak. Kontrol yang memang bisa menggulir (tabel panjang) tetap
// menggulir dirinya sendiri lebih dulu, lalu meneruskan di ujung.
public static class PenerusRodaMouse
{
    private static bool _terpasang;

    public static void Pasang()
    {
        if (_terpasang)
        {
            return;
        }

        _terpasang = true;
        var penangan = new MouseWheelEventHandler(SaatRoda);
        foreach (var tipe in new[] { typeof(DataGrid), typeof(TextBox), typeof(ComboBox) })
        {
            EventManager.RegisterClassHandler(tipe, UIElement.PreviewMouseWheelEvent, penangan);
        }
    }

    private static void SaatRoda(object sender, MouseWheelEventArgs e)
    {
        if (e.Handled || sender is not DependencyObject kontrol || !KontrolPerluMeneruskan(kontrol, e.Delta))
        {
            return;
        }

        var target = CariPenggulirInduk(kontrol, e.Delta);
        if (target is null)
        {
            return;
        }

        var baris = Math.Max(1, (int)Math.Round(Math.Abs(e.Delta) * SystemParameters.WheelScrollLines / 120.0));
        for (var i = 0; i < baris; i++)
        {
            if (e.Delta > 0)
            {
                target.LineUp();
            }
            else
            {
                target.LineDown();
            }
        }

        e.Handled = true;
    }

    private static bool KontrolPerluMeneruskan(DependencyObject kontrol, int delta) => kontrol switch
    {
        // ComboBox terbuka: roda menggulir daftar pilihannya. Tertutup: JANGAN mengubah
        // pilihan lewat roda (WPF melakukannya bila ComboBox sedang fokus) — gulirkan halaman.
        ComboBox cb => !cb.IsDropDownOpen,
        DataGrid dg => !BisaGulir(PenggulirDalam(dg), delta),
        TextBox tb => !(tb.ExtentHeight > tb.ViewportHeight + 0.5 && BisaGulirOffset(tb.VerticalOffset, tb.ExtentHeight - tb.ViewportHeight, delta)),
        _ => false,
    };

    private static ScrollViewer? PenggulirDalam(DependencyObject akar)
    {
        for (var i = 0; i < VisualTreeHelper.GetChildrenCount(akar); i++)
        {
            var anak = VisualTreeHelper.GetChild(akar, i);
            if (anak is ScrollViewer sv)
            {
                return sv;
            }

            var dalam = PenggulirDalam(anak);
            if (dalam is not null)
            {
                return dalam;
            }
        }

        return null;
    }

    private static ScrollViewer? CariPenggulirInduk(DependencyObject kontrol, int delta)
    {
        for (var induk = VisualTreeHelper.GetParent(kontrol); induk is not null; induk = VisualTreeHelper.GetParent(induk))
        {
            if (induk is ScrollViewer sv && BisaGulir(sv, delta))
            {
                return sv;
            }
        }

        return null;
    }

    private static bool BisaGulir(ScrollViewer? sv, int delta) =>
        sv is not null && BisaGulirOffset(sv.VerticalOffset, sv.ScrollableHeight, delta);

    // delta > 0 = roda ke atas (isi bergerak turun, offset berkurang).
    private static bool BisaGulirOffset(double offset, double maksimum, int delta) =>
        maksimum > 0.5 && (delta > 0 ? offset > 0.5 : offset < maksimum - 0.5);
}
