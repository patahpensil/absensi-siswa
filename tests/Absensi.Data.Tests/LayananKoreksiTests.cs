using Absensi.Data.Akun;
using Absensi.Data.Koreksi;
using Absensi.Data.Repositori;
using Absensi.Domain.Akun;
using Absensi.Domain.Kehadiran;

namespace Absensi.Data.Tests;

// RENCANA-TUGAS.md Tugas 11 — layar Koreksi (PRD §16.10).
public sealed class LayananKoreksiTests
{
    private static readonly DateOnly Tanggal = new(2026, 9, 17);
    private static readonly DateTime Waktu = Tanggal.ToDateTime(new TimeOnly(15, 0));
    private static readonly AkunTersimpan Admin = new(1, "admin1", "x", PeranAkun.Admin, null, true);
    private static readonly AkunTersimpan WaliA = new(3, "wali_a", "x", PeranAkun.WaliKelas, 1, true);
    private static readonly AkunTersimpan WaliB = new(4, "wali_b", "x", PeranAkun.WaliKelas, 2, true);

    [Fact]
    public void KoreksiStatusSiswa_WaliKelasSendiri_Diizinkan()
    {
        using var database = new MigratedDatabase();
        database.SeedSekolahUntukKoreksi();
        var layanan = new LayananKoreksi();

        layanan.KoreksiStatusSiswa(database.Connection, WaliA, Waktu, kelasId: 1, siswaId: 1, "Siswa Satu", Tanggal, StatusAbsensiSiswa.Sakit, "Demam", atasPermintaan: null);

        using var command = database.Connection.CreateCommand();
        command.CommandText = "SELECT status, keterangan, sumber FROM absensi_siswa WHERE siswa_id = 1;";
        using var reader = command.ExecuteReader();
        Assert.True(reader.Read());
        Assert.Equal("sakit", reader.GetString(0));
        Assert.Equal("Demam", reader.GetString(1));
        Assert.Equal("koreksi", reader.GetString(2));
    }

    [Fact]
    public void KoreksiStatusSiswa_WaliKelasLain_Ditolak()
    {
        using var database = new MigratedDatabase();
        database.SeedSekolahUntukKoreksi();
        var layanan = new LayananKoreksi();

        Assert.Throws<InvalidOperationException>(() =>
            layanan.KoreksiStatusSiswa(database.Connection, WaliB, Waktu, kelasId: 1, siswaId: 1, "Siswa Satu", Tanggal, StatusAbsensiSiswa.Sakit, "x", null));
    }

    [Fact]
    public void KoreksiStatusSiswa_AdminAtasPermintaanWaliKelas_TercatatDiAudit()
    {
        using var database = new MigratedDatabase();
        database.SeedSekolahUntukKoreksi();
        var layanan = new LayananKoreksi();

        layanan.KoreksiStatusSiswa(database.Connection, Admin, Waktu, kelasId: 1, siswaId: 1, "Siswa Satu", Tanggal, StatusAbsensiSiswa.Izin, "Acara keluarga", atasPermintaan: "Wali A");

        using var command = database.Connection.CreateCommand();
        command.CommandText = "SELECT nilai_lama, nilai_baru, atas_permintaan FROM audit WHERE aksi = 'koreksi_status_siswa';";
        using var reader = command.ExecuteReader();
        Assert.True(reader.Read());
        Assert.Equal("terlambat", reader.GetString(0));
        Assert.Equal("izin", reader.GetString(1));
        Assert.Equal("Wali A", reader.GetString(2));
    }

    [Fact]
    public void KoreksiStatusSiswa_BulanTerkunci_Ditolak()
    {
        using var database = new MigratedDatabase();
        database.SeedSekolahUntukKoreksi();
        new LayananKuncimBulan().KunciBulan(database.Connection, Admin, Waktu, Tanggal.Year, Tanggal.Month);
        var layanan = new LayananKoreksi();

        Assert.Throws<InvalidOperationException>(() =>
            layanan.KoreksiStatusSiswa(database.Connection, Admin, Waktu, kelasId: 1, siswaId: 1, "Siswa Satu", Tanggal, StatusAbsensiSiswa.Sakit, "x", null));

        using var command = database.Connection.CreateCommand();
        command.CommandText = "SELECT status FROM absensi_siswa WHERE siswa_id = 1;";
        Assert.Equal("terlambat", command.ExecuteScalar());
    }

