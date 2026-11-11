using System.Globalization;
using Absensi.Data.Laporan;
using Absensi.Data.Repositori;
using Absensi.Domain.Laporan;
using Absensi.Laporan;

namespace Absensi.Web.Layanan;

public enum ReportKind { Daily, Weekly, Monthly, PerPerson, Semester }

public sealed record ReportRequest(ReportKind Kind, JenisSubjekLaporan Subject, int? ClassId, DateOnly Date, DateOnly EndDate, int? PersonId, int? PeriodId = null);

public sealed record ReportFile(string Name, string Mime, byte[] Bytes);

// Report screen for the browser test (PRD §11, §17.8). Data comes from
// LayananLaporan (which keeps its PRD §3 role check) and files from the real
// PenulisExcelLaporan/PenulisPdfLaporan; titles and ranges follow the desktop
// LaporanViewModel so both produce the same documents.
public static class ReportView
{
    private static readonly CultureInfo Indonesian = new("id-ID");
    private static readonly LayananLaporan Reports = new();

    public static DateOnly WeekStart(DateOnly date) => date.AddDays(-(((int)date.DayOfWeek + 6) % 7));

    public static IReadOnlyList<BarisKejadian> Events(StatusAplikasi state, ReportRequest r)
    {
        var (start, end) = r.Kind == ReportKind.Weekly ? (WeekStart(r.Date), WeekStart(r.Date).AddDays(5)) : (r.Date, r.Date);
        return Reports.Kejadian(state.Koneksi!, state.Akun!, start, end, r.ClassId, r.Subject);
    }

    public static LaporanBulanan Monthly(StatusAplikasi state, ReportRequest r) =>
        Reports.Bulanan(state.Koneksi!, state.Akun!, r.PeriodId ?? state.TahunAjaranId ?? 0, r.Date.Year, r.Date.Month, r.ClassId, r.Subject);

    public static KartuOrang Person(StatusAplikasi state, ReportRequest r) =>
        WithPhoto(state, Reports.PerOrang(state.Koneksi!, state.Akun!, r.PersonId ?? 0, r.Subject, r.PeriodId ?? state.TahunAjaranId ?? 0, r.Date, r.EndDate));

    public static IReadOnlyList<KartuOrang> People(StatusAplikasi state, ReportRequest r) =>
        Reports.PerKelasAtauSemua(state.Koneksi!, state.Akun!, r.Subject, r.ClassId, r.PeriodId ?? state.TahunAjaranId ?? 0, r.Date, r.EndDate)
            .Select(k => WithPhoto(state, k)).ToList();

    public static IReadOnlyList<BarisSemester> Semester(StatusAplikasi state, ReportRequest r) =>
        Reports.Semester(state.Koneksi!, state.Akun!, r.PeriodId ?? state.TahunAjaranId ?? 0, r.ClassId, r.Subject);

    public static ReportFile Export(StatusAplikasi state, ReportRequest r, bool excel)
    {
        var header = Header(state, r);
        var excelWriter = new PenulisExcelLaporan();
        // Created only for PDF: its static constructor configures QuestPDF.
        static PenulisPdfLaporan Pdf() => new();
        var showClass = r.Subject == JenisSubjekLaporan.Siswa;
        var name = header.Judul;
        byte[] bytes;
        switch (r.Kind)
        {
            case ReportKind.Daily or ReportKind.Weekly:
                var events = Events(state, r);
                bytes = excel ? excelWriter.TulisKejadian(header, events, showClass) : Pdf().TulisKejadian(header, events, showClass);
                break;
            case ReportKind.Monthly:
                var monthly = Monthly(state, r);
                bytes = excel ? excelWriter.TulisBulanan(header, monthly, r.Subject) : Pdf().TulisBulanan(header, monthly, r.Subject);
                break;
            case ReportKind.PerPerson:
                IReadOnlyList<KartuOrang> cards = r.PersonId is null ? People(state, r) : [Person(state, r)];
                if (r.PersonId is not null) name = $"{header.Judul} - {cards[0].Nama}";
                bytes = excel ? excelWriter.TulisKartuOrang(header, cards, r.Subject) : Pdf().TulisKartuOrang(header, cards, r.Subject);
                break;
            default:
                var semester = Semester(state, r);
                bytes = excel ? excelWriter.TulisSemester(header, semester, r.Subject) : Pdf().TulisSemester(header, semester, r.Subject);
                break;
        }

        return excel
            ? new ReportFile(name + ".xlsx", "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", bytes)
            : new ReportFile(name + ".pdf", "application/pdf", bytes);
    }

    public static string Title(ReportRequest r)
    {
        var subject = r.Subject == JenisSubjekLaporan.Siswa ? "Siswa" : "Guru";
        return r.Kind switch
        {
            ReportKind.Daily => $"Laporan Harian {subject}",
            ReportKind.Weekly => $"Laporan Mingguan {subject}",
            ReportKind.Monthly => $"Laporan Bulanan {subject}",
            ReportKind.PerPerson => $"Laporan Per {subject}",
            _ => $"Laporan Semester {subject}",
        };
    }

    public static string RangeText(StatusAplikasi state, ReportRequest r) => r.Kind switch
    {
        ReportKind.Daily => r.Date.ToString("d MMMM yyyy", Indonesian),
        ReportKind.Weekly => $"{WeekStart(r.Date).ToString("d MMMM", Indonesian)} - {WeekStart(r.Date).AddDays(5).ToString("d MMMM yyyy", Indonesian)}",
        ReportKind.Monthly => r.Date.ToString("MMMM yyyy", Indonesian),
        ReportKind.PerPerson => $"{r.Date.ToString("d MMMM yyyy", Indonesian)} - {r.EndDate.ToString("d MMMM yyyy", Indonesian)}",
        _ => SemesterText(state, r),
    };

    private static string SemesterText(StatusAplikasi state, ReportRequest request)
    {
        var periodId = request.PeriodId ?? state.TahunAjaranId;
        var period = new RepositoriTahunAjaran().ListPeriods(state.Koneksi!).FirstOrDefault(item => item.Id == periodId);
        return period is null ? state.TahunAjaran :
            $"{period.Name} · {period.Semester} ({period.Start.ToString("d MMMM yyyy", Indonesian)} - {period.End.ToString("d MMMM yyyy", Indonesian)})";
    }

    private static KepalaLaporan Header(StatusAplikasi state, ReportRequest r)
    {
        var school = new RepositoriSekolah().CariSatuSatunya(state.Koneksi!);
        var printedBy = state.Akun!.GuruId is { } teacherId ? new RepositoriGuru().CariNama(state.Koneksi!, teacherId) : state.Akun.Username;
        return new KepalaLaporan(school?.Nama ?? "Nama sekolah belum diatur", school?.Npsn, state.PhotoFile(school?.LogoPath),
            Title(r), RangeText(state, r), printedBy, state.Jam.Sekarang);
    }

    private static KartuOrang WithPhoto(StatusAplikasi state, KartuOrang card) => card with { FotoPath = state.PhotoFile(card.FotoPath) };
}
