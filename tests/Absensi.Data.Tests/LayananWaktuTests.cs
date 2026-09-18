using Absensi.Data.Akun;
using Absensi.Data.Koreksi;
using Absensi.Data.Repositori;
using Absensi.Data.Waktu;
using Absensi.Domain.Akun;
using Absensi.Domain.Kehadiran;

namespace Absensi.Data.Tests;

// RENCANA-TUGAS.md Tugas 9 — Kalender dan Jadwal Harian (PRD §6, §7).
public sealed class LayananWaktuTests
{
    private static readonly DateTime Waktu = new(2026, 9, 18, 8, 0, 0);
    private static readonly AkunTersimpan Admin = new(1, "admin1", "x", PeranAkun.Admin, null, true);
    private static readonly AkunTersimpan WaliKelas = new(2, "wali_a", "x", PeranAkun.WaliKelas, 1, true);

    // PRD §3: "kalender pendidikan" dan "pengaturan" adalah kewenangan
    // penuh (Kepala Sekolah/Admin) — wali kelas tidak disebutkan berwenang.
    [Fact]
    public void TandaiRentang_WaliKelas_Ditolak()
    {
        using var database = new MigratedDatabase();
        database.SeedMasterDataUntukUji();
        var layanan = new LayananKalender();
        var tanggal = new DateOnly(2026, 9, 20);

        Assert.Throws<InvalidOperationException>(() =>
            layanan.TandaiRentang(database.Connection, WaliKelas, Waktu, tanggal, tanggal, JenisHariKalender.Libur, "x"));
        Assert.Empty(new RepositoriKalender().DaftarUntukBulan(database.Connection, 2026, 9));
    }

    [Fact]
    public void JadwalHari_Perbarui_WaliKelas_Ditolak()
    {
        using var database = new MigratedDatabase();
        database.SeedMasterDataUntukUji();
        var layanan = new LayananJadwalHari();

        Assert.Throws<InvalidOperationException>(() =>
            layanan.Perbarui(database.Connection, WaliKelas, Waktu, tahunAjaranId: 1, BuatJadwalStandar()));

        using var command = database.Connection.CreateCommand();
        command.CommandText = "SELECT COUNT(*) FROM jadwal_hari WHERE tahun_ajaran_id = 1;";
        Assert.Equal(0, Convert.ToInt32(command.ExecuteScalar()));
    }

    // ---------- LayananKalender ----------

    [Fact]
    public void TandaiRentang_SatuTanggalLibur_TersimpanDenganKeterangan()
    {
        using var database = new MigratedDatabase();
        database.SeedMasterDataUntukUji();
        var layanan = new LayananKalender();
        var tanggal = new DateOnly(2026, 9, 20);

        layanan.TandaiRentang(database.Connection, Admin, Waktu, tanggal, tanggal, JenisHariKalender.Libur, "Libur mendadak banjir");

        var entri = Assert.Single(new RepositoriKalender().DaftarUntukBulan(database.Connection, 2026, 9));
        Assert.Equal(tanggal, entri.Tanggal);
        Assert.Equal(JenisHariKalender.Libur, entri.Jenis);
        Assert.Equal("Libur mendadak banjir", entri.Keterangan);
        AssertAdaBarisAudit(database, "tandai_libur", jumlahDiharapkan: 1);
    }

    [Fact]
    public void TandaiRentang_RentangTigaHari_KetigaTanggalTersimpanTerpisah()
    {
        using var database = new MigratedDatabase();
        database.SeedMasterDataUntukUji();
        var layanan = new LayananKalender();

        layanan.TandaiRentang(
            database.Connection, Admin, Waktu,
            new DateOnly(2026, 12, 22), new DateOnly(2026, 12, 24),
            JenisHariKalender.Libur, "Libur semester ganjil");

        var daftar = new RepositoriKalender().DaftarUntukBulan(database.Connection, 2026, 12);
        Assert.Equal(3, daftar.Count);
        Assert.All(daftar, e => Assert.Equal(JenisHariKalender.Libur, e.Jenis));
    }

