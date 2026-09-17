namespace Absensi.App;

// PRD §16.5: selama Mode Registrasi aktif, seluruh tap berhenti dicatat
// sebagai absensi (ditegakkan di Absensi.Domain.Kehadiran.PenentuStatusKehadiran
// lewat KonteksTap.ModeRegistrasiAktif). Sengaja hanya status di memori,
// bukan baris database — kalau aplikasi tertutup paksa di tengah sesi,
// buka ulang membuatnya otomatis "mati" lagi, sehingga pencatatan absensi
// normal berjalan kembali (gagal-aman), bukan tersangkut dalam kondisi
// absensi mati (dikonfirmasi pemilik proyek 2026-09-18).
public sealed class StatusModeRegistrasi
{
    public bool Aktif { get; private set; }

    public void Aktifkan() => Aktif = true;

    public void Matikan() => Aktif = false;
}
