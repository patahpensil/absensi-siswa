using Absensi.App.Laporan;
using Absensi.Data.Repositori;
using Absensi.Domain.Akun;
using Absensi.Domain.Laporan;

namespace Absensi.App.Tests;

// Fitur Cetak (terpisah dari Ekspor PDF, instruksi eksplisit pemilik
// proyek 2026-09-25). Cakupan di sini murni logika LaporanViewModel --
// jenis laporan mana memicu Gambar* mana, dan pesan galat untuk laporan
// kosong/orang belum dipilih -- BUKAN PrintDialog/jendela pratinjau WPF
// sungguhan (lihat ILayananCetakLaporan: itu alasan pemisahannya, persis
// supaya bagian ini testable lewat LayananCetakPalsu tanpa dialog cetak
// Windows beneran). Pagination/gambar per halaman sendiri sudah dibuktikan
// di PenulisLaporanTests (Absensi.Laporan.Tests) -- tidak diulang di sini.
public sealed class LaporanViewModelTests
{
    // SeedSekolahUntukGerbang menyeed absensi_siswa/absensi_guru HANYA
    // untuk 2026-09-17 (5 siswa + 1 guru, semua "alpa") -- tanggal lain
    // otomatis jadi kasus "tidak ada data" tanpa seed tambahan.
    private static readonly DateTime TanggalBerdata = new(2026, 9, 17, 8, 0, 0);
    private static readonly DateTime TanggalKosong = new(2026, 9, 18, 8, 0, 0);
    private static readonly AkunTersimpan Admin = new(1, "admin1", "x", PeranAkun.Admin, null, true);

    private sealed class LayananCetakPalsu : ILayananCetakLaporan
    {
        public IReadOnlyList<byte[]>? HalamanTerakhir { get; private set; }

        public string? JudulJobTerakhir { get; private set; }

        public int JumlahPanggilan { get; private set; }

        public void Cetak(IReadOnlyList<byte[]> halamanPng, string judulJob, Action<string> pesanGalat)
        {
            JumlahPanggilan++;
            HalamanTerakhir = halamanPng;
            JudulJobTerakhir = judulJob;
        }
    }

    private static LaporanViewModel BuatViewModel(MigratedDatabase database, DateTime waktu, LayananCetakPalsu layananCetak) =>
        new(database.Connection, Admin, new PenyediaWaktuUji(waktu), tahunAjaranId: 1, layananCetak);

    [Fact]
    public void CetakCommand_Harian_AdaData_MemanggilLayananCetakDenganHalaman()
    {
        using var database = new MigratedDatabase();
        database.SeedSekolahUntukGerbang();
        var layananCetak = new LayananCetakPalsu();
        var viewModel = BuatViewModel(database, TanggalBerdata, layananCetak);

        viewModel.CetakCommand.Execute(null);

        Assert.Equal(1, layananCetak.JumlahPanggilan);
        Assert.NotEmpty(layananCetak.HalamanTerakhir!);
        Assert.Equal("Laporan Harian Siswa", layananCetak.JudulJobTerakhir);
        Assert.Null(viewModel.PesanGalat);
    }

    [Fact]
    public void CetakCommand_Harian_TidakAdaData_PesanJelasTanpaMemanggilLayananCetak()
    {
        using var database = new MigratedDatabase();
        database.SeedSekolahUntukGerbang();
        var layananCetak = new LayananCetakPalsu();
        var viewModel = BuatViewModel(database, TanggalKosong, layananCetak);

        viewModel.CetakCommand.Execute(null);

        Assert.Equal(0, layananCetak.JumlahPanggilan);
        Assert.Equal("Tidak ada data untuk dicetak. Ubah tanggal atau kelas, lalu coba lagi.", viewModel.PesanGalat);
    }

    [Fact]
    public void CetakCommand_Bulanan_AdaData_MemanggilLayananCetak()
    {
        using var database = new MigratedDatabase();
        database.SeedSekolahUntukGerbang();
        var layananCetak = new LayananCetakPalsu();
        var viewModel = BuatViewModel(database, TanggalBerdata, layananCetak);
        viewModel.JenisTerpilih = JenisLaporan.Bulanan;

        viewModel.CetakCommand.Execute(null);

        Assert.Equal(1, layananCetak.JumlahPanggilan);
        Assert.NotEmpty(layananCetak.HalamanTerakhir!);
    }

