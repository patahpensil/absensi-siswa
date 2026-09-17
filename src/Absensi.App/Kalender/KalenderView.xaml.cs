using System.Linq;
using System.Windows.Controls;

namespace Absensi.App.Kalender;

public partial class KalenderView : UserControl
{
    public KalenderView()
    {
        InitializeComponent();
    }

    private KalenderViewModel? ViewModel => DataContext as KalenderViewModel;

    private void KalenderKontrol_SelectedDatesChanged(object sender, SelectionChangedEventArgs e) =>
        ViewModel?.TetapkanTanggalTerpilih(KalenderKontrol.SelectedDates.ToList());

    private void KalenderKontrol_DisplayDateChanged(object sender, CalendarDateChangedEventArgs e) =>
        ViewModel?.PindahBulan(e.AddedDate ?? KalenderKontrol.DisplayDate);
}
