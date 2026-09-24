namespace Absensi.Data.Audit;

// Satu baris riwayat siap tampil -- bisa berasal dari database aktif atau
// salah satu arsip pra-restore (A-39 Tahap 1), sudah di-dedup berdasarkan
// AuditUid oleh LayananAudit. Asal = "Aktif" atau "Arsip <nama berkas>".
public sealed record BarisAuditGabungan(
    string AuditUid,
    DateTime Waktu,
    string Aksi,
    string Entitas,
    int? EntitasId,
    string? NilaiLama,
    string? NilaiBaru,
    string? AtasPermintaan,
    string NamaPelaku,
    string Asal);
