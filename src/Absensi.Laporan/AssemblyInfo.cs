using System.Runtime.CompilerServices;

// Satu-satunya alasan atribut ini ada: PenulisPdfLaporan.BagiKolomTanggalBulanan/
// KolomPerHalamanBulanan (2026-09-25, perbaikan keterbacaan laporan Bulanan)
// sengaja internal (bukan public -- murni detail implementasi tata letak,
// bukan API), tapi perlu diuji LANGSUNG sebagai fungsi murni (pembagian
// tanggal per halaman benar, tidak ada tanggal hilang/dobel) tanpa memaksa
// tes membaca ulang teks dari gambar PNG hasil render.
[assembly: InternalsVisibleTo("Absensi.Laporan.Tests")]
