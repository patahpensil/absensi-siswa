using Absensi.App.Koreksi;
using Absensi.Data.Repositori;
using Absensi.Domain.Akun;
using Absensi.Domain.Kehadiran;

namespace Absensi.App.Tests;

// RENCANA-TUGAS.md Tugas 11 — layar Koreksi (PRD §16.10, §10).
public sealed class KoreksiViewModelTests
{
    private static readonly DateTime Waktu = new(2026, 9, 17, 15, 0, 0);
    private static readonly AkunTersimpan Admin = new(1, "admin1", "x", PeranAkun.Admin, null, true);
    private static readonly AkunTersimpan WaliA = new(2, "wali_a", "x", PeranAkun.WaliKelas, 1, true);

    private static void SeedDasar(MigratedDatabase database)
    {
        database.Execute("""
            INSERT INTO tahun_ajaran (id, nama, semester, tgl_mulai, tgl_selesai, aktif)
            VALUES (1, '2026/2027', 'ganjil', '2026-07-01', '2026-12-19', 1);

            INSERT INTO guru (id, nip, nama, aktif) VALUES (1, '111', 'Wali A', 1);
            INSERT INTO kelas (id, tahun_ajaran_id, nama, tingkat, wali_guru_id) VALUES (1, 1, '7A', 7, 1);

            INSERT INTO siswa (id, nisn, nama, jenis_kelamin, aktif) VALUES (3, '0003', 'Siswa Alpa', 'L', 1);

            INSERT INTO absensi_siswa (tanggal, siswa_id, kelas_id, status, sumber)
            VALUES ('2026-09-17', 3, 1, 'alpa', 'sistem');
            """);

        database.TambahAkun(1, "admin1", "admin", guruId: null);
        database.TambahAkun(2, "wali_a", "wali_kelas", guruId: 1);
    }

    [Fact]
    public void Konstruksi_Admin_MelihatSemuaKelas()
    {
        using var database = new MigratedDatabase();
        SeedDasar(database);
        var viewModel = new KoreksiViewModel(database.Connection, Admin, new PenyediaWaktuUji(Waktu), tahunAjaranId: 1);

        Assert.Single(viewModel.DaftarKelas);
    }

    [Fact]
    public void Konstruksi_WaliKelas_HanyaMelihatKelasSendiri()
    {
        using var database = new MigratedDatabase();
        SeedDasar(database);
        var viewModel = new KoreksiViewModel(database.Connection, WaliA, new PenyediaWaktuUji(Waktu), tahunAjaranId: 1);

        Assert.Single(viewModel.DaftarKelas);
        Assert.Equal("7A", viewModel.DaftarKelas[0].Nama);
    }

    [Fact]
    public void TampilkanAtasPermintaan_WaliKelas_False()
    {
        using var database = new MigratedDatabase();
        SeedDasar(database);
        var viewModel = new KoreksiViewModel(database.Connection, WaliA, new PenyediaWaktuUji(Waktu), tahunAjaranId: 1);

        Assert.False(viewModel.TampilkanAtasPermintaan);
    }

    [Fact]
    public void TampilkanAtasPermintaan_Admin_True()
    {
        using var database = new MigratedDatabase();
        SeedDasar(database);
        var viewModel = new KoreksiViewModel(database.Connection, Admin, new PenyediaWaktuUji(Waktu), tahunAjaranId: 1);

        Assert.True(viewModel.TampilkanAtasPermintaan);
    }

    [Fact]
    public void PilihKelasDanSiswaLaluSimpan_StatusTersimpan()
    {
        using var database = new MigratedDatabase();
        SeedDasar(database);
        var viewModel = new KoreksiViewModel(database.Connection, WaliA, new PenyediaWaktuUji(Waktu), tahunAjaranId: 1);
        viewModel.TanggalTerpilih = new DateTime(2026, 9, 17);
        viewModel.KelasTerpilih = viewModel.DaftarKelas[0];
        viewModel.SiswaTerpilih = viewModel.Daftar.Single(s => s.Nama == "Siswa Alpa");
        viewModel.StatusBaru = StatusAbsensiSiswa.Izin;
        viewModel.Keterangan = "Sakit demam";

        viewModel.SimpanCommand.Execute(null);

        using var command = database.Connection.CreateCommand();
        command.CommandText = "SELECT status, keterangan FROM absensi_siswa WHERE siswa_id = 3;";
        using var reader = command.ExecuteReader();
        Assert.True(reader.Read());
        Assert.Equal("izin", reader.GetString(0));
        Assert.Equal("Sakit demam", reader.GetString(1));
    }

    [Fact]
    public void KunciBulanCommand_WaliKelas_TidakBisaDieksekusi()
    {
        using var database = new MigratedDatabase();
        SeedDasar(database);
        var viewModel = new KoreksiViewModel(database.Connection, WaliA, new PenyediaWaktuUji(Waktu), tahunAjaranId: 1);

        Assert.False(viewModel.KunciBulanCommand.CanExecute(null));
    }

    [Fact]
    public void SimpanCommand_BulanTerkunci_TidakBisaDieksekusi()
    {
        using var database = new MigratedDatabase();
        SeedDasar(database);
        new Absensi.Data.Koreksi.LayananKuncimBulan().KunciBulan(database.Connection, Admin, Waktu, 2026, 9);
        var viewModel = new KoreksiViewModel(database.Connection, WaliA, new PenyediaWaktuUji(Waktu), tahunAjaranId: 1);
        viewModel.TanggalTerpilih = new DateTime(2026, 9, 17);
        viewModel.KelasTerpilih = viewModel.DaftarKelas[0];
        viewModel.SiswaTerpilih = viewModel.Daftar.First();

        Assert.True(viewModel.BulanTerkunci);
        Assert.False(viewModel.SimpanCommand.CanExecute(null));
    }
}
