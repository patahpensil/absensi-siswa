using Absensi.Data.Repositori;
using Absensi.Domain.Akun;

namespace Absensi.App.Pengaturan;

// Satu baris tabel akun di layar Pengaturan → Akun (RENCANA-TUGAS.md
// Tugas 14) — bungkus tampilan atas AkunDaftar (Absensi.Data) supaya
// DataGrid punya teks peran berbahasa Indonesia siap-pakai. CanManage: the
// signed-in account may reset or deactivate this one (P-21, LayananAkun).
public sealed record AkunDaftarTampilan(AkunDaftar Akun, bool CanManage = false)
{
    public string Username => Akun.Username;

    public string TeksPeran => Akun.Peran switch
    {
        PeranAkun.KepalaSekolah => "Kepala Sekolah",
        PeranAkun.Admin => "Admin",
        PeranAkun.WaliKelas => "Wali Kelas",
        _ => Akun.Peran.ToString(),
    };

    public string GuruTerkait => Akun.NamaGuru ?? "—";

    public bool Aktif => Akun.Aktif;
}
