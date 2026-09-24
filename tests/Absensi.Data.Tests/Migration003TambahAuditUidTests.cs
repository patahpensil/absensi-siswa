using Absensi.Data.Migrations;
using Microsoft.Data.Sqlite;

namespace Absensi.Data.Tests;

// Master Bug & UI/UX Register A-39: identitas audit_uid permanen -- baris
// LAMA (fingerprint deterministik) harus konsisten lintas database/lini
// masa independen, baris BARU (GUID) tidak boleh pernah tergabung akibat
// `id` SQLite yang dipakai ulang setelah restore. Dibuktikan dulu di
// prototipe scratchpad (2026-09-24) sebelum migrasi ini ditulis.
public sealed class Migration003TambahAuditUidTests
{
    [Fact]
    public void BarisBaru_LewatRepositoriAudit_MendapatAuditUidGuidBerbeda()
    {
        using var database = new MigratedDatabase();
        database.SeedMasterDataUntukUji();
        var repositori = new Absensi.Data.Repositori.RepositoriAudit();

        using (var transaksi = database.Connection.BeginTransaction())
        {
            repositori.Catat(database.Connection, transaksi, DateTime.UtcNow, 1, "buat_akun", "akun", 2, null, "username=admin2", null);
            repositori.Catat(database.Connection, transaksi, DateTime.UtcNow, 1, "buat_akun", "akun", 3, null, "username=admin3", null);
            transaksi.Commit();
        }

        var uid = database.Connection.CreateCommand();
        uid.CommandText = "SELECT audit_uid FROM audit ORDER BY id;";
        using var reader = uid.ExecuteReader();
        var nilai = new List<string>();
        while (reader.Read())
        {
            nilai.Add(reader.GetString(0));
        }

        Assert.Equal(2, nilai.Count);
        Assert.NotEqual(nilai[0], nilai[1]);
        Assert.DoesNotContain('|', nilai[0]); // gaya GUID, bukan gaya fingerprint
        Assert.DoesNotContain('|', nilai[1]);
    }

    [Fact]
    public void BarisLama_DibackfillSaatUpgrade_FingerprintKonsistenLintasLiniMasaIndependen()
    {
        // Dua database TERPISAH, masing-masing berhenti di skema versi 2,
        // diisi baris audit dengan ISI YANG SAMA PERSIS (meniru dua salinan
        // fisik dari peristiwa nyata yang sama -- mis. live db dan sebuah
        // backup lama), lalu migrasi 003 dijalankan SECARA INDEPENDEN di
        // masing-masing. Hasilnya HARUS identik -- itulah yang membuat
        // dedup lintas restore aman (lihat LayananArsipAudit).
        var auditUid1 = BackfillSatuBarisIndependen("2026-09-01T08:00:00.0000001Z");
        var auditUid2 = BackfillSatuBarisIndependen("2026-09-01T08:00:00.0000001Z");

        Assert.Equal(auditUid1, auditUid2);
        Assert.Contains('|', auditUid1); // gaya fingerprint, bukan GUID
    }

    [Fact]
    public void BarisLama_IsiBerbeda_MenghasilkanFingerprintBerbeda()
    {
        var uidA = BackfillSatuBarisIndependen("2026-09-01T08:00:00.0000001Z");
        var uidB = BackfillSatuBarisIndependen("2026-09-02T08:00:00.0000001Z"); // waktu beda = peristiwa beda

        Assert.NotEqual(uidA, uidB);
    }

    [Fact]
    public void BarisLama_IdBerbedaIsiSama_TetapAuditUidSama()
    {
        // Membuktikan identitas TIDAK bergantung pada `id` SQLite sama
        // sekali -- persis properti yang membuat penggabungan aman
        // walau `id` dipakai ulang berkali-kali setelah restore.
        var uidId5 = BackfillSatuBarisIndependen("2026-09-10T09:00:00.0000001Z", id: 5);
        var uidId99 = BackfillSatuBarisIndependen("2026-09-10T09:00:00.0000001Z", id: 99);

        Assert.Equal(uidId5, uidId99);
    }

