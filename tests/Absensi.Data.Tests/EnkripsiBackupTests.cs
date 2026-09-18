using Absensi.Data.Backup;

namespace Absensi.Data.Tests;

public sealed class EnkripsiBackupTests
{
    [Fact]
    public void Dekripsi_SetelahEnkripsi_MengembalikanDataAsli()
    {
        var data = "isi database contoh"u8.ToArray();

        var terenkripsi = EnkripsiBackup.Enkripsi(data, "KUNCI-LISENSI-1");
        var hasil = EnkripsiBackup.Dekripsi(terenkripsi, "KUNCI-LISENSI-1");

        Assert.Equal(data, hasil);
    }

    [Fact]
    public void Enkripsi_DipanggilDuaKaliDenganDataSama_MenghasilkanKeluaranBerbeda()
    {
        var data = "isi database contoh"u8.ToArray();

        var pertama = EnkripsiBackup.Enkripsi(data, "KUNCI-LISENSI-1");
        var kedua = EnkripsiBackup.Enkripsi(data, "KUNCI-LISENSI-1");

        Assert.NotEqual(pertama, kedua);
    }

    [Fact]
    public void Dekripsi_KunciLisensiSalah_Ditolak()
    {
        var data = "isi database contoh"u8.ToArray();
        var terenkripsi = EnkripsiBackup.Enkripsi(data, "KUNCI-LISENSI-1");

        Assert.ThrowsAny<Exception>(() => EnkripsiBackup.Dekripsi(terenkripsi, "KUNCI-LAIN"));
    }
}
