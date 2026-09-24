using Absensi.Data.Migrations;

namespace Absensi.Data.Tests;

public sealed class MigrationRunnerTests
{
    [Fact]
    public void Run_CalledTwice_DoesNotDuplicateSchemaVersionRow()
    {
        using var database = new MigratedDatabase();

        // Jumlah baris tidak di-hardcode ke satu migrasi tertentu -- yang
        // diuji adalah PERILAKU idempoten (Run kedua tidak menambah baris
        // apa pun), bukan jumlah migrasi yang kebetulan ada saat ini.
        using var countCommand = database.Connection.CreateCommand();
        countCommand.CommandText = "SELECT COUNT(*) FROM skema_versi;";
        var rowCountBeforeSecondRun = Convert.ToInt32(countCommand.ExecuteScalar());

        new MigrationRunner().Run(database.Connection);

        var rowCountAfterSecondRun = Convert.ToInt32(countCommand.ExecuteScalar());

        Assert.Equal(rowCountBeforeSecondRun, rowCountAfterSecondRun);
    }
}
