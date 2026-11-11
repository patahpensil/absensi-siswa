using Microsoft.Data.Sqlite;

namespace Absensi.Data.Repositori;

public sealed class RepositoriGuru
{
    public IReadOnlyList<int> DaftarIdAktif(SqliteConnection connection)
    {
        using var command = connection.CreateCommand();
        command.CommandText = "SELECT id FROM guru WHERE aktif = 1;";

        using var reader = command.ExecuteReader();
        var daftar = new List<int>();
        while (reader.Read())
        {
            daftar.Add(reader.GetInt32(0));
        }

        return daftar;
    }

    // Dipakai LayananRegistrasiKartu untuk pesan penolakan UID terpakai
    // (PRD §16.5 langkah 3.4).
    public string CariNama(SqliteConnection connection, int guruId)
    {
        using var command = connection.CreateCommand();
        command.CommandText = "SELECT nama FROM guru WHERE id = $id;";
        command.Parameters.AddWithValue("$id", guruId);

        var hasil = command.ExecuteScalar();
        return hasil is null ? throw new InvalidOperationException($"Guru {guruId} tidak ditemukan.") : (string)hasil;
    }

    // Dipakai LayananLaporan (Tugas 12) untuk kartu Laporan Guru (PRD §17.8:
    // "mengikuti format laporan siswa" — termasuk foto).
    public string? CariFotoPath(SqliteConnection connection, int guruId)
    {
        using var command = connection.CreateCommand();
        command.CommandText = "SELECT foto_path FROM guru WHERE id = $id;";
        command.Parameters.AddWithValue("$id", guruId);
        return command.ExecuteScalar() as string;
    }

    // Dipakai LayananLaporan (Tugas 12) untuk identitas kartu Laporan Guru
    // walau guru itu tidak punya baris absensi di rentang laporan.
    public (string Nama, string Nip)? CariIdentitasUntukLaporan(SqliteConnection connection, int guruId)
    {
        using var command = connection.CreateCommand();
        command.CommandText = "SELECT nama, nip FROM guru WHERE id = $id;";
        command.Parameters.AddWithValue("$id", guruId);

        using var reader = command.ExecuteReader();
        return reader.Read() ? (reader.GetString(0), reader.GetString(1)) : null;
    }

    public bool IsAktif(SqliteConnection connection, int guruId)
    {
        using var command = connection.CreateCommand();
        command.CommandText = "SELECT aktif FROM guru WHERE id = $id;";
        command.Parameters.AddWithValue("$id", guruId);

        var aktif = command.ExecuteScalar();
        return aktif is not null && Convert.ToInt32(aktif) == 1;
    }

    // Dipakai layar gerbang (Tugas 6) untuk memuat seluruh nama/NIP/foto
    // guru aktif ke memori sekali saat layar dibuka.
    public IReadOnlyList<GuruRingkas> DaftarAktifRingkas(SqliteConnection connection)
    {
        using var command = connection.CreateCommand();
        command.CommandText = "SELECT id, nama, nip, foto_path FROM guru WHERE aktif = 1;";

        using var reader = command.ExecuteReader();
        var daftar = new List<GuruRingkas>();
        while (reader.Read())
        {
            daftar.Add(new GuruRingkas(
                reader.GetInt32(0),
                reader.GetString(1),
                reader.GetString(2),
                reader.IsDBNull(3) ? null : reader.GetString(3)));
        }

        return daftar;
    }

    public int? CariIdBerdasarkanNip(SqliteConnection connection, string nip, SqliteTransaction? transaction = null)
    {
        using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = "SELECT id FROM guru WHERE nip = $nip;";
        command.Parameters.AddWithValue("$nip", nip);

        var hasil = command.ExecuteScalar();
        return hasil is null ? null : Convert.ToInt32(hasil);
    }

    // Hanya dipanggil lewat Absensi.Data.MasterData.LayananGuru, yang
    // membungkusnya dengan JalurPerubahanData (PRD §12).
    public int Tambah(SqliteConnection connection, SqliteTransaction transaction, DataGuru data)
    {
        using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = """
            INSERT INTO guru (nip, nama, pangkat, jabatan, aktif)
            VALUES ($nip, $nama, $pangkat, $jabatan, 1);
            """;
        IsiParameterGuru(command, data);
        command.ExecuteNonQuery();

        using var commandId = connection.CreateCommand();
        commandId.Transaction = transaction;
        commandId.CommandText = "SELECT last_insert_rowid();";
        return Convert.ToInt32(commandId.ExecuteScalar());
    }

