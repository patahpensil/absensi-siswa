namespace Absensi.Data.Tests;

public sealed class PemrosesTapTests
{
    private static readonly DateOnly HariKamis = new(2026, 9, 17);

    private readonly PembukaanHari _pembukaanHari = new();
    private readonly PemrosesTap _pemrosesTap = new();

    private static DateTime Waktu(int jam, int menit) => HariKamis.ToDateTime(new TimeOnly(jam, menit));

    [Fact]
    public void Proses_TapSiswaSebelumJamMasuk_MenulisHadirDanTapLog()
    {
        using var database = new MigratedDatabase();
        database.SeedSekolahUntukTap();
        _pembukaanHari.Jalankan(database.Connection, HariKamis);

        var keputusan = _pemrosesTap.Proses(database.Connection, "UID-SISWA-1", Waktu(7, 29), modeRegistrasiAktif: false);

        Assert.Equal(Absensi.Domain.Kehadiran.JenisTindakanTap.IsiJamMasuk, keputusan.Tindakan);
        Assert.Equal(Absensi.Domain.Kehadiran.StatusAbsensiSiswa.Hadir, keputusan.StatusSiswaBaru);

        using var command = database.Connection.CreateCommand();
        command.CommandText = "SELECT status, jam_masuk FROM absensi_siswa WHERE siswa_id = 1;";
        using var reader = command.ExecuteReader();
        Assert.True(reader.Read());
        Assert.Equal("hadir", reader.GetString(0));
        Assert.Equal("07:29", reader.GetString(1));

        AssertTapLogTunggal(database, hasilDiharapkan: "hadir", pemilikTipe: "siswa", pemilikId: 1);
    }

    [Fact]
    public void Proses_TapDuaKaliMasukLaluPulang_KeduanyaTercatatDiTapLog()
    {
        using var database = new MigratedDatabase();
        database.SeedSekolahUntukTap();
        _pembukaanHari.Jalankan(database.Connection, HariKamis);

        _pemrosesTap.Proses(database.Connection, "UID-SISWA-1", Waktu(7, 29), modeRegistrasiAktif: false);
        var keputusanPulang = _pemrosesTap.Proses(database.Connection, "UID-SISWA-1", Waktu(13, 15), modeRegistrasiAktif: false);

        Assert.Equal(Absensi.Domain.Kehadiran.JenisTindakanTap.IsiJamPulang, keputusanPulang.Tindakan);

        using var command = database.Connection.CreateCommand();
        command.CommandText = "SELECT COUNT(*) FROM tap_log WHERE uid = 'UID-SISWA-1';";
        Assert.Equal(2, Convert.ToInt32(command.ExecuteScalar()));

        using var commandAbsensi = database.Connection.CreateCommand();
        commandAbsensi.CommandText = "SELECT status, jam_masuk, jam_pulang FROM absensi_siswa WHERE siswa_id = 1;";
        using var reader = commandAbsensi.ExecuteReader();
        Assert.True(reader.Read());
        Assert.Equal("hadir", reader.GetString(0));
        Assert.Equal("07:29", reader.GetString(1));
        Assert.Equal("13:15", reader.GetString(2));
    }

    [Fact]
    public void Proses_KartuTidakTerdaftar_TercatatDitolakDiTapLog()
    {
        using var database = new MigratedDatabase();
        database.SeedSekolahUntukTap();
        _pembukaanHari.Jalankan(database.Connection, HariKamis);

        var keputusan = _pemrosesTap.Proses(database.Connection, "UID-TIDAK-ADA", Waktu(7, 29), modeRegistrasiAktif: false);

        Assert.Equal(Absensi.Domain.Kehadiran.JenisTindakanTap.Ditolak, keputusan.Tindakan);
        Assert.Equal(Absensi.Domain.Kehadiran.AlasanTap.KartuTidakTerdaftar, keputusan.Alasan);

        AssertTapLogTunggal(database, hasilDiharapkan: "ditolak_tidak_terdaftar", pemilikTipe: null, pemilikId: null);
    }

    [Fact]
    public void Proses_GuruNonaktif_TercatatDitolak()
    {
        using var database = new MigratedDatabase();
        database.SeedSekolahUntukTap();
        _pembukaanHari.Jalankan(database.Connection, HariKamis);

        var keputusan = _pemrosesTap.Proses(database.Connection, "UID-GURU-2", Waktu(7, 0), modeRegistrasiAktif: false);

        Assert.Equal(Absensi.Domain.Kehadiran.JenisTindakanTap.Ditolak, keputusan.Tindakan);
        Assert.Equal(Absensi.Domain.Kehadiran.AlasanTap.PemilikTidakAktif, keputusan.Alasan);

        AssertTapLogTunggal(database, hasilDiharapkan: "ditolak_tidak_terdaftar", pemilikTipe: "guru", pemilikId: 2);
    }

    [Fact]
    public void Proses_TanggalLibur_SemuaTapDitolakDanTetapTercatat()
    {
        using var database = new MigratedDatabase();
        database.SeedSekolahUntukTap();
        database.Execute("""
            INSERT INTO kalender (tanggal, jenis, keterangan)
            VALUES ('2026-09-17', 'libur', 'Libur mendadak untuk pengujian');
            """);

        var keputusan = _pemrosesTap.Proses(database.Connection, "UID-SISWA-1", Waktu(7, 29), modeRegistrasiAktif: false);

        Assert.Equal(Absensi.Domain.Kehadiran.JenisTindakanTap.Ditolak, keputusan.Tindakan);
        Assert.Equal(Absensi.Domain.Kehadiran.AlasanTap.TanggalLibur, keputusan.Alasan);

        AssertTapLogTunggal(database, hasilDiharapkan: "ditolak_libur", pemilikTipe: "siswa", pemilikId: 1);
    }

