using System.IO;
using Absensi.App.MasterData;
using Absensi.Data.Repositori;
using Absensi.Domain.Akun;
using ClosedXML.Excel;

namespace Absensi.App.Tests;

// A-12 (Master Bug & UI/UX Register, docs/PRD.md §16.2 langkah 3): command
// unduh template pada SiswaViewModel/GuruViewModel.
// SimpanTemplateKeBerkas(path) dites langsung tanpa SaveFileDialog
// sungguhan -- pola sama seperti AnalisisBerkas (A-8, ImporWizardKegagalanBerkasTests).
public sealed class TemplateUnduhTests
{
    private static readonly DateTime Waktu = new(2026, 9, 17, 8, 0, 0);
    private static readonly AkunTersimpan Admin = new(1, "admin1", "x", PeranAkun.Admin, null, true);

    // ========================= SiswaViewModel =========================

    [Fact]
    public void Siswa_SimpanTemplateKeBerkas_MenulisBerkasXlsxValid()
    {
        using var database = new MigratedDatabase();
        var viewModel = new SiswaViewModel(database.Connection, Admin, new PenyediaWaktuUji(Waktu), new StatusModeRegistrasi(), tahunAjaranId: 1);
        var path = Path.Combine(Path.GetTempPath(), $"template-siswa-simpan-{Guid.NewGuid():N}.xlsx");

        try
        {
            viewModel.SimpanTemplateKeBerkas(path);

            Assert.True(File.Exists(path));
            using var workbook = new XLWorkbook(path);
            Assert.Single(workbook.Worksheets);
            Assert.Null(viewModel.PesanGalat);
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public void Siswa_SimpanTemplateKeBerkas_AksesDitolak_PesanJelasTanpaTeksMentah()
    {
        using var database = new MigratedDatabase();
        var viewModel = new SiswaViewModel(database.Connection, Admin, new PenyediaWaktuUji(Waktu), new StatusModeRegistrasi(), tahunAjaranId: 1);
        var pathDirektori = Path.Combine(Path.GetTempPath(), $"siswa-template-akses-ditolak-{Guid.NewGuid():N}.xlsx");
        Directory.CreateDirectory(pathDirektori);

        try
        {
            viewModel.SimpanTemplateKeBerkas(pathDirektori);

            Assert.Equal("Berkas tidak bisa disimpan karena akses ditolak. Pilih lokasi lain atau periksa izin berkas.", viewModel.PesanGalat);
        }
        finally
        {
            Directory.Delete(pathDirektori);
        }
    }

    [Fact]
    public void Siswa_SimpanTemplateKeBerkas_BerkasTerkunciProsesLain_PesanJelasTanpaTeksMentah()
    {
        using var database = new MigratedDatabase();
        var viewModel = new SiswaViewModel(database.Connection, Admin, new PenyediaWaktuUji(Waktu), new StatusModeRegistrasi(), tahunAjaranId: 1);
        var path = Path.Combine(Path.GetTempPath(), $"template-siswa-terkunci-{Guid.NewGuid():N}.xlsx");
        File.WriteAllBytes(path, new byte[] { 0 });

        try
        {
            using var kunciEksklusif = new FileStream(path, FileMode.Open, FileAccess.ReadWrite, FileShare.None);

            viewModel.SimpanTemplateKeBerkas(path);

            Assert.Equal("Berkas sedang dipakai aplikasi lain (mis. Excel). Tutup berkasnya, lalu coba lagi.", viewModel.PesanGalat);
        }
        finally
        {
            File.Delete(path);
        }
    }

    // ========================= GuruViewModel =========================

    [Fact]
    public void Guru_SimpanTemplateKeBerkas_MenulisBerkasXlsxValid()
    {
        using var database = new MigratedDatabase();
        var viewModel = new GuruViewModel(database.Connection, Admin, new PenyediaWaktuUji(Waktu), new StatusModeRegistrasi(), tahunAjaranId: 1);
        var path = Path.Combine(Path.GetTempPath(), $"template-guru-simpan-{Guid.NewGuid():N}.xlsx");

        try
        {
            viewModel.SimpanTemplateKeBerkas(path);

            Assert.True(File.Exists(path));
            using var workbook = new XLWorkbook(path);
            Assert.Single(workbook.Worksheets);
            Assert.Null(viewModel.PesanGalat);
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public void Guru_SimpanTemplateKeBerkas_AksesDitolak_PesanJelasTanpaTeksMentah()
    {
        using var database = new MigratedDatabase();
        var viewModel = new GuruViewModel(database.Connection, Admin, new PenyediaWaktuUji(Waktu), new StatusModeRegistrasi(), tahunAjaranId: 1);
        var pathDirektori = Path.Combine(Path.GetTempPath(), $"guru-template-akses-ditolak-{Guid.NewGuid():N}.xlsx");
        Directory.CreateDirectory(pathDirektori);

        try
        {
            viewModel.SimpanTemplateKeBerkas(pathDirektori);

            Assert.Equal("Berkas tidak bisa disimpan karena akses ditolak. Pilih lokasi lain atau periksa izin berkas.", viewModel.PesanGalat);
        }
        finally
        {
            Directory.Delete(pathDirektori);
        }
    }

    [Fact]
    public void Guru_SimpanTemplateKeBerkas_BerkasTerkunciProsesLain_PesanJelasTanpaTeksMentah()
    {
        using var database = new MigratedDatabase();
        var viewModel = new GuruViewModel(database.Connection, Admin, new PenyediaWaktuUji(Waktu), new StatusModeRegistrasi(), tahunAjaranId: 1);
        var path = Path.Combine(Path.GetTempPath(), $"template-guru-terkunci-{Guid.NewGuid():N}.xlsx");
        File.WriteAllBytes(path, new byte[] { 0 });

        try
        {
            using var kunciEksklusif = new FileStream(path, FileMode.Open, FileAccess.ReadWrite, FileShare.None);

            viewModel.SimpanTemplateKeBerkas(path);

            Assert.Equal("Berkas sedang dipakai aplikasi lain (mis. Excel). Tutup berkasnya, lalu coba lagi.", viewModel.PesanGalat);
        }
        finally
        {
            File.Delete(path);
        }
    }
}
