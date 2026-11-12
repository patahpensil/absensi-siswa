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
        command.CommandText = "SELECT nama, logo_path, npsn FROM sekolah WHERE id = 1;";

        using var reader = command.ExecuteReader();
        if (!reader.Read())
        {
            return null;
        }

        return new SekolahTersimpan(
            reader.GetString(0),
            reader.IsDBNull(1) ? null : reader.GetString(1),
            reader.IsDBNull(2) ? null : reader.GetString(2));
    }

    public SchoolIdentity? FindIdentity(SqliteConnection connection, SqliteTransaction? transaction = null)
    {
        using var command = connection.CreateCommand();
        if (transaction is not null) command.Transaction = transaction;
        command.CommandText = "SELECT nama, npsn, alamat FROM sekolah WHERE id = 1;";
        using var reader = command.ExecuteReader();
        return reader.Read()
            ? new SchoolIdentity(reader.GetString(0), reader.IsDBNull(1) ? null : reader.GetString(1), reader.IsDBNull(2) ? null : reader.GetString(2))
            : null;
    }

    // Only called through Absensi.Data.Pengaturan.SchoolIdentityService (audited).
    public void UpdateIdentity(SqliteConnection connection, SqliteTransaction transaction, SchoolIdentity identity)
    {
        using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = "UPDATE sekolah SET nama = $nama, npsn = $npsn, alamat = $alamat WHERE id = 1;";
        command.Parameters.AddWithValue("$nama", identity.Name);
        command.Parameters.AddWithValue("$npsn", (object?)identity.Npsn ?? DBNull.Value);
        command.Parameters.AddWithValue("$alamat", (object?)identity.Address ?? DBNull.Value);
        if (command.ExecuteNonQuery() != 1)
        {
            throw new InvalidOperationException("Identitas sekolah belum ada; selesaikan instalasi terlebih dahulu.");
        }
    }

    // Hanya dipanggil dari Absensi.Data.Wizard.LayananWizard. Baris id=1 ini
    // menjadi penanda "wizard sudah selesai" (App.xaml.cs) — begitu ada,
    // wizard tidak pernah dijalankan ulang (PRD §16.1).
    public void Simpan(SqliteConnection connection, SqliteTransaction transaction, string nama, string? npsn, string? alamat, string? logoPath)
    {
        using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = """
            INSERT INTO sekolah (id, nama, npsn, alamat, logo_path) VALUES (1, $nama, $npsn, $alamat, $logoPath);
            """;
        command.Parameters.AddWithValue("$nama", nama);
        command.Parameters.AddWithValue("$npsn", (object?)npsn ?? DBNull.Value);
        command.Parameters.AddWithValue("$alamat", (object?)alamat ?? DBNull.Value);
        command.Parameters.AddWithValue("$logoPath", (object?)logoPath ?? DBNull.Value);
        command.ExecuteNonQuery();
    }
}

public sealed record SchoolIdentity(string Name, string? Npsn, string? Address);
