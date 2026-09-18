namespace Absensi.Data.Backup;

public sealed record BackupCloudTerakhir(byte[] DataTerenkripsi, DateTime WaktuBackup);

// PRD §18.2/§18.3: "Cloud: server developer untuk tahap awal". Server itu
// belum ada spesifikasinya di dokumen manapun (dikonfirmasi pemilik proyek
// 2026-09-18: bangun dulu dengan implementasi lokal). Antarmuka ini
// memisahkan LayananBackup dari cara pengunggahan sesungguhnya dilakukan,
// supaya implementasi HTTP nanti tinggal disambungkan tanpa mengubah
// LayananBackup atau wizard.
public interface IPenyimpanCloudBackup
{
    Task UnggahAsync(string kunciLisensi, byte[] dataTerenkripsi, DateTime waktuBackup);

    Task<BackupCloudTerakhir?> UnduhTerakhirAsync(string kunciLisensi);
}
