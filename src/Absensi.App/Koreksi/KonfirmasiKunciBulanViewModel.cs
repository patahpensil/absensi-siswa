using Absensi.App.Mvvm;

namespace Absensi.App.Koreksi;

// Dialog konfirmasi Kunci Bulan (RENCANA-TUGAS.md Tugas 11: "dialog yang
// menyebut akibatnya") — tidak ada jalur buka-kunci lewat aplikasi, jadi
// pesannya menegaskan ini tidak bisa dibatalkan.
public sealed class KonfirmasiKunciBulanViewModel : ObservableObject
{
    public KonfirmasiKunciBulanViewModel(int tahun, int bulan)
    {
        NamaBulan = new DateOnly(tahun, bulan, 1).ToString("MMMM yyyy", new System.Globalization.CultureInfo("id-ID"));
        KonfirmasiCommand = new RelayCommand(() => Dikonfirmasi?.Invoke(this, EventArgs.Empty));
    }

    public string NamaBulan { get; }

    public event EventHandler? Dikonfirmasi;

    public RelayCommand KonfirmasiCommand { get; }
}
