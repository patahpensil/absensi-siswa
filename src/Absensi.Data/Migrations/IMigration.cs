namespace Absensi.Data.Migrations;

internal interface IMigration
{
    int Number { get; }

    string Sql { get; }
}
