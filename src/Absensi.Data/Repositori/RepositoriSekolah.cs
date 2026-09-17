using Microsoft.Data.Sqlite;

namespace Absensi.Data.Repositori;

// Tabel sekolah dibatasi satu baris (docs/SKEMA-DATABASE.md §3) — satu
// instalasi untuk satu sekolah. null berarti belum diisi lewat wizard
// instalasi pertama (Tugas 13).
public sealed class RepositoriSekolah
{
    public SekolahTersimpan? CariSatuSatunya(SqliteConnection connection)
    {
        using var command = connection.CreateCommand();
        command.CommandText = "SELECT nama, logo_path FROM sekolah WHERE id = 1;";

        using var reader = command.ExecuteReader();
        if (!reader.Read())
        {
            return null;
        }

        return new SekolahTersimpan(reader.GetString(0), reader.IsDBNull(1) ? null : reader.GetString(1));
    }
}
