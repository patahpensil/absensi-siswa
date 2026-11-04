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
public sealed class LaporanLiburSetelahHariDibukaTests
{
    private static readonly DateOnly Senin = new(2026, 9, 14);
    private static readonly DateOnly Sabtu = new(2026, 9, 19);
    private static readonly DateOnly TanggalLibur = new(2026, 9, 17);
    private static readonly AkunTersimpan Admin = new(1, "admin1", "x", PeranAkun.Admin, null, true);

    private readonly LayananLaporan _layanan = new();

    private static void TambahBarisPadaTanggalLibur(MigratedDatabase database) => database.Execute("""
        INSERT INTO absensi_siswa (tanggal, siswa_id, kelas_id, status, jam_masuk, jam_pulang, keterangan, sumber) VALUES
            ('2026-09-17', 1, 1, 'terlambat', '07:45', NULL, NULL, 'tap'),
            ('2026-09-17', 2, 1, 'alpa', NULL, NULL, NULL, 'sistem');
        INSERT INTO absensi_guru (tanggal, guru_id, status, jam_masuk, jam_pulang, keterangan, sumber) VALUES
            ('2026-09-17', 1, 'alpa', NULL, NULL, NULL, 'sistem');
        """);

    [Fact]
    public void Bulanan_BarisPadaTanggalLibur_TidakMengubahRekapDanTidakTampilDiGrid()
    {
        using var database = new MigratedDatabase();
        database.SeedSekolahUntukLaporan();
        var sebelum = _layanan.Bulanan(database.Connection, Admin, tahunAjaranId: 1, tahun: 2026, bulan: 9, kelasId: 1, JenisSubjekLaporan.Siswa);

        TambahBarisPadaTanggalLibur(database);
        var sesudah = _layanan.Bulanan(database.Connection, Admin, tahunAjaranId: 1, tahun: 2026, bulan: 9, kelasId: 1, JenisSubjekLaporan.Siswa);

        foreach (var baris in sesudah.Baris)
        {
            var barisSebelum = sebelum.Baris.Single(b => b.Nama == baris.Nama);
            Assert.Equal(barisSebelum.Rekap, baris.Rekap);
            Assert.False(baris.KodePerTanggal.ContainsKey(TanggalLibur));
        }

        Assert.True(sesudah.Kolom.Single(k => k.Tanggal == TanggalLibur).Libur);
    }

    [Fact]
    public void PerOrangDanSemester_BarisPadaTanggalLibur_TidakDihitungDanTidakMasukRincian()
    {
        using var database = new MigratedDatabase();
        database.SeedSekolahUntukLaporan();
        var kartuSebelum = _layanan.PerOrang(database.Connection, Admin, id: 2, JenisSubjekLaporan.Siswa, tahunAjaranId: 1, Senin, Sabtu);
        var semesterSebelum = _layanan.Semester(database.Connection, Admin, tahunAjaranId: 1, kelasId: 1, JenisSubjekLaporan.Siswa);

        TambahBarisPadaTanggalLibur(database);
        var kartu = _layanan.PerOrang(database.Connection, Admin, id: 2, JenisSubjekLaporan.Siswa, tahunAjaranId: 1, Senin, Sabtu);
        var semester = _layanan.Semester(database.Connection, Admin, tahunAjaranId: 1, kelasId: 1, JenisSubjekLaporan.Siswa);

        Assert.Equal(kartuSebelum.Rekap, kartu.Rekap);
        Assert.DoesNotContain(kartu.Rincian, r => r.Tanggal == TanggalLibur);
        foreach (var baris in semester)
        {
            Assert.Equal(semesterSebelum.Single(b => b.Nama == baris.Nama).Rekap, baris.Rekap);
        }
    }

    [Fact]
    public void KejadianDanGuru_BarisPadaTanggalLibur_TidakTampil()
    {
        using var database = new MigratedDatabase();
        database.SeedSekolahUntukLaporan();
        var guruSebelum = _layanan.Bulanan(database.Connection, Admin, tahunAjaranId: 1, tahun: 2026, bulan: 9, kelasId: null, JenisSubjekLaporan.Guru);

        TambahBarisPadaTanggalLibur(database);

        Assert.Empty(_layanan.Kejadian(database.Connection, Admin, TanggalLibur, TanggalLibur, kelasId: 1, JenisSubjekLaporan.Siswa));
        Assert.Empty(_layanan.Kejadian(database.Connection, Admin, TanggalLibur, TanggalLibur, kelasId: null, JenisSubjekLaporan.Guru));
        var guru = _layanan.Bulanan(database.Connection, Admin, tahunAjaranId: 1, tahun: 2026, bulan: 9, kelasId: null, JenisSubjekLaporan.Guru);
        Assert.Equal(guruSebelum.Baris.Single().Rekap, guru.Baris.Single().Rekap);
    }

    [Fact]
    public void BarisPadaTanggalLibur_TetapTersimpanSebagaiJejakKejadian()
    {
        using var database = new MigratedDatabase();
        database.SeedSekolahUntukLaporan();
        TambahBarisPadaTanggalLibur(database);

        _layanan.Bulanan(database.Connection, Admin, tahunAjaranId: 1, tahun: 2026, bulan: 9, kelasId: 1, JenisSubjekLaporan.Siswa);

        using var command = database.Connection.CreateCommand();
        command.CommandText = "SELECT COUNT(*) FROM absensi_siswa WHERE tanggal = '2026-09-17';";
        Assert.Equal(2, Convert.ToInt32(command.ExecuteScalar()));
    }
}
