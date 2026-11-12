namespace Absensi.Data.Migrations;

// PRD §5.3/§11 P-18: siswa pindah masuk atau keluar di tengah periode punya
// tanggal mulai bersekolah dan/atau hari terakhir bersekolah pada
// keanggotaan kelasnya. NULL berarti mengikuti kalender periode, jadi siswa
// yang ikut sejak awal tidak perlu tanggal khusus dan data lama tidak berubah.
internal sealed class Migration009StudentEnrollmentDates : IMigration
{
    public int Number => 9;

    public string Sql => """
        ALTER TABLE siswa_kelas ADD COLUMN start_date TEXT;
        ALTER TABLE siswa_kelas ADD COLUMN end_date TEXT;
        """;
}
