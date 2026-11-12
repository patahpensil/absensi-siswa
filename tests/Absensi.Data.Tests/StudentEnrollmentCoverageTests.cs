using Absensi.Data.Laporan;
using Absensi.Data.MasterData;
using Absensi.Data.Repositori;
using Absensi.Domain.Akun;
using Absensi.Domain.Laporan;

namespace Absensi.Data.Tests;

// P-18 (keputusan pemilik 12 November 2026: otomatis dari data): siswa pindah
// masuk dihitung sejak tanggal ditambahkan ke kelas, siswa mutasi sampai hari
// terakhir bersekolah. Siswa yang ikut sejak awal tetap mengikuti kalender.
public sealed class StudentEnrollmentCoverageTests
{
    private static readonly AkunTersimpan Admin = new(1, "admin", "x", PeranAkun.Admin, null, true);

    [Fact]
    public void StudentAddedMidPeriod_CountedFromJoinDateOnly()
    {
        using var database = Prepare();
        foreach (var day in new[] { 14, 15, 16 })
            new PembukaanHari().Jalankan(database.Connection, new DateOnly(2026, 9, day));

        var newId = new LayananSiswa().Tambah(database.Connection, Admin, new DateTime(2026, 9, 17, 9, 0, 0),
            new DataSiswa("0000000777", "Siswa Pindahan (contoh)", "P", null, null, null, null, null), (1, 1));
        foreach (var day in new[] { 17, 18 })
            new PembukaanHari().Jalankan(database.Connection, new DateOnly(2026, 9, day));

        var report = new LayananLaporan().Bulanan(database.Connection, Admin, 1, 2026, 9, null, JenisSubjekLaporan.Siswa);
        var newcomer = report.Baris.Single(b => b.Nama == "Siswa Pindahan (contoh)").Rekap;
        var original = report.Baris.Single(b => b.Nama == "Siswa Aktif").Rekap;

        Assert.Equal(SchoolDays(new DateOnly(2026, 9, 17), new DateOnly(2026, 9, 30)), newcomer.HariEfektif);
        Assert.Equal(SchoolDays(new DateOnly(2026, 9, 1), new DateOnly(2026, 9, 30)), original.HariEfektif);
        Assert.Equal(2, newcomer.Alpa);
        Assert.True(newId > 0);
    }

    [Fact]
    public void StudentAddedBeforeAnyDayOpened_FollowsCalendar()
    {
        using var database = Prepare();

        new LayananSiswa().Tambah(database.Connection, Admin, new DateTime(2026, 9, 17, 9, 0, 0),
            new DataSiswa("0000000778", "Siswa Awal (contoh)", "L", null, null, null, null, null), (1, 1));

        Assert.False(new RepositoriSiswaKelas().ListEnrollmentDates(database.Connection, 1).Any());
    }

    [Fact]
    public void TransferredStudent_DaysAfterLastSchoolDayNotAlpa()
    {
        using var database = Prepare();
        foreach (var day in new[] { 14, 15 })
            new PembukaanHari().Jalankan(database.Connection, new DateOnly(2026, 9, day));

        // Mutasi pagi 16/09 sebelum tap: hari terakhir 15/09.
        new PembukaanHari().Jalankan(database.Connection, new DateOnly(2026, 9, 16));
        new LayananSiswa().TandaiTidakAktif(database.Connection, Admin, new DateTime(2026, 9, 16, 7, 0, 0), 1, "pindah");

        var rekap = new LayananLaporan().Bulanan(database.Connection, Admin, 1, 2026, 9, null, JenisSubjekLaporan.Siswa)
            .Baris.Single(b => b.Nama == "Siswa Aktif").Rekap;

        Assert.Equal(SchoolDays(new DateOnly(2026, 9, 1), new DateOnly(2026, 9, 15)), rekap.HariEfektif);
        Assert.Equal(2, rekap.Alpa);
        Assert.Equal(((DateOnly?)null, (DateOnly?)new DateOnly(2026, 9, 15)), new RepositoriSiswaKelas().ListEnrollmentDates(database.Connection, 1)[1]);
    }

    [Fact]
    public void TransferredAfterTappingIn_MutationDayIncluded()
    {
        using var database = Prepare();
        new PembukaanHari().Jalankan(database.Connection, new DateOnly(2026, 9, 16));
        new PemrosesTap().Proses(database.Connection, "UID-SISWA-1", new DateTime(2026, 9, 16, 7, 10, 0), modeRegistrasiAktif: false);

        new LayananSiswa().TandaiTidakAktif(database.Connection, Admin, new DateTime(2026, 9, 16, 10, 0, 0), 1, "pindah");

        Assert.Equal(new DateOnly(2026, 9, 16), new RepositoriSiswaKelas().ListEnrollmentDates(database.Connection, 1)[1].End);
    }

    [Fact]
    public void DailyAndWeeklyReports_ExcludeOldRowsOutsideEnrollmentDates()
    {
        using var database = Prepare();
        var monday = new DateOnly(2026, 9, 14);
        var tuesday = monday.AddDays(1);
        var wednesday = monday.AddDays(2);
        foreach (var day in new[] { monday, tuesday, wednesday })
            new PembukaanHari().Jalankan(database.Connection, day);

        database.Execute("UPDATE siswa_kelas SET start_date = '2026-09-15', end_date = '2026-09-15' WHERE siswa_id = 1;");
        var service = new LayananLaporan();
        Assert.Empty(service.Kejadian(database.Connection, Admin, monday, monday, 1, JenisSubjekLaporan.Siswa));
        Assert.Empty(service.Kejadian(database.Connection, Admin, wednesday, wednesday, 1, JenisSubjekLaporan.Siswa));
        Assert.Equal(tuesday, Assert.Single(service.Kejadian(database.Connection, Admin, monday, wednesday, 1, JenisSubjekLaporan.Siswa)).Tanggal);
    }

    // Senin–Sabtu adalah hari sekolah pada data uji (SeedSekolahUntukTap).
    private static int SchoolDays(DateOnly from, DateOnly to)
    {
        var count = 0;
        for (var d = from; d <= to; d = d.AddDays(1))
            if (d.DayOfWeek != DayOfWeek.Sunday) count++;
        return count;
    }

    private static MigratedDatabase Prepare()
    {
        var database = new MigratedDatabase();
        database.SeedSekolahUntukTap();
        database.Execute("""
            INSERT OR IGNORE INTO akun (id, username, sandi_hash, peran, guru_id, aktif, dibuat_pada)
            VALUES (1, 'admin', 'x', 'admin', NULL, 1, '2026-09-01T00:00:00');
            """);
        return database;
    }
}
