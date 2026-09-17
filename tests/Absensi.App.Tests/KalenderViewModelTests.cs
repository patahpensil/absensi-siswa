using Absensi.App.Kalender;
using Absensi.Data.Repositori;
using Absensi.Domain.Akun;

namespace Absensi.App.Tests;

// RENCANA-TUGAS.md Tugas 9 — layar Kalender (PRD §6).
public sealed class KalenderViewModelTests
{
    private static readonly DateTime Waktu = new(2026, 9, 18, 8, 0, 0);
    private static readonly AkunTersimpan Admin = new(1, "admin1", "x", PeranAkun.Admin, null, true);
    private static readonly AkunTersimpan WaliKelas = new(2, "wali_a", "x", PeranAkun.WaliKelas, 1, true);

    private static KalenderViewModel BuatViewModel(MigratedDatabase database, AkunTersimpan? akun = null) =>
        new(database.Connection, akun ?? Admin, new PenyediaWaktuUji(Waktu));

    // PRD §3: kalender pendidikan adalah kewenangan penuh — wali kelas
    // tidak bisa menandai walau sudah memilih tanggal dan mengisi keterangan.
    [Fact]
    public void WaliKelas_TidakBisaMenandaiWalauSudahIsiFormulir()
    {
        using var database = new MigratedDatabase();
        database.SeedKelasUntukRegistrasiKartu();
        var viewModel = BuatViewModel(database, WaliKelas);
        viewModel.TetapkanTanggalTerpilih(new[] { new DateTime(2026, 9, 20) });
        viewModel.Keterangan = "Libur mendadak";

        Assert.False(viewModel.TandaiLiburCommand.CanExecute(null));
        Assert.False(viewModel.TandaiSekolahCommand.CanExecute(null));
        Assert.False(viewModel.BolehMengubah);
    }

    [Fact]
    public void TetapkanTanggalTerpilih_SatuTanggal_TandaiSekolahBisaDieksekusiTandaiLiburBelum()
    {
        using var database = new MigratedDatabase();
        database.SeedKelasUntukRegistrasiKartu();
        var viewModel = BuatViewModel(database);

        viewModel.TetapkanTanggalTerpilih(new[] { new DateTime(2026, 9, 20) });

        Assert.Equal("20 September 2026", viewModel.RentangTeks);
        Assert.True(viewModel.TandaiSekolahCommand.CanExecute(null));
        Assert.False(viewModel.TandaiLiburCommand.CanExecute(null));
    }

    [Fact]
    public void TetapkanTanggalTerpilih_Rentang_RentangTeksMenampilkanAwalDanAkhir()
    {
        using var database = new MigratedDatabase();
        database.SeedKelasUntukRegistrasiKartu();
        var viewModel = BuatViewModel(database);

        viewModel.TetapkanTanggalTerpilih(new[]
        {
            new DateTime(2026, 12, 22), new DateTime(2026, 12, 23), new DateTime(2026, 12, 24),
        });

        Assert.Equal("22 Desember 2026 – 24 Desember 2026", viewModel.RentangTeks);
    }

    [Fact]
    public void TandaiLiburCommand_KeteranganDiisi_TersimpanDanMasukTanggalLibur()
    {
        using var database = new MigratedDatabase();
        database.SeedKelasUntukRegistrasiKartu();
        var viewModel = BuatViewModel(database);
        viewModel.TetapkanTanggalTerpilih(new[] { new DateTime(2026, 9, 20) });
        viewModel.Keterangan = "Libur mendadak banjir";

        viewModel.TandaiLiburCommand.Execute(null);

        Assert.Contains(new DateOnly(2026, 9, 20), viewModel.TanggalLibur);
        Assert.Single(viewModel.DaftarBulanIni);
        Assert.Contains("dibatalkan", viewModel.PesanStatus);
        Assert.Null(viewModel.PesanGalat);
    }

    // Pemanggilan langsung (bukan lewat tombol UI yang sudah dinonaktifkan
    // CanExecute) tetap dijaga di dalam ViewModel — pertahanan berlapis.
    [Fact]
    public void TandaiLiburCommand_TanpaKeterangan_DitolakDenganPesanGalat()
    {
        using var database = new MigratedDatabase();
        database.SeedKelasUntukRegistrasiKartu();
        var viewModel = BuatViewModel(database);
        viewModel.TetapkanTanggalTerpilih(new[] { new DateTime(2026, 9, 20) });

        viewModel.TandaiLiburCommand.Execute(null);

        Assert.NotNull(viewModel.PesanGalat);
        Assert.Empty(viewModel.DaftarBulanIni);
    }

    [Fact]
    public void TandaiSekolahCommand_TanpaKeterangan_Diizinkan()
    {
        using var database = new MigratedDatabase();
        database.SeedKelasUntukRegistrasiKartu();
        var viewModel = BuatViewModel(database);
        viewModel.TetapkanTanggalTerpilih(new[] { new DateTime(2026, 9, 20) });

        viewModel.TandaiSekolahCommand.Execute(null);

        var entri = Assert.Single(viewModel.DaftarBulanIni);
        Assert.Equal(Absensi.Domain.Kehadiran.JenisHariKalender.Sekolah, entri.Jenis);
    }

    [Fact]
    public void PindahBulan_MemuatUlangDaftarUntukBulanBaru()
    {
        using var database = new MigratedDatabase();
        database.SeedKelasUntukRegistrasiKartu();
        var viewModel = BuatViewModel(database);
        viewModel.TetapkanTanggalTerpilih(new[] { new DateTime(2026, 9, 20) });
        viewModel.Keterangan = "Libur A";
        viewModel.TandaiLiburCommand.Execute(null);

        viewModel.PindahBulan(new DateTime(2026, 10, 1));

        Assert.Equal("Oktober 2026", viewModel.BulanTeks);
        Assert.Empty(viewModel.DaftarBulanIni);
    }
}
