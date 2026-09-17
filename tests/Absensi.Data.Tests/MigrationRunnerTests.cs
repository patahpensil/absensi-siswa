using Absensi.Data.Migrations;

namespace Absensi.Data.Tests;

public sealed class MigrationRunnerTests
{
    [Fact]
    public void Run_CalledTwice_DoesNotDuplicateSchemaVersionRow()
    {
        using var database = new MigratedDatabase();

        new MigrationRunner().Run(database.Connection);

        using var command = database.Connection.CreateCommand();
        command.CommandText = "SELECT COUNT(*) FROM skema_versi;";
        var rowCount = Convert.ToInt32(command.ExecuteScalar());

        Assert.Equal(1, rowCount);
    }
}
