namespace Absensi.Data.Backup;

// PRD §18.4 langkah 3: "aplikasi menampilkan tanggal dan jumlah data di
// berkas backup sebelum tombol ditekan, sehingga admin tahu berapa hari
// data yang akan hilang" — "jumlah data" di sini berarti jumlah HARI data
// (selisih tanggal berkas ke hari ini), bukan jumlah baris tabel.
public sealed record BerkasBackup(string PathBerkas, DateTime WaktuDibuat)
{
    public string NamaBerkas => Path.GetFileName(PathBerkas);

    public int HariDataYangHilangJikaDipulihkan(DateTime sekarang) =>
        Math.Max(0, (sekarang.Date - WaktuDibuat.Date).Days);
}
