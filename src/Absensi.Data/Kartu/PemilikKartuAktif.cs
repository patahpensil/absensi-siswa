using Absensi.Domain.Kehadiran;

namespace Absensi.Data.Kartu;

// Hasil pencarian pemilik sebuah UID yang sedang aktif (PRD §17.9: dicek
// lintas master siswa dan guru). KelasNama hanya terisi untuk siswa —
// dipakai layar registrasi untuk pesan penolakan "kartu ini sudah dipakai
// <nama> di kelas <X>" (PRD §16.5 langkah 3.4).
public sealed record PemilikKartuAktif(JenisPemilikKartu Tipe, int Id, string Nama, string? KelasNama);
