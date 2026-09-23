using Microsoft.Data.Sqlite;

namespace Absensi.Data.Repositori;

public sealed class RepositoriSiswa
{
    public bool IsAktif(SqliteConnection connection, int siswaId)
    {
        using var command = connection.CreateCommand();
        command.CommandText = "SELECT aktif FROM siswa WHERE id = $id;";
        command.Parameters.AddWithValue("$id", siswaId);

        var aktif = command.ExecuteScalar();
        return aktif is not null && Convert.ToInt32(aktif) == 1;
    }

    // Dipakai LayananLaporan (Tugas 12) untuk kartu Laporan Per Siswa
    // (PRD §11.3: "Identitas (nama, NISN, kelas, foto)").
    public string? CariFotoPath(SqliteConnection connection, int siswaId)
    {
        using var command = connection.CreateCommand();
        command.CommandText = "SELECT foto_path FROM siswa WHERE id = $id;";
        command.Parameters.AddWithValue("$id", siswaId);
        return command.ExecuteScalar() as string;
    }

    // Dipakai layar gerbang (Tugas 6) untuk memuat seluruh nama/NISN/foto
    // siswa aktif ke memori sekali saat layar dibuka, supaya tiap tap tidak
    // menunggu kueri database untuk menampilkan kartu.
    public IReadOnlyList<SiswaRingkas> DaftarAktifRingkas(SqliteConnection connection)
    {
        using var command = connection.CreateCommand();
        command.CommandText = "SELECT id, nama, nisn, foto_path FROM siswa WHERE aktif = 1;";

        using var reader = command.ExecuteReader();
        var daftar = new List<SiswaRingkas>();
        while (reader.Read())
        {
            daftar.Add(new SiswaRingkas(
                reader.GetInt32(0),
                reader.GetString(1),
                reader.GetString(2),
                reader.IsDBNull(3) ? null : reader.GetString(3)));
        }

        return daftar;
    }

    // Dipakai LayananLaporan (Tugas 12) untuk identitas kartu Per Siswa
    // (PRD §11.3) walau siswa itu tidak punya baris absensi sama sekali di
    // rentang laporan (mis. rentang sebelum dia terdaftar) — null berarti
    // id-nya sendiri tidak ada, beda dari "tidak ikut kelas manapun".
    public (string Nama, string Nisn, string? KelasNama)? CariIdentitasUntukLaporan(SqliteConnection connection, int siswaId, int tahunAjaranId)
    {
        using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT s.nama, s.nisn, k.nama
            FROM siswa s
            LEFT JOIN siswa_kelas sk ON sk.siswa_id = s.id AND sk.tahun_ajaran_id = $tahunAjaranId
            LEFT JOIN kelas k ON k.id = sk.kelas_id
            WHERE s.id = $id;
            """;
        command.Parameters.AddWithValue("$id", siswaId);
        command.Parameters.AddWithValue("$tahunAjaranId", tahunAjaranId);

        using var reader = command.ExecuteReader();
        if (!reader.Read())
        {
            return null;
        }

        return (reader.GetString(0), reader.GetString(1), reader.IsDBNull(2) ? null : reader.GetString(2));
    }

    // Dipakai LayananRegistrasiKartu untuk pesan penolakan UID terpakai
    // (PRD §16.5 langkah 3.4) — perlu nama dan kelas pemilik, bukan hanya id.
    public (string Nama, string? KelasNama) CariNamaDanKelas(SqliteConnection connection, int siswaId, int tahunAjaranId)
    {
        using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT s.nama, k.nama
            FROM siswa s
            LEFT JOIN siswa_kelas sk ON sk.siswa_id = s.id AND sk.tahun_ajaran_id = $tahunAjaranId
            LEFT JOIN kelas k ON k.id = sk.kelas_id
            WHERE s.id = $id;
            """;
        command.Parameters.AddWithValue("$id", siswaId);
        command.Parameters.AddWithValue("$tahunAjaranId", tahunAjaranId);

        using var reader = command.ExecuteReader();
        if (!reader.Read())
        {
            throw new InvalidOperationException($"Siswa {siswaId} tidak ditemukan.");
        }

        return (reader.GetString(0), reader.IsDBNull(1) ? null : reader.GetString(1));
    }

