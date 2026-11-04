namespace Absensi.Data.Migrations;

// PRD §7 keputusan P-06: daftar belum tap untuk wali kelas (08.00) dan rekap
// notifikasi orang tua (08.10) adalah dua jadwal berbeda. Sebelumnya keduanya
// memakai satu kolom jam_rekap. Kolom jam_rekap tetap berarti rekap orang tua
// (juga batas mulai notifikasi Terlambat real-time, PRD §9.2), dan kolom baru
// jam_daftar_belum_tap menjadi jadwal daftar belum tap wali kelas.
//
// Baris lama diisi dengan nilai jam_rekap yang sudah tersimpan, sehingga
// perilaku instalasi yang ada tidak berubah diam-diam (P-15: perubahan jadwal
// tidak menulis ulang riwayat). Admin menyesuaikan jam rekap orang tua sendiri
// lewat Pengaturan bila ingin mengikuti 08.10.
internal sealed class Migration004TambahJamDaftarBelumTap : IMigration
{
    public int Number => 4;

    public string Sql => """
        ALTER TABLE jadwal_hari ADD COLUMN jam_daftar_belum_tap TEXT;

        UPDATE jadwal_hari SET jam_daftar_belum_tap = jam_rekap;
        """;
}
