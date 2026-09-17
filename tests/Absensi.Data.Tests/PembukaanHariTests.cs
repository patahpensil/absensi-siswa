namespace Absensi.Data.Tests;

public sealed class PembukaanHariTests
{
    private static readonly DateOnly HariKamis = new(2026, 9, 17);
    private static readonly DateOnly HariMinggu = new(2026, 9, 20);

    private readonly PembukaanHari _pembukaanHari = new();

    [Fact]
    public void Jalankan_DuaKali_TidakMenggandakanBaris()
    {
        using var database = new MigratedDatabase();
        database.SeedSekolahUntukTap();

        _pembukaanHari.Jalankan(database.Connection, HariKamis);
        _pembukaanHari.Jalankan(database.Connection, HariKamis);

        Assert.Equal(1, HitungBaris(database, "absensi_siswa"));
        Assert.Equal(1, HitungBaris(database, "absensi_guru"));
    }

    [Fact]
    public void Jalankan_HariMingguHariSekolahNol_TidakMembuatBaris()
    {
        using var database = new MigratedDatabase();
        database.SeedSekolahUntukTap();

        _pembukaanHari.Jalankan(database.Connection, HariMinggu);

        Assert.Equal(0, HitungBaris(database, "absensi_siswa"));
        Assert.Equal(0, HitungBaris(database, "absensi_guru"));
    }

    [Fact]
    public void Jalankan_TanggalLiburDiKalender_TidakMembuatBarisWalauHariSekolah()
    {
        using var database = new MigratedDatabase();
        database.SeedSekolahUntukTap();
        database.Execute("""
            INSERT INTO kalender (tanggal, jenis, keterangan)
            VALUES ('2026-09-17', 'libur', 'Libur mendadak untuk pengujian');
            """);

        _pembukaanHari.Jalankan(database.Connection, HariKamis);

        Assert.Equal(0, HitungBaris(database, "absensi_siswa"));
        Assert.Equal(0, HitungBaris(database, "absensi_guru"));
    }

    [Fact]
    public void Jalankan_HanyaMembuatBarisUntukSiswaDanGuruAktif()
    {
        using var database = new MigratedDatabase();
        database.SeedSekolahUntukTap();

        _pembukaanHari.Jalankan(database.Connection, HariKamis);

        using var command = database.Connection.CreateCommand();
        command.CommandText = "SELECT siswa_id FROM absensi_siswa;";
        Assert.Equal(1L, command.ExecuteScalar());

        using var commandGuru = database.Connection.CreateCommand();
        commandGuru.CommandText = "SELECT guru_id FROM absensi_guru;";
        Assert.Equal(1L, commandGuru.ExecuteScalar());
    }

    private static int HitungBaris(MigratedDatabase database, string tabel)
    {
        using var command = database.Connection.CreateCommand();
        command.CommandText = $"SELECT COUNT(*) FROM {tabel};";
        return Convert.ToInt32(command.ExecuteScalar());
    }
}