    [Fact]
    public void TandaiRentang_LiburTanpaKeterangan_Ditolak()
    {
        using var database = new MigratedDatabase();
        database.SeedMasterDataUntukUji();
        var layanan = new LayananKalender();
        var tanggal = new DateOnly(2026, 9, 20);

        Assert.Throws<ArgumentException>(() =>
            layanan.TandaiRentang(database.Connection, Admin, Waktu, tanggal, tanggal, JenisHariKalender.Libur, keterangan: null));

        Assert.Empty(new RepositoriKalender().DaftarUntukBulan(database.Connection, 2026, 9));
    }

    [Fact]
    public void TandaiRentang_TanggalAkhirSebelumAwal_Ditolak()
    {
        using var database = new MigratedDatabase();
        database.SeedMasterDataUntukUji();
        var layanan = new LayananKalender();

        Assert.Throws<ArgumentException>(() => layanan.TandaiRentang(
            database.Connection, Admin, Waktu, new DateOnly(2026, 9, 20), new DateOnly(2026, 9, 19), JenisHariKalender.Libur, "x"));
    }

    // PRD §6, kriteria selesai Tugas 9: menandai libur membatalkan seluruh
    // notifikasi antre pada tanggal itu.
    [Fact]
    public void TandaiRentang_Libur_MembatalkanOutboxAntreTapiBukanYangSudahTerkirim()
    {
        using var database = new MigratedDatabase();
        database.SeedMasterDataUntukUji();
        SeedOutbox(database, tanggal: "2026-09-20", status: "antre", jenis: "hadir");
        SeedOutbox(database, tanggal: "2026-09-20", status: "terkirim", jenis: "terlambat");
        SeedOutbox(database, tanggal: "2026-09-21", status: "antre", jenis: "hadir");
        var layanan = new LayananKalender();

        layanan.TandaiRentang(database.Connection, Admin, Waktu, new DateOnly(2026, 9, 20), new DateOnly(2026, 9, 20), JenisHariKalender.Libur, "Libur mendadak");

        Assert.Equal("dibatalkan", BacaStatusOutbox(database, "2026-09-20", "hadir"));
        Assert.Equal("terkirim", BacaStatusOutbox(database, "2026-09-20", "terlambat"));
        Assert.Equal("antre", BacaStatusOutbox(database, "2026-09-21", "hadir"));
    }

    [Fact]
    public void TandaiRentang_HariSekolah_TidakMembatalkanOutboxWalauAdaYangAntre()
    {
        using var database = new MigratedDatabase();
        database.SeedMasterDataUntukUji();
        SeedOutbox(database, tanggal: "2026-09-20", status: "antre", jenis: "hadir");
        var layanan = new LayananKalender();

        layanan.TandaiRentang(database.Connection, Admin, Waktu, new DateOnly(2026, 9, 20), new DateOnly(2026, 9, 20), JenisHariKalender.Sekolah, keterangan: null);

        Assert.Equal("antre", BacaStatusOutbox(database, "2026-09-20", "hadir"));
    }

    [Fact]
    public void TandaiRentang_TandaiUlangSebagaiSekolah_MenggantikanBarisLamaBukanMenambah()
    {
        using var database = new MigratedDatabase();
        database.SeedMasterDataUntukUji();
        var layanan = new LayananKalender();
        var tanggal = new DateOnly(2026, 7, 20);
        layanan.TandaiRentang(database.Connection, Admin, Waktu, tanggal, tanggal, JenisHariKalender.Libur, "Libur semester");

        layanan.TandaiRentang(database.Connection, Admin, Waktu, tanggal, tanggal, JenisHariKalender.Sekolah, keterangan: "MPLS");

        var entri = Assert.Single(new RepositoriKalender().DaftarUntukBulan(database.Connection, 2026, 7));
        Assert.Equal(JenisHariKalender.Sekolah, entri.Jenis);
        Assert.Equal("MPLS", entri.Keterangan);
    }

    // ---------- Kunci bulan (celah ditemukan saat tinjauan Tugas 11) ----------
    // PRD §10/§16.13: "bulan itu beku, tidak dapat diubah siapa pun" —
    // ditegakkan di lapisan layanan, bukan hanya UI (dikonfirmasi pemilik
    // proyek 2026-09-18).

