using System.IO;

namespace Absensi.App;

// PRD §15: database, foto, dan berkas data sekolah lain hidup di folder
// data/ bersebelahan dengan .exe (bukan ditanam di dalamnya), supaya
// mengganti .exe saat update tidak pernah menimpa data sekolah. Satu
// sumber jalur ini dipakai App.xaml.cs (database) dan CacheTampilanGerbang
// (foto siswa/guru, logo sekolah).
public static class LokasiData
{
    public static string FolderData => Path.Combine(AppContext.BaseDirectory, "data");

    public static string PathDatabase => Path.Combine(FolderData, "absensi.db");

    // PRD §13.3: "database dan seluruh data sekolah berada di folder
    // terpisah (data/, config/, backup/)" — backup/ sengaja SEBELAH data/,
    // bukan di dalamnya, dengan alasan yang sama (penggantian .exe/data
    // tidak boleh menyentuh backup, dan sebaliknya).
    public static string FolderBackup => Path.Combine(AppContext.BaseDirectory, "backup");

    // Stub "cloud" (RENCANA-TUGAS.md Tugas 13 — lihat catatan di
    // IPenyimpanCloudBackup): folder terpisah dari 7 berkas lokal supaya
    // tidak ikut kena rotasi/hitungan PRD §18.2.
    public static string FolderBackupCloudStub => Path.Combine(FolderBackup, "cloud-stub");

    public static void PastikanFolderDataAda() => Directory.CreateDirectory(FolderData);
}
