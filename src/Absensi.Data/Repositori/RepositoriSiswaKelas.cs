using Microsoft.Data.Sqlite;

namespace Absensi.Data.Repositori;

// Keanggotaan kelas siswa per tahun ajaran (docs/SKEMA-DATABASE.md §5) —
// dipisah dari RepositoriKelas karena ini tentang siswa_kelas (baris
// keanggotaan), bukan kelas itu sendiri.
public sealed class RepositoriSiswaKelas
{
    // UNIQUE(siswa_id, tahun_ajaran_id) menjamin satu kelas per siswa per
    // tahun ajaran (docs/SKEMA-DATABASE.md §10) — perbarui kelas_id bila
    // baris sudah ada (mis. impor ulang dengan KELAS berbeda), tambah baris
    // baru bila belum.
    public void Tetapkan(
        SqliteConnection connection,
        SqliteTransaction transaction,
        int siswaId,
        int kelasId,
        int tahunAjaranId)
    {
        using var commandUpdate = connection.CreateCommand();
        commandUpdate.Transaction = transaction;
        commandUpdate.CommandText = """
            UPDATE siswa_kelas SET kelas_id = $kelasId
            WHERE siswa_id = $siswaId AND tahun_ajaran_id = $tahunAjaranId;
            """;
        commandUpdate.Parameters.AddWithValue("$siswaId", siswaId);
        commandUpdate.Parameters.AddWithValue("$kelasId", kelasId);
        commandUpdate.Parameters.AddWithValue("$tahunAjaranId", tahunAjaranId);

        if (commandUpdate.ExecuteNonQuery() > 0)
        {
            return;
        }

        using var commandInsert = connection.CreateCommand();
        commandInsert.Transaction = transaction;
        commandInsert.CommandText = """
            INSERT INTO siswa_kelas (siswa_id, kelas_id, tahun_ajaran_id)
            VALUES ($siswaId, $kelasId, $tahunAjaranId);
            """;
        commandInsert.Parameters.AddWithValue("$siswaId", siswaId);
        commandInsert.Parameters.AddWithValue("$kelasId", kelasId);
        commandInsert.Parameters.AddWithValue("$tahunAjaranId", tahunAjaranId);
        commandInsert.ExecuteNonQuery();
    }

    // Nama kelas siswa pada tahun ajaran itu (null bila belum punya kelas) —
    // nilai lama/baru "kelas" di jejak audit perubahan siswa.
    public string? CariNamaKelas(SqliteConnection connection, SqliteTransaction transaction, int siswaId, int tahunAjaranId)
    {
        using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = "SELECT k.nama FROM siswa_kelas sk JOIN kelas k ON k.id = sk.kelas_id WHERE sk.siswa_id = $siswaId AND sk.tahun_ajaran_id = $tahunAjaranId;";
        command.Parameters.AddWithValue("$siswaId", siswaId);
        command.Parameters.AddWithValue("$tahunAjaranId", tahunAjaranId);
        return command.ExecuteScalar() as string;
    }
}
