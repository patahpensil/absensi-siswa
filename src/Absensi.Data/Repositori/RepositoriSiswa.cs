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
}
