namespace Absensi.Data.Migrations;

// PRD §7 P-15: a schedule change for the future has an effective date and
// earlier versions are kept. jadwal_hari stays the version that applies from
// the start of the academic period; each row here overrides one weekday from
// its effective date onwards. Attendance therefore always uses the schedule
// that applied on the attendance date, including late-synced taps.
internal sealed class Migration008ScheduleChange : IMigration
{
    public int Number => 8;

    public string Sql => """
        CREATE TABLE schedule_change (
            id                   INTEGER PRIMARY KEY,
            academic_period_id   INTEGER NOT NULL REFERENCES tahun_ajaran(id),
            effective_from       TEXT NOT NULL,
            day                  INTEGER NOT NULL CHECK (day BETWEEN 1 AND 7),
            school_day           INTEGER NOT NULL CHECK (school_day IN (0, 1)),
            arrival_limit        TEXT,
            parent_recap         TEXT,
            entry_deadline       TEXT,
            departure_start      TEXT,
            not_tapped_list_time TEXT,
            UNIQUE (academic_period_id, effective_from, day)
        );
        """;
}
