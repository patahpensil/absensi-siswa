using System.Security.Cryptography;
using System.Text;

namespace Absensi.Data.Backup;

// Implementasi sementara IPenyimpanCloudBackup selama server developer
// belum ada (lihat catatan di IPenyimpanCloudBackup): "unggah" hanya
// menulis ke folder lokal, dikelompokkan per kunci lisensi supaya jalur
// pemulihan cloud (PRD §18.3: "masukkan kunci lisensi → ambil backup
// terakhir") tetap bisa diuji dan didemokan di satu mesin yang sama.
public sealed class PenyimpanCloudBackupLokal : IPenyimpanCloudBackup
{
    private readonly string _folderStub;

    public PenyimpanCloudBackupLokal(string folderStub)
    {
        _folderStub = folderStub;
    }

    public Task UnggahAsync(string kunciLisensi, byte[] dataTerenkripsi, DateTime waktuBackup)
    {
        var folder = FolderUntukKunci(kunciLisensi);
        Directory.CreateDirectory(folder);
        File.WriteAllBytes(Path.Combine(folder, "terakhir.enc"), dataTerenkripsi);
        File.WriteAllText(Path.Combine(folder, "terakhir.txt"), waktuBackup.ToString("o"));
        return Task.CompletedTask;
    }

    public Task<BackupCloudTerakhir?> UnduhTerakhirAsync(string kunciLisensi)
    {
        var folder = FolderUntukKunci(kunciLisensi);
        var pathData = Path.Combine(folder, "terakhir.enc");
        var pathWaktu = Path.Combine(folder, "terakhir.txt");

        if (!File.Exists(pathData) || !File.Exists(pathWaktu))
        {
            return Task.FromResult<BackupCloudTerakhir?>(null);
        }

        var data = File.ReadAllBytes(pathData);
        var waktu = DateTime.Parse(File.ReadAllText(pathWaktu));
        return Task.FromResult<BackupCloudTerakhir?>(new BackupCloudTerakhir(data, waktu));
    }

    // Nama folder tidak boleh langsung memakai kunci lisensi mentah (bisa
    // memuat karakter yang tidak sah untuk nama folder Windows).
    private string FolderUntukKunci(string kunciLisensi)
    {
        var namaAman = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(kunciLisensi)));
        return Path.Combine(_folderStub, namaAman);
    }
}
