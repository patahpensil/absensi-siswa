using Absensi.Data.Laporan;
using Absensi.Data.Repositori;
using Absensi.Domain.Akun;
using Absensi.Domain.Laporan;

namespace Absensi.Data.Tests;

// RENCANA-TUGAS.md Tugas 12 (PRD §11, §17.8). Seed MigratedDatabase.
// SeedSekolahUntukLaporan (14-19 Sept 2026 = Senin-Sabtu, 17 Sept libur
// mendadak) dipilih supaya jumlah baris absensi persis sama dengan hari
// efektif rentang itu (5 hari) — lihat komentar di seed-nya.
public sealed class LayananLaporanTests
{
    private static readonly DateOnly Senin = new(2026, 9, 14);
    private static readonly DateOnly Sabtu = new(2026, 9, 19);
    private static readonly AkunTersimpan Admin = new(1, "admin1", "x", PeranAkun.Admin, null, true);
    private static readonly AkunTersimpan WaliKelas = new(2, "wali_a", "x", PeranAkun.WaliKelas, 1, true);

    private readonly LayananLaporan _layanan = new();

    // PRD §3 P-22: wali kelas membuka laporan siswa kelas yang sedang ia walikan saja.
    [Fact]
    public void WaliKelas_LaporanKelasnyaSendiri_Diizinkan()
    {
        using var database = new MigratedDatabase();
        database.SeedSekolahUntukLaporan();

        var baris = _layanan.Kejadian(database.Connection, WaliKelas, Senin, Sabtu, kelasId: 1, JenisSubjekLaporan.Siswa);
        var bulanan = _layanan.Bulanan(database.Connection, WaliKelas, 1, 2026, 9, kelasId: 1, JenisSubjekLaporan.Siswa);
        var semester = _layanan.Semester(database.Connection, WaliKelas, 1, kelasId: 1, JenisSubjekLaporan.Siswa);
        var kartu = _layanan.PerOrang(database.Connection, WaliKelas, 1, JenisSubjekLaporan.Siswa, 1, Senin, Sabtu);

        Assert.Equal(10, baris.Count);
        Assert.Equal(2, bulanan.Baris.Count);
        Assert.Equal(2, semester.Count);
        Assert.Equal("Siswa Satu", kartu.Nama);
    }

    [Fact]
    public void WaliKelas_SemuaKelasLaporanGuruAtauKelasLain_Ditolak()
    {
        using var database = new MigratedDatabase();
        database.SeedSekolahUntukLaporan();
        var otherHomeroom = WaliKelas with { Id = 3, Username = "wali_lain", GuruId = 99 };

        Assert.Throws<InvalidOperationException>(() =>
            _layanan.Kejadian(database.Connection, WaliKelas, Senin, Sabtu, kelasId: null, JenisSubjekLaporan.Siswa));
        Assert.Throws<InvalidOperationException>(() =>
            _layanan.Kejadian(database.Connection, WaliKelas, Senin, Sabtu, kelasId: null, JenisSubjekLaporan.Guru));
        Assert.Throws<InvalidOperationException>(() =>
            _layanan.PerOrang(database.Connection, WaliKelas, 1, JenisSubjekLaporan.Guru, 1, Senin, Sabtu));
        Assert.Throws<InvalidOperationException>(() =>
            _layanan.Kejadian(database.Connection, otherHomeroom, Senin, Sabtu, kelasId: 1, JenisSubjekLaporan.Siswa));
        Assert.Throws<InvalidOperationException>(() =>
            _layanan.PerOrang(database.Connection, otherHomeroom, 1, JenisSubjekLaporan.Siswa, 1, Senin, Sabtu));
    }

    [Fact]
    public void Kejadian_Siswa_SatuKelas_LimaBarisPerSiswaDenganKeteranganBenar()
    {
        using var database = new MigratedDatabase();
        database.SeedSekolahUntukLaporan();

        var baris = _layanan.Kejadian(database.Connection, Admin, Senin, Sabtu, kelasId: 1, JenisSubjekLaporan.Siswa);

        Assert.Equal(10, baris.Count);
        var terlambat = Assert.Single(baris, b => b.Nama == "Siswa Satu" && b.Tanggal == new DateOnly(2026, 9, 15));
        Assert.Equal("T", terlambat.StatusHuruf);
        Assert.Equal("Terlambat 07:52", terlambat.Keterangan);

        var bolos = Assert.Single(baris, b => b.Nama == "Siswa Satu" && b.Tanggal == new DateOnly(2026, 9, 18));
        Assert.Equal("B", bolos.StatusHuruf); // bukan "H" — koreksi 2026-09-18, pemilik proyek minta huruf sendiri
        Assert.Equal("Bolos", bolos.Keterangan);

        var sakit = Assert.Single(baris, b => b.Nama == "Siswa Satu" && b.Tanggal == new DateOnly(2026, 9, 16));
        Assert.Equal("Sakit: Demam", sakit.Keterangan);
    }

