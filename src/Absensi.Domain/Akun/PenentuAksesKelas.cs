namespace Absensi.Domain.Akun;

// PRD §3/§24.3: kepala sekolah dan admin berwenang penuh;
// wali kelas hanya menyentuh kelasnya sendiri, ditentukan dari akun.guru_id
// yang cocok dengan kelas.wali_guru_id (PRD §3). Pemeriksaan ini murni,
// tidak menyentuh database — pemanggil yang menyelesaikan guru_id akun dan
// wali_guru_id kelas dari database.
public sealed class PenentuAksesKelas
{
    public bool BolehMenyentuh(PeranAkun peran, int? guruIdAkun, int waliGuruIdKelas)
    {
        if (peran is PeranAkun.KepalaSekolah or PeranAkun.Admin)
        {
            return true;
        }

        return guruIdAkun is { } guruId && guruId == waliGuruIdKelas;
    }
}
