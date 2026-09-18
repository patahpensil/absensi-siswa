using Absensi.Domain.Kehadiran;

namespace Absensi.Data.Repositori;

// Satu baris layar Guru→Kehadiran (PRD §16.17) — satu tanggal, seluruh
// guru aktif. Status null berarti belum ada baris absensi_guru sama sekali
// untuk tanggal ini (tanggal mendatang, sebelum pembukaan hari berjalan —
// wajar untuk penetapan "DI MUKA", bukan berarti data hilang).
public sealed record GuruUntukKehadiran(int GuruId, string Nip, string Nama, StatusAbsensiGuru? Status, string? Keterangan);
