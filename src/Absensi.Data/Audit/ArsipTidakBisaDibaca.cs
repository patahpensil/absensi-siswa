namespace Absensi.Data.Audit;

// Ditemukan lewat baris `restore_backup` di database aktif (menunjuk arsip
// yang SEHARUSNYA ada, lihat LayananAudit.AmbilArsipDirujuk) tapi berkasnya
// hilang, ATAU ditemukan lewat integrity_check gagal saat arsip dibaca.
// Ditampilkan sebagai banner eksplisit di layar Audit -- riwayat yang tidak
// bisa ditampilkan TIDAK PERNAH disembunyikan diam-diam.
public sealed record ArsipTidakBisaDibaca(string NamaBerkas, DateTime WaktuRestore, string Alasan);
