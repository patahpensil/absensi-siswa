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
}
