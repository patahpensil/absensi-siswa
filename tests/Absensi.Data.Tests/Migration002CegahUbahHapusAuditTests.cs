using Absensi.Data.Migrations;
using Microsoft.Data.Sqlite;

namespace Absensi.Data.Tests;

// Master Bug & UI/UX Register A-38: PRD §12 "tidak dapat dihapus atau
// diedit siapa pun". Migrasi 002 menambah trigger di database (bukan
// sekadar kebiasaan RepositoriAudit yang cuma pernah INSERT). Diuji di
// sini: INSERT tetap normal, UPDATE/DELETE ditolak, baris lama (yang
// sudah ada SEBELUM migrasi 002 dijalankan -- skenario nyata instalasi
// yang sudah berjalan sebelum perbaikan ini rilis) ikut terlindungi.
public sealed class Migration002CegahUbahHapusAuditTests
{
    [Fact]
    public void AuditInsert_StillSucceeds()
    {
        using var database = new MigratedDatabase();
        database.SeedMasterDataUntukUji();

        database.Execute("""
            INSERT INTO audit (waktu, akun_id, aksi, entitas, entitas_id, nilai_lama, nilai_baru)
            VALUES ('2026-09-24T00:00:00Z', 1, 'perbarui_siswa', 'siswa', 1, 'nama=Lama', 'nama=Baru');
            """);

        using var command = database.Connection.CreateCommand();
        command.CommandText = "SELECT COUNT(*) FROM audit;";
        Assert.Equal(1, Convert.ToInt32(command.ExecuteScalar()));
    }

    [Fact]
    public void AuditUpdate_IsRejected()
    {
        using var database = new MigratedDatabase();
        database.SeedMasterDataUntukUji();
        database.Execute("""
            INSERT INTO audit (waktu, akun_id, aksi, entitas, entitas_id, nilai_lama, nilai_baru)
            VALUES ('2026-09-24T00:00:00Z', 1, 'perbarui_siswa', 'siswa', 1, 'nama=Lama', 'nama=Baru');
            """);

        Assert.Throws<SqliteException>(() => database.Execute("""
            UPDATE audit SET nilai_baru = 'DIUBAH-PAKSA' WHERE aksi = 'perbarui_siswa';
            """));
    }

    [Fact]
    public void AuditDelete_IsRejected()
    {
        using var database = new MigratedDatabase();
        database.SeedMasterDataUntukUji();
        database.Execute("""
            INSERT INTO audit (waktu, akun_id, aksi, entitas, entitas_id, nilai_lama, nilai_baru)
            VALUES ('2026-09-24T00:00:00Z', 1, 'perbarui_siswa', 'siswa', 1, 'nama=Lama', 'nama=Baru');
            """);

        Assert.Throws<SqliteException>(() => database.Execute("""
            DELETE FROM audit WHERE aksi = 'perbarui_siswa';
            """));
    }

    [Fact]
    public void AuditRow_UnchangedAfterRejectedUpdate()
    {
        using var database = new MigratedDatabase();
        database.SeedMasterDataUntukUji();
        database.Execute("""
            INSERT INTO audit (waktu, akun_id, aksi, entitas, entitas_id, nilai_lama, nilai_baru)
            VALUES ('2026-09-24T00:00:00Z', 1, 'perbarui_siswa', 'siswa', 1, 'nama=Lama', 'nama=Baru');
            """);

        Assert.Throws<SqliteException>(() => database.Execute("""
            UPDATE audit SET nilai_baru = 'DIUBAH-PAKSA' WHERE aksi = 'perbarui_siswa';
            """));

        using var command = database.Connection.CreateCommand();
        command.CommandText = "SELECT nilai_baru FROM audit WHERE aksi = 'perbarui_siswa';";
        Assert.Equal("nama=Baru", (string)command.ExecuteScalar()!);
    }

    [Fact]
    public void ExistingAuditRow_FromBeforeMigration_UpdateIsRejectedAfterUpgrade()
    {
        using var database = DatabaseAtSchemaVersion1();
        database.Execute("""
            INSERT INTO akun (id, username, sandi_hash, peran, aktif, dibuat_pada)
            VALUES (1, 'admin1', 'tidak-terpakai', 'admin', 1, '2026-01-01T00:00:00Z');
            """);
        database.Execute("""
            INSERT INTO audit (waktu, akun_id, aksi, entitas, entitas_id, nilai_lama, nilai_baru)
            VALUES ('2026-01-01T00:00:00Z', 1, 'perbarui_siswa', 'siswa', 1, 'nama=Lama', 'nama=Baru');
            """);

        // Simulasi upgrade: EXE baru dibuka, MigrationRunner menjalankan
        // migrasi yang nomornya lebih besar dari versi tersimpan (di sini:
        // migrasi 002) terhadap database yang SUDAH punya baris audit lama.
        new MigrationRunner().Run(database.Connection);

        Assert.Throws<SqliteException>(() => database.Execute("""
            UPDATE audit SET nilai_baru = 'DIUBAH-PAKSA' WHERE aksi = 'perbarui_siswa';
            """));
    }

    [Fact]
    public void ExistingAuditRow_FromBeforeMigration_DeleteIsRejectedAfterUpgrade()
    {
        using var database = DatabaseAtSchemaVersion1();
        database.Execute("""
            INSERT INTO akun (id, username, sandi_hash, peran, aktif, dibuat_pada)
            VALUES (1, 'admin1', 'tidak-terpakai', 'admin', 1, '2026-01-01T00:00:00Z');
            """);
        database.Execute("""
            INSERT INTO audit (waktu, akun_id, aksi, entitas, entitas_id, nilai_lama, nilai_baru)
            VALUES ('2026-01-01T00:00:00Z', 1, 'perbarui_siswa', 'siswa', 1, 'nama=Lama', 'nama=Baru');
            """);

        new MigrationRunner().Run(database.Connection);

        Assert.Throws<SqliteException>(() => database.Execute("""
            DELETE FROM audit WHERE aksi = 'perbarui_siswa';
            """));
    }

    // Membangun database yang HANYA sampai skema migrasi 001 (skema dasar,
    // tanpa trigger migrasi 002) -- meniru persis kondisi instalasi yang
    // sudah berjalan sebelum perbaikan A-38 dirilis, sebelum di-upgrade.
    private static MigratedDatabase DatabaseAtSchemaVersion1()
    {
        var database = new MigratedDatabase(applyAllMigrations: false);
        database.Execute(new Migration001CreateSchema().Sql);
        database.Execute("""
            INSERT INTO skema_versi (versi, diterapkan) VALUES (1, '2026-01-01T00:00:00Z');
            """);
        return database;
    }
}
