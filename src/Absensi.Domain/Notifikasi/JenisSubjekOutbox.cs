namespace Absensi.Domain.Notifikasi;

// docs/SKEMA-DATABASE.md §8 (outbox.subjek_tipe) — beda dari
// Absensi.Domain.Kehadiran.JenisPemilikKartu karena outbox juga mengenal
// 'kelas' (pesan wali kelas ditujukan ke satu kelas, dikirim ke chat_id
// wali guru kelas itu).
public enum JenisSubjekOutbox
{
    Siswa,
    Guru,
    Kelas,
}
