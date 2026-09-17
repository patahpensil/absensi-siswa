using Absensi.Domain.Kehadiran;

namespace Absensi.Data.Repositori;

// Satu baris `kalender` — tanggal yang menyimpang dari pola mingguan
// (docs/SKEMA-DATABASE.md §4). Hari biasa tidak punya baris di sini.
public sealed record KalenderEntri(DateOnly Tanggal, JenisHariKalender Jenis, string? Keterangan);
