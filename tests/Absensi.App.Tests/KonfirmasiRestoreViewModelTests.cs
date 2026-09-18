using Absensi.App.Pengaturan;
using Absensi.Data.Backup;

namespace Absensi.App.Tests;

// PRD §18.4 langkah 3: tanggal + hari data yang hilang ditampilkan sebelum
// tombol ditekan, dan aplikasi menutup diri setelah restore harus disebut
// EKSPLISIT (dikonfirmasi pemilik proyek 2026-09-18) — baris tersendiri di
// KonfirmasiRestoreWindow.xaml, bukan disisipkan di akhir kalimat lain.
public sealed class KonfirmasiRestoreViewModelTests
{
    [Fact]
    public void TeksJudul_MenyebutTanggalBerkasFormatIndonesia()
    {
        var berkas = new BerkasBackup("dummy.db", new DateTime(2026, 9, 15));
        var viewModel = new KonfirmasiRestoreViewModel(berkas, hariDataHilang: 3);

        Assert.Equal("Pulihkan dari backup 15 September 2026?", viewModel.TeksJudul);
    }

    [Fact]
    public void HariDataHilang_DisimpanApaAdanya()
    {
        var berkas = new BerkasBackup("dummy.db", new DateTime(2026, 9, 15));
        var viewModel = new KonfirmasiRestoreViewModel(berkas, hariDataHilang: 3);

        Assert.Equal(3, viewModel.HariDataHilang);
    }

    [Fact]
    public void KonfirmasiCommand_Dieksekusi_MemicuEventDikonfirmasi()
    {
        var berkas = new BerkasBackup("dummy.db", new DateTime(2026, 9, 15));
        var viewModel = new KonfirmasiRestoreViewModel(berkas, hariDataHilang: 3);
        var terpicu = false;
        viewModel.Dikonfirmasi += (_, _) => terpicu = true;

        viewModel.KonfirmasiCommand.Execute(null);

        Assert.True(terpicu);
    }
}
