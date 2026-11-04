namespace Absensi.Data.Migrations;

// PRD §24.2: identifier baru memakai bahasa Inggris. Migrasi 004 sempat
// merilis kolom daftar belum tap wali kelas dengan nama berbahasa Indonesia
// di build uji; migrasi 004 tidak diubah (bisa sudah diterapkan di database
// uji), jadi kolomnya diganti nama di sini. Nilai yang tersimpan tidak
// berubah.
internal sealed class Migration005RenameNotTappedListTime : IMigration
{
    public int Number => 5;

    public string Sql => """
        ALTER TABLE jadwal_hari RENAME COLUMN jam_daftar_belum_tap TO not_tapped_list_time;
        """;
}
