using System.Media;

namespace Absensi.App.Gerbang;

// Memakai bunyi bawaan Windows — tidak ada berkas audio khusus yang
// dibundel proyek ini. Ganti dengan berkas audio khusus sekolah bila
// pemilik proyek menginginkannya.
public sealed class PemutarBunyiSistem : IPemutarBunyi
{
    public void Berhasil() => SystemSounds.Asterisk.Play();

    public void Gagal() => SystemSounds.Hand.Play();
}