    [Fact]
    public void AuditUid_DuplikatDitolakOlehIndexUnik()
    {
        using var database = new MigratedDatabase();
        database.SeedMasterDataUntukUji();
        database.Execute("""
            INSERT INTO audit (waktu, akun_id, aksi, entitas, entitas_id, nilai_lama, nilai_baru, atas_permintaan, audit_uid)
            VALUES ('2026-09-20T08:00:00', 1, 'buat_akun', 'akun', 2, NULL, 'x', NULL, 'sama-persis');
            """);

        Assert.Throws<SqliteException>(() => database.Execute("""
            INSERT INTO audit (waktu, akun_id, aksi, entitas, entitas_id, nilai_lama, nilai_baru, atas_permintaan, audit_uid)
            VALUES ('2026-09-21T08:00:00', 1, 'buat_akun', 'akun', 3, NULL, 'y', NULL, 'sama-persis');
            """));
    }

    [Fact]
    public void SetelahMigrasi003_TriggerA38TetapMenolakUpdateDanDelete()
    {
        // Regresi: migrasi 003 melepas lalu memasang ulang trigger A-38
        // untuk bisa backfill -- pastikan keduanya benar-benar aktif lagi
        // di keadaan akhir, bukan cuma diasumsikan dari baca kode.
        using var database = new MigratedDatabase();
        database.SeedMasterDataUntukUji();
        database.Execute("""
            INSERT INTO audit (waktu, akun_id, aksi, entitas, entitas_id, nilai_lama, nilai_baru, atas_permintaan, audit_uid)
            VALUES ('2026-09-20T08:00:00', 1, 'buat_akun', 'akun', 2, NULL, 'x', NULL, 'uji-regresi-a38');
            """);

        Assert.Throws<SqliteException>(() => database.Execute(
            "UPDATE audit SET nilai_baru = 'DIUBAH-PAKSA' WHERE audit_uid = 'uji-regresi-a38';"));
        Assert.Throws<SqliteException>(() => database.Execute(
            "DELETE FROM audit WHERE audit_uid = 'uji-regresi-a38';"));
    }

    // Membangun database yang berhenti di skema versi 2 (migrasi 001+002
    // saja), menyisipkan SATU baris audit format lama (tanpa audit_uid),
    // lalu menjalankan MigrationRunner secara independen untuk memicu
    // backfill migrasi 003 -- mengembalikan audit_uid hasilnya.
    private static string BackfillSatuBarisIndependen(string waktu, int id = 1)
    {
        using var database = new MigratedDatabase(applyAllMigrations: false);
        database.Execute(new Migration001CreateSchema().Sql);
        database.Execute(new Migration002CegahUbahHapusAudit().Sql);
        database.Execute("INSERT INTO skema_versi (versi, diterapkan) VALUES (2, '2026-01-01T00:00:00Z');");
        database.Execute("INSERT INTO akun (id, username, sandi_hash, peran, aktif, dibuat_pada) " +
                          "VALUES (1, 'admin1', 'x', 'admin', 1, '2026-01-01T00:00:00Z');");

        var insert = database.Connection.CreateCommand();
        insert.CommandText = """
            INSERT INTO audit (id, waktu, akun_id, aksi, entitas, entitas_id, nilai_lama, nilai_baru, atas_permintaan)
            VALUES ($id, $waktu, 1, 'perbarui_kelas', 'kelas', 1, NULL, 'nama=7A', NULL);
            """;
        insert.Parameters.AddWithValue("$id", id);
        insert.Parameters.AddWithValue("$waktu", waktu);
        insert.ExecuteNonQuery();

        new MigrationRunner().Run(database.Connection);

        var baca = database.Connection.CreateCommand();
        baca.CommandText = "SELECT audit_uid FROM audit WHERE id = $id;";
        baca.Parameters.AddWithValue("$id", id);
        return (string)baca.ExecuteScalar()!;
    }
}
