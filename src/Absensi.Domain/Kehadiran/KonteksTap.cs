namespace Absensi.Domain.Kehadiran;

// Masukan penuh untuk PenentuStatusKehadiran.Tentukan, sesuai daftar masukan
// di RENCANA-TUGAS.md Tugas 2: nomor kartu (diwakili lewat pencarian
// pemiliknya oleh pemanggil), waktu, jenis pemilik, keadaan absensi hari itu,
// konfigurasi jadwal hari itu, dan status kalender.
//
// Siswa dan Guru diisi oleh pemanggil hanya sesuai PemilikTerdaftar; yang
// tidak relevan dibiarkan null.
public sealed record KonteksTap(
    DateTime Waktu,
    bool ModeRegistrasiAktif,
    JenisHariKalender JenisHariIni,
    JenisPemilikKartu? PemilikTerdaftar,
    KonteksTapSiswa? Siswa = null,
    KonteksTapGuru? Guru = null);
