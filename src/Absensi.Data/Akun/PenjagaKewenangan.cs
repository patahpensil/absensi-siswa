using Absensi.Data.Repositori;
using Absensi.Domain.Akun;

namespace Absensi.Data.Akun;

// CLAUDE.md §4.5 aturan 19: peran diperiksa di lapisan layanan, bukan hanya
// dengan menyembunyikan tombol. PRD §3: master siswa/guru/kelas, registrasi
// dan penggantian kartu, impor, pengaturan, serta pairing termasuk
// "kewenangan penuh" (Kepala Sekolah/Admin). Wali kelas hanya mengoreksi
// status kehadiran kelasnya sendiri (PRD §3.2), diperiksa di LayananKoreksi.
// Publik agar ViewModel dapat memeriksa lebih awal sebelum efek samping di
// luar database (mis. menyalin berkas foto); layanan tetap memeriksa ulang.
public static class PenjagaKewenangan
{
    public static void PastikanKewenanganPenuh(AkunTersimpan akun, string tindakan)
    {
        if (akun.Peran is PeranAkun.KepalaSekolah or PeranAkun.Admin)
        {
            return;
        }

        throw new InvalidOperationException($"Wali kelas tidak berwenang {tindakan} (PRD §3).");
    }
}
