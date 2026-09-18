namespace Absensi.Domain.Laporan;

// PANDUAN-UI-UX.md §11: "Kepala halaman: logo, nama sekolah, NPSN, judul
// laporan, rentang tanggal" + "Kaki halaman: nomor halaman, tanggal
// cetak, nama pencetak" — satu record dipakai penulis Excel dan PDF
// supaya kedua keluaran menyebut hal yang sama.
public sealed record KepalaLaporan(
    string NamaSekolah,
    string? Npsn,
    string? LogoPath,
    string Judul,
    string RentangTeks,
    string DicetakOleh,
    DateTime DicetakPada);
