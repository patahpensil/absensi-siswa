using Microsoft.Data.Sqlite;

namespace Absensi.Data.Repositori;

public sealed class RepositoriJadwalHari
{
    // hari: 1=Senin .. 7=Minggu (docs/SKEMA-DATABASE.md §4).
    // tanggal (PRD §7 P-15): when given, the latest schedule_change for this
    // weekday effective on or before that date wins over jadwal_hari.
    public JadwalHariKonfigurasi? CariUntukHari(SqliteConnection connection, int tahunAjaranId, int hari, SqliteTransaction? transaction = null, DateOnly? tanggal = null)
    {
        if (tanggal is { } date && FindChange(connection, transaction, tahunAjaranId, hari, date) is { } change)
        {
            return new JadwalHariKonfigurasi(change.HariSekolah, change.JamMasuk, change.JamRekap, change.BatasTapMasuk, change.MulaiPulang, change.NotTappedListTime);
        }

        using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = """
            SELECT hari_sekolah, jam_masuk, jam_rekap, batas_tap_masuk, mulai_pulang, not_tapped_list_time
            FROM jadwal_hari
            WHERE tahun_ajaran_id = $tahunAjaranId AND hari = $hari;
            """;
        command.Parameters.AddWithValue("$tahunAjaranId", tahunAjaranId);
        command.Parameters.AddWithValue("$hari", hari);

        using var reader = command.ExecuteReader();
        if (!reader.Read())
        {
            return null;
        }

        return new JadwalHariKonfigurasi(
            HariSekolah: reader.GetInt32(0) == 1,
            JamMasuk: BacaJam(reader, 1),
            JamRekap: BacaJam(reader, 2),
            BatasTapMasuk: BacaJam(reader, 3),
            MulaiPulang: BacaJam(reader, 4),
            NotTappedListTime: BacaJam(reader, 5));
    }

    // PRD §7 / layar Pengaturan: ketujuh hari sekaligus, termasuk yang
    // belum pernah disimpan (tahun ajaran baru) — diisi default supaya
    // layar selalu menampilkan 7 baris utuh, bukan sebagian. Default
    // mengikuti PRD §7 ("Minggu libur", sisanya hari sekolah) —
    // sekadar isian awal formulir, admin tetap bebas mengubahnya sebelum
    // Simpan; bukan nilai yang ditegakkan di kode (aturan 2).
    public IReadOnlyList<JadwalHariBaris> DaftarUntukTahunAjaran(SqliteConnection connection, int tahunAjaranId, DateOnly? tanggal = null)
    {
        if (tanggal is { } date)
        {
            var timeline = LoadTimeline(connection, tahunAjaranId);
            return Enumerable.Range(1, 7)
                .Select(day => timeline.For(date, day) ?? new JadwalHariBaris(day, HariSekolah: day != 7, null, null, null, null))
                .ToList();
        }

        var tersimpan = ReadStoredBase(connection, tahunAjaranId, null).ToDictionary(row => row.Hari);

        var daftar = new List<JadwalHariBaris>();
        for (var hari = 1; hari <= 7; hari++)
        {
            daftar.Add(tersimpan.TryGetValue(hari, out var baris)
                ? baris
                : new JadwalHariBaris(hari, HariSekolah: hari != 7, null, null, null, null));
        }

        return daftar;
    }

    // Only the rows actually stored in jadwal_hari (no form defaults).
    private static List<JadwalHariBaris> ReadStoredBase(SqliteConnection connection, int tahunAjaranId, SqliteTransaction? transaction)
    {
        var tersimpan = new List<JadwalHariBaris>();
        using var command = connection.CreateCommand();
        if (transaction is not null) command.Transaction = transaction;
        command.CommandText = """
            SELECT hari, hari_sekolah, jam_masuk, jam_rekap, batas_tap_masuk, mulai_pulang, not_tapped_list_time
            FROM jadwal_hari WHERE tahun_ajaran_id = $tahunAjaranId;
            """;
        command.Parameters.AddWithValue("$tahunAjaranId", tahunAjaranId);

        using (var reader = command.ExecuteReader())
        {
            while (reader.Read())
            {
                var hari = reader.GetInt32(0);
                tersimpan.Add(new JadwalHariBaris(
                    hari,
                    reader.GetInt32(1) == 1,
                    BacaJam(reader, 2), BacaJam(reader, 3), BacaJam(reader, 4), BacaJam(reader, 5),
                    NotTappedListTime: BacaJam(reader, 6)));
            }
        }

        return tersimpan;
    }

    // Hanya dipanggil lewat Absensi.Data.Waktu.LayananJadwalHari, yang
    // membungkusnya dengan JalurPerubahanData (PRD §12).
    // Upsert per hari — ketujuh baris ditulis dalam satu transaksi oleh
    // pemanggil (PRD §7: "seluruh nilai dapat diubah per hari").
    public void Perbarui(SqliteConnection connection, SqliteTransaction transaction, int tahunAjaranId, JadwalHariBaris data)
    {
        using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = """
            INSERT INTO jadwal_hari (tahun_ajaran_id, hari, hari_sekolah, jam_masuk, jam_rekap, batas_tap_masuk, mulai_pulang, not_tapped_list_time)
            VALUES ($tahunAjaranId, $hari, $hariSekolah, $jamMasuk, $jamRekap, $batasTapMasuk, $mulaiPulang, $notTappedListTime)
            ON CONFLICT(tahun_ajaran_id, hari) DO UPDATE SET
                hari_sekolah = excluded.hari_sekolah,
                jam_masuk = excluded.jam_masuk,
                jam_rekap = excluded.jam_rekap,
                batas_tap_masuk = excluded.batas_tap_masuk,
                mulai_pulang = excluded.mulai_pulang,
                not_tapped_list_time = excluded.not_tapped_list_time;
            """;
        command.Parameters.AddWithValue("$tahunAjaranId", tahunAjaranId);
        command.Parameters.AddWithValue("$hari", data.Hari);
        command.Parameters.AddWithValue("$hariSekolah", data.HariSekolah ? 1 : 0);
        command.Parameters.AddWithValue("$jamMasuk", TimeValue(data.JamMasuk));
        command.Parameters.AddWithValue("$jamRekap", TimeValue(data.JamRekap));
        command.Parameters.AddWithValue("$batasTapMasuk", TimeValue(data.BatasTapMasuk));
        command.Parameters.AddWithValue("$mulaiPulang", TimeValue(data.MulaiPulang));
        command.Parameters.AddWithValue("$notTappedListTime", TimeValue(data.NotTappedListTime));
        command.ExecuteNonQuery();
    }

