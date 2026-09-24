namespace Absensi.App.Laporan;

// Implementasi WPF sungguhan dari ILayananCetakLaporan (lihat interface
// itu untuk alasan pemisahannya). Galat printer/PrintDialog ditangani di
// dalam JendelaPratinjauCetak sendiri (lihat komentar di sana); pesanGalat
// di sini praktis tidak pernah terpanggil untuk galat cetak itu sendiri,
// hanya bagian dari kontrak interface supaya konsisten dengan pola
// layanan lain di ViewModel ini.
public sealed class LayananCetakLaporan : ILayananCetakLaporan
{
    public void Cetak(IReadOnlyList<byte[]> halamanPng, string judulJob, Action<string> pesanGalat) =>
        new JendelaPratinjauCetak(halamanPng, judulJob).ShowDialog();
}
