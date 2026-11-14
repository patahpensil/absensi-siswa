using Absensi.Data.Akun;
using Absensi.Data.Repositori;
using Absensi.Domain.Akun;

namespace Absensi.Data.Tests;

public sealed class LayananAkunTests
{
    private static readonly DateTime Waktu = new(2026, 9, 17, 8, 0, 0);

    private readonly LayananAkun _layananAkun = new();

    [Fact]
    public void Autentikasi_SandiBenar_MengembalikanAkun()
    {
        using var database = new MigratedDatabase();
        database.SeedAkunUntukUji();

        var akun = _layananAkun.Autentikasi(database.Connection, "kepsek", MigratedDatabase.SandiKepsek);

        Assert.NotNull(akun);
        Assert.Equal(PeranAkun.KepalaSekolah, akun!.Peran);
    }

    [Fact]
    public void Autentikasi_SandiSalah_MengembalikanNull()
    {
        using var database = new MigratedDatabase();
        database.SeedAkunUntukUji();

        var akun = _layananAkun.Autentikasi(database.Connection, "kepsek", "salah");

        Assert.Null(akun);
    }

    [Fact]
    public void Autentikasi_UsernameTidakAda_MengembalikanNull()
    {
        using var database = new MigratedDatabase();
        database.SeedAkunUntukUji();

        var akun = _layananAkun.Autentikasi(database.Connection, "tidak_ada", "apa saja");

        Assert.Null(akun);
    }

    [Fact]
    public void Autentikasi_AkunNonaktif_MengembalikanNull()
    {
        using var database = new MigratedDatabase();
        database.SeedAkunUntukUji();

        var akun = _layananAkun.Autentikasi(database.Connection, "wali_b_nonaktif", "apa saja");

        Assert.Null(akun);
    }

    [Fact]
    public void BuatAkun_OlehKepsek_BerhasilDanTercatatDiAudit()
    {
        using var database = new MigratedDatabase();
        database.SeedAkunUntukUji();
        var kepsek = _layananAkun.Autentikasi(database.Connection, "kepsek", MigratedDatabase.SandiKepsek)!;

        var akunBaru = _layananAkun.BuatAkun(
            database.Connection, kepsek, Waktu, "wali_c", "SandiWaliC1!", PeranAkun.WaliKelas, guruId: 2);

        Assert.Equal("wali_c", akunBaru.Username);
        Assert.True(HashSandi.Verifikasi("SandiWaliC1!", akunBaru.SandiHash));

        using var command = database.Connection.CreateCommand();
        command.CommandText = "SELECT akun_id, aksi, entitas, entitas_id, nilai_baru FROM audit;";
        using var reader = command.ExecuteReader();
        Assert.True(reader.Read());
        Assert.Equal(kepsek.Id, reader.GetInt32(0));
        Assert.Equal("buat_akun", reader.GetString(1));
        Assert.Equal("akun", reader.GetString(2));
        Assert.Equal(akunBaru.Id, reader.GetInt32(3));
        Assert.Equal("wali_c", reader.GetString(4));
        Assert.False(reader.Read());
    }

    [Fact]
    public void BuatAkun_OlehWaliKelas_Ditolak()
    {
        using var database = new MigratedDatabase();
        database.SeedAkunUntukUji();
        var waliA = new AkunTersimpan(2, "wali_a", "tidak-terpakai", PeranAkun.WaliKelas, GuruId: 1, Aktif: true);

        Assert.Throws<InvalidOperationException>(() => _layananAkun.BuatAkun(
            database.Connection, waliA, Waktu, "siapapun", "Sandi123!", PeranAkun.WaliKelas, guruId: 2));

        using var command = database.Connection.CreateCommand();
        command.CommandText = "SELECT COUNT(*) FROM audit;";
        Assert.Equal(0, Convert.ToInt32(command.ExecuteScalar()));
    }

    // RENCANA-TUGAS.md Tugas 14 — PRD §3.1: "Kepala Sekolah → akun Admin",
    // "Admin → akun Wali Kelas". Akun Kepala Sekolah sendiri (Tugas 4 pola
    // lama) tidak pernah lewat BuatAkun — hanya lewat wizard.
    [Fact]
    public void BuatAkun_KepsekBuatAdmin_Berhasil()
    {
        using var database = new MigratedDatabase();
        database.SeedAkunUntukUji();
        var kepsek = _layananAkun.Autentikasi(database.Connection, "kepsek", MigratedDatabase.SandiKepsek)!;

        var admin = _layananAkun.BuatAkun(database.Connection, kepsek, Waktu, "admin_baru", "SandiAdmin1!", PeranAkun.Admin, guruId: null);

        Assert.Equal(PeranAkun.Admin, admin.Peran);
    }

