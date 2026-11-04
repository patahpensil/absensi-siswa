using Absensi.Domain.Kehadiran;

namespace Absensi.Simulator.Core;

// Keadaan absensi satu pemilik kartu untuk hari yang disimulasikan, setara
// baris absensi_siswa/absensi_guru ditambah waktu tap terakhir dari tap_log.
public sealed class SimulatedPerson
{
    public SimulatedPerson(string cardUid, string name, JenisPemilikKartu kind, bool active = true)
    {
        CardUid = cardUid;
        Name = name;
        Kind = kind;
        Active = active;
    }

    public string CardUid { get; }

    public string Name { get; }

    public JenisPemilikKartu Kind { get; }

    public bool Active { get; set; }

    // CLAUDE.md §4.3 aturan 11: status awal setiap siswa setiap hari Alpa.
    public StatusAbsensiSiswa StudentStatus { get; set; } = StatusAbsensiSiswa.Alpa;

    public StatusAbsensiGuru TeacherStatus { get; set; } = StatusAbsensiGuru.Alpa;

    // PRD §17.5: status guru yang ditetapkan di muka (izin, dinas luar, ...)
    // membuat tap tidak mengubah apa pun.
    public StatusAbsensiGuru? TeacherPresetStatus { get; set; }

    public TimeOnly? EntryTime { get; set; }

    public TimeOnly? DepartureTime { get; set; }

    public DateTime? LastTap { get; set; }

    public string StatusText => Kind == JenisPemilikKartu.Siswa
        ? StatusLabels.Student(StudentStatus)
        : StatusLabels.Teacher(TeacherPresetStatus ?? TeacherStatus);

    internal void ResetDay()
    {
        StudentStatus = StatusAbsensiSiswa.Alpa;
        TeacherStatus = StatusAbsensiGuru.Alpa;
        EntryTime = null;
        DepartureTime = null;
        LastTap = null;
    }
}
