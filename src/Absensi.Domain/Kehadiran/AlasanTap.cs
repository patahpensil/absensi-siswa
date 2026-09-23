namespace Absensi.Domain.Kehadiran;

public enum AlasanTap
{
    TanggalLibur,
    KartuTidakTerdaftar,
    PemilikTidakAktif,
    JedaTapTerlaluCepat,
    BelumPunyaCatatanMasuk,
    LewatBatasTapMasuk,
    StatusSudahDitetapkanDiMuka,

    // Ditolak oleh PemrosesTap SEBELUM konteks dibangun untuk
    // PenentuStatusKehadiran (bukan oleh kelas itu sendiri) — jadwal_hari
    // hari ini ada dan hari_sekolah=true, tapi jam intinya kosong (bug
    // wizard yang diperbaiki bersamaan dengan alasan ini: sebelumnya jam
    // NULL diam-diam dijadikan 00:00, membuat setiap tap pertama hari itu
    // salah ditolak sebagai BelumPunyaCatatanMasuk). Lihat PemrosesTap.cs.
    JadwalBelumDiatur,
}
