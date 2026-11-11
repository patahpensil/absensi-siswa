using Absensi.Data.Akun;
using Absensi.Data.Repositori;
using Absensi.Domain.Kehadiran;
using Microsoft.Data.Sqlite;

namespace Absensi.Data.MasterData;

public sealed class AcademicPeriodService
{
    private readonly RepositoriTahunAjaran _years = new();
    private readonly RepositoriJadwalHari _schedule = new();
    private readonly RepositoriSiswaKelas _memberships = new();
    private readonly RepositoriSiswa _students = new();
    private readonly RepositoriKartu _cards = new();
    private readonly JalurPerubahanData _changes = new();

    public int Prepare(SqliteConnection connection, AkunTersimpan account, DateTime time,
        string name, string semester, DateOnly start, DateOnly end)
    {
        AuthorizationGuard.EnsureFullAuthority(account, "menyiapkan tahun ajaran");
        name = name.Trim();
        if (name.Length is 0 or > 30 || semester is not ("ganjil" or "genap") || end < start)
            throw new InvalidOperationException("Isi nama, semester, serta rentang tanggal yang sah.");

        var activeId = _years.CariIdAktif(connection) ?? throw new InvalidOperationException("Tahun ajaran aktif belum tersedia.");
        var template = _schedule.DaftarUntukTahunAjaran(connection, activeId);
        var id = 0;
        _changes.Jalankan(connection, account, time, "siapkan_tahun_ajaran", "tahun_ajaran", transaction =>
        {
            if (_years.ListPeriods(connection, transaction).Any(period => start <= period.End && end >= period.Start))
                throw new InvalidOperationException("Rentang bertumpang tindih dengan periode yang sudah ada.");

            id = _years.Tambah(connection, transaction, name, semester, start, end, aktif: false);
            foreach (var day in template)
                _schedule.Perbarui(connection, transaction, id, day);
            return id;
        }, nilaiBaru: $"{name} {semester} · {start:yyyy-MM-dd} – {end:yyyy-MM-dd}");
        return id;
    }

    public void Activate(SqliteConnection connection, AkunTersimpan account, DateTime time, int periodId)
    {
        AuthorizationGuard.EnsureFullAuthority(account, "mengaktifkan tahun ajaran");
        _changes.JalankanDenganCatatan(connection, account, time, "aktifkan_tahun_ajaran", "tahun_ajaran", transaction =>
        {
            var periods = _years.ListPeriods(connection, transaction);
            var target = periods.SingleOrDefault(period => period.Id == periodId)
                ?? throw new InvalidOperationException("Tahun ajaran tidak ditemukan.");
            var current = periods.SingleOrDefault(period => period.Active);
            var today = DateOnly.FromDateTime(time);
            if (target.Active || today < target.Start || today > target.End || current?.Start > target.Start)
                throw new InvalidOperationException("Aktivasi hanya dapat dilakukan pada rentang periode berikutnya.");
            if (!HasClasses(connection, transaction, target.Id))
                throw new InvalidOperationException("Buat kelas untuk periode tujuan sebelum mengaktifkannya.");

            _years.Activate(connection, transaction, target.Id);
            return new CatatanPerubahan(target.Id, current is null ? null : Label(current), Label(target));
        });
    }

    public void Promote(SqliteConnection connection, AkunTersimpan account, DateTime time, int sourceClassId,
        int targetClassId, IReadOnlyCollection<int> studentIds)
    {
        AuthorizationGuard.EnsureFullAuthority(account, "memindahkan siswa ke periode berikutnya");
        var ids = ValidateSelection(studentIds);
        _changes.Jalankan(connection, account, time, "naik_kelas", "kelas", transaction =>
        {
            var (sourcePeriod, sourceLevel) = ReadClass(connection, transaction, sourceClassId);
            var (targetPeriod, targetLevel) = ReadClass(connection, transaction, targetClassId);
            var periods = _years.ListPeriods(connection, transaction);
            var source = periods.Single(period => period.Id == sourcePeriod);
            var target = periods.Single(period => period.Id == targetPeriod);
            if (target.Start <= source.End || targetLevel < sourceLevel || targetLevel > sourceLevel + 1)
                throw new InvalidOperationException("Pilih kelas pada periode berikutnya dengan tingkat yang sama atau naik satu tingkat.");

            foreach (var id in ids)
            {
                EnsureActiveMember(connection, transaction, id, sourceClassId);
                if (ReadPlacement(connection, transaction, id, targetPeriod) is { } existing && existing != targetClassId)
                    throw new InvalidOperationException("Salah satu siswa sudah ditempatkan di kelas lain pada periode tujuan.");
            }

            foreach (var id in ids)
                _memberships.Tetapkan(connection, transaction, id, targetClassId, targetPeriod);
            return targetClassId;
        }, nilaiLama: $"Kelas {sourceClassId}", nilaiBaru: $"Kelas {targetClassId} · siswa {string.Join(", ", ids)}");
    }

