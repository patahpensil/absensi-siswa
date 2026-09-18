using Absensi.Domain.Kehadiran;

namespace Absensi.Data.Repositori;

// Hasil RepositoriKalender.DaftarJenisHariUntukRentang: jenis hari (dari
// kalender bila menyimpang, kalau tidak diturunkan dari jadwal_hari) plus
// alasan bila jenisnya Libur dan tersimpan sebagai override di kalender
// (dipakai catatan kaki "alasan libur mendadak" laporan bulanan, PRD
// §11.2). Null Alasan berarti libur mingguan biasa (mis. Minggu), bukan
// override kalender.
public sealed record JenisHariInfo(JenisHariKalender Jenis, string? Alasan);
