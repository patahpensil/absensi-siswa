namespace Absensi.Domain.Laporan;

// PRD §11.3/§17.8: laporan siswa dan laporan guru berbagi bentuk yang
// sama (§17.8 "mengikuti format laporan siswa"), dibedakan lewat subjek
// ini alih-alih menduplikasi setiap jenis laporan. Mirip
// Absensi.Domain.Notifikasi.JenisSubjekOutbox, tapi tanpa nilai 'Kelas' —
// laporan tidak pernah ditujukan ke satu kelas sebagai subjek tunggal.
public enum JenisSubjekLaporan
{
    Siswa,
    Guru,
}