    [Fact]
    public void Kejadian_Guru_LimaBarisDenganKeteranganTidakLengkap()
    {
        using var database = new MigratedDatabase();
        database.SeedSekolahUntukLaporan();

        var baris = _layanan.Kejadian(database.Connection, Admin, Senin, Sabtu, kelasId: null, JenisSubjekLaporan.Guru);

        Assert.Equal(5, baris.Count);
        var tidakLengkap = Assert.Single(baris, b => b.Tanggal == new DateOnly(2026, 9, 18));
        Assert.Equal("L", tidakLengkap.StatusHuruf); // bukan "H" — koreksi 2026-09-18
        Assert.Equal("Tidak lengkap (belum tap pulang)", tidakLengkap.Keterangan);
        var dinasLuar = Assert.Single(baris, b => b.Tanggal == new DateOnly(2026, 9, 15));
        Assert.Equal("DL", dinasLuar.StatusHuruf);
        Assert.Equal("Dinas luar: Rapat dinas", dinasLuar.Keterangan);
    }

    [Fact]
    public void Bulanan_Siswa_KolomLiburMendadakTerdeteksiDenganAlasan()
    {
        using var database = new MigratedDatabase();
        database.SeedSekolahUntukLaporan();

        var laporan = _layanan.Bulanan(database.Connection, Admin, tahunAjaranId: 1, tahun: 2026, bulan: 9, kelasId: 1, JenisSubjekLaporan.Siswa);

        Assert.Equal(30, laporan.Kolom.Count);
        var kolomLibur = laporan.Kolom.Single(k => k.Tanggal == new DateOnly(2026, 9, 17));
        Assert.True(kolomLibur.Libur);
        Assert.Equal("Libur mendadak banjir", kolomLibur.Alasan);
        Assert.False(laporan.Kolom.Single(k => k.Tanggal == new DateOnly(2026, 9, 14)).Libur);

        Assert.Equal(2, laporan.Baris.Count);
        var siswaSatu = laporan.Baris.Single(b => b.Nama == "Siswa Satu");
        Assert.Equal("T", siswaSatu.KodePerTanggal[new DateOnly(2026, 9, 15)]);
        Assert.Equal("B", siswaSatu.KodePerTanggal[new DateOnly(2026, 9, 18)]); // Bolos -> huruf B (bukan H) di grid
        Assert.Equal(1, siswaSatu.Rekap.Terlambat);
        Assert.Equal(1, siswaSatu.Rekap.Bolos);
        Assert.Equal(1, siswaSatu.Rekap.Sakit);
    }

    // Koreksi 2026-09-18: Bolos/TidakLengkap dipisah hurufnya dari Hadir
    // di grid, tapi Bulanan_Siswa_PersentaseDihitungDariHariEfektifBukan
    // JumlahHariKalender di bawah membuktikan persentase kehadirannya
    // tidak ikut berubah — dua hal yang sengaja dipisah.
    [Fact]
    public void Bulanan_Guru_KolomTidakLengkapPunyaHurufLDanRekapTerpisahDariHadir()
    {
        using var database = new MigratedDatabase();
        database.SeedSekolahUntukLaporan();

        var laporan = _layanan.Bulanan(database.Connection, Admin, tahunAjaranId: 1, tahun: 2026, bulan: 9, kelasId: null, JenisSubjekLaporan.Guru);

        var guru = Assert.Single(laporan.Baris);
        Assert.Equal("L", guru.KodePerTanggal[new DateOnly(2026, 9, 18)]);
        Assert.Equal(2, guru.Rekap.Hadir); // 14 dan 16 Sept — TIDAK termasuk 18 Sept (tidak lengkap)
        Assert.Equal(1, guru.Rekap.TidakLengkap);
        Assert.Equal(1, guru.Rekap.DinasLuar);
        Assert.Equal(1, guru.Rekap.Cuti);
    }

    // RENCANA-TUGAS.md Tugas 12, "Selesai bila": persentase dihitung dari
    // hari efektif kalender (Senin-Sabtu dikurangi libur mendadak),
    // bukan jumlah hari kalender mentah — dihitung independen di sini
    // (langsung dari DayOfWeek) untuk memastikan LayananLaporan tidak
    // diam-diam memakai jumlah hari kalender mentah.
    [Fact]
    public void Bulanan_Siswa_PersentaseDihitungDariHariEfektifBukanJumlahHariKalender()
    {
        using var database = new MigratedDatabase();
        database.SeedSekolahUntukLaporan();
        var hariEfektifSeptember = Enumerable.Range(1, 30)
            .Select(d => new DateOnly(2026, 9, d))
            .Count(t => t.DayOfWeek != DayOfWeek.Sunday) - 1; // -1: 17 Sept libur mendadak

        var laporan = _layanan.Bulanan(database.Connection, Admin, tahunAjaranId: 1, tahun: 2026, bulan: 9, kelasId: 1, JenisSubjekLaporan.Siswa);

        var siswaSatu = laporan.Baris.Single(b => b.Nama == "Siswa Satu");
        Assert.Equal(hariEfektifSeptember, siswaSatu.Rekap.HariEfektif);
        Assert.NotEqual(30, siswaSatu.Rekap.HariEfektif); // jumlah hari kalender mentah
        var persentaseDiharapkan = Math.Round(4 * 100.0 / hariEfektifSeptember, 1); // Hadir+Terlambat+Bolos = 2+1+1
        Assert.Equal(persentaseDiharapkan, siswaSatu.Rekap.Persentase);
    }

