using Microsoft.Data.Sqlite;

namespace Absensi.Data.Repositori;

public sealed class RepositoriJadwalHari
{
    // hari: 1=Senin .. 7=Minggu (docs/SKEMA-DATABASE.md §4).
    public JadwalHariKonfigurasi? CariUntukHari(SqliteConnection connection, int tahunAjaranId, int hari)
    {
        using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT hari_sekolah, jam_masuk, jam_rekap, batas_tap_masuk, mulai_pulang, jam_daftar_belum_tap
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
            MulaiPulang: BacaJam(reader, 4),
            JamDaftarBelumTap: BacaJam(reader, 5));
    }

    // PRD §7 / layar Pengaturan: ketujuh hari sekaligus, termasuk yang
    // belum pernah disimpan (tahun ajaran baru) — diisi default supaya
    // layar selalu menampilkan 7 baris utuh, bukan sebagian. Default
    // mengikuti CLAUDE.md §7 ("Minggu libur", sisanya hari sekolah) —
    // sekadar isian awal formulir, admin tetap bebas mengubahnya sebelum
    // Simpan; bukan nilai yang ditegakkan di kode (aturan 2).
    public IReadOnlyList<JadwalHariBaris> DaftarUntukTahunAjaran(SqliteConnection connection, int tahunAjaranId)
    {
        var tersimpan = new Dictionary<int, JadwalHariBaris>();

        using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT hari, hari_sekolah, jam_masuk, jam_rekap, batas_tap_masuk, mulai_pulang, jam_daftar_belum_tap
            FROM jadwal_hari WHERE tahun_ajaran_id = $tahunAjaranId;
            """;
        command.Parameters.AddWithValue("$tahunAjaranId", tahunAjaranId);

        using (var reader = command.ExecuteReader())
        {
            while (reader.Read())
            {
                var hari = reader.GetInt32(0);
                tersimpan[hari] = new JadwalHariBaris(
                    hari,
                    reader.GetInt32(1) == 1,
                    BacaJam(reader, 2), BacaJam(reader, 3), BacaJam(reader, 4), BacaJam(reader, 5),
                    JamDaftarBelumTap: BacaJam(reader, 6));
            }
        }

        var daftar = new List<JadwalHariBaris>();
        for (var hari = 1; hari <= 7; hari++)
        {
            daftar.Add(tersimpan.TryGetValue(hari, out var baris)
                ? baris
                : new JadwalHariBaris(hari, HariSekolah: hari != 7, null, null, null, null));
        }

        return daftar;
    }

    // Hanya dipanggil lewat Absensi.Data.Waktu.LayananJadwalHari, yang
    // membungkusnya dengan JalurPerubahanData (CLAUDE.md §4.1 aturan 4).
    // Upsert per hari — ketujuh baris ditulis dalam satu transaksi oleh
    // pemanggil (PRD §7: "seluruh nilai dapat diubah per hari").
    public void Perbarui(SqliteConnection connection, SqliteTransaction transaction, int tahunAjaranId, JadwalHariBaris data)
    {
        using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = """
            INSERT INTO jadwal_hari (tahun_ajaran_id, hari, hari_sekolah, jam_masuk, jam_rekap, batas_tap_masuk, mulai_pulang, jam_daftar_belum_tap)
            VALUES ($tahunAjaranId, $hari, $hariSekolah, $jamMasuk, $jamRekap, $batasTapMasuk, $mulaiPulang, $jamDaftarBelumTap)
            ON CONFLICT(tahun_ajaran_id, hari) DO UPDATE SET
                hari_sekolah = excluded.hari_sekolah,
                jam_masuk = excluded.jam_masuk,
                jam_rekap = excluded.jam_rekap,
                batas_tap_masuk = excluded.batas_tap_masuk,
                mulai_pulang = excluded.mulai_pulang,
                jam_daftar_belum_tap = excluded.jam_daftar_belum_tap;
            """;
        command.Parameters.AddWithValue("$tahunAjaranId", tahunAjaranId);
        command.Parameters.AddWithValue("$hari", data.Hari);
        command.Parameters.AddWithValue("$hariSekolah", data.HariSekolah ? 1 : 0);
        command.Parameters.AddWithValue("$jamMasuk", (object?)data.JamMasuk?.ToString("HH:mm") ?? DBNull.Value);
        command.Parameters.AddWithValue("$jamRekap", (object?)data.JamRekap?.ToString("HH:mm") ?? DBNull.Value);
        command.Parameters.AddWithValue("$batasTapMasuk", (object?)data.BatasTapMasuk?.ToString("HH:mm") ?? DBNull.Value);
        command.Parameters.AddWithValue("$mulaiPulang", (object?)data.MulaiPulang?.ToString("HH:mm") ?? DBNull.Value);
        command.Parameters.AddWithValue("$jamDaftarBelumTap", (object?)data.JamDaftarBelumTap?.ToString("HH:mm") ?? DBNull.Value);
        command.ExecuteNonQuery();
    }

    private static TimeOnly? BacaJam(SqliteDataReader reader, int kolom) =>
        reader.IsDBNull(kolom) ? null : TimeOnly.Parse(reader.GetString(kolom));
}
