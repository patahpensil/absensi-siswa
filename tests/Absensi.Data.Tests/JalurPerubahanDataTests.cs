using Absensi.Data.Akun;
using Absensi.Domain.Akun;
using Microsoft.Data.Sqlite;

namespace Absensi.Data.Tests;

public sealed class JalurPerubahanDataTests
{
    private static readonly DateTime Waktu = new(2026, 9, 17, 8, 0, 0);

    private readonly JalurPerubahanData _jalurPerubahanData = new();

    [Fact]
    public void Jalankan_MenulisPerubahanDanAuditDalamSatuTransaksi()
    {
        using var database = new MigratedDatabase();
        database.SeedAkunUntukUji();
        var kepsek = new Absensi.Data.Repositori.AkunTersimpan(1, "kepsek", "x", PeranAkun.KepalaSekolah, null, true);

        _jalurPerubahanData.Jalankan(
            database.Connection,
            kepsek,
            Waktu,
            aksi: "uji_tambah_guru",
            entitas: "guru",
            perubahan: transaction => TambahGuruUjiCoba(database.Connection, transaction),
            nilaiBaru: "Guru Uji Coba");

        using var commandGuru = database.Connection.CreateCommand();
        commandGuru.CommandText = "SELECT COUNT(*) FROM guru WHERE nama = 'Guru Uji Coba';";
        Assert.Equal(1, Convert.ToInt32(commandGuru.ExecuteScalar()));

        using var commandAudit = database.Connection.CreateCommand();
        commandAudit.CommandText = "SELECT akun_id, aksi, entitas, entitas_id, nilai_baru FROM audit;";
        using var reader = commandAudit.ExecuteReader();
        Assert.True(reader.Read());
        Assert.Equal(1, reader.GetInt32(0));
        Assert.Equal("uji_tambah_guru", reader.GetString(1));
        Assert.Equal("guru", reader.GetString(2));
        Assert.False(reader.IsDBNull(3));
        Assert.Equal("Guru Uji Coba", reader.GetString(4));
    }

    [Fact]
    public void Jalankan_PerubahanMelemparGalat_TidakMenulisApaPunTermasukAudit()
    {
        using var database = new MigratedDatabase();
        database.SeedAkunUntukUji();
        var kepsek = new Absensi.Data.Repositori.AkunTersimpan(1, "kepsek", "x", PeranAkun.KepalaSekolah, null, true);

        Assert.Throws<InvalidOperationException>(() => _jalurPerubahanData.Jalankan(
            database.Connection,
            kepsek,
            Waktu,
            aksi: "uji_gagal",
            entitas: "guru",
            perubahan: transaction =>
            {
                TambahGuruUjiCoba(database.Connection, transaction);
                throw new InvalidOperationException("Simulasi galat setelah menulis perubahan.");
            }));

        using var commandGuru = database.Connection.CreateCommand();
        commandGuru.CommandText = "SELECT COUNT(*) FROM guru WHERE nama = 'Guru Uji Coba';";
        Assert.Equal(0, Convert.ToInt32(commandGuru.ExecuteScalar()));

        using var commandAudit = database.Connection.CreateCommand();
        commandAudit.CommandText = "SELECT COUNT(*) FROM audit;";
        Assert.Equal(0, Convert.ToInt32(commandAudit.ExecuteScalar()));
    }

    private static int TambahGuruUjiCoba(SqliteConnection connection, SqliteTransaction transaction)
    {
        using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = "INSERT INTO guru (nip, nama) VALUES ('999', 'Guru Uji Coba');";
        command.ExecuteNonQuery();

        using var commandId = connection.CreateCommand();
        commandId.Transaction = transaction;
        commandId.CommandText = "SELECT last_insert_rowid();";
        return Convert.ToInt32(commandId.ExecuteScalar());
    }
}
