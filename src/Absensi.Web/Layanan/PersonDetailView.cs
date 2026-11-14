using Absensi.Data.Laporan;
using Absensi.Data.Notifikasi;
using Absensi.Data.Repositori;
using Absensi.Domain.Kehadiran;
using Absensi.Domain.Laporan;

namespace Absensi.Web.Layanan;

public enum PersonKind { Student, Teacher }

public sealed record PersonHistoryRow(
    DateOnly Date, string Status, string Tone, TimeOnly? TimeIn, TimeOnly? TimeOut, string? Note, bool Corrected, bool MarkedHoliday,
    // Student rows only: what the correction form needs (PRD §16.10).
    int? ClassId = null, StatusAbsensiSiswa? StudentStatus = null, string? StoredNote = null, bool CanCorrect = false);

public sealed record PersonDetail(
    PersonKind Kind,
    int Id,
    string Name,
    string Identity,
    string? ClassName,
    string? Gender,
    bool Active,
    string TodayStatus,
    string TodayTone,
    string? CardUid,
    string? ParentName,
    bool TelegramLinked,
    RekapKehadiran? Recap,
    string? RecapNote,
    IReadOnlyList<PersonHistoryRow> History,
    string? PhotoName = null,
    // Students only: parent notification channel (PRD P-20).
    ParentChannelSettings? ParentChannel = null);

// Read-only panel data for PRD §14.6 (student detail). Visibility follows the
// same scoping as the lists; the monthly recap comes from LayananLaporan so
// holiday/effective-day rules are not duplicated here, and that service keeps
// its own role check (PRD §3: reports for Kepala Sekolah/Admin only).
public static class PersonDetailView
{
    public static PersonDetail? Student(StatusAplikasi state, int studentId, DateOnly month)
    {
        var student = WorkspaceView.Students(state).FirstOrDefault(s => s.Id == studentId);
        if (student is null) return null;

        var connection = state.Koneksi!;
        var today = DateOnly.FromDateTime(state.Jam.Sekarang);
        var todayRow = new RepositoriAbsensi().CariHariIniSiswa(connection, today, studentId);
        var (start, end) = Range(month, today);
        IReadOnlySet<DateOnly> holidays = end < start ? new HashSet<DateOnly>() : new RepositoriKalender().ListMarkedHolidayDates(connection, start, end);
        IReadOnlyList<PersonHistoryRow> history = end < start ? Array.Empty<PersonHistoryRow>() : new RepositoriAbsensi()
            .DaftarSiswaUntukRentang(connection, start, end, kelasId: null)
            .Where(r => r.SiswaId == studentId)
            .OrderByDescending(r => r.Tanggal)
            .Select(r => new PersonHistoryRow(r.Tanggal, r.Status.ToString(), WorkspaceView.Tone(r.Status), r.JamMasuk, r.JamPulang,
                KodeStatus.Keterangan(r.Status, r.JamMasuk, r.Keterangan), r.Sumber == "koreksi", holidays.Contains(r.Tanggal),
                r.KelasId, r.Status, r.Keterangan, WorkspaceView.CanCorrect(state, r.KelasId)))
            .ToList();
        var (recap, note) = Recap(state, studentId, JenisSubjekLaporan.Siswa, start, end);

        return new PersonDetail(PersonKind.Student, student.Id, student.Nama, student.Nisn, student.KelasNama, student.JenisKelamin, student.Aktif,
            todayRow?.Status.ToString() ?? "Belum ada catatan", todayRow is null ? "neutral" : WorkspaceView.Tone(todayRow.Status),
            student.KartuUid, new RepositoriSiswa().CariUntukAudit(connection, null, studentId)?.Data.NamaOrtu, student.TertautTelegram,
            recap, note, history, new RepositoriSiswa().CariFotoPath(connection, studentId),
            new ParentRecipientResolver().Read(connection, studentId));
    }

    public static PersonDetail? Teacher(StatusAplikasi state, int teacherId, DateOnly month)
    {
        if (!WorkspaceView.CanManage(state)) return null;
        var connection = state.Koneksi!;
        var teacher = new RepositoriGuru().DaftarLengkap(connection).FirstOrDefault(g => g.Id == teacherId);
        if (teacher is null) return null;

        var today = DateOnly.FromDateTime(state.Jam.Sekarang);
        var todayRow = new RepositoriAbsensi().CariHariIniGuru(connection, today, teacherId);
        var (start, end) = Range(month, today);
        IReadOnlySet<DateOnly> holidays = end < start ? new HashSet<DateOnly>() : new RepositoriKalender().ListMarkedHolidayDates(connection, start, end);
        IReadOnlyList<PersonHistoryRow> history = end < start ? Array.Empty<PersonHistoryRow>() : new RepositoriAbsensi()
            .DaftarGuruUntukRentang(connection, start, end)
            .Where(r => r.GuruId == teacherId)
            .OrderByDescending(r => r.Tanggal)
            .Select(r => new PersonHistoryRow(r.Tanggal, TeacherStatus(r.Status), TeacherTone(r.Status), r.JamMasuk, r.JamPulang,
                KodeStatus.Keterangan(r.Status, r.JamMasuk, r.Keterangan), r.Sumber == "koreksi", holidays.Contains(r.Tanggal)))
            .ToList();
        var (recap, note) = Recap(state, teacherId, JenisSubjekLaporan.Guru, start, end);

        return new PersonDetail(PersonKind.Teacher, teacher.Id, teacher.Nama, teacher.Nip, teacher.Jabatan, null, teacher.Aktif,
            todayRow is null ? "Belum ada catatan" : TeacherStatus(todayRow.Status), todayRow is null ? "neutral" : TeacherTone(todayRow.Status),
            teacher.KartuUid, null, teacher.TertautTelegram, recap, note, history, new RepositoriGuru().CariFotoPath(connection, teacherId));
    }

    // History and recap cover the selected month up to today; future days have no rows yet.
    private static (DateOnly Start, DateOnly End) Range(DateOnly month, DateOnly today)
    {
        var start = new DateOnly(month.Year, month.Month, 1);
        var monthEnd = start.AddMonths(1).AddDays(-1);
        return (start, monthEnd < today ? monthEnd : today);
    }

    private static (RekapKehadiran? Recap, string? Note) Recap(StatusAplikasi state, int id, JenisSubjekLaporan subject, DateOnly start, DateOnly end)
    {
        // P-22: LayananLaporan decides; homeroom teachers get the recap for their own students.
        if (end < start) return (null, "Bulan ini belum berjalan.");
        try
        {
            return (new LayananLaporan().PerOrang(state.Koneksi!, state.Akun!, id, subject, state.TahunAjaranId ?? 0, start, end).Rekap, null);
        }
        catch (InvalidOperationException ex) { return (null, ex.Message); }
    }

    public static string TeacherStatus(StatusAbsensiGuru status) => status switch
    {
        StatusAbsensiGuru.TidakLengkap => "Tidak lengkap",
        StatusAbsensiGuru.DinasLuar => "Dinas luar",
        _ => status.ToString(),
    };

    public static string TeacherTone(StatusAbsensiGuru status) => status switch
    {
        StatusAbsensiGuru.Hadir or StatusAbsensiGuru.Masuk => "positive",
        StatusAbsensiGuru.TidakLengkap => "warning",
        StatusAbsensiGuru.Izin or StatusAbsensiGuru.Sakit or StatusAbsensiGuru.DinasLuar or StatusAbsensiGuru.Cuti => "info",
        StatusAbsensiGuru.Alpa => "danger",
        _ => "neutral",
    };
}