    [Fact]
    public void TandaiRentang_BulanTerkunci_DitolakDenganPesanMenyebutBulanPenguncian()
    {
        using var database = new MigratedDatabase();
        database.SeedMasterDataUntukUji();
        new LayananKuncimBulan().KunciBulan(database.Connection, Admin, new DateTime(2026, 10, 3, 9, 0, 0), 2026, 9);
        var layanan = new LayananKalender();
        var tanggal = new DateOnly(2026, 9, 20);

        var galat = Assert.Throws<InvalidOperationException>(() =>
            layanan.TandaiRentang(database.Connection, Admin, Waktu, tanggal, tanggal, JenisHariKalender.Libur, "x"));

        Assert.Equal("Tidak bisa mengubah. Bulan September 2026 sudah dikunci pada 3 Oktober oleh admin1.", galat.Message);
        Assert.Empty(new RepositoriKalender().DaftarUntukBulan(database.Connection, 2026, 9));
    }

    [Fact]
    public void TandaiRentang_RentangMenyentuhBulanTerkunciDiUjung_Ditolak()
    {
        using var database = new MigratedDatabase();
        database.SeedMasterDataUntukUji();
        new LayananKuncimBulan().KunciBulan(database.Connection, Admin, new DateTime(2026, 8, 1, 9, 0, 0), 2026, 7);
        var layanan = new LayananKalender();

        Assert.Throws<InvalidOperationException>(() => layanan.TandaiRentang(
            database.Connection, Admin, Waktu, new DateOnly(2026, 7, 28), new DateOnly(2026, 8, 2), JenisHariKalender.Libur, "x"));
    }

    [Fact]
    public void TandaiRentang_BulanLainTerkunci_TidakTerpengaruh()
    {
        using var database = new MigratedDatabase();
        database.SeedMasterDataUntukUji();
        new LayananKuncimBulan().KunciBulan(database.Connection, Admin, new DateTime(2026, 8, 1, 9, 0, 0), 2026, 7);
        var layanan = new LayananKalender();
        var tanggal = new DateOnly(2026, 9, 20);

        layanan.TandaiRentang(database.Connection, Admin, Waktu, tanggal, tanggal, JenisHariKalender.Libur, "x");

        Assert.Single(new RepositoriKalender().DaftarUntukBulan(database.Connection, 2026, 9));
    }

    // jadwal_hari berdampak retroaktif ke seluruh bulan tahun ajaran —
    // satu bulan terkunci di mana pun dalam rentangnya cukup untuk menolak,
    // bukan cuma bulan berjalan.
    [Fact]
    public void JadwalHari_Perbarui_AdaBulanTerkunciDalamTahunAjaran_DitolakDenganPesanMenyebutBulanPenguncian()
    {
        using var database = new MigratedDatabase();
        database.SeedMasterDataUntukUji();
        new LayananKuncimBulan().KunciBulan(database.Connection, Admin, new DateTime(2026, 10, 3, 9, 0, 0), 2026, 9);
        var layanan = new LayananJadwalHari();

        var galat = Assert.Throws<InvalidOperationException>(() =>
            layanan.Perbarui(database.Connection, Admin, Waktu, tahunAjaranId: 1, BuatJadwalStandar()));

        Assert.Equal("Tidak bisa mengubah. Bulan September 2026 sudah dikunci pada 3 Oktober oleh admin1.", galat.Message);
        using var command = database.Connection.CreateCommand();
        command.CommandText = "SELECT COUNT(*) FROM jadwal_hari WHERE tahun_ajaran_id = 1;";
        Assert.Equal(0, Convert.ToInt32(command.ExecuteScalar()));
    }

    // ---------- LayananJadwalHari ----------

    [Fact]
    public void DaftarUntukTahunAjaran_BelumPernahDisimpan_TujuhBarisDenganDefaultMingguLibur()
    {
        using var database = new MigratedDatabase();
        database.SeedMasterDataUntukUji();

        var daftar = new RepositoriJadwalHari().DaftarUntukTahunAjaran(database.Connection, tahunAjaranId: 1);

        Assert.Equal(7, daftar.Count);
        Assert.True(daftar.Single(h => h.Hari == 1).HariSekolah);
        Assert.False(daftar.Single(h => h.Hari == 7).HariSekolah);
    }

