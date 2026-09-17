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

    public static void PastikanFolderDataAda() => Directory.CreateDirectory(FolderData);
}
