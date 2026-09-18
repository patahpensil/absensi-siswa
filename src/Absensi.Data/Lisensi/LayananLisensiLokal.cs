namespace Absensi.Data.Lisensi;

// Implementasi sementara ILayananLisensi selama server lisensi developer
// belum ada (lihat catatan di ILayananLisensi). Tidak memanggil jaringan
// sama sekali — hanya memvalidasi bahwa kunci diisi, lalu selalu berhasil.
// Ganti kelas ini (bukan pemanggilnya) begitu server sungguhan tersedia.
public sealed class LayananLisensiLokal : ILayananLisensi
{
    public HasilAktivasiLisensi Aktifkan(string kunciLisensi)
    {
        if (string.IsNullOrWhiteSpace(kunciLisensi))
        {
            return new HasilAktivasiLisensi(false, "Kunci lisensi wajib diisi.");
        }

        return new HasilAktivasiLisensi(true, null);
    }
}