    [Fact]
    public void Perbarui_TujuhBaris_TersimpanDanBisaDibacaUlang()
    {
        using var database = new MigratedDatabase();
        database.SeedMasterDataUntukUji();
        var layanan = new LayananJadwalHari();
        var hari = BuatJadwalStandar();

        layanan.Perbarui(database.Connection, Admin, Waktu, tahunAjaranId: 1, hari);

        var tersimpan = new RepositoriJadwalHari().DaftarUntukTahunAjaran(database.Connection, tahunAjaranId: 1);
        var jumat = tersimpan.Single(h => h.Hari == 5);
        Assert.Equal(new TimeOnly(10, 0), jumat.BatasTapMasuk);
        AssertAdaBarisAudit(database, "perbarui_jadwal_hari", jumlahDiharapkan: 1);
    }

    [Fact]
    public void Perbarui_DipanggilDuaKali_TidakMenggandakanBaris()
    {
        using var database = new MigratedDatabase();
        database.SeedMasterDataUntukUji();
        var layanan = new LayananJadwalHari();

        layanan.Perbarui(database.Connection, Admin, Waktu, tahunAjaranId: 1, BuatJadwalStandar());
        layanan.Perbarui(database.Connection, Admin, Waktu, tahunAjaranId: 1, BuatJadwalStandar());

        using var command = database.Connection.CreateCommand();
        command.CommandText = "SELECT COUNT(*) FROM jadwal_hari WHERE tahun_ajaran_id = 1;";
        Assert.Equal(7, Convert.ToInt32(command.ExecuteScalar()));
    }

    private static List<JadwalHariBaris> BuatJadwalStandar()
    {
        var hari = new List<JadwalHariBaris>();
        for (var h = 1; h <= 6; h++)
        {
            hari.Add(new JadwalHariBaris(
                h,
                HariSekolah: true,
                new TimeOnly(7, 30),
                new TimeOnly(8, 0),
                h == 5 ? new TimeOnly(10, 0) : new TimeOnly(12, 0),
                h == 5 ? new TimeOnly(11, 30) : new TimeOnly(13, 10)));
        }

        hari.Add(new JadwalHariBaris(7, HariSekolah: false, null, null, null, null));
        return hari;
    }

    private static void SeedOutbox(MigratedDatabase database, string tanggal, string status, string jenis)
    {
        using var command = database.Connection.CreateCommand();
        command.CommandText = """
            INSERT INTO outbox (tanggal, subjek_tipe, subjek_id, jenis, chat_id, isi, status, dibuat_pada)
            VALUES ($tanggal, 'siswa', 1, $jenis, 'chat-1', 'isi pesan', $status, $tanggal);
            """;
        command.Parameters.AddWithValue("$tanggal", tanggal);
        command.Parameters.AddWithValue("$jenis", jenis);
        command.Parameters.AddWithValue("$status", status);
        command.ExecuteNonQuery();
    }

    private static string BacaStatusOutbox(MigratedDatabase database, string tanggal, string jenis)
    {
        using var command = database.Connection.CreateCommand();
        command.CommandText = "SELECT status FROM outbox WHERE tanggal = $tanggal AND jenis = $jenis;";
        command.Parameters.AddWithValue("$tanggal", tanggal);
        command.Parameters.AddWithValue("$jenis", jenis);
        return (string)command.ExecuteScalar()!;
    }

    private static void AssertAdaBarisAudit(MigratedDatabase database, string aksi, int jumlahDiharapkan)
    {
        using var command = database.Connection.CreateCommand();
        command.CommandText = "SELECT COUNT(*) FROM audit WHERE aksi = $aksi;";
        command.Parameters.AddWithValue("$aksi", aksi);
        Assert.Equal(jumlahDiharapkan, Convert.ToInt32(command.ExecuteScalar()));
    }
}
