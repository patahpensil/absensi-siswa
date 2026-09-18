using Absensi.Data.Lisensi;

namespace Absensi.Data.Tests;

public sealed class RepositoriLisensiTests
{
    private readonly RepositoriLisensi _repositori = new();

    [Fact]
    public void BacaStatus_BelumPernahDitulis_MengembalikanNull()
    {
        using var database = new MigratedDatabase();

        Assert.Null(_repositori.BacaStatus(database.Connection));
    }

    [Fact]
    public void BacaStatus_SetelahTulis_MengembalikanNilaiTersimpan()
    {
        using var database = new MigratedDatabase();
        var waktu = new DateTime(2026, 9, 18, 8, 0, 0);

        using (var transaction = database.Connection.BeginTransaction())
        {
            _repositori.Tulis(database.Connection, transaction, "SEKOLAH-001-ABCD", waktu);
            transaction.Commit();
        }

        var status = _repositori.BacaStatus(database.Connection);

        Assert.NotNull(status);
        Assert.Equal("SEKOLAH-001-ABCD", status!.Kunci);
        Assert.True(status.Aktif);
        Assert.Equal(waktu, status.TanggalAktivasi);
    }
}
