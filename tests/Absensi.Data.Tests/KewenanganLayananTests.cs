using Absensi.Data.Akun;
using Absensi.Data.Kartu;
using Absensi.Data.MasterData;
using Absensi.Data.Notifikasi;
using Absensi.Data.Pengaturan;
using Absensi.Data.Repositori;
using Absensi.Domain.Akun;
using Absensi.Domain.Kehadiran;

namespace Absensi.Data.Tests;

// T6 (PROGRES-DAN-UTANG.md §3) / CLAUDE.md §4.5 aturan 19: peran diperiksa di
// lapisan layanan. PRD §3: wali kelas tidak dapat mengubah master data,
// mendaftarkan kartu, mengimpor, mengubah pengaturan, atau membuat pairing.
// Penolakan harus terjadi sebelum transaksi: tidak ada data maupun audit yang
// tertulis.
public sealed class KewenanganLayananTests
{
    private static readonly DateTime Waktu = new(2026, 9, 17, 8, 0, 0);
    private static readonly AkunTersimpan Admin = new(1, "admin1", "x", PeranAkun.Admin, null, true);
    private static readonly AkunTersimpan KepalaSekolah = new(3, "kepsek", "x", PeranAkun.KepalaSekolah, null, true);
    private static readonly AkunTersimpan WaliKelas = new(2, "wali_a", "x", PeranAkun.WaliKelas, 1, true);

    private static readonly DataSiswa SiswaContoh = new("0011122233", "Siswa Uji", "L", null, null, null, null, null);
    private static readonly DataGuru GuruContoh = new("999", "Guru Uji", null, null);

    [Theory]
    [InlineData(PeranAkun.KepalaSekolah)]
    [InlineData(PeranAkun.Admin)]
    public void PenjagaKewenangan_KepalaSekolahDanAdmin_Diizinkan(PeranAkun peran)
    {
        var akun = new AkunTersimpan(9, "pengguna", "x", peran, null, true);

        PenjagaKewenangan.PastikanKewenanganPenuh(akun, "menguji");
    }

    [Fact]
    public void PenjagaKewenangan_WaliKelas_DitolakDenganPesanMenyebutTindakanDanPrd()
    {
        var galat = Assert.Throws<InvalidOperationException>(
            () => PenjagaKewenangan.PastikanKewenanganPenuh(WaliKelas, "menguji"));

        Assert.Equal("Wali kelas tidak berwenang menguji (PRD §3).", galat.Message);
    }

    [Fact]
    public void LayananSiswa_SemuaOperasiOlehWaliKelas_DitolakTanpaPerubahan()
    {
        using var database = new MigratedDatabase();
        database.SeedMasterDataUntukUji();
        var layanan = new LayananSiswa();
        var id = layanan.Tambah(database.Connection, Admin, Waktu, SiswaContoh);
        var ubahan = SiswaContoh with { Nama = "Nama Diubah" };

        AssertDitolakTanpaAudit(database, () => layanan.Tambah(database.Connection, WaliKelas, Waktu, SiswaContoh with { Nisn = "0099988877" }));
        AssertDitolakTanpaAudit(database, () => layanan.Perbarui(database.Connection, WaliKelas, Waktu, id, ubahan));
        AssertDitolakTanpaAudit(database, () => layanan.TandaiTidakAktif(database.Connection, WaliKelas, Waktu, id, "pindah"));
        AssertDitolakTanpaAudit(database, () => layanan.SimpanFoto(database.Connection, WaliKelas, Waktu, id, "foto.jpg"));

        Assert.Equal("Siswa Uji", BacaTeks(database, $"SELECT nama FROM siswa WHERE id = {id};"));
        Assert.Equal("1", BacaTeks(database, $"SELECT aktif FROM siswa WHERE id = {id};"));
        Assert.Equal("1", BacaTeks(database, "SELECT COUNT(*) FROM siswa;"));
    }

    [Fact]
    public void LayananGuru_SemuaOperasiOlehWaliKelas_DitolakTanpaPerubahan()
    {
        using var database = new MigratedDatabase();
        database.SeedMasterDataUntukUji();
        var layanan = new LayananGuru();
        var id = layanan.Tambah(database.Connection, Admin, Waktu, GuruContoh);

        AssertDitolakTanpaAudit(database, () => layanan.Tambah(database.Connection, WaliKelas, Waktu, GuruContoh with { Nip = "888" }));
        AssertDitolakTanpaAudit(database, () => layanan.Perbarui(database.Connection, WaliKelas, Waktu, id, GuruContoh with { Nama = "Nama Diubah" }));
        AssertDitolakTanpaAudit(database, () => layanan.TandaiTidakAktif(database.Connection, WaliKelas, Waktu, id, "pensiun"));
        AssertDitolakTanpaAudit(database, () => layanan.SimpanFoto(database.Connection, WaliKelas, Waktu, id, "foto.jpg"));

        Assert.Equal("Guru Uji", BacaTeks(database, $"SELECT nama FROM guru WHERE id = {id};"));
        Assert.Equal("1", BacaTeks(database, $"SELECT aktif FROM guru WHERE id = {id};"));
    }