    public void Graduate(SqliteConnection connection, AkunTersimpan account, DateTime time, int sourceClassId,
        IReadOnlyCollection<int> studentIds)
    {
        AuthorizationGuard.EnsureFullAuthority(account, "menetapkan kelulusan siswa");
        var ids = ValidateSelection(studentIds);
        _changes.Jalankan(connection, account, time, "luluskan_siswa", "kelas", transaction =>
        {
            var (periodId, level) = ReadClass(connection, transaction, sourceClassId);
            var periods = _years.ListPeriods(connection, transaction);
            var period = periods.Single(item => item.Id == periodId);
            if (level != 9 || DateOnly.FromDateTime(time) <= period.End)
                throw new InvalidOperationException("Kelulusan hanya untuk kelas 9 setelah periode sumber selesai.");

            foreach (var id in ids)
            {
                EnsureActiveMember(connection, transaction, id, sourceClassId);
                if (periods.Where(item => item.Start > period.End).Any(item => ReadPlacement(connection, transaction, id, item.Id) is not null))
                    throw new InvalidOperationException("Salah satu siswa masih terdaftar pada periode berikutnya.");
            }

            foreach (var id in ids)
            {
                _students.TandaiTidakAktif(connection, transaction, id, "Lulus");
                _cards.Nonaktifkan(connection, transaction, JenisPemilikKartu.Siswa, id, time, "lulus");
            }
            return sourceClassId;
        }, nilaiBaru: $"Lulus · siswa {string.Join(", ", ids)}");
    }

    private static int[] ValidateSelection(IReadOnlyCollection<int> selected)
    {
        var ids = selected.Distinct().Order().ToArray();
        if (ids.Length == 0) throw new InvalidOperationException("Pilih setidaknya satu siswa.");
        return ids;
    }

    private static bool HasClasses(SqliteConnection connection, SqliteTransaction transaction, int periodId)
    {
        using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = "SELECT 1 FROM kelas WHERE tahun_ajaran_id = $period LIMIT 1;";
        command.Parameters.AddWithValue("$period", periodId);
        return command.ExecuteScalar() is not null;
    }

    private static (int Period, int Level) ReadClass(SqliteConnection connection, SqliteTransaction transaction, int classId)
    {
        using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = "SELECT tahun_ajaran_id, tingkat FROM kelas WHERE id = $id;";
        command.Parameters.AddWithValue("$id", classId);
        using var reader = command.ExecuteReader();
        if (!reader.Read()) throw new InvalidOperationException("Kelas tidak ditemukan.");
        return (reader.GetInt32(0), reader.GetInt32(1));
    }

    private static void EnsureActiveMember(SqliteConnection connection, SqliteTransaction transaction, int studentId, int classId)
    {
        using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = """
            SELECT 1 FROM siswa_kelas sk JOIN siswa s ON s.id = sk.siswa_id
            WHERE sk.siswa_id = $student AND sk.kelas_id = $class AND s.aktif = 1;
            """;
        command.Parameters.AddWithValue("$student", studentId);
        command.Parameters.AddWithValue("$class", classId);
        if (command.ExecuteScalar() is null)
            throw new InvalidOperationException("Daftar siswa telah berubah. Pilih ulang kelas sumber.");
    }

    private static int? ReadPlacement(SqliteConnection connection, SqliteTransaction transaction, int studentId, int periodId)
    {
        using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = "SELECT kelas_id FROM siswa_kelas WHERE siswa_id = $student AND tahun_ajaran_id = $period;";
        command.Parameters.AddWithValue("$student", studentId);
        command.Parameters.AddWithValue("$period", periodId);
        return command.ExecuteScalar() is long id ? checked((int)id) : null;
    }

    private static string Label(AcademicPeriod period) => $"{period.Name} · {period.Semester}";
}
