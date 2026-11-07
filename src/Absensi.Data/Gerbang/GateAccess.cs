using Absensi.Data.Akun;
using Absensi.Data.Repositori;
using Absensi.Domain.Akun;
using Microsoft.Data.Sqlite;

namespace Absensi.Data.Gerbang;

public enum GateAction { OpenDisplay, CloseDisplay, StartReceiving, StopReceiving }

public sealed class GateAccess
{
    public AkunTersimpan EnsureAuthorized(SqliteConnection connection, AkunTersimpan? account)
    {
        if (account is null || !account.Aktif)
            throw new InvalidOperationException("Hanya kepala sekolah dan admin/TU yang berwenang mengendalikan layar gerbang.");
        AuthorizationGuard.EnsureFullAuthority(account, "mengendalikan layar gerbang");
        var current = new RepositoriAkun().CariBerdasarkanId(connection, account.Id);
        if (current is null || !current.Aktif || current.Peran is not (PeranAkun.Admin or PeranAkun.KepalaSekolah))
            throw new InvalidOperationException("Kewenangan akun untuk mengendalikan gerbang sudah tidak berlaku.");
        return current;
    }

    public bool VerifyPassword(SqliteConnection connection, AkunTersimpan? account, string password)
    {
        var current = EnsureAuthorized(connection, account);
        return new LayananAkun().Autentikasi(connection, current.Username, password)?.Id == current.Id;
    }

    public void Record(SqliteConnection connection, AkunTersimpan? account, DateTime time, GateAction action)
    {
        var current = EnsureAuthorized(connection, account);
        var name = action switch
        {
            GateAction.OpenDisplay => "buka_layar_gerbang",
            GateAction.CloseDisplay => "tutup_layar_gerbang",
            GateAction.StartReceiving => "mulai_penerimaan_tap",
            GateAction.StopReceiving => "hentikan_penerimaan_tap",
            _ => throw new ArgumentOutOfRangeException(nameof(action)),
        };
        new JalurPerubahanData().Jalankan(connection, current, time, name, "gerbang", _ => null,
            nilaiBaru: action.ToString());
    }
}