    public void Perbarui(SqliteConnection connection, SqliteTransaction transaction, int id, DataGuru data)
    {
        using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = """
            UPDATE guru SET nama = $nama, pangkat = $pangkat, jabatan = $jabatan
            WHERE id = $id;
            """;
        command.Parameters.AddWithValue("$id", id);
        IsiParameterGuru(command, data);
        command.ExecuteNonQuery();
    }

    // Guru pensiun/pindah ditandai tidak aktif, tidak pernah dihapus
    // (PRD §5.3/§12).
    public void TandaiTidakAktif(SqliteConnection connection, SqliteTransaction transaction, int id, string alasan)
    {
        using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = "UPDATE guru SET aktif = 0, alasan_nonaktif = $alasan WHERE id = $id;";
        command.Parameters.AddWithValue("$id", id);
        command.Parameters.AddWithValue("$alasan", alasan);
        command.ExecuteNonQuery();
    }

    public void PerbaruiFotoPath(SqliteConnection connection, SqliteTransaction transaction, int id, string fotoPath)
    {
        using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = "UPDATE guru SET foto_path = $fotoPath WHERE id = $id;";
        command.Parameters.AddWithValue("$id", id);
        command.Parameters.AddWithValue("$fotoPath", fotoPath);
        command.ExecuteNonQuery();
    }

    // Subquery LIMIT 1 aman karena ix_kartu_uid_aktif menjamin paling banyak
    // SATU kartu aktif per pemilik (sama seperti RepositoriSiswa).
    public IReadOnlyList<GuruDaftar> DaftarLengkap(SqliteConnection connection)
    {
        using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT
                g.id, g.nip, g.nama, g.aktif, g.alasan_nonaktif,
                EXISTS(SELECT 1 FROM kartu WHERE pemilik_tipe = 'guru' AND pemilik_id = g.id AND aktif = 1),
                EXISTS(SELECT 1 FROM pairing WHERE subjek_tipe = 'guru' AND subjek_id = g.id AND status = 'tertaut'),
                (SELECT uid FROM kartu WHERE pemilik_tipe = 'guru' AND pemilik_id = g.id AND aktif = 1 LIMIT 1),
                g.pangkat, g.jabatan
            FROM guru g
            ORDER BY g.nama;
            """;

        using var reader = command.ExecuteReader();
        var daftar = new List<GuruDaftar>();
        while (reader.Read())
        {
            daftar.Add(new GuruDaftar(
                reader.GetInt32(0),
                reader.GetString(1),
                reader.GetString(2),
                reader.GetInt32(3) == 1,
                reader.IsDBNull(4) ? null : reader.GetString(4),
                reader.GetInt32(5) == 1,
                reader.GetInt32(6) == 1,
                KartuUid: reader.IsDBNull(7) ? null : reader.GetString(7),
                Pangkat: reader.IsDBNull(8) ? null : reader.GetString(8),
                Jabatan: reader.IsDBNull(9) ? null : reader.GetString(9)));
        }

        return daftar;
    }

    // Keadaan guru sebelum diubah, untuk nilai lama di jejak audit (PRD §12).
    public GuruUntukAudit? CariUntukAudit(SqliteConnection connection, SqliteTransaction? transaction, int id)
    {
        using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = "SELECT nip, nama, pangkat, jabatan, aktif, alasan_nonaktif, foto_path FROM guru WHERE id = $id;";
        command.Parameters.AddWithValue("$id", id);

        using var reader = command.ExecuteReader();
        if (!reader.Read())
        {
            return null;
        }

        string? Teks(int i) => reader.IsDBNull(i) ? null : reader.GetString(i);
        return new GuruUntukAudit(
            reader.GetString(0), reader.GetString(1), Teks(2), Teks(3), reader.GetInt32(4) == 1, Teks(5), Teks(6));
    }

    // Null bila tidak ditemukan (berbeda dari CariNama): dipakai teks jejak audit.
    public string? CariNamaUntukAudit(SqliteConnection connection, int id, SqliteTransaction? transaction = null)
    {
        using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = "SELECT nama FROM guru WHERE id = $id;";
        command.Parameters.AddWithValue("$id", id);
        return command.ExecuteScalar() as string;
    }

    private static void IsiParameterGuru(SqliteCommand command, DataGuru data)
    {
        command.Parameters.AddWithValue("$nip", data.Nip);
        command.Parameters.AddWithValue("$nama", data.Nama);
        command.Parameters.AddWithValue("$pangkat", (object?)data.Pangkat ?? DBNull.Value);
        command.Parameters.AddWithValue("$jabatan", (object?)data.Jabatan ?? DBNull.Value);
    }
}
