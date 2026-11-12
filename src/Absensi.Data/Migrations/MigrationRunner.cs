using Microsoft.Data.Sqlite;

namespace Absensi.Data.Migrations;

// Membaca tabel skema_versi dan menjalankan migrasi yang nomornya lebih besar
// dari versi tersimpan (docs/SKEMA-DATABASE.md §11). Aman dipanggil berulang
// kali: migrasi yang sudah tercatat di skema_versi tidak dijalankan lagi.
public sealed class MigrationRunner
{
    private static readonly IMigration[] AllMigrations =
    {
        new Migration001CreateSchema(),
        new Migration002CegahUbahHapusAudit(),
        new Migration003TambahAuditUid(),
        new Migration004AddNotTappedListTime(),
        new Migration005RenameNotTappedListTime(),
        new Migration006SchoolOperations(),
        new Migration007ParentNotificationChannel(),
        new Migration008ScheduleChange(),
    };

    public void Run(SqliteConnection connection)
    {
        var currentVersion = ReadCurrentVersion(connection);

        var pendingMigrations = AllMigrations
            .Where(migration => migration.Number > currentVersion)
            .OrderBy(migration => migration.Number);

        foreach (var migration in pendingMigrations)
        {
            ApplyMigration(connection, migration);
        }
    }

    private static void ApplyMigration(SqliteConnection connection, IMigration migration)
    {
        using var transaction = connection.BeginTransaction();

        using (var migrationCommand = connection.CreateCommand())
        {
            migrationCommand.Transaction = transaction;
            migrationCommand.CommandText = migration.Sql;
            migrationCommand.ExecuteNonQuery();
        }

        using (var recordVersionCommand = connection.CreateCommand())
        {
            recordVersionCommand.Transaction = transaction;
            recordVersionCommand.CommandText =
                "INSERT INTO skema_versi (versi, diterapkan) VALUES ($versi, $diterapkan);";
            recordVersionCommand.Parameters.AddWithValue("$versi", migration.Number);
            // Master Bug & UI/UX Register A-11: skema_versi.diterapkan adalah
            // metadata audit teknis (kapan migrasi ini dijalankan), bukan
            // waktu bisnis/domain -- tidak pernah dibaca balik oleh logika
            // aplikasi. Sengaja memakai UTC sistem langsung, bukan
            // IPenyediaWaktu, konsisten dengan kebijakan timestamp
            // diagnostik proyek (lihat PencatatGalat.cs / INVESTIGASI-CRASH
            // §6.5 poin 4 untuk preseden serupa).
            recordVersionCommand.Parameters.AddWithValue("$diterapkan", DateTime.UtcNow.ToString("o"));
            recordVersionCommand.ExecuteNonQuery();
        }

        transaction.Commit();
    }

    private static int ReadCurrentVersion(SqliteConnection connection)
    {
        using var tableExistsCommand = connection.CreateCommand();
        tableExistsCommand.CommandText =
            "SELECT name FROM sqlite_master WHERE type = 'table' AND name = 'skema_versi';";
        var schemaVersionTableExists = tableExistsCommand.ExecuteScalar() is not null;

        if (!schemaVersionTableExists)
        {
            return 0;
        }

        using var maxVersionCommand = connection.CreateCommand();
        maxVersionCommand.CommandText = "SELECT COALESCE(MAX(versi), 0) FROM skema_versi;";
        return Convert.ToInt32(maxVersionCommand.ExecuteScalar());
    }
}
