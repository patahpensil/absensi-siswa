using Absensi.App.Gerbang;

namespace Absensi.App.Tests;

// Tidak benar-benar berbunyi saat pengujian — hanya menghitung panggilan.
internal sealed class PemutarBunyiDiam : IPemutarBunyi
{
    public int JumlahBerhasil { get; private set; }

    public int JumlahGagal { get; private set; }

    public void Berhasil() => JumlahBerhasil++;

    public void Gagal() => JumlahGagal++;
}