    [Fact]
    public void BuatAkun_PeranKepalaSekolah_SelaluDitolak()
    {
        using var database = new MigratedDatabase();
        database.SeedAkunUntukUji();
        var kepsek = _layananAkun.Autentikasi(database.Connection, "kepsek", MigratedDatabase.SandiKepsek)!;

        Assert.Throws<InvalidOperationException>(() => _layananAkun.BuatAkun(
            database.Connection, kepsek, Waktu, "kepsek_lain", "Sandi123!", PeranAkun.KepalaSekolah, guruId: null));
    }

    // Temuan Tugas 14: dulu tidak ada UI apa pun untuk ini, sekarang dibatasi
    // di lapisan layanan — Admin BUKAN Kepala Sekolah, jadi tidak berwenang
    // membuat Admin lain (PRD §3.1).
    [Fact]
    public void BuatAkun_AdminBuatAdminLain_Ditolak()
    {
        using var database = new MigratedDatabase();
        database.SeedMasterDataUntukUji();
        var admin = _layananAkun.Autentikasi(database.Connection, "admin1", MigratedDatabase.SandiKepsek)!;

        var pengecualian = Assert.Throws<InvalidOperationException>(() => _layananAkun.BuatAkun(
            database.Connection, admin, Waktu, "admin_lain", "Sandi123!", PeranAkun.Admin, guruId: null));
        Assert.Contains("Kepala Sekolah", pengecualian.Message);
    }

    [Fact]
    public void BuatAkun_AdminBuatWaliKelas_Berhasil()
    {
        using var database = new MigratedDatabase();
        database.SeedMasterDataUntukUji();
        database.Execute("INSERT INTO guru (id, nip, nama) VALUES (1, '111', 'Guru Satu');");
        var admin = _layananAkun.Autentikasi(database.Connection, "admin1", MigratedDatabase.SandiKepsek)!;

        var waliKelas = _layananAkun.BuatAkun(database.Connection, admin, Waktu, "wali_baru", "SandiWali1!", PeranAkun.WaliKelas, guruId: 1);

        Assert.Equal(PeranAkun.WaliKelas, waliKelas.Peran);
        Assert.Equal(1, waliKelas.GuruId);
    }

    [Fact]
    public void BuatAkun_WaliKelasTanpaGuruId_Ditolak()
    {
        using var database = new MigratedDatabase();
        database.SeedAkunUntukUji();
        var kepsek = _layananAkun.Autentikasi(database.Connection, "kepsek", MigratedDatabase.SandiKepsek)!;

        var pengecualian = Assert.Throws<InvalidOperationException>(() => _layananAkun.BuatAkun(
            database.Connection, kepsek, Waktu, "wali_tanpa_guru", "Sandi123!", PeranAkun.WaliKelas, guruId: null));
        Assert.Contains("guru", pengecualian.Message);
    }

    [Fact]
    public void DaftarAkun_OlehWaliKelas_Ditolak()
    {
        using var database = new MigratedDatabase();
        database.SeedAkunUntukUji();
        var waliA = new AkunTersimpan(2, "wali_a", "x", PeranAkun.WaliKelas, GuruId: 1, Aktif: true);

        Assert.Throws<InvalidOperationException>(() => _layananAkun.DaftarAkun(database.Connection, waliA));
    }

    [Fact]
    public void DaftarAkun_OlehAdmin_MengembalikanSemuaAkunBesertaNamaGuru()
    {
        using var database = new MigratedDatabase();
        database.SeedAkunUntukUji();
        var kepsek = _layananAkun.Autentikasi(database.Connection, "kepsek", MigratedDatabase.SandiKepsek)!;

        var daftar = _layananAkun.DaftarAkun(database.Connection, kepsek);

        Assert.Contains(daftar, a => a.Username == "kepsek" && a.NamaGuru == null);
        Assert.Contains(daftar, a => a.Username == "wali_a" && a.NamaGuru == "Wali Kelas Satu");
        Assert.Contains(daftar, a => a.Username == "wali_b_nonaktif" && !a.Aktif);
    }

    [Fact]
    public void NonaktifkanAkun_OlehWaliKelas_Ditolak()
    {
        using var database = new MigratedDatabase();
        database.SeedAkunUntukUji();
        var waliA = new AkunTersimpan(2, "wali_a", "x", PeranAkun.WaliKelas, GuruId: 1, Aktif: true);

        Assert.Throws<InvalidOperationException>(() => _layananAkun.NonaktifkanAkun(database.Connection, waliA, Waktu, akunIdTarget: 3));
    }

