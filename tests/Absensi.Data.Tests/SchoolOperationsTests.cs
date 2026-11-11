using Absensi.Data.Akun;
using Absensi.Data.Audit;
using Absensi.Data.Kartu;
using Absensi.Data.Koreksi;
using Absensi.Data.MasterData;
using Absensi.Data.Repositori;
using Absensi.Data.Waktu;
using Absensi.Domain.Akun;
using Absensi.Domain.Kehadiran;

namespace Absensi.Data.Tests;

public sealed class SchoolOperationsTests
{
    private static readonly AkunTersimpan Admin = new(1, "admin1", "x", PeranAkun.Admin, null, true);
    private static readonly DateTime FirstDay = new(2026, 9, 17, 7, 29, 0);
    private static readonly DateOnly Date = DateOnly.FromDateTime(FirstDay);

    [Fact]
    public void ReplacementRejectsStaleOldCardAndPreservesExistingOwner()
    {
        using var database = new MigratedDatabase();
        database.SeedSekolahUntukTap();
        AddAdmin(database);
        var service = new LayananRegistrasiKartu();

        Assert.Throws<InvalidOperationException>(() => service.GantiKartuHilang(database.Connection, Admin, FirstDay,
            JenisPemilikKartu.Siswa, 1, "00000007", "rusak", "STALE"));
        Assert.Throws<InvalidOperationException>(() => service.GantiKartuHilang(database.Connection, Admin, FirstDay,
            JenisPemilikKartu.Siswa, 1, "UID-GURU-1", "rusak", "UID-SISWA-1"));
        Assert.Equal("UID-SISWA-1", service.CariUidAktifSiswa(database.Connection, 1));

        service.GantiKartuHilang(database.Connection, Admin, FirstDay, JenisPemilikKartu.Siswa, 1, "00000007", "rusak", "UID-SISWA-1");
        Assert.Equal("00000007", service.CariUidAktifSiswa(database.Connection, 1));
        using var cmd = database.Connection.CreateCommand();
        cmd.CommandText = "SELECT alasan FROM kartu WHERE uid = 'UID-SISWA-1' AND aktif = 0;";
        Assert.Equal("rusak", cmd.ExecuteScalar());
    }

    [Fact]
    public void ReassigningHomeroomRevokesExistingAccountAndKeepsAssignmentHistory()
    {
        using var database = new MigratedDatabase();
        database.SeedAkunUntukUji();
        var principal = Admin with { Username = "kepsek", Peran = PeranAkun.KepalaSekolah };
        var previous = new AkunTersimpan(2, "wali_a", "x", PeranAkun.WaliKelas, 1, true);
        var accounts = new LayananAkun();
        Assert.True(accounts.BolehMenyentuhKelas(database.Connection, previous, 1));

        new LayananKelas().Perbarui(database.Connection, principal, FirstDay, 1, "7A", 7, 2);
        Assert.False(accounts.BolehMenyentuhKelas(database.Connection, previous, 1));
        var history = new RepositoriKelas().HomeroomHistory(database.Connection, 1);
        Assert.Equal(2, history.Count);
        Assert.Equal(FirstDay, history[1].EndedAt);
        Assert.Null(history[0].EndedAt);
        Assert.Throws<InvalidOperationException>(() => new LayananAudit().ReadActive(database.Connection, previous,
            FirstDay.AddDays(-1), FirstDay));
    }

    [Fact]
    public void PromoteKeepsOriginalAttendanceClassAndCard()
    {
        using var database = new MigratedDatabase();
        database.SeedSekolahUntukTap();
        AddAdmin(database);
        new PembukaanHari().Jalankan(database.Connection, Date);
        new PemrosesTap().Proses(database.Connection, "UID-SISWA-1", FirstDay, false);

        var periods = new AcademicPeriodService();
        var next = new DateOnly(2027, 1, 4);
        var nextId = periods.Prepare(database.Connection, Admin, FirstDay, "2026/2027", "genap", next, next.AddMonths(5));
        var classId = new LayananKelas().Tambah(database.Connection, Admin, FirstDay, nextId, "8A", 8, null);
        periods.Promote(database.Connection, Admin, FirstDay, 1, classId, [1]);
        periods.Activate(database.Connection, Admin, next.ToDateTime(new TimeOnly(7, 0)), nextId);

        Assert.Equal("7A", new RepositoriSiswaKelas().CariNamaKelas(database.Connection, null, 1, 1));
        Assert.Equal("8A", new RepositoriSiswaKelas().CariNamaKelas(database.Connection, null, 1, nextId));
        Assert.Equal("UID-SISWA-1", new LayananRegistrasiKartu().CariUidAktifSiswa(database.Connection, 1));
        var recorded = Assert.Single(new RepositoriAbsensi().DaftarSiswaUntukRentang(database.Connection, Date, Date, 1));
        Assert.Equal("7A", recorded.KelasNama);
        Assert.Equal(new TimeOnly(7, 29), recorded.JamMasuk);
    }

    [Fact]
    public void LocalMonthLockRequiresCompleteDaysAndRejectsSubsequentTapAndCalendarChanges()
    {
        using var database = new MigratedDatabase();
        database.SeedSekolahUntukTap();
        AddAdmin(database);
        database.Execute("UPDATE tahun_ajaran SET tgl_mulai = '2026-09-17', tgl_selesai = '2026-09-17';");
        var closure = new LocalAttendanceClosure();
        var locks = new LayananKuncimBulan();
        var closingTime = new DateTime(2026, 10, 1, 8, 0, 0);

        Assert.Throws<InvalidOperationException>(() => locks.LockVerifiedLocalMonth(database.Connection, Admin, closingTime, Date));
        new PembukaanHari().Jalankan(database.Connection, Date);
        new PemrosesTap().Proses(database.Connection, "UID-SISWA-1", FirstDay, false);
        Assert.Throws<InvalidOperationException>(() => locks.LockVerifiedLocalMonth(database.Connection, Admin, closingTime, Date));

        closure.FinalizeDay(database.Connection, Admin, closingTime, Date);
        Assert.Equal(StatusAbsensiSiswa.Bolos, new RepositoriAbsensi().CariHariIniSiswa(database.Connection, Date, 1)?.Status);
        locks.LockVerifiedLocalMonth(database.Connection, Admin, closingTime, Date);
        Assert.True(locks.SudahDikunci(database.Connection, Date.Year, Date.Month));
        Assert.Equal(AlasanTap.BulanTerkunci, new PemrosesTap().Proses(database.Connection, "UID-SISWA-1", FirstDay, false).Alasan);
        Assert.Throws<InvalidOperationException>(() => new LayananKalender().TandaiRentang(database.Connection, Admin,
            closingTime, Date, Date, JenisHariKalender.Libur, "Uji"));
    }

    private static void AddAdmin(MigratedDatabase database) => database.Execute("""
        INSERT INTO akun (id, username, sandi_hash, peran, aktif, dibuat_pada)
        VALUES (1, 'admin1', 'x', 'admin', 1, '2026-09-17T00:00:00');
        """);
}
