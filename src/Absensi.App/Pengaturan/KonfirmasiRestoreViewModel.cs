using System.Globalization;
using Absensi.App.Mvvm;
using Absensi.Data.Backup;

namespace Absensi.App.Pengaturan;

// Dialog konfirmasi Restore (PRD §18.4 langkah 3: "menampilkan tanggal dan
// jumlah data di berkas backup sebelum tombol ditekan, sehingga admin tahu
// berapa hari data yang akan hilang") — dan menyebut EKSPLISIT bahwa
// aplikasi akan tertutup setelah restore selesai (dikonfirmasi pemilik
// proyek: harus baris tersendiri, bukan disisipkan di akhir kalimat lain).
public sealed class KonfirmasiRestoreViewModel : ObservableObject
{
    public KonfirmasiRestoreViewModel(BerkasBackup berkas, int hariDataHilang)
    {
        WaktuBerkas = berkas.WaktuDibuat;
        HariDataHilang = hariDataHilang;
        TeksJudul = $"Pulihkan dari backup {WaktuBerkas.ToString("d MMMM yyyy", new CultureInfo("id-ID"))}?";
        KonfirmasiCommand = new RelayCommand(() => Dikonfirmasi?.Invoke(this, EventArgs.Empty));
    }

    public DateTime WaktuBerkas { get; }

    public int HariDataHilang { get; }

    public string TeksJudul { get; }

    public event EventHandler? Dikonfirmasi;

    public RelayCommand KonfirmasiCommand { get; }
}