    [Fact]
    public void NonaktifkanAkun_OlehAdmin_MenandaiTidakAktifBukanMenghapus()
    {
        using var database = new MigratedDatabase();
        database.SeedAkunUntukUji();
        var kepsek = _layananAkun.Autentikasi(database.Connection, "kepsek", MigratedDatabase.SandiKepsek)!;

        _layananAkun.NonaktifkanAkun(database.Connection, kepsek, Waktu, akunIdTarget: 2);

        using var command = database.Connection.CreateCommand();
        command.CommandText = "SELECT aktif FROM akun WHERE id = 2;";
        Assert.Equal(0L, command.ExecuteScalar());

        using var commandAudit = database.Connection.CreateCommand();
        commandAudit.CommandText = "SELECT aksi FROM audit WHERE entitas_id = 2;";
        Assert.Equal("nonaktifkan_akun", commandAudit.ExecuteScalar());
    }

    [Fact]
    public void ResetSandi_OlehWaliKelas_Ditolak()
    {
        using var database = new MigratedDatabase();
        database.SeedAkunUntukUji();
        var waliA = new AkunTersimpan(2, "wali_a", "x", PeranAkun.WaliKelas, GuruId: 1, Aktif: true);

        Assert.Throws<InvalidOperationException>(() => _layananAkun.ResetSandi(database.Connection, waliA, Waktu, akunIdTarget: 3, "SandiBaru1!"));
    }

    [Fact]
    public void ResetSandi_OlehAdmin_SandiBaruBisaDipakaiLogin()
    {
        using var database = new MigratedDatabase();
        database.SeedAkunUntukUji();
        var kepsek = _layananAkun.Autentikasi(database.Connection, "kepsek", MigratedDatabase.SandiKepsek)!;

        _layananAkun.ResetSandi(database.Connection, kepsek, Waktu, akunIdTarget: 2, "SandiBaruWaliA1!");

        Assert.Null(_layananAkun.Autentikasi(database.Connection, "wali_a", MigratedDatabase.SandiKepsek));
        Assert.NotNull(_layananAkun.Autentikasi(database.Connection, "wali_a", "SandiBaruWaliA1!"));
    }

    [Fact]
    public void BolehMenyentuhKelas_KepalaSekolah_SelaluBoleh()
    {
        using var database = new MigratedDatabase();
        database.SeedAkunUntukUji();
        var kepsek = new AkunTersimpan(1, "kepsek", "x", PeranAkun.KepalaSekolah, GuruId: null, Aktif: true);

        Assert.True(_layananAkun.BolehMenyentuhKelas(database.Connection, kepsek, kelasId: 2));
    }

    [Fact]
    public void BolehMenyentuhKelas_WaliKelasSendiri_Boleh()
    {
        using var database = new MigratedDatabase();
        database.SeedAkunUntukUji();
        var waliA = new AkunTersimpan(2, "wali_a", "x", PeranAkun.WaliKelas, GuruId: 1, Aktif: true);

        Assert.True(_layananAkun.BolehMenyentuhKelas(database.Connection, waliA, kelasId: 1));
    }

    [Fact]
    public void BolehMenyentuhKelas_WaliKelasLain_Ditolak()
    {
        using var database = new MigratedDatabase();
        database.SeedAkunUntukUji();
        var waliA = new AkunTersimpan(2, "wali_a", "x", PeranAkun.WaliKelas, GuruId: 1, Aktif: true);

        Assert.False(_layananAkun.BolehMenyentuhKelas(database.Connection, waliA, kelasId: 2));
    }

    [Fact]
    public void BolehMenyentuhKelas_KelasTanpaWaliDitugaskan_WaliKelasDitolak()
    {
        using var database = new MigratedDatabase();
        database.SeedAkunUntukUji();
        var waliA = new AkunTersimpan(2, "wali_a", "x", PeranAkun.WaliKelas, GuruId: 1, Aktif: true);

        Assert.False(_layananAkun.BolehMenyentuhKelas(database.Connection, waliA, kelasId: 3));
    }

    // ---- Hierarki wewenang atas akun TARGET (PRD §3.1) ----

    private static AkunTersimpan BuatAdmin(MigratedDatabase database)
    {
        database.Execute("""
            INSERT INTO akun (id, username, sandi_hash, peran, guru_id, aktif, dibuat_pada)
            VALUES (9, 'admin_tu', 'tidak-terpakai', 'admin', NULL, 1, '2026-09-01T00:00:00');
            """);
        return new AkunTersimpan(9, "admin_tu", "x", PeranAkun.Admin, GuruId: null, Aktif: true);
    }

    [Fact]
    // P-21 (revisi 14 November 2026): Admin tidak dapat mengatur ulang sandi Kepala Sekolah.
    public void ResetSandi_AdminKeKepalaSekolah_Ditolak()
    {
        using var database = new MigratedDatabase();
        database.SeedAkunUntukUji();
        var admin = BuatAdmin(database);

        Assert.Throws<InvalidOperationException>(() => _layananAkun.ResetSandi(database.Connection, admin, Waktu, akunIdTarget: 1, "SandiBaru1!"));
        Assert.NotNull(_layananAkun.Autentikasi(database.Connection, "kepsek", MigratedDatabase.SandiKepsek));
    }

