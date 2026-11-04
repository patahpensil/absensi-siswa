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
public sealed class KewenanganViewModelTests
{
    private static readonly DateTime Waktu = new(2026, 9, 17, 8, 0, 0);
    private static readonly AkunTersimpan WaliKelas = new(2, "wali_a", "x", PeranAkun.WaliKelas, 1, true);

    [Fact]
    public void SiswaViewModel_UnggahFotoOlehWaliKelas_DitolakSebelumBerkasDisalin()
    {
        using var database = new MigratedDatabase();
        database.SeedSekolahUntukGerbang();
        var viewModel = new SiswaViewModel(database.Connection, WaliKelas, new PenyediaWaktuUji(Waktu), new StatusModeRegistrasi(), tahunAjaranId: 1);
        var siswa = viewModel.Daftar.Single(s => s.Id == 1);
        var pathSumber = Path.Combine(Path.GetTempPath(), $"foto-uji-wali-{Guid.NewGuid():N}.jpg");
        File.WriteAllBytes(pathSumber, new byte[] { 1, 2, 3, 4, 5 });
        var pathTujuan = Path.Combine(LokasiData.FolderData, "foto", $"{siswa.Nisn}.png");
        var adaSebelum = File.Exists(pathTujuan);
        var fotoPathSebelum = new RepositoriSiswa().CariFotoPath(database.Connection, siswa.Id);

        try
        {
            viewModel.UnggahFotoDariBerkas(siswa, pathSumber);

            Assert.StartsWith("Wali kelas tidak berwenang", viewModel.PesanGalat);
            Assert.Null(viewModel.PesanStatus);
            Assert.Equal(adaSebelum, File.Exists(pathTujuan));
            Assert.Equal(fotoPathSebelum, new RepositoriSiswa().CariFotoPath(database.Connection, siswa.Id));
        }
        finally
        {
            File.Delete(pathSumber);
            if (!adaSebelum && File.Exists(pathTujuan))
            {
                File.Delete(pathTujuan);
            }
        }
    }

    [Fact]
    public void TapKartuTunggal_WaliKelasDaftarkanKartuSiswa_PesanGalatTanpaMenutupDialog()
    {
        using var database = new MigratedDatabase();
        database.SeedSekolahUntukGerbang();
        var viewModel = new TapKartuTunggalViewModel(
            database.Connection, WaliKelas, new PenyediaWaktuUji(Waktu), new StatusModeRegistrasi(),
            tahunAjaranId: 1, JenisPemilikKartu.Siswa, pemilikId: 1, namaPemilik: "Siswa Satu", modeGanti: true);
        var dialogDitutup = false;
        viewModel.Selesai += (_, _) => dialogDitutup = true;

        viewModel.TapUid("UID-BARU-WALI");

        Assert.StartsWith("Wali kelas tidak berwenang", viewModel.PesanGalat);
        Assert.False(dialogDitutup);
        Assert.Null(new RepositoriKartu().CariAktifBerdasarkanUid(database.Connection, "UID-BARU-WALI"));
    }
}
