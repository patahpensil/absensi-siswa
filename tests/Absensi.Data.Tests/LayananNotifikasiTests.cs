using Absensi.Data.Notifikasi;
using Absensi.Data.Repositori;

namespace Absensi.Data.Tests;

// RENCANA-TUGAS.md Tugas 10 — seluruh titik pengiriman batch PRD §9.2/§9.3.
public sealed class LayananNotifikasiTests
{
    private static readonly DateOnly Tanggal = new(2026, 9, 17);
    private static readonly DateTime Waktu = Tanggal.ToDateTime(new TimeOnly(8, 0));

    [Fact]
    public void JalankanRekapPagi_HadirDanTerlambat_KeduanyaDiantreKeOrangTuaMasingMasing()
    {
        using var database = new MigratedDatabase();
        database.SeedSekolahUntukNotifikasi();
        var layanan = new LayananNotifikasi();

        layanan.JalankanRekapPagi(database.Connection, Tanggal, Waktu);

        AssertOutboxTerantre(database, "chat-siswa-1", "hadir");
        AssertOutboxTerantre(database, "chat-siswa-2", "terlambat");
    }

    [Fact]
    public void JalankanRekapPagi_SiswaAlpa_TidakDapatPesanPribadiTapiWaliDapatDaftarBelumTap()
    {
        using var database = new MigratedDatabase();
        database.SeedSekolahUntukNotifikasi();
        var layanan = new LayananNotifikasi();

        layanan.JalankanRekapPagi(database.Connection, Tanggal, Waktu);

        AssertTidakAdaOutboxUntuk(database, "chat-siswa-3");
        var isi = AssertOutboxTerantre(database, "chat-wali-1", "rekap_wali_belum_tap");
        Assert.Contains("Siswa Alpa", isi);
    }

    [Fact]
    public void JalankanRekapPagi_DipanggilDuaKali_TidakMenggandakanPesan()
    {
        using var database = new MigratedDatabase();
        database.SeedSekolahUntukNotifikasi();
        var layanan = new LayananNotifikasi();

        layanan.JalankanRekapPagi(database.Connection, Tanggal, Waktu);
        layanan.JalankanRekapPagi(database.Connection, Tanggal, Waktu);

        using var command = database.Connection.CreateCommand();
        command.CommandText = "SELECT COUNT(*) FROM outbox WHERE chat_id = 'chat-siswa-1';";
        Assert.Equal(1, Convert.ToInt32(command.ExecuteScalar()));
    }

    [Fact]
    public void JalankanBatasAkhirTapMasuk_SiswaAlpa_DiantreKeOrangTua()
    {
        using var database = new MigratedDatabase();
        database.SeedSekolahUntukNotifikasi();
        var layanan = new LayananNotifikasi();

        layanan.JalankanBatasAkhirTapMasuk(database.Connection, Tanggal, Waktu);

        AssertOutboxTerantre(database, "chat-siswa-3", "alpa");
    }

    [Fact]
    public void JalankanBatasAkhirTapMasuk_GuruAlpa_DapatPengingatSendiriGuruSudahTapTidak()
    {
        using var database = new MigratedDatabase();
        database.SeedSekolahUntukNotifikasi();
        var layanan = new LayananNotifikasi();

        layanan.JalankanBatasAkhirTapMasuk(database.Connection, Tanggal, Waktu);

        AssertOutboxTerantre(database, "chat-guru-2", "guru_tanpa_kabar");
        AssertTidakAdaOutboxUntuk(database, "chat-wali-1");
    }

    [Fact]
    public void JalankanFinalisasiBolos_SiswaBelumTapPulang_JadiBolosDanWaliDapatDaftar()
    {
        using var database = new MigratedDatabase();
        database.SeedSekolahUntukNotifikasi();
        var layanan = new LayananNotifikasi();

        layanan.JalankanFinalisasiBolos(database.Connection, Tanggal, Waktu);

        using var command = database.Connection.CreateCommand();
        command.CommandText = "SELECT status FROM absensi_siswa WHERE siswa_id = 5;";
        Assert.Equal("bolos", command.ExecuteScalar());

        using var commandLain = database.Connection.CreateCommand();
        commandLain.CommandText = "SELECT status FROM absensi_siswa WHERE siswa_id = 1;";
        Assert.Equal("hadir", commandLain.ExecuteScalar());

        var isi = AssertOutboxTerantre(database, "chat-wali-1", "bolos_wali");
        Assert.Contains("Siswa Bolos", isi);
    }

    private static string AssertOutboxTerantre(MigratedDatabase database, string chatId, string jenis)
    {
        using var command = database.Connection.CreateCommand();
        command.CommandText = "SELECT isi FROM outbox WHERE chat_id = $chatId AND jenis = $jenis AND status = 'antre';";
        command.Parameters.AddWithValue("$chatId", chatId);
        command.Parameters.AddWithValue("$jenis", jenis);
        var isi = command.ExecuteScalar() as string;
        Assert.NotNull(isi);
        return isi!;
    }

    private static void AssertTidakAdaOutboxUntuk(MigratedDatabase database, string chatId)
    {
        using var command = database.Connection.CreateCommand();
        command.CommandText = "SELECT COUNT(*) FROM outbox WHERE chat_id = $chatId;";
        command.Parameters.AddWithValue("$chatId", chatId);
        Assert.Equal(0, Convert.ToInt32(command.ExecuteScalar()));
    }
}