    [Fact]
    public void CetakCommand_Semester_AdaData_MemanggilLayananCetak()
    {
        using var database = new MigratedDatabase();
        database.SeedSekolahUntukGerbang();
        var layananCetak = new LayananCetakPalsu();
        var viewModel = BuatViewModel(database, TanggalBerdata, layananCetak);
        viewModel.JenisTerpilih = JenisLaporan.Semester;

        viewModel.CetakCommand.Execute(null);

        Assert.Equal(1, layananCetak.JumlahPanggilan);
        Assert.NotEmpty(layananCetak.HalamanTerakhir!);
    }

    [Fact]
    public void CetakCommand_PerOrangSatuOrang_BelumPilihOrang_PesanJelasTanpaMemanggilLayananCetak()
    {
        using var database = new MigratedDatabase();
        database.SeedSekolahUntukGerbang();
        var layananCetak = new LayananCetakPalsu();
        var viewModel = BuatViewModel(database, TanggalBerdata, layananCetak);
        viewModel.JenisTerpilih = JenisLaporan.PerOrang;
        viewModel.CetakSatuOrang = true;

        viewModel.CetakCommand.Execute(null);

        Assert.Equal(0, layananCetak.JumlahPanggilan);
        Assert.Equal("Pilih orang yang akan dicetak terlebih dahulu.", viewModel.PesanGalat);
    }

    [Fact]
    public void CetakCommand_PerOrangSatuOrang_SudahPilihOrang_MemanggilLayananCetakSatuHalaman()
    {
        using var database = new MigratedDatabase();
        database.SeedSekolahUntukGerbang();
        var layananCetak = new LayananCetakPalsu();
        var viewModel = BuatViewModel(database, TanggalBerdata, layananCetak);
        viewModel.JenisTerpilih = JenisLaporan.PerOrang;
        viewModel.CetakSatuOrang = true;
        viewModel.OrangTerpilihId = 1;

        viewModel.CetakCommand.Execute(null);

        Assert.Equal(1, layananCetak.JumlahPanggilan);
        Assert.Single(layananCetak.HalamanTerakhir!); // GambarKartuOrang: satu halaman per orang, satu orang di sini
    }

    [Fact]
    public void CetakCommand_PerOrangBatch_AdaData_MemanggilLayananCetakSesuaiJumlahOrang()
    {
        using var database = new MigratedDatabase();
        database.SeedSekolahUntukGerbang();
        var layananCetak = new LayananCetakPalsu();
        var viewModel = BuatViewModel(database, TanggalBerdata, layananCetak);
        viewModel.JenisTerpilih = JenisLaporan.PerOrang; // CetakSatuOrang tetap bawaan (false) -> batch semua orang.

        viewModel.CetakCommand.Execute(null);

        Assert.Equal(1, layananCetak.JumlahPanggilan);
        Assert.Equal(5, layananCetak.HalamanTerakhir!.Count); // 5 siswa aktif dari SeedSekolahUntukGerbang, satu halaman per orang
    }

    // Ekspor menyetel PesanStatus setelah SaveFileDialog berhasil menulis
    // berkas; Cetak TIDAK PERNAH menyentuh berkas sama sekali, jadi
    // PesanStatus semestinya tetap null -- jendela pratinjau/PrintDialog
    // yang memberi tahu penggunanya sendiri, bukan lewat teks status
    // panel Laporan.
    [Fact]
    public void CetakCommand_TidakMengubahPesanStatus()
    {
        using var database = new MigratedDatabase();
        database.SeedSekolahUntukGerbang();
        var layananCetak = new LayananCetakPalsu();
        var viewModel = BuatViewModel(database, TanggalBerdata, layananCetak);

        viewModel.CetakCommand.Execute(null);

        Assert.Null(viewModel.PesanStatus);
    }
}