    [Fact]
    public void PerOrang_Siswa_RekapDanRincianTidakMemuatHariHadirPenuh()
    {
        using var database = new MigratedDatabase();
        database.SeedSekolahUntukLaporan();

        var kartu = _layanan.PerOrang(database.Connection, Admin, id: 1, JenisSubjekLaporan.Siswa, tahunAjaranId: 1, Senin, Sabtu);

        Assert.Equal("Siswa Satu", kartu.Nama);
        Assert.Equal("0001", kartu.Identitas);
        Assert.Equal("7A", kartu.Kelas);
        Assert.Equal(5, kartu.Rekap.HariEfektif);
        Assert.Equal(80.0, kartu.Rekap.Persentase);
        Assert.Equal(3, kartu.Rincian.Count); // 5 baris dikurangi 2 Hadir penuh (14 dan 19 Sept)
        Assert.DoesNotContain(kartu.Rincian, r => r.Tanggal == new DateOnly(2026, 9, 14));
        Assert.DoesNotContain(kartu.Rincian, r => r.Tanggal == new DateOnly(2026, 9, 19));
    }

    [Fact]
    public void PerOrang_SiswaTanpaBarisSamaSekali_TetapMenghasilkanKartuIdentitas()
    {
        using var database = new MigratedDatabase();
        database.SeedSekolahUntukLaporan();

        var kartu = _layanan.PerOrang(database.Connection, Admin, id: 2, JenisSubjekLaporan.Siswa, tahunAjaranId: 1,
            new DateOnly(2026, 9, 21), new DateOnly(2026, 9, 26)); // rentang tanpa data sama sekali

        Assert.Equal("Siswa Dua", kartu.Nama);
        Assert.Empty(kartu.Rincian);
        Assert.Equal(0, kartu.Rekap.Hadir + kartu.Rekap.Alpa + kartu.Rekap.Izin);
    }

    [Fact]
    public void PerKelasAtauSemua_Guru_MengembalikanSeluruhGuruTanpaMemandangKelasId()
    {
        using var database = new MigratedDatabase();
        database.SeedSekolahUntukLaporan();

        var kartu = _layanan.PerKelasAtauSemua(database.Connection, Admin, JenisSubjekLaporan.Guru, kelasId: 999, tahunAjaranId: 1, Senin, Sabtu);

        var guru = Assert.Single(kartu);
        Assert.Equal("Wali A", guru.Nama);
        Assert.Equal(2, guru.Rekap.Hadir); // TidakLengkap TIDAK ikut Hadir — koreksi 2026-09-18, PRD §17.3
        Assert.Equal(1, guru.Rekap.TidakLengkap);
        Assert.Equal(1, guru.Rekap.DinasLuar);
        Assert.Equal(1, guru.Rekap.Cuti);
        var tidakLengkap = Assert.Single(guru.Rincian, r => r.Tanggal == new DateOnly(2026, 9, 18));
        Assert.Equal("L", tidakLengkap.StatusHuruf);
    }

    [Fact]
    public void Semester_MenggunakanRentangTahunAjaranAktifBukanTanggalKustom()
    {
        using var database = new MigratedDatabase();
        database.SeedSekolahUntukLaporan();

        var baris = _layanan.Semester(database.Connection, Admin, tahunAjaranId: 1, kelasId: null, JenisSubjekLaporan.Siswa);

        Assert.Equal(2, baris.Count);
        var siswaDua = baris.Single(b => b.Nama == "Siswa Dua");
        Assert.Equal(3, siswaDua.Rekap.Hadir);
        Assert.Equal(1, siswaDua.Rekap.Alpa);
        Assert.Equal(1, siswaDua.Rekap.Izin);
    }

    [Fact]
    public void Semester_Guru_TidakLengkapTerpisahDariHadir()
    {
        using var database = new MigratedDatabase();
        database.SeedSekolahUntukLaporan();

        var baris = _layanan.Semester(database.Connection, Admin, tahunAjaranId: 1, kelasId: null, JenisSubjekLaporan.Guru);

        var guru = Assert.Single(baris);
        Assert.Equal(2, guru.Rekap.Hadir);
        Assert.Equal(1, guru.Rekap.TidakLengkap);
        Assert.Equal(1, guru.Rekap.DinasLuar);
        Assert.Equal(1, guru.Rekap.Cuti);
    }
}