    public int? CariIdBerdasarkanNisn(SqliteConnection connection, string nisn)
    {
        using var command = connection.CreateCommand();
        command.CommandText = "SELECT id FROM siswa WHERE nisn = $nisn;";
        command.Parameters.AddWithValue("$nisn", nisn);

        var hasil = command.ExecuteScalar();
        return hasil is null ? null : Convert.ToInt32(hasil);
    }

    // Hanya dipanggil lewat Absensi.Data.MasterData.LayananSiswa, yang
    // membungkusnya dengan JalurPerubahanData (CLAUDE.md §4.1 aturan 4).
    public int Tambah(SqliteConnection connection, SqliteTransaction transaction, DataSiswa data)
    {
        using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = """
            INSERT INTO siswa (nisn, nama, jenis_kelamin, tempat_lahir, tanggal_lahir, alamat, nama_ortu, no_hp_ortu, aktif)
            VALUES ($nisn, $nama, $jenisKelamin, $tempatLahir, $tanggalLahir, $alamat, $namaOrtu, $noHpOrtu, 1);
            """;
        IsiParameterSiswa(command, data);
        command.ExecuteNonQuery();

        using var commandId = connection.CreateCommand();
        commandId.Transaction = transaction;
        commandId.CommandText = "SELECT last_insert_rowid();";
        return Convert.ToInt32(commandId.ExecuteScalar());
    }

    public void Perbarui(SqliteConnection connection, SqliteTransaction transaction, int id, DataSiswa data)
    {
        using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = """
            UPDATE siswa SET
                nama = $nama, jenis_kelamin = $jenisKelamin, tempat_lahir = $tempatLahir,
                tanggal_lahir = $tanggalLahir, alamat = $alamat, nama_ortu = $namaOrtu, no_hp_ortu = $noHpOrtu
            WHERE id = $id;
            """;
        command.Parameters.AddWithValue("$id", id);
        IsiParameterSiswa(command, data);
        command.ExecuteNonQuery();
    }

    // PRD §5.3: siswa keluar/pindah ditandai tidak aktif, tidak pernah
    // dihapus (CLAUDE.md §4.2 aturan 8).
    public void TandaiTidakAktif(SqliteConnection connection, SqliteTransaction transaction, int id, string alasan)
    {
        using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = "UPDATE siswa SET aktif = 0, alasan_nonaktif = $alasan WHERE id = $id;";
        command.Parameters.AddWithValue("$id", id);
        command.Parameters.AddWithValue("$alasan", alasan);
        command.ExecuteNonQuery();
    }

    public void PerbaruiFotoPath(SqliteConnection connection, SqliteTransaction transaction, int id, string fotoPath)
    {
        using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = "UPDATE siswa SET foto_path = $fotoPath WHERE id = $id;";
        command.Parameters.AddWithValue("$id", id);
        command.Parameters.AddWithValue("$fotoPath", fotoPath);
        command.ExecuteNonQuery();
    }

