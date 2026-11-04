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
public sealed class ServiceAuthorizationTests
{
    private static readonly DateTime Waktu = new(2026, 9, 17, 8, 0, 0);
    private static readonly AkunTersimpan Admin = new(1, "admin1", "x", PeranAkun.Admin, null, true);
    private static readonly AkunTersimpan KepalaSekolah = new(3, "kepsek", "x", PeranAkun.KepalaSekolah, null, true);
    private static readonly AkunTersimpan WaliKelas = new(2, "wali_a", "x", PeranAkun.WaliKelas, 1, true);

    private static readonly DataSiswa SampleStudent = new("0011122233", "Siswa Uji", "L", null, null, null, null, null);
    private static readonly DataGuru SampleTeacher = new("999", "Guru Uji", null, null);

    [Theory]
    [InlineData(PeranAkun.KepalaSekolah)]
    [InlineData(PeranAkun.Admin)]
    public void Guard_PrincipalAndAdmin_Allowed(PeranAkun peran)
    {
        var akun = new AkunTersimpan(9, "pengguna", "x", peran, null, true);

        AuthorizationGuard.EnsureFullAuthority(akun, "menguji");
    }

    [Fact]
    public void Guard_HomeroomTeacher_RejectedWithActionAndPrdInMessage()
    {
        var error = Assert.Throws<InvalidOperationException>(
            () => AuthorizationGuard.EnsureFullAuthority(WaliKelas, "menguji"));

        Assert.Equal("Wali kelas tidak berwenang menguji (PRD §3).", error.Message);
    }

    [Fact]
    public void StudentService_AllOperationsByHomeroomTeacher_RejectedWithoutChanges()
    {
        using var database = new MigratedDatabase();
        database.SeedMasterDataUntukUji();
        var layanan = new LayananSiswa();
        var id = layanan.Tambah(database.Connection, Admin, Waktu, SampleStudent);
        var changed = SampleStudent with { Nama = "Nama Diubah" };

        AssertRejectedWithoutAudit(database, () => layanan.Tambah(database.Connection, WaliKelas, Waktu, SampleStudent with { Nisn = "0099988877" }));
        AssertRejectedWithoutAudit(database, () => layanan.Perbarui(database.Connection, WaliKelas, Waktu, id, changed));
        AssertRejectedWithoutAudit(database, () => layanan.TandaiTidakAktif(database.Connection, WaliKelas, Waktu, id, "pindah"));
        AssertRejectedWithoutAudit(database, () => layanan.SimpanFoto(database.Connection, WaliKelas, Waktu, id, "foto.jpg"));

        Assert.Equal("Siswa Uji", ReadScalarText(database, $"SELECT nama FROM siswa WHERE id = {id};"));
        Assert.Equal("1", ReadScalarText(database, $"SELECT aktif FROM siswa WHERE id = {id};"));
        Assert.Equal("1", ReadScalarText(database, "SELECT COUNT(*) FROM siswa;"));
    }

    [Fact]
    public void TeacherService_AllOperationsByHomeroomTeacher_RejectedWithoutChanges()
    {
        using var database = new MigratedDatabase();
        database.SeedMasterDataUntukUji();
        var layanan = new LayananGuru();
        var id = layanan.Tambah(database.Connection, Admin, Waktu, SampleTeacher);

        AssertRejectedWithoutAudit(database, () => layanan.Tambah(database.Connection, WaliKelas, Waktu, SampleTeacher with { Nip = "888" }));
        AssertRejectedWithoutAudit(database, () => layanan.Perbarui(database.Connection, WaliKelas, Waktu, id, SampleTeacher with { Nama = "Nama Diubah" }));
        AssertRejectedWithoutAudit(database, () => layanan.TandaiTidakAktif(database.Connection, WaliKelas, Waktu, id, "pensiun"));
        AssertRejectedWithoutAudit(database, () => layanan.SimpanFoto(database.Connection, WaliKelas, Waktu, id, "foto.jpg"));

        Assert.Equal("Guru Uji", ReadScalarText(database, $"SELECT nama FROM guru WHERE id = {id};"));
        Assert.Equal("1", ReadScalarText(database, $"SELECT aktif FROM guru WHERE id = {id};"));
    }

    [Fact]
    public void ClassService_AddAndUpdateByHomeroomTeacher_RejectedWithoutChanges()
    {
        using var database = new MigratedDatabase();
        database.SeedMasterDataUntukUji();
        var layanan = new LayananKelas();

        AssertRejectedWithoutAudit(database, () => layanan.Tambah(database.Connection, WaliKelas, Waktu, tahunAjaranId: 1, "7B", 7, null));
        AssertRejectedWithoutAudit(database, () => layanan.Perbarui(database.Connection, WaliKelas, Waktu, id: 1, "7Z", 7, null));

        Assert.Equal("7A", ReadScalarText(database, "SELECT nama FROM kelas WHERE id = 1;"));
        Assert.Equal("1", ReadScalarText(database, "SELECT COUNT(*) FROM kelas;"));
    }

