using Microsoft.Data.Sqlite;

namespace Absensi.Data.Repositori;

public sealed class RepositoriKelas
{
    // Keanggotaan kelas siswa aktif pada satu tahun ajaran, lewat siswa_kelas
    // (docs/SKEMA-DATABASE.md §5). Dipakai pembukaan hari untuk menyalin
    // kelas_id ke baris absensi_siswa saat baris itu dibuat.
    public IReadOnlyList<AnggotaKelas> DaftarSiswaAktifPerKelas(SqliteConnection connection, int tahunAjaranId)
    {
        using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT sk.siswa_id, sk.kelas_id
            FROM siswa_kelas sk
            JOIN siswa s ON s.id = sk.siswa_id
            WHERE sk.tahun_ajaran_id = $tahunAjaranId AND s.aktif = 1;
            """;
        command.Parameters.AddWithValue("$tahunAjaranId", tahunAjaranId);

        using var reader = command.ExecuteReader();
        var daftar = new List<AnggotaKelas>();
        while (reader.Read())
        {
            daftar.Add(new AnggotaKelas(reader.GetInt32(0), reader.GetInt32(1)));
        }

        return daftar;
    }

    // Dipakai LayananAkun.BolehMenyentuhKelas untuk menyelesaikan
    // kelas.wali_guru_id (docs/SKEMA-DATABASE.md §5) yang dibandingkan
    // terhadap akun.guru_id (CLAUDE.md §4.5 aturan 20).
    public int? CariWaliGuruId(SqliteConnection connection, int kelasId)
    {
        using var command = connection.CreateCommand();
        command.CommandText = "SELECT wali_guru_id FROM kelas WHERE id = $kelasId;";
        command.Parameters.AddWithValue("$kelasId", kelasId);

        using var reader = command.ExecuteReader();
        if (!reader.Read())
        {
            throw new InvalidOperationException($"Kelas {kelasId} tidak ditemukan.");
        }

        // null berarti kelas ini belum punya wali kelas ditugaskan, bukan
        // kelas yang tidak ada.
        return reader.IsDBNull(0) ? null : reader.GetInt32(0);
    }
}