    // All versions of one period loaded once, for code that walks many dates
    // (calendar/report ranges) and must not query per date.
    public ScheduleTimeline LoadTimeline(SqliteConnection connection, int tahunAjaranId, SqliteTransaction? transaction = null)
    {
        return new ScheduleTimeline(ReadStoredBase(connection, tahunAjaranId, transaction), ListChanges(connection, tahunAjaranId, transaction));
    }

    public IReadOnlyList<ScheduleChange> ListChanges(SqliteConnection connection, int tahunAjaranId, SqliteTransaction? transaction = null)
    {
        using var command = connection.CreateCommand();
        if (transaction is not null) command.Transaction = transaction;
        command.CommandText = """
            SELECT effective_from, day, school_day, arrival_limit, parent_recap, entry_deadline, departure_start, not_tapped_list_time
            FROM schedule_change WHERE academic_period_id = $period
            ORDER BY effective_from, day;
            """;
        command.Parameters.AddWithValue("$period", tahunAjaranId);
        using var reader = command.ExecuteReader();
        var changes = new List<ScheduleChange>();
        while (reader.Read())
        {
            changes.Add(new ScheduleChange(DateOnly.Parse(reader.GetString(0)), new JadwalHariBaris(
                reader.GetInt32(1), reader.GetInt32(2) == 1,
                BacaJam(reader, 3), BacaJam(reader, 4), BacaJam(reader, 5), BacaJam(reader, 6), BacaJam(reader, 7))));
        }

        return changes;
    }

    // Only called through Absensi.Data.Waktu.LayananJadwalHari (audited).
    public void SaveChange(SqliteConnection connection, SqliteTransaction transaction, int tahunAjaranId, DateOnly effectiveFrom, JadwalHariBaris data)
    {
        using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = """
            INSERT INTO schedule_change (academic_period_id, effective_from, day, school_day, arrival_limit, parent_recap, entry_deadline, departure_start, not_tapped_list_time)
            VALUES ($period, $from, $day, $school, $arrival, $recap, $deadline, $departure, $notTapped)
            ON CONFLICT(academic_period_id, effective_from, day) DO UPDATE SET
                school_day = excluded.school_day,
                arrival_limit = excluded.arrival_limit,
                parent_recap = excluded.parent_recap,
                entry_deadline = excluded.entry_deadline,
                departure_start = excluded.departure_start,
                not_tapped_list_time = excluded.not_tapped_list_time;
            """;
        command.Parameters.AddWithValue("$period", tahunAjaranId);
        command.Parameters.AddWithValue("$from", effectiveFrom.ToString("yyyy-MM-dd"));
        command.Parameters.AddWithValue("$day", data.Hari);
        command.Parameters.AddWithValue("$school", data.HariSekolah ? 1 : 0);
        command.Parameters.AddWithValue("$arrival", TimeValue(data.JamMasuk));
        command.Parameters.AddWithValue("$recap", TimeValue(data.JamRekap));
        command.Parameters.AddWithValue("$deadline", TimeValue(data.BatasTapMasuk));
        command.Parameters.AddWithValue("$departure", TimeValue(data.MulaiPulang));
        command.Parameters.AddWithValue("$notTapped", TimeValue(data.NotTappedListTime));
        command.ExecuteNonQuery();
    }

    private JadwalHariBaris? FindChange(SqliteConnection connection, SqliteTransaction? transaction, int tahunAjaranId, int day, DateOnly date)
    {
        using var command = connection.CreateCommand();
        if (transaction is not null) command.Transaction = transaction;
        command.CommandText = """
            SELECT school_day, arrival_limit, parent_recap, entry_deadline, departure_start, not_tapped_list_time
            FROM schedule_change
            WHERE academic_period_id = $period AND day = $day AND effective_from <= $date
            ORDER BY effective_from DESC LIMIT 1;
            """;
        command.Parameters.AddWithValue("$period", tahunAjaranId);
        command.Parameters.AddWithValue("$day", day);
        command.Parameters.AddWithValue("$date", date.ToString("yyyy-MM-dd"));
        using var reader = command.ExecuteReader();
        return reader.Read()
            ? new JadwalHariBaris(day, reader.GetInt32(0) == 1, BacaJam(reader, 1), BacaJam(reader, 2), BacaJam(reader, 3), BacaJam(reader, 4), BacaJam(reader, 5))
            : null;
    }

    private static object TimeValue(TimeOnly? time) => time is { } value ? StoredTime.Minutes(value) : DBNull.Value;

    private static TimeOnly? BacaJam(SqliteDataReader reader, int kolom) =>
        reader.IsDBNull(kolom) ? null : StoredTime.Parse(reader.GetString(kolom));
}
