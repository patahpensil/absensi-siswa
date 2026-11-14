namespace Absensi.Data.Migrations;

// PRD §3 P-21: after a Kepala Sekolah or Admin resets another account's
// password, the owner must choose a new password at the next login. Existing
// accounts start without that requirement.
internal sealed class Migration010PasswordChangeRequired : IMigration
{
    public int Number => 10;

    public string Sql => """
        ALTER TABLE akun ADD COLUMN must_change_password INTEGER NOT NULL DEFAULT 0;
        """;
}