    [Fact]
    public void ClassService_AddByPrincipal_Allowed()
    {
        using var database = new MigratedDatabase();
        database.SeedMasterDataUntukUji();
        // Aktor audit wajib ada di tabel akun (FK audit.akun_id).
        database.Execute("INSERT INTO akun (id, username, sandi_hash, peran, guru_id, aktif, dibuat_pada) VALUES (3, 'kepsek', 'x', 'kepala_sekolah', NULL, 1, '2026-09-01T00:00:00');");

        var id = new LayananKelas().Tambah(database.Connection, KepalaSekolah, Waktu, tahunAjaranId: 1, "7B", 7, null);

        Assert.Equal("7B", ReadScalarText(database, $"SELECT nama FROM kelas WHERE id = {id};"));
    }

    [Fact]
    public void SettingsService_SaveByHomeroomTeacher_RejectedWithoutChanges()
    {
        using var database = new MigratedDatabase();
        database.SeedMasterDataUntukUji();

        AssertRejectedWithoutAudit(database, () => new LayananPengaturan().Simpan(database.Connection, WaliKelas, Waktu, "toleransi_jadwal_menit", "20"));

        Assert.Equal("0", ReadScalarText(database, "SELECT COUNT(*) FROM pengaturan WHERE kunci = 'toleransi_jadwal_menit' AND nilai = '20';"));
    }

    [Fact]
    public void PairingService_CreateTokenAndBulkPrintByHomeroomTeacher_Rejected()
    {
        using var database = new MigratedDatabase();
        database.SeedMasterDataUntukUji();
        var layanan = new LayananPairing();

        AssertRejectedWithoutAudit(database, () => layanan.BuatTokenBaru(database.Connection, WaliKelas, Waktu, JenisPemilikKartu.Siswa, subjekId: 1));
        AssertRejectedWithoutAudit(database, () => layanan.PastikanTokenUntukCetakMassal(database.Connection, WaliKelas, Waktu, JenisPemilikKartu.Siswa, subjekId: 1));
    }

    [Fact]
    public void Import_StudentsAndTeachersByHomeroomTeacher_Rejected()
    {
        using var database = new MigratedDatabase();
        database.SeedMasterDataUntukUji();

        AssertRejectedWithoutAudit(database, () => new ImporSiswaExcel().Impor(database.Connection, WaliKelas, Waktu, tahunAjaranId: 1, Array.Empty<BarisImporSiswa>()));
        AssertRejectedWithoutAudit(database, () => new ImporGuruExcel().Impor(database.Connection, WaliKelas, Waktu, Array.Empty<BarisImporGuru>()));
    }

    [Fact]
    public void CardRegistration_RegisterAndReplaceByHomeroomTeacher_RejectedWithoutChanges()
    {
        using var database = new MigratedDatabase();
        database.SeedKelasUntukRegistrasiKartu();
        var layanan = new LayananRegistrasiKartu();

        AssertRejectedWithoutAudit(database, () => layanan.DaftarkanSatu(database.Connection, WaliKelas, Waktu, "UID-BARU-1", siswaId: 2, "Siswa Belum Satu"));
        AssertRejectedWithoutAudit(database, () => layanan.GantiKartuHilang(database.Connection, WaliKelas, Waktu, JenisPemilikKartu.Siswa, pemilikId: 1, "UID-PENGGANTI"));

        Assert.Null(new RepositoriKartu().CariAktifBerdasarkanUid(database.Connection, "UID-BARU-1"));
        Assert.NotNull(new RepositoriKartu().CariAktifBerdasarkanUid(database.Connection, "UID-SISWA-LAMA"));
    }

    private static void AssertRejectedWithoutAudit(MigratedDatabase database, Action action)
    {
        var auditBefore = ReadScalarText(database, "SELECT COUNT(*) FROM audit;");

        var error = Assert.Throws<InvalidOperationException>(action);

        Assert.StartsWith("Wali kelas tidak berwenang", error.Message);
        Assert.Equal(auditBefore, ReadScalarText(database, "SELECT COUNT(*) FROM audit;"));
    }

    private static string ReadScalarText(MigratedDatabase database, string sql)
    {
        using var command = database.Connection.CreateCommand();
        command.CommandText = sql;
        return Convert.ToString(command.ExecuteScalar(), System.Globalization.CultureInfo.InvariantCulture) ?? string.Empty;
    }
}