    [Fact]
    public void ResetSandi_KepalaSekolahKeAdmin_LangsungBerlakuDanTercatatDiAudit()
    {
        using var database = new MigratedDatabase();
        database.SeedAkunUntukUji();
        BuatAdmin(database);
        var kepsek = _layananAkun.Autentikasi(database.Connection, "kepsek", MigratedDatabase.SandiKepsek)!;

        _layananAkun.ResetSandi(database.Connection, kepsek, Waktu, 9, "SandiAdminBaru1!");

        Assert.NotNull(_layananAkun.Autentikasi(database.Connection, "admin_tu", "SandiAdminBaru1!"));
        using var command = database.Connection.CreateCommand();
        command.CommandText = "SELECT COUNT(*) FROM audit WHERE aksi = 'reset_sandi_akun' AND akun_id = 1 AND entitas_id = 9;";
        Assert.Equal(1L, command.ExecuteScalar());
    }

    [Fact]
    public void ResetSandi_AdminKeAdminLainAtauDiriSendiri_Ditolak()
    {
        using var database = new MigratedDatabase();
        database.SeedAkunUntukUji();
        var admin = BuatAdmin(database);
        var kepsek = _layananAkun.Autentikasi(database.Connection, "kepsek", MigratedDatabase.SandiKepsek)!;
        var otherAdmin = _layananAkun.BuatAkun(database.Connection, kepsek, Waktu, "admin2", "SandiAdmin2!", PeranAkun.Admin, null);

        Assert.Throws<InvalidOperationException>(() => _layananAkun.ResetSandi(database.Connection, admin, Waktu, otherAdmin.Id, "SandiBaru1!"));
        Assert.Throws<InvalidOperationException>(() => _layananAkun.ResetSandi(database.Connection, admin, Waktu, admin.Id, "SandiBaru1!"));
        var self = Assert.Throws<InvalidOperationException>(() => _layananAkun.ResetSandi(database.Connection, kepsek, Waktu, kepsek.Id, "SandiBaru1!"));
        // P-21 revised: there is no own-password menu, so the message must not point to one.
        Assert.Equal("Sandi akun sendiri tidak dapat diatur ulang dari akun ini.", self.Message);
    }

    [Fact]
    public void NonaktifkanAkun_AdminKeKepalaSekolah_Ditolak()
    {
        using var database = new MigratedDatabase();
        database.SeedAkunUntukUji();
        var admin = BuatAdmin(database);

        Assert.Throws<InvalidOperationException>(() => _layananAkun.NonaktifkanAkun(database.Connection, admin, Waktu, akunIdTarget: 1));
        Assert.NotNull(_layananAkun.Autentikasi(database.Connection, "kepsek", MigratedDatabase.SandiKepsek));
    }

    [Fact]
    public void NonaktifkanAkun_AdminKeDirinyaSendiri_Ditolak()
    {
        using var database = new MigratedDatabase();
        database.SeedAkunUntukUji();
        var admin = BuatAdmin(database);

        Assert.Throws<InvalidOperationException>(() => _layananAkun.NonaktifkanAkun(database.Connection, admin, Waktu, akunIdTarget: 9));
    }

    [Fact]
    public void NonaktifkanAkun_KepalaSekolahKeDirinyaSendiri_Ditolak()
    {
        using var database = new MigratedDatabase();
        database.SeedAkunUntukUji();
        var kepsek = _layananAkun.Autentikasi(database.Connection, "kepsek", MigratedDatabase.SandiKepsek)!;

        Assert.Throws<InvalidOperationException>(() => _layananAkun.NonaktifkanAkun(database.Connection, kepsek, Waktu, akunIdTarget: kepsek.Id));
    }

    [Fact]
    public void ResetSandi_AdminKeWaliKelas_Berhasil()
    {
        using var database = new MigratedDatabase();
        database.SeedAkunUntukUji();
        var admin = BuatAdmin(database);

        _layananAkun.ResetSandi(database.Connection, admin, Waktu, akunIdTarget: 2, "SandiBaruWaliA1!");

        Assert.NotNull(_layananAkun.Autentikasi(database.Connection, "wali_a", "SandiBaruWaliA1!"));
    }

    [Fact]
    public void ResetSandi_TargetTidakAda_Ditolak()
    {
        using var database = new MigratedDatabase();
        database.SeedAkunUntukUji();
        var kepsek = _layananAkun.Autentikasi(database.Connection, "kepsek", MigratedDatabase.SandiKepsek)!;

        Assert.Throws<InvalidOperationException>(() => _layananAkun.ResetSandi(database.Connection, kepsek, Waktu, akunIdTarget: 999, "SandiBaru1!"));
    }
}
