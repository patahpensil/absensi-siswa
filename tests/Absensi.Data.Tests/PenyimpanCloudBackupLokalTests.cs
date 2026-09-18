using Absensi.Data.Backup;

namespace Absensi.Data.Tests;

public sealed class PenyimpanCloudBackupLokalTests : IDisposable
{
    private readonly string _folder = Path.Combine(Path.GetTempPath(), $"absensi-cloud-stub-{Guid.NewGuid():N}");

    [Fact]
    public async Task UnduhTerakhirAsync_BelumPernahDiunggah_MengembalikanNull()
    {
        var penyimpan = new PenyimpanCloudBackupLokal(_folder);

        var hasil = await penyimpan.UnduhTerakhirAsync("KUNCI-1");

        Assert.Null(hasil);
    }

    [Fact]
    public async Task UnduhTerakhirAsync_SetelahUnggah_MengembalikanDataDanWaktu()
    {
        var penyimpan = new PenyimpanCloudBackupLokal(_folder);
        var data = new byte[] { 1, 2, 3, 4 };
        var waktu = new DateTime(2026, 9, 18, 12, 0, 0);

        await penyimpan.UnggahAsync("KUNCI-1", data, waktu);
        var hasil = await penyimpan.UnduhTerakhirAsync("KUNCI-1");

        Assert.NotNull(hasil);
        Assert.Equal(data, hasil!.DataTerenkripsi);
        Assert.Equal(waktu, hasil.WaktuBackup);
    }

    [Fact]
    public async Task UnduhTerakhirAsync_KunciLisensiBerbeda_TidakSalingTerlihat()
    {
        var penyimpan = new PenyimpanCloudBackupLokal(_folder);
        await penyimpan.UnggahAsync("KUNCI-SEKOLAH-A", new byte[] { 9 }, DateTime.UtcNow);

        var hasil = await penyimpan.UnduhTerakhirAsync("KUNCI-SEKOLAH-B");

        Assert.Null(hasil);
    }

    public void Dispose()
    {
        if (Directory.Exists(_folder))
        {
            Directory.Delete(_folder, recursive: true);
        }
    }
}
