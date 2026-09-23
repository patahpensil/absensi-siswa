using System.Windows;

namespace Absensi.App.Perilaku;

// Teks petunjuk abu-abu pada ComboBox yang belum punya pilihan (mis. "Pilih
// kelas"). Audit UI 2026-09-24: ComboBox kosong tampil sebagai kotak
// abu-abu tanpa keterangan apa pun (Koreksi, Kelas, Notifikasi, Registrasi
// Kartu) sehingga pengguna tidak tahu apa yang harus dipilih. Dipakai oleh
// template ComboBox di GayaAplikasi.xaml.
public static class Petunjuk
{
    public static readonly DependencyProperty TeksProperty = DependencyProperty.RegisterAttached(
        "Teks", typeof(string), typeof(Petunjuk), new PropertyMetadata(string.Empty));

    public static string GetTeks(DependencyObject obj) => (string)obj.GetValue(TeksProperty);

    public static void SetTeks(DependencyObject obj, string value) => obj.SetValue(TeksProperty, value);
}
