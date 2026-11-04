using Absensi.Domain.Kehadiran;

namespace Absensi.Simulator.Core;

// Satu hari sekolah di memori. Setiap tap dibangun menjadi KonteksTap dan
// diputuskan oleh PenentuStatusKehadiran yang sama dengan aplikasi desktop
// (CLAUDE.md §4.1 aturan 1); kelas ini hanya menerapkan keputusan ke keadaan
// hari itu, setara bagian penyimpanan di Absensi.Data.PemrosesTap.
public sealed class SimulationSession
{
    private readonly PenentuStatusKehadiran _engine = new();
    private readonly List<SimulatedPerson> _people;
    private readonly List<TapLogEntry> _log = new();

    public SimulationSession(DaySettings settings, IEnumerable<SimulatedPerson> people)
    {
        Settings = settings;
        _people = people.ToList();
    }

    public DaySettings Settings { get; set; }

    public IReadOnlyList<SimulatedPerson> People => _people;

    public IReadOnlyList<TapLogEntry> Log => _log;

    // Data contoh, bukan data sungguhan (CLAUDE.md §6). Nama sengaja diberi
    // penanda "(contoh)".
    public static SimulationSession CreateWithSampleData(DateOnly date) => new(
        DaySettings.DefaultFor(date),
        new[]
        {
            new SimulatedPerson("SISWA-001", "Ani (contoh)", JenisPemilikKartu.Siswa),
            new SimulatedPerson("SISWA-002", "Budi (contoh)", JenisPemilikKartu.Siswa),
            new SimulatedPerson("SISWA-003", "Citra (contoh)", JenisPemilikKartu.Siswa),
            new SimulatedPerson("SISWA-004", "Dodi (contoh, sudah keluar)", JenisPemilikKartu.Siswa, active: false),
            new SimulatedPerson("GURU-001", "Bu Eka (contoh)", JenisPemilikKartu.Guru),
            new SimulatedPerson("GURU-002", "Pak Fajar (contoh)", JenisPemilikKartu.Guru),
        });

    public SimulatedPerson? FindPerson(string cardUid) =>
        _people.FirstOrDefault(p => string.Equals(p.CardUid, cardUid.Trim(), StringComparison.OrdinalIgnoreCase));

    public TapLogEntry Tap(string cardUid, TimeOnly time)
    {
        var moment = Settings.Date.ToDateTime(time);
        var person = FindPerson(cardUid);
        var decision = _engine.Tentukan(BuildContext(person, moment));

        if (person is not null && decision.Tindakan != JenisTindakanTap.Registrasi)
        {
            Apply(person, decision, time);
            // Setara tap_log: setiap tap yang diproses tercatat dan menjadi
            // acuan jeda tap berikutnya, termasuk tap yang ditolak.
            person.LastTap = moment;
        }

        var entry = new TapLogEntry(moment, cardUid.Trim(), person?.Name, decision);
        _log.Insert(0, entry);
        return entry;
    }

    // PRD §3.2/§10: koreksi Izin/Sakit oleh pihak berwenang; status ini
    // kemudian didahulukan atas tap (PRD §8.3).
    public void CorrectStudentStatus(string cardUid, StatusAbsensiSiswa status)
    {
        var person = RequirePerson(cardUid, JenisPemilikKartu.Siswa);
        person.StudentStatus = status;
    }

    // PRD §16.17: status guru ditetapkan di muka oleh Kepala Sekolah.
    public void SetTeacherPresetStatus(string cardUid, StatusAbsensiGuru? status)
    {
        var person = RequirePerson(cardUid, JenisPemilikKartu.Guru);
        person.TeacherPresetStatus = status;
    }

    // PRD §16.7/§16.16: finalisasi akhir hari. Siswa yang masuk tanpa tap
    // pulang menjadi Bolos; guru yang hanya tap masuk menjadi Tidak lengkap.
    public IReadOnlyList<string> FinishDay()
    {
        var changes = new List<string>();
        foreach (var person in _people)
        {
            if (person.Kind == JenisPemilikKartu.Siswa)
            {
                var final = _engine.TentukanStatusAkhirHariSiswa(person.StudentStatus, person.DepartureTime is not null);
                if (final != person.StudentStatus)
                {
                    changes.Add($"{person.Name}: {StatusLabels.Student(person.StudentStatus)} → {StatusLabels.Student(final)}");
                    person.StudentStatus = final;
                }
            }
            else if (person.TeacherPresetStatus is null)
            {
                var final = _engine.TentukanStatusAkhirHariGuru(person.TeacherStatus);
                if (final != person.TeacherStatus)
                {
                    changes.Add($"{person.Name}: {StatusLabels.Teacher(person.TeacherStatus)} → {StatusLabels.Teacher(final)}");
                    person.TeacherStatus = final;
                }
            }
        }

        return changes;
    }

    public void ResetDay()
    {
        foreach (var person in _people)
        {
            person.ResetDay();
        }

        _log.Clear();
    }

    private KonteksTap BuildContext(SimulatedPerson? person, DateTime moment) => new(
        moment,
        Settings.RegistrationModeActive,
        Settings.DayType,
        person?.Kind,
        Siswa: person is { Kind: JenisPemilikKartu.Siswa } ? new KonteksTapSiswa(
            person.Active,
            person.LastTap,
            Settings.MinimumTapInterval,
            SudahTapMasukHariIni: person.EntryTime is not null,
            Settings.EntryTime,
            Settings.EntryDeadline,
            Settings.DepartureStart,
            person.StudentStatus) : null,
        Guru: person is { Kind: JenisPemilikKartu.Guru } ? new KonteksTapGuru(
            person.Active,
            person.LastTap,
            Settings.MinimumTapInterval,
            person.TeacherPresetStatus,
            SudahTapMasukHariIni: person.EntryTime is not null,
            Settings.DepartureStart) : null);

    private static void Apply(SimulatedPerson person, KeputusanTap decision, TimeOnly time)
    {
        switch (decision.Tindakan)
        {
            case JenisTindakanTap.IsiJamMasuk:
                person.EntryTime = time;
                if (decision.StatusSiswaBaru is { } studentStatus)
                {
                    person.StudentStatus = studentStatus;
                }

                if (decision.StatusGuruBaru is { } teacherStatus)
                {
                    person.TeacherStatus = teacherStatus;
                }

                break;

            case JenisTindakanTap.IsiJamPulang:
                person.DepartureTime = time;
                if (decision.StatusGuruBaru is { } finalTeacherStatus)
                {
                    person.TeacherStatus = finalTeacherStatus;
                }

                break;
        }
    }

    private SimulatedPerson RequirePerson(string cardUid, JenisPemilikKartu kind) =>
        FindPerson(cardUid) is { } person && person.Kind == kind
            ? person
            : throw new InvalidOperationException($"Kartu {cardUid} bukan milik {(kind == JenisPemilikKartu.Siswa ? "siswa" : "guru")} di simulator.");
}