    // PRD §14.4: tabel Daftar dengan penanda Kartu dan TG, plus kelas siswa
    // pada tahun ajaran yang dipilih.
    //
    // UAT Bug #11 (2026-09-23): kolom terakhir (uid kartu) ditambahkan —
    // panel Siswa sekarang menampilkan UID sebenarnya, bukan True/False.
    // Subquery LIMIT 1 aman karena ix_kartu_uid_aktif menjamin paling
    // banyak SATU kartu aktif per pemilik pada satu waktu (aturan "satu
    // siswa satu kartu aktif" ditegakkan di LayananRegistrasiKartu, bukan
    // di query ini).
    public IReadOnlyList<SiswaDaftar> DaftarUntukTahunAjaran(SqliteConnection connection, int tahunAjaranId)
    {
        using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT
                s.id, s.nisn, s.nama, s.jenis_kelamin, s.aktif, s.alasan_nonaktif, k.nama,
                EXISTS(SELECT 1 FROM kartu WHERE pemilik_tipe = 'siswa' AND pemilik_id = s.id AND aktif = 1),
                EXISTS(SELECT 1 FROM pairing WHERE subjek_tipe = 'siswa' AND subjek_id = s.id AND status = 'tertaut'),
                s.foto_path,
                (SELECT uid FROM kartu WHERE pemilik_tipe = 'siswa' AND pemilik_id = s.id AND aktif = 1 LIMIT 1)
            FROM siswa s
            LEFT JOIN siswa_kelas sk ON sk.siswa_id = s.id AND sk.tahun_ajaran_id = $tahunAjaranId
            LEFT JOIN kelas k ON k.id = sk.kelas_id
            ORDER BY s.nama;
            """;
        command.Parameters.AddWithValue("$tahunAjaranId", tahunAjaranId);

        using var reader = command.ExecuteReader();
        var daftar = new List<SiswaDaftar>();
        while (reader.Read())
        {
            daftar.Add(new SiswaDaftar(
                reader.GetInt32(0),
                reader.GetString(1),
                reader.GetString(2),
                reader.GetString(3),
                reader.GetInt32(4) == 1,
                reader.IsDBNull(5) ? null : reader.GetString(5),
                reader.IsDBNull(6) ? null : reader.GetString(6),
                reader.GetInt32(7) == 1,
                reader.GetInt32(8) == 1,
                PunyaFoto: !reader.IsDBNull(9),
                KartuUid: reader.IsDBNull(10) ? null : reader.GetString(10)));
        }

        return daftar;
    }

    // Keadaan siswa sebelum diubah, untuk nilai lama di jejak audit (PRD §12).
    public SiswaUntukAudit? CariUntukAudit(SqliteConnection connection, SqliteTransaction transaction, int id)
    {
        using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = "SELECT nisn, nama, jenis_kelamin, tempat_lahir, tanggal_lahir, alamat, nama_ortu, no_hp_ortu, aktif, alasan_nonaktif, foto_path FROM siswa WHERE id = $id;";
        command.Parameters.AddWithValue("$id", id);

        using var reader = command.ExecuteReader();
        if (!reader.Read())
        {
            return null;
        }

        string? Teks(int i) => reader.IsDBNull(i) ? null : reader.GetString(i);
        return new SiswaUntukAudit(
            new DataSiswa(reader.GetString(0), reader.GetString(1), reader.GetString(2), Teks(3), Teks(4), Teks(5), Teks(6), Teks(7)),
            reader.GetInt32(8) == 1,
            Teks(9),
            Teks(10));
    }

    // Null bila tidak ditemukan (berbeda dari CariNama): dipakai teks jejak audit.
    public string? CariNamaUntukAudit(SqliteConnection connection, int id, SqliteTransaction? transaction = null)
    {
        using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = "SELECT nama FROM siswa WHERE id = $id;";
        command.Parameters.AddWithValue("$id", id);
        return command.ExecuteScalar() as string;
    }

    private static void IsiParameterSiswa(SqliteCommand command, DataSiswa data)
    {
        command.Parameters.AddWithValue("$nisn", data.Nisn);
        command.Parameters.AddWithValue("$nama", data.Nama);
        command.Parameters.AddWithValue("$jenisKelamin", data.JenisKelamin);
        command.Parameters.AddWithValue("$tempatLahir", (object?)data.TempatLahir ?? DBNull.Value);
        command.Parameters.AddWithValue("$tanggalLahir", (object?)data.TanggalLahir ?? DBNull.Value);
        command.Parameters.AddWithValue("$alamat", (object?)data.Alamat ?? DBNull.Value);
        command.Parameters.AddWithValue("$namaOrtu", (object?)data.NamaOrtu ?? DBNull.Value);
        command.Parameters.AddWithValue("$noHpOrtu", (object?)data.NoHpOrtu ?? DBNull.Value);
    }
}
