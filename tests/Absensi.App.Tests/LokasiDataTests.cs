using System.IO;

namespace Absensi.App.Tests;

// Koreksi Tugas 6: database di data/absensi.db (PRD §15), bukan langsung
// di sebelah .exe, dengan folder data/ dibuat otomatis kalau belum ada.
public sealed class LokasiDataTests
{
    [Fact]
    public void FolderData_BeradaDiBawahBaseDirectory()
    {
        Assert.Equal(Path.Combine(AppContext.BaseDirectory, "data"), LokasiData.FolderData);
    }

    [Fact]
    public void PathDatabase_BeradaDiDalamFolderData()
    {
        Assert.Equal(Path.Combine(LokasiData.FolderData, "absensi.db"), LokasiData.PathDatabase);
    }

    [Fact]
    public void PastikanFolderDataAda_MembuatFolderKalauBelumAda()
    {
        if (Directory.Exists(LokasiData.FolderData))
        {
            Directory.Delete(LokasiData.FolderData, recursive: true);
        }

        LokasiData.PastikanFolderDataAda();

        Assert.True(Directory.Exists(LokasiData.FolderData));
    }

    [Fact]
    public void PastikanFolderDataAda_DipanggilBerulang_TidakMelemparGalat()
    {
        LokasiData.PastikanFolderDataAda();

        var pengecualian = Record.Exception(() => LokasiData.PastikanFolderDataAda());

        Assert.Null(pengecualian);
        Assert.True(Directory.Exists(LokasiData.FolderData));
    }

    // RENCANA-TUGAS.md Tugas 13 — PRD §13.3: backup/ sebelah data/, bukan
    // di dalamnya.
    [Fact]
    public void FolderBackup_SebelahFolderDataBukanDiDalamnya()
    {
        Assert.Equal(Path.Combine(AppContext.BaseDirectory, "backup"), LokasiData.FolderBackup);
    }

    [Fact]
    public void FolderBackupCloudStub_BeradaDiDalamFolderBackup()
    {
        Assert.Equal(Path.Combine(LokasiData.FolderBackup, "cloud-stub"), LokasiData.FolderBackupCloudStub);
    }
}
