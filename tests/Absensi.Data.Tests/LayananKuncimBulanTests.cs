using Absensi.Data.Akun;
using Absensi.Data.Koreksi;
using Absensi.Data.Repositori;
using Absensi.Domain.Akun;

namespace Absensi.Data.Tests;

// RENCANA-TUGAS.md Tugas 11 — Kunci Bulan (PRD §10).
public sealed class LayananKuncimBulanTests
{
    private static readonly DateTime Waktu = new(2026, 9, 18, 8, 0, 0);
    private static readonly AkunTersimpan Admin = new(1, "admin1", "x", PeranAkun.Admin, null, true);
    private static readonly AkunTersimpan Kepsek = new(2, "kepsek", "x", PeranAkun.KepalaSekolah, null, true);
    private static readonly AkunTersimpan WaliKelas = new(3, "wali_a", "x", PeranAkun.WaliKelas, 1, true);

    [Fact]
    public void KunciBulan_Admin_TerkunciDanTercatatDiAudit()
    {
        using var database = new MigratedDatabase();
        database.SeedSekolahUntukKoreksi();
        var layanan = new LayananKuncimBulan();

        layanan.KunciBulan(database.Connection, Admin, Waktu, 2026, 9);

        Assert.True(layanan.SudahDikunci(database.Connection, 2026, 9));
        using var command = database.Connection.CreateCommand();
        command.CommandText = "SELECT COUNT(*) FROM audit WHERE aksi = 'kunci_bulan';";
        Assert.Equal(1, Convert.ToInt32(command.ExecuteScalar()));
    }

    [Fact]
    public void KunciBulan_KepalaSekolah_Diizinkan()
    {
        using var database = new MigratedDatabase();
        database.SeedSekolahUntukKoreksi();
        var layanan = new LayananKuncimBulan();

        layanan.KunciBulan(database.Connection, Kepsek, Waktu, 2026, 9);

        Assert.True(layanan.SudahDikunci(database.Connection, 2026, 9));
    }

    [Fact]
    public void KunciBulan_WaliKelas_Ditolak()
    {
        using var database = new MigratedDatabase();
        database.SeedSekolahUntukKoreksi();
        var layanan = new LayananKuncimBulan();

        Assert.Throws<InvalidOperationException>(() => layanan.KunciBulan(database.Connection, WaliKelas, Waktu, 2026, 9));
        Assert.False(layanan.SudahDikunci(database.Connection, 2026, 9));
    }

    [Fact]
    public void KunciBulan_SudahTerkunci_Ditolak()
    {
        using var database = new MigratedDatabase();
        database.SeedSekolahUntukKoreksi();
        var layanan = new LayananKuncimBulan();
        layanan.KunciBulan(database.Connection, Admin, Waktu, 2026, 9);

        Assert.Throws<InvalidOperationException>(() => layanan.KunciBulan(database.Connection, Admin, Waktu, 2026, 9));
    }
}
