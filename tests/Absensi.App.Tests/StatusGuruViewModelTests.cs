using Absensi.App.Koreksi;
using Absensi.Data.Repositori;
using Absensi.Domain.Akun;
using Absensi.Domain.Kehadiran;

namespace Absensi.App.Tests;

// RENCANA-TUGAS.md Tugas 11 — layar Guru→Kehadiran (PRD §16.17, §17.10).
public sealed class StatusGuruViewModelTests
{
    private static readonly DateTime Waktu = new(2026, 9, 17, 8, 0, 0);
    private static readonly AkunTersimpan Kepsek = new(1, "kepsek", "x", PeranAkun.KepalaSekolah, null, true);
    private static readonly AkunTersimpan Admin = new(2, "admin1", "x", PeranAkun.Admin, null, true);

    private static void SeedDasar(MigratedDatabase database)
    {
        database.Execute("""
            INSERT INTO tahun_ajaran (id, nama, semester, tgl_mulai, tgl_selesai, aktif)
            VALUES (1, '2026/2027', 'ganjil', '2026-07-01', '2026-12-19', 1);

            INSERT INTO guru (id, nip, nama, aktif) VALUES (1, '111', 'Guru Alpa', 1);

            INSERT INTO absensi_guru (tanggal, guru_id, status, sumber)
            VALUES ('2026-09-17', 1, 'alpa', 'sistem');
            """);

        database.TambahAkun(1, "kepsek", "kepala_sekolah", guruId: null);
    }

    [Fact]
    public void BolehMengubah_Kepsek_True()
    {
        using var database = new MigratedDatabase();
        SeedDasar(database);
        var viewModel = new StatusGuruViewModel(database.Connection, Kepsek, new PenyediaWaktuUji(Waktu));

        Assert.True(viewModel.BolehMengubah);
        Assert.False(viewModel.TidakBolehMengubah);
    }

    [Fact]
    public void BolehMengubah_Admin_False()
    {
        using var database = new MigratedDatabase();
        SeedDasar(database);
        var viewModel = new StatusGuruViewModel(database.Connection, Admin, new PenyediaWaktuUji(Waktu));

        Assert.False(viewModel.BolehMengubah);
        Assert.True(viewModel.TidakBolehMengubah);
    }

    [Fact]
    public void SimpanCommand_Admin_TidakBisaDieksekusi()
    {
        using var database = new MigratedDatabase();
        SeedDasar(database);
        var viewModel = new StatusGuruViewModel(database.Connection, Admin, new PenyediaWaktuUji(Waktu))
        {
            GuruTerpilih = new GuruUntukKehadiran(1, "111", "Guru Alpa", StatusAbsensiGuru.Alpa, null),
        };

        Assert.False(viewModel.SimpanCommand.CanExecute(null));
    }

    [Fact]
    public void PilihGuruLaluSimpan_Kepsek_StatusTersimpan()
    {
        using var database = new MigratedDatabase();
        SeedDasar(database);
        var viewModel = new StatusGuruViewModel(database.Connection, Kepsek, new PenyediaWaktuUji(Waktu));
        viewModel.GuruTerpilih = viewModel.Daftar.Single();
        viewModel.StatusBaru = StatusAbsensiGuru.Sakit;
        viewModel.Keterangan = "Demam";

        viewModel.SimpanCommand.Execute(null);

        using var command = database.Connection.CreateCommand();
        command.CommandText = "SELECT status, keterangan FROM absensi_guru WHERE guru_id = 1;";
        using var reader = command.ExecuteReader();
        Assert.True(reader.Read());
        Assert.Equal("sakit", reader.GetString(0));
        Assert.Equal("Demam", reader.GetString(1));
    }
}
