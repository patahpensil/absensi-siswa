using Absensi.Data.Akun;
using Absensi.Data.Koreksi;
using Absensi.Data.Repositori;
using Absensi.Domain.Akun;
using Absensi.Domain.Kehadiran;

namespace Absensi.Data.Tests;

// RENCANA-TUGAS.md Tugas 11 — layar Guru→Kehadiran (PRD §16.17, §17.10).
public sealed class LayananStatusGuruTests
{
    private static readonly DateOnly HariIni = new(2026, 9, 17);
    private static readonly DateOnly Mendatang = new(2026, 9, 25);
    private static readonly DateTime Waktu = HariIni.ToDateTime(new TimeOnly(8, 0));
    private static readonly AkunTersimpan Kepsek = new(2, "kepsek", "x", PeranAkun.KepalaSekolah, null, true);
    private static readonly AkunTersimpan Admin = new(1, "admin1", "x", PeranAkun.Admin, null, true);
    private static readonly AkunTersimpan WaliKelas = new(3, "wali_a", "x", PeranAkun.WaliKelas, 1, true);

    [Fact]
    public void TetapkanStatus_Setelah_MemperbaruiBarisYangSudahAda()
    {
        using var database = new MigratedDatabase();
        database.SeedSekolahUntukKoreksi();
        database.Execute("""
            INSERT INTO absensi_guru (tanggal, guru_id, status, sumber) VALUES ('2026-09-17', 3, 'alpa', 'sistem');
            """);
        var layanan = new LayananStatusGuru();

        layanan.TetapkanStatus(database.Connection, Kepsek, Waktu, guruId: 3, HariIni, StatusAbsensiGuru.Sakit, "Demam");

        using var command = database.Connection.CreateCommand();
        command.CommandText = "SELECT status, keterangan FROM absensi_guru WHERE guru_id = 3 AND tanggal = '2026-09-17';";
        using var reader = command.ExecuteReader();
        Assert.True(reader.Read());
        Assert.Equal("sakit", reader.GetString(0));
        Assert.Equal("Demam", reader.GetString(1));
    }

    // PRD §16.17 langkah 3.1: "DI MUKA" — tanggal mendatang belum punya
    // baris sama sekali.
    [Fact]
    public void TetapkanStatus_DiMuka_MembuatBarisBaruUntukTanggalMendatang()
    {
        using var database = new MigratedDatabase();
        database.SeedSekolahUntukKoreksi();
        var layanan = new LayananStatusGuru();

        layanan.TetapkanStatus(database.Connection, Kepsek, Waktu, guruId: 3, Mendatang, StatusAbsensiGuru.Cuti, "Cuti tahunan");

        using var command = database.Connection.CreateCommand();
        command.CommandText = "SELECT status FROM absensi_guru WHERE guru_id = 3 AND tanggal = '2026-09-25';";
        Assert.Equal("cuti", command.ExecuteScalar());
    }

    // Baris yang sudah ditetapkan "di muka" harus selamat dari pembukaan
    // hari (INSERT OR IGNORE) saat tanggal itu benar-benar tiba.
    [Fact]
    public void TetapkanStatus_DiMuka_TidakTertimpaSaatPembukaanHariBerjalan()
    {
        using var database = new MigratedDatabase();
        database.SeedSekolahUntukKoreksi();
        // Mendatang (2026-09-25) jatuh hari Jumat (hari=5) — perlu
        // jadwal_hari supaya PembukaanHari tidak mengiranya libur.
        database.Execute("""
            INSERT INTO jadwal_hari (tahun_ajaran_id, hari, hari_sekolah, jam_masuk, jam_rekap, batas_tap_masuk, mulai_pulang)
            VALUES (1, 5, 1, '07:30', '08:00', '10:00', '11:30');
            """);
        var layanan = new LayananStatusGuru();
        layanan.TetapkanStatus(database.Connection, Kepsek, Waktu, guruId: 3, Mendatang, StatusAbsensiGuru.Izin, "Izin keluarga");

        new PembukaanHari().Jalankan(database.Connection, Mendatang);

        using var command = database.Connection.CreateCommand();
        command.CommandText = "SELECT status, keterangan FROM absensi_guru WHERE guru_id = 3 AND tanggal = '2026-09-25';";
        using var reader = command.ExecuteReader();
        Assert.True(reader.Read());
        Assert.Equal("izin", reader.GetString(0));
        Assert.Equal("Izin keluarga", reader.GetString(1));
    }

    [Fact]
    public void TetapkanStatus_Admin_Ditolak()
    {
        using var database = new MigratedDatabase();
        database.SeedSekolahUntukKoreksi();
        var layanan = new LayananStatusGuru();

        Assert.Throws<InvalidOperationException>(() =>
            layanan.TetapkanStatus(database.Connection, Admin, Waktu, guruId: 3, HariIni, StatusAbsensiGuru.Sakit, "x"));
    }

    [Fact]
    public void TetapkanStatus_WaliKelas_Ditolak()
    {
        using var database = new MigratedDatabase();
        database.SeedSekolahUntukKoreksi();
        var layanan = new LayananStatusGuru();

        Assert.Throws<InvalidOperationException>(() =>
            layanan.TetapkanStatus(database.Connection, WaliKelas, Waktu, guruId: 3, HariIni, StatusAbsensiGuru.Sakit, "x"));
    }

    [Fact]
    public void TetapkanStatus_BulanTerkunci_Ditolak()
    {
        using var database = new MigratedDatabase();
        database.SeedSekolahUntukKoreksi();
        new LayananKuncimBulan().KunciBulan(database.Connection, Admin, Waktu, HariIni.Year, HariIni.Month);
        var layanan = new LayananStatusGuru();

        Assert.Throws<InvalidOperationException>(() =>
            layanan.TetapkanStatus(database.Connection, Kepsek, Waktu, guruId: 3, HariIni, StatusAbsensiGuru.Sakit, "x"));
    }
}
