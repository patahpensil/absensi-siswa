using Microsoft.Data.Sqlite;

namespace Absensi.Data.Tests;

// Delapan aturan yang harus ditegakkan oleh struktur database sendiri,
// bukan oleh kode aplikasi (docs/SKEMA-DATABASE.md §10).
public sealed class SchemaConstraintTests
{
    [Fact]
    public void ActiveCard_SecondCardWithSameUid_IsRejected()
    {
        using var database = new MigratedDatabase();
        database.SeedBaselineReferenceRows();
        database.Execute("""
            INSERT INTO kartu (uid, pemilik_tipe, pemilik_id, aktif, didaftar_pada)
            VALUES ('UID-001', 'siswa', 1, 1, '2026-09-17T00:00:00Z');
            """);

        Assert.Throws<SqliteException>(() => database.Execute("""
            INSERT INTO kartu (uid, pemilik_tipe, pemilik_id, aktif, didaftar_pada)
            VALUES ('UID-001', 'guru', 1, 1, '2026-09-17T00:00:00Z');
            """));
    }

    [Fact]
    public void StudentAttendance_SecondRowForSameStudentAndDate_IsRejected()
    {
        using var database = new MigratedDatabase();
        database.SeedBaselineReferenceRows();
        database.Execute("""
            INSERT INTO absensi_siswa (tanggal, siswa_id, kelas_id, status)
            VALUES ('2026-09-17', 1, 1, 'alpa');
            """);

        Assert.Throws<SqliteException>(() => database.Execute("""
            INSERT INTO absensi_siswa (tanggal, siswa_id, kelas_id, status)
            VALUES ('2026-09-17', 1, 1, 'hadir');
            """));
    }

    [Fact]
    public void TeacherAttendance_SecondRowForSameTeacherAndDate_IsRejected()
    {
        using var database = new MigratedDatabase();
        database.SeedBaselineReferenceRows();
        database.Execute("""
            INSERT INTO absensi_guru (tanggal, guru_id, status)
            VALUES ('2026-09-17', 1, 'alpa');
            """);

        Assert.Throws<SqliteException>(() => database.Execute("""
            INSERT INTO absensi_guru (tanggal, guru_id, status)
            VALUES ('2026-09-17', 1, 'masuk');
            """));
    }

    [Fact]
    public void ClassMembership_SecondClassForSameStudentAndSchoolYear_IsRejected()
    {
        using var database = new MigratedDatabase();
        database.SeedBaselineReferenceRows();
        database.Execute("""
            INSERT INTO siswa_kelas (siswa_id, kelas_id, tahun_ajaran_id)
            VALUES (1, 1, 1);
            """);

        Assert.Throws<SqliteException>(() => database.Execute("""
            INSERT INTO siswa_kelas (siswa_id, kelas_id, tahun_ajaran_id)
            VALUES (1, 2, 1);
            """));
    }

    [Fact]
    public void Pairing_SecondLinkedPairingForSameSubject_IsRejected()
    {
        using var database = new MigratedDatabase();
        database.SeedBaselineReferenceRows();
        database.Execute("""
            INSERT INTO pairing (subjek_tipe, subjek_id, token, status, dibuat_pada)
            VALUES ('siswa', 1, 'AAA111', 'tertaut', '2026-09-17T00:00:00Z');
            """);

        Assert.Throws<SqliteException>(() => database.Execute("""
            INSERT INTO pairing (subjek_tipe, subjek_id, token, status, dibuat_pada)
            VALUES ('siswa', 1, 'BBB222', 'tertaut', '2026-09-17T00:00:00Z');
            """));
    }

    [Fact]
    public void Outbox_SecondMessageOfSameKindForSameSubjectAndDate_IsRejected()
    {
        using var database = new MigratedDatabase();
        database.SeedBaselineReferenceRows();
        database.Execute("""
            INSERT INTO outbox (tanggal, subjek_tipe, subjek_id, jenis, chat_id, isi, dibuat_pada)
            VALUES ('2026-09-17', 'siswa', 1, 'hadir', '123', 'isi pesan', '2026-09-17T00:00:00Z');
            """);

        Assert.Throws<SqliteException>(() => database.Execute("""
            INSERT INTO outbox (tanggal, subjek_tipe, subjek_id, jenis, chat_id, isi, dibuat_pada)
            VALUES ('2026-09-17', 'siswa', 1, 'hadir', '123', 'isi pesan lain', '2026-09-17T00:00:00Z');
            """));
    }

    [Fact]
    public void StudentAttendanceStatus_OutsideAllowedList_IsRejected()
    {
        using var database = new MigratedDatabase();
        database.SeedBaselineReferenceRows();

        Assert.Throws<SqliteException>(() => database.Execute("""
            INSERT INTO absensi_siswa (tanggal, siswa_id, kelas_id, status)
            VALUES ('2026-09-17', 1, 1, 'status_tidak_sah');
            """));
    }

    [Fact]
    public void ClassGradeLevel_OutsideOneToNine_IsRejected()
    {
        using var database = new MigratedDatabase();
        database.SeedBaselineReferenceRows();

        Assert.Throws<SqliteException>(() => database.Execute("""
            INSERT INTO kelas (tahun_ajaran_id, nama, tingkat, wali_guru_id)
            VALUES (1, '10A', 10, 1);
            """));
    }
}
