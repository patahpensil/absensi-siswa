using Absensi.Data.Gerbang;
using Absensi.Data.Repositori;
using Absensi.Domain.Akun;

namespace Absensi.Data.Tests;

public sealed class GateAccessTests
{
    private static readonly DateTime Time = new(2026, 11, 7, 7, 0, 0);
    private static readonly AkunTersimpan Admin = new(1, "admin1", "x", PeranAkun.Admin, null, true);

    [Theory]
    [InlineData("admin", PeranAkun.Admin)]
    [InlineData("kepala_sekolah", PeranAkun.KepalaSekolah)]
    public void Record_AuthorizedRole_WritesActorAndAction(string storedRole, PeranAkun role)
    {
        using var db = new MigratedDatabase();
        db.SeedKelasUntukRegistrasiKartu();
        using var update = db.Connection.CreateCommand();
        update.CommandText = "UPDATE akun SET peran = $role WHERE id = 1;";
        update.Parameters.AddWithValue("$role", storedRole);
        update.ExecuteNonQuery();
        var service = new GateAccess();

        service.Record(db.Connection, Admin with { Peran = role }, Time, GateAction.StopReceiving);

        using var audit = db.Connection.CreateCommand();
        audit.CommandText = "SELECT COUNT(*) FROM audit WHERE aksi = 'hentikan_penerimaan_tap' AND akun_id = 1 AND entitas = 'gerbang';";
        Assert.Equal(1L, audit.ExecuteScalar());
    }

    [Fact]
    public void EnsureAuthorized_HomeroomTeacher_Rejects()
    {
        using var db = new MigratedDatabase();
        db.SeedKelasUntukRegistrasiKartu();
        var teacher = new RepositoriAkun().CariBerdasarkanId(db.Connection, 2)!;
        Assert.Throws<InvalidOperationException>(() => new GateAccess().EnsureAuthorized(db.Connection, teacher));
    }

    [Fact]
    public void EnsureAuthorized_ForgedAdminRole_UsesStoredRole()
    {
        using var db = new MigratedDatabase();
        db.SeedKelasUntukRegistrasiKartu();
        var teacher = new RepositoriAkun().CariBerdasarkanId(db.Connection, 2)!;
        Assert.Throws<InvalidOperationException>(() => new GateAccess().EnsureAuthorized(db.Connection, teacher with { Peran = PeranAkun.Admin }));
    }

    [Theory]
    [InlineData("UPDATE akun SET aktif = 0 WHERE id = 1;")]
    [InlineData("UPDATE akun SET peran = 'wali_kelas' WHERE id = 1;")]
    public void Record_RevokedSession_RejectsWithoutAudit(string sql)
    {
        using var db = new MigratedDatabase();
        db.SeedKelasUntukRegistrasiKartu();
        using var command = db.Connection.CreateCommand();
        command.CommandText = sql;
        command.ExecuteNonQuery();

        Assert.Throws<InvalidOperationException>(() => new GateAccess().Record(db.Connection, Admin, Time, GateAction.OpenDisplay));

        command.CommandText = "SELECT COUNT(*) FROM audit WHERE entitas = 'gerbang';";
        Assert.Equal(0L, command.ExecuteScalar());
    }

    [Fact]
    public void EnsureAuthorized_NoSession_Rejects()
    {
        using var db = new MigratedDatabase();
        db.SeedKelasUntukRegistrasiKartu();
        Assert.Throws<InvalidOperationException>(() => new GateAccess().EnsureAuthorized(db.Connection, null));
    }

    [Fact]
    public void VerifyPassword_IncorrectPassword_RejectsWithoutAudit()
    {
        using var db = new MigratedDatabase();
        db.SeedKelasUntukRegistrasiKartu();
        Assert.False(new GateAccess().VerifyPassword(db.Connection, Admin, "sandi-tidak-cocok"));
    }
}
