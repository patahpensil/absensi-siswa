using Microsoft.Data.Sqlite;

namespace Absensi.Data.Repositori;

public sealed class RepositoriJadwalHari
{
    // hari: 1=Senin .. 7=Minggu (docs/SKEMA-DATABASE.md §4).
    public JadwalHariKonfigurasi? CariUntukHari(SqliteConnection connection, int tahunAjaranId, int hari)
    {
        using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT hari_sekolah, jam_masuk, jam_rekap, batas_tap_masuk, mulai_pulang
            FROM jadwal_hari
            WHERE tahun_ajaran_id = $tahunAjaranId AND hari = $hari;
            """;
        command.Parameters.AddWithValue("$tahunAjaranId", tahunAjaranId);
        command.Parameters.AddWithValue("$hari", hari);

        using var reader = command.ExecuteReader();
        if (!reader.Read())
        {
            return null;
        }

        return new JadwalHariKonfigurasi(
            HariSekolah: reader.GetInt32(0) == 1,
            JamMasuk: BacaJam(reader, 1),
            JamRekap: BacaJam(reader, 2),
            BatasTapMasuk: BacaJam(reader, 3),
            MulaiPulang: BacaJam(reader, 4));
    }

    private static TimeOnly? BacaJam(SqliteDataReader reader, int kolom) =>
        reader.IsDBNull(kolom) ? null : TimeOnly.Parse(reader.GetString(kolom));
}
