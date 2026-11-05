using Absensi.Data.Akun;
using Absensi.Data.Repositori;
using Absensi.Domain.Akun;
using Absensi.Domain.Kehadiran;

namespace Absensi.Web.Layanan;

// Presentation queries only. Attendance decisions remain in Absensi.Domain/Data.
public static class WorkspaceView
{
    public static bool CanManage(StatusAplikasi state) => state.Akun?.Peran is PeranAkun.Admin or PeranAkun.KepalaSekolah;

    public static bool IsClassTeacher(StatusAplikasi state) => state.Akun?.Peran == PeranAkun.WaliKelas;

    // PRD §3/§3.2: wali kelas corrects only their own class; the service checks again.
    public static bool CanCorrect(StatusAplikasi state, int classId) =>
        state.Akun is { } account && new LayananAkun().BolehMenyentuhKelas(state.Koneksi!, account, classId);

    public static IReadOnlyList<KelasDaftar> Classes(StatusAplikasi state)
    {
        var classes = new RepositoriKelas().DaftarUntukTahunAjaran(state.Koneksi!, state.TahunAjaranId ?? 0);
        return CanManage(state) ? classes : classes.Where(c => new LayananAkun().BolehMenyentuhKelas(state.Koneksi!, state.Akun!, c.Id)).ToList();
    }

    public static IReadOnlyList<SiswaDaftar> Students(StatusAplikasi state)
    {
        var students = new RepositoriSiswa().DaftarUntukTahunAjaran(state.Koneksi!, state.TahunAjaranId ?? 0);
        if (CanManage(state)) return students;
        var names = Classes(state).Select(c => c.Nama).ToHashSet();
        return students.Where(s => s.KelasNama is not null && names.Contains(s.KelasNama)).ToList();
    }

    public static IReadOnlyList<AbsensiSiswaUntukNotifikasi> Attendance(StatusAplikasi state)
    {
        var rows = new RepositoriAbsensi().DaftarSiswaUntukNotifikasi(state.Koneksi!, DateOnly.FromDateTime(state.Jam.Sekarang));
        if (CanManage(state)) return rows;
        var ids = Classes(state).Select(c => c.Id).ToHashSet();
        return rows.Where(r => ids.Contains(r.KelasId)).ToList();
    }

    public static string Tone(StatusAbsensiSiswa status) => status switch
    {
        StatusAbsensiSiswa.Hadir => "positive", StatusAbsensiSiswa.Terlambat => "warning",
        StatusAbsensiSiswa.Izin or StatusAbsensiSiswa.Sakit => "info", StatusAbsensiSiswa.Bolos => "danger", _ => "neutral",
    };
    public static string Initials(string name) => string.Concat(name.Split(' ', StringSplitOptions.RemoveEmptyEntries).Take(2).Select(p => p[0])).ToUpperInvariant();
    public static string Time(TimeOnly? time) => time?.ToString("HH:mm:ss") ?? "—";
}
