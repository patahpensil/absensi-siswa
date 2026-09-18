namespace Absensi.Data.Lisensi;

// PRD §13.1: "Aktivasi sekali online" — server lisensi developer belum ada
// spesifikasinya di dokumen manapun (dikonfirmasi pemilik proyek
// 2026-09-18: bangun dulu dengan implementasi lokal, ganti dengan
// pemanggilan HTTP sungguhan nanti). Antarmuka ini memisahkan wizard dari
// cara aktivasi sesungguhnya dilakukan, supaya penggantian itu tidak
// menyentuh Absensi.App.
public interface ILayananLisensi
{
    HasilAktivasiLisensi Aktifkan(string kunciLisensi);
}
