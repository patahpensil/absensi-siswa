using Absensi.Data.MasterData;
using Absensi.Data.Repositori;
using Absensi.Domain.Akun;

namespace Absensi.Data.Tests;

// Correcting the date range of an academic period after the wizard (PROGRES §29).
public sealed class AcademicPeriodDateChangeTests
{
    private static readonly DateTime Today = new(2026, 9, 17, 9, 0, 0);
    private static readonly AkunTersimpan Admin = new(1, "admin", "x", PeranAkun.Admin, null, true);
    private static readonly AkunTersimpan Homeroom = new(2, "wali", "x", PeranAkun.WaliKelas, 1, true);
    private readonly AcademicPeriodService _service = new();

    [Fact]
    public void ChangeDates_ActivePeriod_StoredAndAudited()
    {
        using var database = Prepare();

        _service.ChangeDates(database.Connection, Admin, Today, 1, new DateOnly(2026, 7, 13), new DateOnly(2026, 12, 23));

        Assert.Equal("2026-07-13", Scalar(database, "SELECT tgl_mulai FROM tahun_ajaran WHERE id = 1;"));
        Assert.Equal("2026-12-23", Scalar(database, "SELECT tgl_selesai FROM tahun_ajaran WHERE id = 1;"));
        Assert.Equal("2026/2027 · ganjil · 2026-07-01 – 2026-12-19",
            Scalar(database, "SELECT nilai_lama FROM audit WHERE aksi = 'ubah_rentang_tahun_ajaran';"));
        Assert.Equal("2026/2027 · ganjil · 2026-07-13 – 2026-12-23",
            Scalar(database, "SELECT nilai_baru FROM audit WHERE aksi = 'ubah_rentang_tahun_ajaran';"));
    }

    [Fact]
    public void ChangeDates_OverlapWithAnotherPeriod_RejectedWithoutChange()
    {
        using var database = Prepare();
        _service.Prepare(database.Connection, Admin, Today, "2026/2027", "genap", new DateOnly(2027, 1, 4), new DateOnly(2027, 6, 30));

        var error = Assert.Throws<InvalidOperationException>(() =>
            _service.ChangeDates(database.Connection, Admin, Today, 1, new DateOnly(2026, 7, 1), new DateOnly(2027, 1, 10)));

        Assert.Contains("tumpang tindih", error.Message);
        Assert.Equal("2026-12-19", Scalar(database, "SELECT tgl_selesai FROM tahun_ajaran WHERE id = 1;"));
        Assert.Null(Scalar(database, "SELECT aksi FROM audit WHERE aksi = 'ubah_rentang_tahun_ajaran';"));
    }

    [Fact]
    public void ChangeDates_RangeDropsRecordedAttendanceDay_Rejected()
    {
        using var database = Prepare();
        new PembukaanHari().Jalankan(database.Connection, new DateOnly(2026, 9, 17));

        var error = Assert.Throws<InvalidOperationException>(() =>
            _service.ChangeDates(database.Connection, Admin, new DateTime(2026, 10, 1, 9, 0, 0), 1,
                new DateOnly(2026, 9, 18), new DateOnly(2026, 12, 19)));

        Assert.Contains("17/09/2026", error.Message);
        Assert.Equal("2026-07-01", Scalar(database, "SELECT tgl_mulai FROM tahun_ajaran WHERE id = 1;"));
    }

    [Fact]
    public void ChangeDates_ActivePeriodNoLongerCoversToday_Rejected()
    {
        using var database = Prepare();

        var error = Assert.Throws<InvalidOperationException>(() =>
            _service.ChangeDates(database.Connection, Admin, Today, 1, new DateOnly(2026, 7, 1), new DateOnly(2026, 9, 16)));

        Assert.Contains("hari ini", error.Message);
    }

    [Fact]
    public void ChangeDates_FuturePeriod_CanMoveWithoutCoveringToday()
    {
        using var database = Prepare();
        var next = _service.Prepare(database.Connection, Admin, Today, "2026/2027", "genap", new DateOnly(2027, 1, 4), new DateOnly(2027, 6, 30));

        _service.ChangeDates(database.Connection, Admin, Today, next, new DateOnly(2027, 1, 5), new DateOnly(2027, 6, 26));

        Assert.Equal("2027-06-26", Scalar(database, $"SELECT tgl_selesai FROM tahun_ajaran WHERE id = {next};"));
    }

    [Fact]
    public void ChangeDates_EndBeforeStartOrUnchanged_Rejected()
    {
        using var database = Prepare();

        Assert.Throws<InvalidOperationException>(() =>
            _service.ChangeDates(database.Connection, Admin, Today, 1, new DateOnly(2026, 12, 19), new DateOnly(2026, 7, 1)));
        Assert.Throws<InvalidOperationException>(() =>
            _service.ChangeDates(database.Connection, Admin, Today, 1, new DateOnly(2026, 7, 1), new DateOnly(2026, 12, 19)));
    }

    [Fact]
    public void ChangeDates_HomeroomTeacher_Rejected()
    {
        using var database = Prepare();

        Assert.ThrowsAny<Exception>(() =>
            _service.ChangeDates(database.Connection, Homeroom, Today, 1, new DateOnly(2026, 7, 13), new DateOnly(2026, 12, 23)));
        Assert.Equal("2026-07-01", Scalar(database, "SELECT tgl_mulai FROM tahun_ajaran WHERE id = 1;"));
    }

    private static MigratedDatabase Prepare()
    {
        var database = new MigratedDatabase();
        database.SeedSekolahUntukTap();
        database.Execute("""
            INSERT OR IGNORE INTO akun (id, username, sandi_hash, peran, guru_id, aktif, dibuat_pada)
            VALUES (1, 'admin', 'x', 'admin', NULL, 1, '2026-09-01T00:00:00');
            INSERT OR IGNORE INTO akun (id, username, sandi_hash, peran, guru_id, aktif, dibuat_pada)
            VALUES (2, 'wali', 'x', 'wali_kelas', 1, 1, '2026-09-01T00:00:00');
            """);
        return database;
    }

    private static object? Scalar(MigratedDatabase database, string sql)
    {
        using var command = database.Connection.CreateCommand();
        command.CommandText = sql;
        var value = command.ExecuteScalar();
        return value is DBNull ? null : value;
    }
}
