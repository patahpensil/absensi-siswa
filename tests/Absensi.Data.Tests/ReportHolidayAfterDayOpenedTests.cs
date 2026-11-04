using Absensi.Data.Laporan;
using Absensi.Data.Repositori;
using Absensi.Domain.Akun;
using Absensi.Domain.Laporan;

namespace Absensi.Data.Tests;

// T8 / PRD §6, §16.12 keputusan P-13: tanggal yang ditetapkan libur setelah
// hari dibuka masih punya baris absensi (Alpa dari pembukaan hari atau tap
// yang sempat tercatat). Baris itu tetap tersimpan, tetapi tidak dihitung
// dalam laporan apa pun. Seed laporan menandai 17 Sept 2026 sebagai libur;
// tes di sini menambahkan baris absensi pada tanggal itu untuk meniru libur
// yang ditetapkan belakangan.
public sealed class ReportHolidayAfterDayOpenedTests
{
    private static readonly DateOnly Senin = new(2026, 9, 14);
    private static readonly DateOnly Sabtu = new(2026, 9, 19);
    private static readonly DateOnly HolidayDate = new(2026, 9, 17);
    private static readonly AkunTersimpan Admin = new(1, "admin1", "x", PeranAkun.Admin, null, true);

    private readonly LayananLaporan _layanan = new();

    private static void AddRowsOnHolidayDate(MigratedDatabase database) => database.Execute("""
        INSERT INTO absensi_siswa (tanggal, siswa_id, kelas_id, status, jam_masuk, jam_pulang, keterangan, sumber) VALUES
            ('2026-09-17', 1, 1, 'terlambat', '07:45', NULL, NULL, 'tap'),
            ('2026-09-17', 2, 1, 'alpa', NULL, NULL, NULL, 'sistem');
        INSERT INTO absensi_guru (tanggal, guru_id, status, jam_masuk, jam_pulang, keterangan, sumber) VALUES
            ('2026-09-17', 1, 'alpa', NULL, NULL, NULL, 'sistem');
        """);

    [Fact]
    public void Monthly_RowsOnHolidayDate_DoNotChangeRecapOrAppearInGrid()
    {
        using var database = new MigratedDatabase();
        database.SeedSekolahUntukLaporan();
        var before = _layanan.Bulanan(database.Connection, Admin, tahunAjaranId: 1, tahun: 2026, bulan: 9, kelasId: 1, JenisSubjekLaporan.Siswa);

        AddRowsOnHolidayDate(database);
        var after = _layanan.Bulanan(database.Connection, Admin, tahunAjaranId: 1, tahun: 2026, bulan: 9, kelasId: 1, JenisSubjekLaporan.Siswa);

        foreach (var baris in after.Baris)
        {
            var rowBefore = before.Baris.Single(b => b.Nama == baris.Nama);
            Assert.Equal(rowBefore.Rekap, baris.Rekap);
            Assert.False(baris.KodePerTanggal.ContainsKey(HolidayDate));
        }

        Assert.True(after.Kolom.Single(k => k.Tanggal == HolidayDate).Libur);
    }

    [Fact]
    public void PerPersonAndSemester_RowsOnHolidayDate_NotCountedOrListed()
    {
        using var database = new MigratedDatabase();
        database.SeedSekolahUntukLaporan();
        var cardBefore = _layanan.PerOrang(database.Connection, Admin, id: 2, JenisSubjekLaporan.Siswa, tahunAjaranId: 1, Senin, Sabtu);
        var semesterBefore = _layanan.Semester(database.Connection, Admin, tahunAjaranId: 1, kelasId: 1, JenisSubjekLaporan.Siswa);

        AddRowsOnHolidayDate(database);
        var kartu = _layanan.PerOrang(database.Connection, Admin, id: 2, JenisSubjekLaporan.Siswa, tahunAjaranId: 1, Senin, Sabtu);
        var semester = _layanan.Semester(database.Connection, Admin, tahunAjaranId: 1, kelasId: 1, JenisSubjekLaporan.Siswa);

        Assert.Equal(cardBefore.Rekap, kartu.Rekap);
        Assert.DoesNotContain(kartu.Rincian, r => r.Tanggal == HolidayDate);
        foreach (var baris in semester)
        {
            Assert.Equal(semesterBefore.Single(b => b.Nama == baris.Nama).Rekap, baris.Rekap);
        }
    }

    [Fact]
    public void EventsAndTeachers_RowsOnHolidayDate_NotShown()
    {
        using var database = new MigratedDatabase();
        database.SeedSekolahUntukLaporan();
        var teachersBefore = _layanan.Bulanan(database.Connection, Admin, tahunAjaranId: 1, tahun: 2026, bulan: 9, kelasId: null, JenisSubjekLaporan.Guru);

        AddRowsOnHolidayDate(database);

        Assert.Empty(_layanan.Kejadian(database.Connection, Admin, HolidayDate, HolidayDate, kelasId: 1, JenisSubjekLaporan.Siswa));
        Assert.Empty(_layanan.Kejadian(database.Connection, Admin, HolidayDate, HolidayDate, kelasId: null, JenisSubjekLaporan.Guru));
        var guru = _layanan.Bulanan(database.Connection, Admin, tahunAjaranId: 1, tahun: 2026, bulan: 9, kelasId: null, JenisSubjekLaporan.Guru);
        Assert.Equal(teachersBefore.Baris.Single().Rekap, guru.Baris.Single().Rekap);
    }

    [Fact]
    public void RowsOnHolidayDate_RemainStoredAsHistory()
    {
        using var database = new MigratedDatabase();
        database.SeedSekolahUntukLaporan();
        AddRowsOnHolidayDate(database);

        _layanan.Bulanan(database.Connection, Admin, tahunAjaranId: 1, tahun: 2026, bulan: 9, kelasId: 1, JenisSubjekLaporan.Siswa);

        using var command = database.Connection.CreateCommand();
        command.CommandText = "SELECT COUNT(*) FROM absensi_siswa WHERE tanggal = '2026-09-17';";
        Assert.Equal(2, Convert.ToInt32(command.ExecuteScalar()));
    }
}
