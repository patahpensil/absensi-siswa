using Absensi.Data.Pengaturan;
using Absensi.Data.Repositori;
using Absensi.Domain.Akun;

namespace Absensi.Data.Tests;

public sealed class LayananPengaturanAuditTests
{
    private static readonly DateTime Waktu = new(2026, 9, 17, 8, 0, 0);

    [Fact]
    public void Simpan_TokenBot_TersimpanTetapTidakMasukAudit()
    {
        using var database = new MigratedDatabase();
        database.SeedAkunUntukUji();
        var kepsek = new AkunTersimpan(1, "kepsek", "x", PeranAkun.KepalaSekolah, GuruId: null, Aktif: true);
        const string token = "123456:RAHASIA-JANGAN-DI-AUDIT";

        new LayananPengaturan().Simpan(database.Connection, kepsek, Waktu, "token_bot", token);

        using var simpan = database.Connection.CreateCommand();
        simpan.CommandText = "SELECT nilai FROM pengaturan WHERE kunci = 'token_bot';";
        Assert.Equal(token, simpan.ExecuteScalar());

        using var audit = database.Connection.CreateCommand();
        audit.CommandText = "SELECT nilai_baru FROM audit WHERE aksi = 'ubah_pengaturan';";
        var nilaiAudit = (string?)audit.ExecuteScalar();
        Assert.NotNull(nilaiAudit);
        Assert.DoesNotContain("RAHASIA", nilaiAudit);
        Assert.Contains("token_bot", nilaiAudit);
    }

    [Fact]
    public void Simpan_PengaturanBiasa_NilaiTetapTercatatDiAudit()
    {
        using var database = new MigratedDatabase();
        database.SeedAkunUntukUji();
        var kepsek = new AkunTersimpan(1, "kepsek", "x", PeranAkun.KepalaSekolah, GuruId: null, Aktif: true);

        new LayananPengaturan().Simpan(database.Connection, kepsek, Waktu, "toleransi_jadwal_menit", "20");

        using var audit = database.Connection.CreateCommand();
        audit.CommandText = "SELECT nilai_baru FROM audit WHERE aksi = 'ubah_pengaturan';";
        Assert.Equal("toleransi_jadwal_menit=20", audit.ExecuteScalar());
    }
}
