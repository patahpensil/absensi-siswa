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

    // Hanya dipanggil lewat Absensi.Data.Waktu.LayananKalender, yang
    // membungkusnya dengan JalurPerubahanData (CLAUDE.md §4.1 aturan 4).
    // Upsert per tanggal — menandai ulang tanggal yang sudah ditandai
    // sebelumnya (mis. mengembalikan hari libur jadi hari sekolah untuk
    // MPLS/Sumatif, PRD §6) mengganti baris lama, bukan menambah baris baru.
    public void Tandai(
        SqliteConnection connection,
        SqliteTransaction transaction,
        DateOnly tanggal,
        JenisHariKalender jenis,
        string? keterangan,
        int akunId,
        DateTime waktu)
    {
        using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = """
            INSERT INTO kalender (tanggal, jenis, keterangan, dibuat_oleh, dibuat_pada)
            VALUES ($tanggal, $jenis, $keterangan, $akunId, $waktu)
            ON CONFLICT(tanggal) DO UPDATE SET
                jenis = excluded.jenis,
                keterangan = excluded.keterangan,
                dibuat_oleh = excluded.dibuat_oleh,
                dibuat_pada = excluded.dibuat_pada;
            """;
        command.Parameters.AddWithValue("$tanggal", tanggal.ToString("yyyy-MM-dd"));
        command.Parameters.AddWithValue("$jenis", PemetaanEnum.TeksDariJenisHari(jenis));
        command.Parameters.AddWithValue("$keterangan", (object?)keterangan ?? DBNull.Value);
        command.Parameters.AddWithValue("$akunId", akunId);
        command.Parameters.AddWithValue("$waktu", waktu.ToString("o"));
        command.ExecuteNonQuery();
    }

    // PRD §6 / layar Kalender ("tampilan bulanan"): seluruh tanggal yang
    // menyimpang dari pola mingguan pada satu bulan. Hari biasa tidak
    // muncul di sini — itulah kenapa layar perlu tahu jadwal_hari juga
    // untuk membedakan "hari sekolah biasa" dari "libur mingguan biasa"
    // (mis. Minggu) saat menggambar kalender, bukan lewat method ini.
    public IReadOnlyList<KalenderEntri> DaftarUntukBulan(SqliteConnection connection, int tahun, int bulan)
    {
        var awal = new DateOnly(tahun, bulan, 1);
        var akhir = awal.AddMonths(1).AddDays(-1);

        using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT tanggal, jenis, keterangan FROM kalender
            WHERE tanggal BETWEEN $awal AND $akhir
            ORDER BY tanggal;
            """;
        command.Parameters.AddWithValue("$awal", awal.ToString("yyyy-MM-dd"));
        command.Parameters.AddWithValue("$akhir", akhir.ToString("yyyy-MM-dd"));

        using var reader = command.ExecuteReader();
        var daftar = new List<KalenderEntri>();
        while (reader.Read())
        {
            daftar.Add(new KalenderEntri(
                DateOnly.Parse(reader.GetString(0)),
                PemetaanEnum.JenisHariDariTeks(reader.GetString(1)),
                reader.IsDBNull(2) ? null : reader.GetString(2)));
        }

        return daftar;
    }
}
