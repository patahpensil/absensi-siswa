using Microsoft.Data.Sqlite;

namespace Absensi.Data.Repositori;

public sealed class RepositoriTahunAjaran
{
    public int? CariIdAktif(SqliteConnection connection)
    {
        using var command = connection.CreateCommand();
        command.CommandText = "SELECT id FROM tahun_ajaran WHERE aktif = 1 LIMIT 1;";
        var hasil = command.ExecuteScalar();
        return hasil is null ? null : Convert.ToInt32(hasil);
    }

    // Dipakai header jendela utama (PRD §14.2): nama tahun ajaran aktif
    // tampil sebagai pengaman supaya admin sadar bila masih memakai tahun
    // ajaran lama.
    public string? CariNamaAktif(SqliteConnection connection)
    {
        using var command = connection.CreateCommand();
        command.CommandText = "SELECT nama FROM tahun_ajaran WHERE aktif = 1 LIMIT 1;";
        return command.ExecuteScalar() as string;
    }

    // Dipakai LayananJadwalHari untuk memeriksa kunci bulan di seluruh
    // rentang tahun ajaran (jadwal_hari berdampak retroaktif ke semua
    // bulannya, bukan cuma bulan yang sedang dilihat admin).
    public (DateOnly TglMulai, DateOnly TglSelesai)? CariRentang(SqliteConnection connection, int tahunAjaranId)
    {
        using var command = connection.CreateCommand();
        command.CommandText = "SELECT tgl_mulai, tgl_selesai FROM tahun_ajaran WHERE id = $id;";
        command.Parameters.AddWithValue("$id", tahunAjaranId);

        using var reader = command.ExecuteReader();
        if (!reader.Read())
        {
            return null;
        }

        return (DateOnly.Parse(reader.GetString(0)), DateOnly.Parse(reader.GetString(1)));
    }
}
