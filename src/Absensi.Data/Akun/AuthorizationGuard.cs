using Absensi.Data.Repositori;
using Absensi.Domain.Akun;

namespace Absensi.Data.Akun;

// PRD §24.3: peran diperiksa di lapisan layanan, bukan hanya
// dengan menyembunyikan tombol. PRD §3: master siswa/guru/kelas, registrasi
// dan penggantian kartu, impor, pengaturan, serta pairing termasuk
// "kewenangan penuh" (Kepala Sekolah/Admin). Wali kelas hanya mengoreksi
// status kehadiran kelasnya sendiri (PRD §3.2), diperiksa di LayananKoreksi.
// Publik agar ViewModel dapat memeriksa lebih awal sebelum efek samping di
// luar database (mis. menyalin berkas foto); layanan tetap memeriksa ulang.
public static class AuthorizationGuard
{
    public static void EnsureFullAuthority(AkunTersimpan akun, string action)
    {
        if (akun.Peran is PeranAkun.KepalaSekolah or PeranAkun.Admin)
        {
            return;
        }

        throw new InvalidOperationException($"Wali kelas tidak berwenang {action}.");
    }
}
