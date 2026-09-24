using System.IO;
using Absensi.Data.Diagnostik;

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

    // Master Bug & UI/UX Register A-39: arsip riwayat audit pra-restore
    // (audit+akun saja) — subfolder TERPISAH dari 7 berkas backup biasa,
    // TIDAK ikut rotasi, TIDAK dihapus otomatis (lihat LayananArsipAudit
    // dan docs/SKEMA-DATABASE.md §9 untuk retensi/pemeriksaan ruang).
    public static string FolderArsipAudit => Path.Combine(FolderBackup, "arsip-audit");

    // docs/INVESTIGASI-CRASH-2026-09-21.md §4.1: sumber kebenarannya sengaja
    // di Absensi.Data (PencatatGalat.FolderLogBawaan), bukan didefinisikan
    // ulang di sini — supaya Absensi.Telegram (yang tidak boleh
    // mereferensikan Absensi.App) tetap memakai jalur yang sama persis
    // tanpa referensi baru. Properti ini sekadar alias yang sejajar dengan
    // FolderData/FolderBackup di atas untuk kenyamanan baca App.xaml.cs.
    public static string FolderLog => PencatatGalat.FolderLogBawaan;

    public static void PastikanFolderDataAda() => Directory.CreateDirectory(FolderData);

    public static void PastikanFolderLogAda() => Directory.CreateDirectory(FolderLog);
}
