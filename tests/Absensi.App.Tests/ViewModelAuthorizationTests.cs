using System.IO;
using Absensi.App;
using Absensi.App.MasterData;
using Absensi.App.RegistrasiKartu;
using Absensi.Data;
using Absensi.Data.Akun;
using Absensi.Data.Repositori;
using Absensi.Domain.Akun;
using Absensi.Domain.Kehadiran;

namespace Absensi.App.Tests;

// T6: penolakan peran dari lapisan layanan sampai ke operator sebagai pesan,
// tanpa efek samping (berkas foto, kartu) dan tanpa galat tak tertangani.
public sealed class ViewModelAuthorizationTests
{
    private static readonly DateTime Waktu = new(2026, 9, 17, 8, 0, 0);
    private static readonly AkunTersimpan WaliKelas = new(2, "wali_a", "x", PeranAkun.WaliKelas, 1, true);

    [Fact]
    public void StudentViewModel_PhotoUploadByHomeroomTeacher_RejectedBeforeFileIsCopied()
    {
        using var database = new MigratedDatabase();
        database.SeedSekolahUntukGerbang();
        var viewModel = new SiswaViewModel(database.Connection, WaliKelas, new PenyediaWaktuUji(Waktu), new StatusModeRegistrasi(), tahunAjaranId: 1);
        var siswa = viewModel.Daftar.Single(s => s.Id == 1);
        var sourcePath = Path.Combine(Path.GetTempPath(), $"foto-uji-wali-{Guid.NewGuid():N}.jpg");
        File.WriteAllBytes(sourcePath, new byte[] { 1, 2, 3, 4, 5 });
        var targetPath = Path.Combine(LokasiData.FolderData, "foto", $"{siswa.Nisn}.png");
        var existedBefore = File.Exists(targetPath);
        var photoPathBefore = new RepositoriSiswa().CariFotoPath(database.Connection, siswa.Id);

        try
        {
            viewModel.UnggahFotoDariBerkas(siswa, sourcePath);

            Assert.StartsWith("Wali kelas tidak berwenang", viewModel.PesanGalat);
            Assert.Null(viewModel.PesanStatus);
            Assert.Equal(existedBefore, File.Exists(targetPath));
            Assert.Equal(photoPathBefore, new RepositoriSiswa().CariFotoPath(database.Connection, siswa.Id));
        }
        finally
        {
            File.Delete(sourcePath);
            if (!existedBefore && File.Exists(targetPath))
            {
                File.Delete(targetPath);
            }
        }
    }

    [Fact]
    public void SingleCardTap_HomeroomTeacherRegistersStudentCard_ShowsErrorWithoutClosingDialog()
    {
        using var database = new MigratedDatabase();
        database.SeedSekolahUntukGerbang();
        var viewModel = new TapKartuTunggalViewModel(
            database.Connection, WaliKelas, new PenyediaWaktuUji(Waktu), new StatusModeRegistrasi(),
            tahunAjaranId: 1, JenisPemilikKartu.Siswa, pemilikId: 1, namaPemilik: "Siswa Satu", modeGanti: true);
        var dialogClosed = false;
        viewModel.Selesai += (_, _) => dialogClosed = true;

        viewModel.TapUid("UID-BARU-WALI");

        Assert.StartsWith("Wali kelas tidak berwenang", viewModel.PesanGalat);
        Assert.False(dialogClosed);
        Assert.Null(new RepositoriKartu().CariAktifBerdasarkanUid(database.Connection, "UID-BARU-WALI"));
    }
}
