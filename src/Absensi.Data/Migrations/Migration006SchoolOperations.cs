namespace Absensi.Data.Migrations;

internal sealed class Migration006SchoolOperations : IMigration
{
    public int Number => 6;

    public string Sql => """
        CREATE TABLE homeroom_assignment (
            id INTEGER PRIMARY KEY,
            class_id INTEGER NOT NULL REFERENCES kelas(id),
            teacher_id INTEGER NOT NULL REFERENCES guru(id),
            started_at TEXT,
            ended_at TEXT
        );

        CREATE UNIQUE INDEX ix_homeroom_current
            ON homeroom_assignment(class_id) WHERE ended_at IS NULL;

        INSERT INTO homeroom_assignment (class_id, teacher_id)
        SELECT id, wali_guru_id FROM kelas WHERE wali_guru_id IS NOT NULL;
        CREATE TABLE opened_school_day (
            date TEXT PRIMARY KEY,
            academic_period_id INTEGER NOT NULL REFERENCES tahun_ajaran(id),
            closed_at TEXT
        );
        """;
}
