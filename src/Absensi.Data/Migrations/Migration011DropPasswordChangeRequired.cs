namespace Absensi.Data.Migrations;

// P-21 revised 14 November 2026: a reset password works immediately, so the
// "must change password" flag from migration 010 is no longer used. Databases
// that already ran migration 010 (mode nyata browsers) drop the column here.
internal sealed class Migration011DropPasswordChangeRequired : IMigration
{
    public int Number => 11;

    public string Sql => """
        ALTER TABLE akun DROP COLUMN must_change_password;
        """;
}