    [Fact]
    public void Proses_GuruStatusSudahDitetapkanDiMuka_TapTidakMengubahStatus()
    {
        using var database = new MigratedDatabase();
        database.SeedSekolahUntukTap();
        _pembukaanHari.Jalankan(database.Connection, HariKamis);
        database.Execute("UPDATE absensi_guru SET status = 'izin' WHERE guru_id = 1;");

        var keputusan = _pemrosesTap.Proses(database.Connection, "UID-GURU-1", Waktu(7, 0), modeRegistrasiAktif: false);

        Assert.Equal(Absensi.Domain.Kehadiran.JenisTindakanTap.TidakBerubah, keputusan.Tindakan);
        Assert.Equal(Absensi.Domain.Kehadiran.StatusAbsensiGuru.Izin, keputusan.StatusGuruBaru);

        using var command = database.Connection.CreateCommand();
        command.CommandText = "SELECT status, jam_masuk FROM absensi_guru WHERE guru_id = 1;";
        using var reader = command.ExecuteReader();
        Assert.True(reader.Read());
        Assert.Equal("izin", reader.GetString(0));
        Assert.True(reader.IsDBNull(1));

        AssertTapLogTunggal(database, hasilDiharapkan: "diabaikan", pemilikTipe: "guru", pemilikId: 1);
    }

    [Fact]
    public void Proses_ModeRegistrasiAktif_TidakMengubahAbsensi()
    {
        using var database = new MigratedDatabase();
        database.SeedSekolahUntukTap();
        _pembukaanHari.Jalankan(database.Connection, HariKamis);

        var keputusan = _pemrosesTap.Proses(database.Connection, "UID-SISWA-1", Waktu(7, 29), modeRegistrasiAktif: true);

        Assert.Equal(Absensi.Domain.Kehadiran.JenisTindakanTap.Registrasi, keputusan.Tindakan);

        using var command = database.Connection.CreateCommand();
        command.CommandText = "SELECT status, jam_masuk FROM absensi_siswa WHERE siswa_id = 1;";
        using var reader = command.ExecuteReader();
        Assert.True(reader.Read());
        Assert.Equal("alpa", reader.GetString(0));
        Assert.True(reader.IsDBNull(1));

        AssertTapLogTunggal(database, hasilDiharapkan: "registrasi", pemilikTipe: "siswa", pemilikId: 1);
    }

    // RENCANA-TUGAS.md Tugas 10 — PRD §16.8.a langkah 9 / §16.9: Terlambat
    // SETELAH jam rekap diantre real-time (dikonfirmasi pemilik proyek
    // 2026-09-18); sebelum jam rekap tidak, karena itu tugas rekap batch
    // (Absensi.Data.Notifikasi.LayananNotifikasi.JalankanRekapPagi).
    [Fact]
    public void Proses_TerlambatSetelahJamRekap_DiantreRealtimeKeOrangTua()
    {
        using var database = new MigratedDatabase();
        database.SeedSekolahUntukTap();
        _pembukaanHari.Jalankan(database.Connection, HariKamis);

        _pemrosesTap.Proses(database.Connection, "UID-SISWA-1", Waktu(8, 15), modeRegistrasiAktif: false);

        using var command = database.Connection.CreateCommand();
        command.CommandText = "SELECT isi FROM outbox WHERE chat_id = 'chat-siswa-uji' AND jenis = 'terlambat' AND status = 'antre';";
        var isi = command.ExecuteScalar() as string;
        Assert.NotNull(isi);
        Assert.Contains("Siswa Aktif", isi);
        Assert.Contains("08:15", isi);
    }

    [Fact]
    public void Proses_TerlambatSebelumJamRekap_TidakDiantreRealtime()
    {
        using var database = new MigratedDatabase();
        database.SeedSekolahUntukTap();
        _pembukaanHari.Jalankan(database.Connection, HariKamis);

        _pemrosesTap.Proses(database.Connection, "UID-SISWA-1", Waktu(7, 45), modeRegistrasiAktif: false);

        using var command = database.Connection.CreateCommand();
        command.CommandText = "SELECT COUNT(*) FROM outbox WHERE chat_id = 'chat-siswa-uji';";
        Assert.Equal(0, Convert.ToInt32(command.ExecuteScalar()));
    }

    private static void AssertTapLogTunggal(
        MigratedDatabase database,
        string hasilDiharapkan,
        string? pemilikTipe,
        int? pemilikId)
    {
        using var command = database.Connection.CreateCommand();
        command.CommandText = "SELECT hasil, pemilik_tipe, pemilik_id FROM tap_log;";
        using var reader = command.ExecuteReader();

        Assert.True(reader.Read());
        Assert.Equal(hasilDiharapkan, reader.GetString(0));
        Assert.Equal(pemilikTipe, reader.IsDBNull(1) ? null : reader.GetString(1));
        Assert.Equal(pemilikId, reader.IsDBNull(2) ? null : reader.GetInt32(2));
        Assert.False(reader.Read());
    }
}