    [Fact]
    public void LayananKelas_TambahDanPerbaruiOlehWaliKelas_DitolakTanpaPerubahan()
    {
        using var database = new MigratedDatabase();
        database.SeedMasterDataUntukUji();
        var layanan = new LayananKelas();

        AssertDitolakTanpaAudit(database, () => layanan.Tambah(database.Connection, WaliKelas, Waktu, tahunAjaranId: 1, "7B", 7, null));
        AssertDitolakTanpaAudit(database, () => layanan.Perbarui(database.Connection, WaliKelas, Waktu, id: 1, "7Z", 7, null));

        Assert.Equal("7A", BacaTeks(database, "SELECT nama FROM kelas WHERE id = 1;"));
        Assert.Equal("1", BacaTeks(database, "SELECT COUNT(*) FROM kelas;"));
    }

    [Fact]
    public void LayananKelas_TambahOlehKepalaSekolah_Diizinkan()
    {
        using var database = new MigratedDatabase();
        database.SeedMasterDataUntukUji();

        var id = new LayananKelas().Tambah(database.Connection, KepalaSekolah, Waktu, tahunAjaranId: 1, "7B", 7, null);

        Assert.Equal("7B", BacaTeks(database, $"SELECT nama FROM kelas WHERE id = {id};"));
    }

    [Fact]
    public void LayananPengaturan_SimpanOlehWaliKelas_DitolakTanpaPerubahan()
    {
        using var database = new MigratedDatabase();
        database.SeedMasterDataUntukUji();

        AssertDitolakTanpaAudit(database, () => new LayananPengaturan().Simpan(database.Connection, WaliKelas, Waktu, "toleransi_jadwal_menit", "20"));

        Assert.Equal("0", BacaTeks(database, "SELECT COUNT(*) FROM pengaturan WHERE kunci = 'toleransi_jadwal_menit' AND nilai = '20';"));
    }

    [Fact]
    public void LayananPairing_BuatTokenDanCetakMassalOlehWaliKelas_Ditolak()
    {
        using var database = new MigratedDatabase();
        database.SeedMasterDataUntukUji();
        var layanan = new LayananPairing();

        AssertDitolakTanpaAudit(database, () => layanan.BuatTokenBaru(database.Connection, WaliKelas, Waktu, JenisPemilikKartu.Siswa, subjekId: 1));
        AssertDitolakTanpaAudit(database, () => layanan.PastikanTokenUntukCetakMassal(database.Connection, WaliKelas, Waktu, JenisPemilikKartu.Siswa, subjekId: 1));
    }

    [Fact]
    public void Impor_SiswaDanGuruOlehWaliKelas_Ditolak()
    {
        using var database = new MigratedDatabase();
        database.SeedMasterDataUntukUji();

        AssertDitolakTanpaAudit(database, () => new ImporSiswaExcel().Impor(database.Connection, WaliKelas, Waktu, tahunAjaranId: 1, Array.Empty<BarisImporSiswa>()));
        AssertDitolakTanpaAudit(database, () => new ImporGuruExcel().Impor(database.Connection, WaliKelas, Waktu, Array.Empty<BarisImporGuru>()));
    }

    [Fact]
    public void RegistrasiKartu_DaftarSatuDanGantiKartuOlehWaliKelas_DitolakTanpaPerubahan()
    {
        using var database = new MigratedDatabase();
        database.SeedKelasUntukRegistrasiKartu();
        var layanan = new LayananRegistrasiKartu();

        AssertDitolakTanpaAudit(database, () => layanan.DaftarkanSatu(database.Connection, WaliKelas, Waktu, "UID-BARU-1", siswaId: 2, "Siswa Belum Satu"));
        AssertDitolakTanpaAudit(database, () => layanan.GantiKartuHilang(database.Connection, WaliKelas, Waktu, JenisPemilikKartu.Siswa, pemilikId: 1, "UID-PENGGANTI"));

        Assert.Null(new RepositoriKartu().CariAktifBerdasarkanUid(database.Connection, "UID-BARU-1"));
        Assert.NotNull(new RepositoriKartu().CariAktifBerdasarkanUid(database.Connection, "UID-SISWA-LAMA"));
    }

    private static void AssertDitolakTanpaAudit(MigratedDatabase database, Action aksi)
    {
        var auditSebelum = BacaTeks(database, "SELECT COUNT(*) FROM audit;");

        var galat = Assert.Throws<InvalidOperationException>(aksi);

        Assert.StartsWith("Wali kelas tidak berwenang", galat.Message);
        Assert.Equal(auditSebelum, BacaTeks(database, "SELECT COUNT(*) FROM audit;"));
    }

    private static string BacaTeks(MigratedDatabase database, string sql)
    {
        using var command = database.Connection.CreateCommand();
        command.CommandText = sql;
        return Convert.ToString(command.ExecuteScalar(), System.Globalization.CultureInfo.InvariantCulture) ?? string.Empty;
    }
}