    // PRD §16.9 langkah 3 / §16.10 langkah 7.2: belum pernah dinotifikasi
    // → notifikasi normal dikirim.
    [Fact]
    public void KoreksiStatusSiswa_BelumPernahDinotifikasi_MengantreNotifikasiNormal()
    {
        using var database = new MigratedDatabase();
        database.SeedSekolahUntukKoreksi();
        var layanan = new LayananKoreksi();

        layanan.KoreksiStatusSiswa(database.Connection, WaliA, Waktu, kelasId: 1, siswaId: 1, "Siswa Satu", Tanggal, StatusAbsensiSiswa.Hadir, "Hadir, kartu tertinggal", null);

        using var command = database.Connection.CreateCommand();
        command.CommandText = "SELECT jenis, isi FROM outbox WHERE chat_id = 'chat-siswa-1';";
        using var reader = command.ExecuteReader();
        Assert.True(reader.Read());
        Assert.Equal("hadir", reader.GetString(0));
    }

    // PRD §16.10 langkah 7.3: sudah pernah dinotifikasi → PESAN RALAT.
    [Fact]
    public void KoreksiStatusSiswa_SudahPernahDinotifikasi_MengantrePesanRalat()
    {
        using var database = new MigratedDatabase();
        database.SeedSekolahUntukKoreksi();
        database.Execute("""
            INSERT INTO outbox (tanggal, subjek_tipe, subjek_id, jenis, chat_id, isi, status, dibuat_pada)
            VALUES ('2026-09-17', 'siswa', 1, 'terlambat', 'chat-siswa-1', 'isi lama', 'terkirim', '2026-09-17T08:00:00');
            """);
        var layanan = new LayananKoreksi();

        layanan.KoreksiStatusSiswa(database.Connection, WaliA, Waktu, kelasId: 1, siswaId: 1, "Siswa Satu", Tanggal, StatusAbsensiSiswa.Izin, "Sakit lanjutan", null);

        using var command = database.Connection.CreateCommand();
        command.CommandText = "SELECT isi FROM outbox WHERE chat_id = 'chat-siswa-1' AND jenis = 'ralat';";
        var isi = command.ExecuteScalar() as string;
        Assert.NotNull(isi);
        Assert.Contains("IZIN", isi);
        Assert.Contains("Koreksi", isi);
    }

    [Fact]
    public void KoreksiStatusSiswa_StatusTidakBerubah_TidakMengantreApaPun()
    {
        using var database = new MigratedDatabase();
        database.SeedSekolahUntukKoreksi();
        var layanan = new LayananKoreksi();

        layanan.KoreksiStatusSiswa(database.Connection, WaliA, Waktu, kelasId: 1, siswaId: 1, "Siswa Satu", Tanggal, StatusAbsensiSiswa.Terlambat, "tetap terlambat", null);

        using var command = database.Connection.CreateCommand();
        command.CommandText = "SELECT COUNT(*) FROM outbox;";
        Assert.Equal(0, Convert.ToInt32(command.ExecuteScalar()));
    }

    [Fact]
    public void KoreksiStatusSiswa_SiswaBelumTertautTelegram_StatusTetapBerubahTanpaOutbox()
    {
        using var database = new MigratedDatabase();
        database.SeedSekolahUntukKoreksi();
        var layanan = new LayananKoreksi();

        layanan.KoreksiStatusSiswa(database.Connection, WaliB, Waktu, kelasId: 2, siswaId: 2, "Siswa Dua", Tanggal, StatusAbsensiSiswa.Sakit, "Demam", null);

        using var command = database.Connection.CreateCommand();
        command.CommandText = "SELECT status FROM absensi_siswa WHERE siswa_id = 2;";
        Assert.Equal("sakit", command.ExecuteScalar());

        using var commandOutbox = database.Connection.CreateCommand();
        commandOutbox.CommandText = "SELECT COUNT(*) FROM outbox;";
        Assert.Equal(0, Convert.ToInt32(commandOutbox.ExecuteScalar()));
    }

    // PRD §10: alasan koreksi wajib; ditolak sebelum data berubah.
    [Theory]
    [InlineData(null)]
    [InlineData("   ")]
    public void KoreksiStatusSiswa_TanpaAlasan_DitolakDanStatusTidakBerubah(string? alasan)
    {
        using var database = new MigratedDatabase();
        database.SeedSekolahUntukKoreksi();
        var layanan = new LayananKoreksi();

        var galat = Assert.Throws<InvalidOperationException>(() =>
            layanan.KoreksiStatusSiswa(database.Connection, WaliA, Waktu, kelasId: 1, siswaId: 1, "Siswa Satu", Tanggal, StatusAbsensiSiswa.Hadir, alasan, null));

        Assert.Contains("Alasan", galat.Message);
        Assert.Equal(StatusAbsensiSiswa.Terlambat, new RepositoriAbsensi().CariHariIniSiswa(database.Connection, Tanggal, 1)!.Status);
    }
}
