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

    // PRD §14.4/UI-guide: layar Kelas menampilkan nama wali, bukan hanya id.
    public IReadOnlyList<KelasDaftar> DaftarUntukTahunAjaran(SqliteConnection connection, int tahunAjaranId)
    {
        using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT k.id, k.nama, k.tingkat, k.wali_guru_id, g.nama
            FROM kelas k
            LEFT JOIN guru g ON g.id = k.wali_guru_id
            WHERE k.tahun_ajaran_id = $tahunAjaranId
            ORDER BY k.tingkat, k.nama;
            """;
        command.Parameters.AddWithValue("$tahunAjaranId", tahunAjaranId);

        using var reader = command.ExecuteReader();
        var daftar = new List<KelasDaftar>();
        while (reader.Read())
        {
            daftar.Add(new KelasDaftar(
                reader.GetInt32(0),
                reader.GetString(1),
                reader.GetInt32(2),
                reader.IsDBNull(3) ? null : reader.GetInt32(3),
                reader.IsDBNull(4) ? null : reader.GetString(4)));
        }

        return daftar;
    }

    // PRD §16.3 langkah 4.6: kolom KELAS pada impor siswa divalidasi
    // terhadap Master Kelas tahun ajaran yang dipilih.
    public int? CariIdBerdasarkanNama(SqliteConnection connection, int tahunAjaranId, string nama)
    {
        using var command = connection.CreateCommand();
        command.CommandText = "SELECT id FROM kelas WHERE tahun_ajaran_id = $tahunAjaranId AND nama = $nama;";
        command.Parameters.AddWithValue("$tahunAjaranId", tahunAjaranId);
        command.Parameters.AddWithValue("$nama", nama);

        var hasil = command.ExecuteScalar();
        return hasil is null ? null : Convert.ToInt32(hasil);
    }

    // Hanya dipanggil lewat Absensi.Data.MasterData.LayananKelas, yang
    // membungkusnya dengan JalurPerubahanData (CLAUDE.md §4.1 aturan 4).
    public int Tambah(
        SqliteConnection connection,
        SqliteTransaction transaction,
        int tahunAjaranId,
        string nama,
        int tingkat,
        int? waliGuruId)
    {
        using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = """
            INSERT INTO kelas (tahun_ajaran_id, nama, tingkat, wali_guru_id)
            VALUES ($tahunAjaranId, $nama, $tingkat, $waliGuruId);
            """;
        command.Parameters.AddWithValue("$tahunAjaranId", tahunAjaranId);
        command.Parameters.AddWithValue("$nama", nama);
        command.Parameters.AddWithValue("$tingkat", tingkat);
        command.Parameters.AddWithValue("$waliGuruId", (object?)waliGuruId ?? DBNull.Value);
        command.ExecuteNonQuery();

        using var commandId = connection.CreateCommand();
        commandId.Transaction = transaction;
        commandId.CommandText = "SELECT last_insert_rowid();";
        return Convert.ToInt32(commandId.ExecuteScalar());
    }

    public void Perbarui(
        SqliteConnection connection,
        SqliteTransaction transaction,
        int id,
        string nama,
        int tingkat,
        int? waliGuruId)
    {
        using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = """
            UPDATE kelas SET nama = $nama, tingkat = $tingkat, wali_guru_id = $waliGuruId
            WHERE id = $id;
            """;
        command.Parameters.AddWithValue("$id", id);
        command.Parameters.AddWithValue("$nama", nama);
        command.Parameters.AddWithValue("$tingkat", tingkat);
        command.Parameters.AddWithValue("$waliGuruId", (object?)waliGuruId ?? DBNull.Value);
        command.ExecuteNonQuery();
    }

    // Keadaan kelas sebelum diubah, untuk nilai lama di jejak audit (PRD §12).
    public KelasUntukAudit? CariUntukAudit(SqliteConnection connection, SqliteTransaction transaction, int id)
    {
        using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = "SELECT nama, tingkat, wali_guru_id FROM kelas WHERE id = $id;";
        command.Parameters.AddWithValue("$id", id);

        using var reader = command.ExecuteReader();
        return reader.Read()
            ? new KelasUntukAudit(reader.GetString(0), reader.GetInt32(1), reader.IsDBNull(2) ? null : reader.GetInt32(2))
            : null;
    }
}
