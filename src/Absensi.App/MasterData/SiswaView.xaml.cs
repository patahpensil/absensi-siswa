using System.Windows.Controls;

namespace Absensi.App.MasterData;

// DataContext diisi otomatis oleh DataTemplate (Resources/GayaAplikasi.xaml)
// saat dipakai sebagai konten MainWindow, bukan lewat konstruktor.
public partial class SiswaView : UserControl
{
    public SiswaView()
    {
        InitializeComponent();
    }
}
