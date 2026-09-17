using Absensi.Domain.Kehadiran;
using Microsoft.Data.Sqlite;

namespace Absensi.Data.Repositori;

public sealed class RepositoriKalender
{
    private readonly RepositoriJadwalHari _repositoriJadwalHari = new();

    // Kalender hanya memuat tanggal yang menyimpang dari pola mingguan
    // (docs/SKEMA-DATABASE.md §4). Bila tidak ada baris untuk tanggal ini,
    // jenis hari diturunkan dari jadwal_hari.hari_sekolah — inilah yang
    // membuat "Minggu libur" (CLAUDE.md §7) berlaku tanpa mencatat setiap
    // hari Minggu di kalender.
    public JenisHariKalender TentukanJenisHari(SqliteConnection connection, DateOnly tanggal, int tahunAjaranId)
    {
        using var command = connection.CreateCommand();
        command.CommandText = "SELECT jenis FROM kalender WHERE tanggal = $tanggal;";
        command.Parameters.AddWithValue("$tanggal", tanggal.ToString("yyyy-MM-dd"));

        var jenisTersimpan = command.ExecuteScalar() as string;
        if (jenisTersimpan is not null)
        {
            return jenisTersimpan == "libur" ? JenisHariKalender.Libur : JenisHariKalender.Sekolah;
        }

        var hari = HariKalender.DariTanggal(tanggal);
        var jadwal = _repositoriJadwalHari.CariUntukHari(connection, tahunAjaranId, hari);

        return jadwal is { HariSekolah: true } ? JenisHariKalender.Sekolah : JenisHariKalender.Libur;
    }
}
