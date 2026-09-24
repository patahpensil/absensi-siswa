namespace Absensi.App.Laporan;

// Fitur Cetak (terpisah dari Ekspor PDF, instruksi eksplisit pemilik
// proyek 2026-09-25) — interface tersendiri supaya LaporanViewModel bisa
// diuji (jenis laporan mana yang menghasilkan halaman apa, pesan galat
// untuk laporan kosong) tanpa PrintDialog/jendela pratinjau WPF
// sungguhan, pola sama seperti layanan lain yang disuntik lewat
// konstruktor (mis. LayananBackup di MainWindowViewModel).
public interface ILayananCetakLaporan
{
    // halamanPng: satu PNG utuh per halaman (lihat PenulisPdfLaporan.Gambar*)
    // — sudah memuat kepala dan kepala tabel berulang di tiap halaman,
    // persis seperti Ekspor PDF, karena dibangun dari dokumen QuestPDF
    // yang sama. judulJob dipakai sebagai judul pekerjaan cetak (muncul di
    // antrean cetak Windows) dan judul jendela pratinjau.
    void Cetak(IReadOnlyList<byte[]> halamanPng, string judulJob, Action<string> pesanGalat);
}
