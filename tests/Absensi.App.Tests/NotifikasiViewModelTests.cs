using Absensi.App.Notifikasi;
using Absensi.Data.Repositori;
using Absensi.Domain.Akun;

namespace Absensi.App.Tests;

// RENCANA-TUGAS.md Tugas 10 — layar Notifikasi (pemantau pairing, cetak
// massal QR).
public sealed class NotifikasiViewModelTests
{
    private static readonly DateTime Waktu = new(2026, 9, 17, 8, 0, 0);
    private static readonly AkunTersimpan Admin = new(1, "admin1", "x", PeranAkun.Admin, null, true);
    private static readonly AkunTersimpan WaliKelas = new(2, "wali_a", "x", PeranAkun.WaliKelas, 1, true);

    private static void SeedDasar(MigratedDatabase database)
    {
        database.Execute("""
            INSERT INTO tahun_ajaran (id, nama, semester, tgl_mulai, tgl_selesai, aktif)
            VALUES (1, '2026/2027', 'ganjil', '2026-07-01', '2026-12-19', 1);

            INSERT INTO guru (id, nip, nama, aktif) VALUES (1, '111', 'Wali A', 1);
            INSERT INTO kelas (id, tahun_ajaran_id, nama, tingkat, wali_guru_id) VALUES (1, 1, '7A', 7, 1);

            INSERT INTO siswa (id, nisn, nama, jenis_kelamin, aktif) VALUES (1, '0001', 'Siswa Tertaut', 'L', 1);
            INSERT INTO siswa (id, nisn, nama, jenis_kelamin, aktif) VALUES (2, '0002', 'Siswa Belum', 'L', 1);
            INSERT INTO siswa_kelas (siswa_id, kelas_id, tahun_ajaran_id) VALUES (1, 1, 1);
            INSERT INTO siswa_kelas (siswa_id, kelas_id, tahun_ajaran_id) VALUES (2, 1, 1);

            INSERT INTO pairing (subjek_tipe, subjek_id, token, status, chat_id, dibuat_pada, tertaut_pada)
            VALUES ('siswa', 1, 'TOK1', 'tertaut', 'chat-1', '2026-09-01T00:00:00', '2026-09-01T00:00:00');
            """);
        database.TambahAkun(1, "admin1", "admin", guruId: null);
    }

    private static NotifikasiViewModel BuatViewModel(MigratedDatabase database, AkunTersimpan? akun = null) =>
        new(database.Connection, akun ?? Admin, new PenyediaWaktuUji(Waktu), tahunAjaranId: 1);

    [Fact]
    public void Konstruksi_TokenBotBelumDiatur_BotTerkonfigurasiFalse()
    {
        using var database = new MigratedDatabase();
        SeedDasar(database);
        var viewModel = BuatViewModel(database);

        Assert.False(viewModel.BotTerkonfigurasi);
        Assert.True(viewModel.BotBelumDikonfigurasi);
    }

    [Fact]
    public void KelasTerpilih_MenyaringSiswaDanMenghitungRingkasanPairing()
    {
        using var database = new MigratedDatabase();
        SeedDasar(database);
        var viewModel = BuatViewModel(database);

        viewModel.KelasTerpilih = viewModel.DaftarKelas.Single(k => k.Nama == "7A");

        Assert.Equal(2, viewModel.SiswaTampil.Count);
        Assert.Equal("Kelas 7A: 1 dari 2 tertaut Telegram", viewModel.RingkasanPairingKelas);
    }

    [Fact]
    public void LihatQrSiswaCommand_UsernameBotBelumDiatur_TidakBisaDieksekusi()
    {
        using var database = new MigratedDatabase();
        SeedDasar(database);
        var viewModel = BuatViewModel(database);

        Assert.False(viewModel.LihatQrSiswaCommand.CanExecute(new SiswaDaftar(2, "0002", "Siswa Belum", "L", true, null, "7A", false, false, true)));
    }

    [Fact]
    public void LihatQrGuruCommand_WaliKelas_TidakBisaDieksekusiWalauUsernameAda()
    {
        using var database = new MigratedDatabase();
        SeedDasar(database);
        database.Execute("""
            INSERT INTO pengaturan (kunci, nilai) VALUES ('username_bot', 'BotSekolahX');
            """);
        var viewModel = BuatViewModel(database, WaliKelas);

        Assert.False(viewModel.LihatQrGuruCommand.CanExecute(new GuruDaftar(1, "111", "Wali A", true, null, false, false)));
    }

    [Fact]
    public void CetakMassalCommand_SemuaSiswaSudahTertaut_TidakMenghasilkanApaPunTapiPesanMuncul()
    {
        using var database = new MigratedDatabase();
        SeedDasar(database);
        database.Execute("""
            INSERT INTO pengaturan (kunci, nilai) VALUES ('username_bot', 'BotSekolahX');
            INSERT INTO pairing (subjek_tipe, subjek_id, token, status, chat_id, dibuat_pada, tertaut_pada)
            VALUES ('siswa', 2, 'TOK2', 'tertaut', 'chat-2', '2026-09-01T00:00:00', '2026-09-01T00:00:00');
            """);
        var viewModel = BuatViewModel(database);
        viewModel.KelasTerpilih = viewModel.DaftarKelas.Single(k => k.Nama == "7A");
        var terpicu = false;
        viewModel.CetakMassalSiap += (_, _) => terpicu = true;

        viewModel.CetakMassalCommand.Execute(null);

        Assert.False(terpicu);
        Assert.Equal("Seluruh siswa di kelas ini sudah tertaut Telegram.", viewModel.PesanStatus);
    }

    [Fact]
    public void CetakMassalCommand_AdaYangBelumTertaut_MembuatTokenDanMemicuCetak()
    {
        using var database = new MigratedDatabase();
        SeedDasar(database);
        database.Execute("INSERT INTO pengaturan (kunci, nilai) VALUES ('username_bot', 'BotSekolahX');");
        var viewModel = BuatViewModel(database);
        viewModel.KelasTerpilih = viewModel.DaftarKelas.Single(k => k.Nama == "7A");
        IReadOnlyList<QrTampil>? hasil = null;
        viewModel.CetakMassalSiap += (_, daftar) => hasil = daftar;

        viewModel.CetakMassalCommand.Execute(null);

        Assert.NotNull(hasil);
        var satuSatunya = Assert.Single(hasil!);
        Assert.Equal("Siswa Belum", satuSatunya.Nama);
        Assert.Contains("BotSekolahX", satuSatunya.Url);
    }
}
