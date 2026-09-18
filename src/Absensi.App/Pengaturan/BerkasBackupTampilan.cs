using Absensi.Data.Backup;

namespace Absensi.App.Pengaturan;

// Satu baris tabel backup di layar Pengaturan — bungkus tampilan atas
// BerkasBackup (Absensi.Data) supaya DataGrid punya kolom siap-pakai tanpa
// menghitung ulang "hari data hilang" tiap kali dirender.
public sealed record BerkasBackupTampilan(BerkasBackup Berkas, int HariDataHilang)
{
    public string NamaBerkas => Berkas.NamaBerkas;

    public DateTime WaktuDibuat => Berkas.WaktuDibuat;
}
