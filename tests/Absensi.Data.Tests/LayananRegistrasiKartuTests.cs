using Absensi.Data.Akun;
using Absensi.Data.Kartu;
using Absensi.Data.Repositori;
using Absensi.Domain.Akun;
using Absensi.Domain.Kehadiran;
using Microsoft.Data.Sqlite;

namespace Absensi.Data.Tests;

// RENCANA-TUGAS.md Tugas 8 — Registrasi Kartu (PRD §16.5, §16.11, §17.9,
// §17.10). Alur UI (kursor per kelas, progres) diuji di Absensi.App.Tests;
// di sini hanya lapisan data: pemeriksaan lintas master, atomicity sesi,
// dan penggantian kartu hilang.
public sealed class LayananRegistrasiKartuTests
{
    private static readonly DateTime Waktu = new(2026, 9, 17, 8, 0, 0);
    private static readonly AkunTersimpan Admin = new(1, "admin1", "x", PeranAkun.Admin, null, true);
    private static readonly AkunTersimpan WaliKelas = new(2, "wali_a", "x", PeranAkun.WaliKelas, 1, true);

    [Fact]
    public void CariPemilikAktif_UidBebas_Null()
    {
        using var database = new MigratedDatabase();
        database.SeedKelasUntukRegistrasiKartu();
        var layanan = new LayananRegistrasiKartu();

        Assert.Null(layanan.CariPemilikAktif(database.Connection, "UID-BEBAS", tahunAjaranId: 1));
    }

    [Fact]
    public void CariPemilikAktif_UidSiswaTerpakai_KembalikanNamaDanKelas()
    {
        using var database = new MigratedDatabase();
        database.SeedKelasUntukRegistrasiKartu();
        var layanan = new LayananRegistrasiKartu();

        var pemilik = layanan.CariPemilikAktif(database.Connection, "UID-SISWA-LAMA", tahunAjaranId: 1);

        Assert.NotNull(pemilik);
        Assert.Equal(JenisPemilikKartu.Siswa, pemilik!.Tipe);
        Assert.Equal("Siswa Sudah Kartu", pemilik.Nama);
        Assert.Equal("7A", pemilik.KelasNama);
    }

    [Fact]
    public void CariPemilikAktif_UidGuruTerpakai_KembalikanNamaTanpaKelas()
    {
        using var database = new MigratedDatabase();
        database.SeedKelasUntukRegistrasiKartu();
        var layanan = new LayananRegistrasiKartu();

        var pemilik = layanan.CariPemilikAktif(database.Connection, "UID-GURU-LAMA", tahunAjaranId: 1);

        Assert.NotNull(pemilik);
        Assert.Equal(JenisPemilikKartu.Guru, pemilik!.Tipe);
        Assert.Equal("Guru Sudah Kartu", pemilik.Nama);
        Assert.Null(pemilik.KelasNama);
    }

    [Fact]
    public void DaftarkanSesi_DuaPendaftaran_KeduanyaTersimpanSatuBarisAudit()
    {
        using var database = new MigratedDatabase();
        database.SeedKelasUntukRegistrasiKartu();
        var layanan = new LayananRegistrasiKartu();

        layanan.DaftarkanSesi(database.Connection, Admin, Waktu, new[]
        {
            new PendaftaranKartu("UID-BARU-1", JenisPemilikKartu.Siswa, 2, "Siswa Belum Satu"),
            new PendaftaranKartu("UID-BARU-2", JenisPemilikKartu.Siswa, 3, "Siswa Belum Dua"),
        });

        var repositoriKartu = new RepositoriKartu();
        Assert.NotNull(repositoriKartu.CariAktifBerdasarkanUid(database.Connection, "UID-BARU-1"));
        Assert.NotNull(repositoriKartu.CariAktifBerdasarkanUid(database.Connection, "UID-BARU-2"));
        AssertAdaBarisAudit(database, "daftar_kartu", jumlahDiharapkan: 1);
    }

    // PRD §16.3 (impor utuh) + PRD §16.5 langkah 5, sesuai
    // keputusan pemilik proyek 2026-09-18: kalau satu baris gagal, TIDAK
    // ADA yang tersimpan — bukan tersimpan sebagian.
    [Fact]
    public void DaftarkanSesi_SatuUidBentrokDenganBarisLainDalamSesi_TidakAdaYangTersimpan()
    {
        using var database = new MigratedDatabase();
        database.SeedKelasUntukRegistrasiKartu();
        var layanan = new LayananRegistrasiKartu();

        Assert.ThrowsAny<SqliteException>(() => layanan.DaftarkanSesi(database.Connection, Admin, Waktu, new[]
        {
            new PendaftaranKartu("UID-BARU-1", JenisPemilikKartu.Siswa, 2, "Siswa Belum Satu"),
            new PendaftaranKartu("UID-BARU-1", JenisPemilikKartu.Siswa, 3, "Siswa Belum Dua"),
        }));

        Assert.Null(new RepositoriKartu().CariAktifBerdasarkanUid(database.Connection, "UID-BARU-1"));
    }

    [Fact]
    public void GantiKartuHilang_KartuLamaDinonaktifkanBukanDihapusDanKartuBaruAktif()
    {
        using var database = new MigratedDatabase();
        database.SeedKelasUntukRegistrasiKartu();
        var layanan = new LayananRegistrasiKartu();

        layanan.GantiKartuHilang(database.Connection, Admin, Waktu, JenisPemilikKartu.Siswa, pemilikId: 1, uidBaru: "UID-BARU-PENGGANTI");

        using var command = database.Connection.CreateCommand();
        command.CommandText = "SELECT aktif, alasan FROM kartu WHERE uid = 'UID-SISWA-LAMA';";
        using var reader = command.ExecuteReader();
        Assert.True(reader.Read());
        Assert.Equal(0, reader.GetInt32(0));
        Assert.Equal("hilang", reader.GetString(1));

        var kartuBaru = new RepositoriKartu().CariAktifBerdasarkanUid(database.Connection, "UID-BARU-PENGGANTI");
        Assert.NotNull(kartuBaru);
        Assert.Equal(1, kartuBaru!.PemilikId);
        AssertAdaBarisAudit(database, "ganti_kartu", jumlahDiharapkan: 1);
    }

    // PRD §3 dan §17.10: wali kelas tidak berwenang mendaftarkan/mengganti
    // kartu, baik kartu guru maupun kartu siswa.
    [Fact]
    public void DaftarkanSesi_WaliKelasDaftarkanKartuGuru_Ditolak()
    {
        using var database = new MigratedDatabase();
        database.SeedKelasUntukRegistrasiKartu();
        var layanan = new LayananRegistrasiKartu();

        Assert.Throws<InvalidOperationException>(() => layanan.DaftarkanSesi(database.Connection, WaliKelas, Waktu, new[]
        {
            new PendaftaranKartu("UID-BARU-GURU", JenisPemilikKartu.Guru, 1, "Guru Belum Kartu"),
        }));
    }

    // Sebelumnya tes ini mengizinkan wali kelas mendaftarkan kartu siswa.
    // PRD §3 menyatakan wali kelas tidak dapat mendaftarkan kartu, dan PRD
    // yang menang atas perilaku lama (PRD §21.12).
    [Fact]
    public void DaftarkanSesi_WaliKelasDaftarkanKartuSiswa_Ditolak()
    {
        using var database = new MigratedDatabase();
        database.SeedKelasUntukRegistrasiKartu();
        var layanan = new LayananRegistrasiKartu();

        Assert.Throws<InvalidOperationException>(() => layanan.DaftarkanSesi(database.Connection, WaliKelas, Waktu, new[]
        {
            new PendaftaranKartu("UID-BARU-1", JenisPemilikKartu.Siswa, 2, "Siswa Belum Satu"),
        }));

        Assert.Null(new RepositoriKartu().CariAktifBerdasarkanUid(database.Connection, "UID-BARU-1"));
    }

    [Theory]
    [InlineData(PeranAkun.Admin)]
    [InlineData(PeranAkun.KepalaSekolah)]
    public void RegisterTeacherCard_AuthorizedAccount_PreservesUidAndWritesAudit(PeranAkun role)
    {
        using var database = new MigratedDatabase();
        database.SeedKelasUntukRegistrasiKartu();
        var account = Admin with { Peran = role };
        var service = new LayananRegistrasiKartu();

        service.RegisterTeacherCard(database.Connection, account, Waktu, "0001338253", 1);

        var owner = service.CariPemilikAktif(database.Connection, "0001338253", 1);
        Assert.NotNull(owner);
        Assert.Equal(JenisPemilikKartu.Guru, owner!.Tipe);
        Assert.Equal(1, owner.Id);
        Assert.Equal("Guru Belum Kartu", owner.Nama);
        AssertAdaBarisAudit(database, "daftar_kartu", 1);
        using var audit = database.Connection.CreateCommand();
        audit.CommandText = "SELECT nilai_baru FROM audit WHERE aksi = 'daftar_kartu';";
        Assert.Equal("0001338253->Guru Belum Kartu", audit.ExecuteScalar());
    }

    [Theory]
    [InlineData("UID-SISWA-LAMA", JenisPemilikKartu.Siswa)]
    [InlineData("UID-GURU-LAMA", JenisPemilikKartu.Guru)]
    public void RegisterTeacherCard_UidAlreadyAssigned_RejectsWithoutChangingOwner(string uid, JenisPemilikKartu existingKind)
    {
        using var database = new MigratedDatabase();
        database.SeedKelasUntukRegistrasiKartu();
        var service = new LayananRegistrasiKartu();

        Assert.Throws<InvalidOperationException>(() => service.RegisterTeacherCard(database.Connection, Admin, Waktu, uid, 1));

        Assert.Equal(existingKind, service.CariPemilikAktif(database.Connection, uid, 1)!.Tipe);
        Assert.Null(new RepositoriKartu().CariUidAktifUntukPemilik(database.Connection, JenisPemilikKartu.Guru, 1));
        AssertAdaBarisAudit(database, "daftar_kartu", 0);
    }

    [Fact]
    public void RegisterTeacherCard_TeacherAlreadyHasCard_RejectsSecondCard()
    {
        using var database = new MigratedDatabase();
        database.SeedKelasUntukRegistrasiKartu();
        var service = new LayananRegistrasiKartu();
        service.RegisterTeacherCard(database.Connection, Admin, Waktu, "0001338253", 1);

        Assert.Throws<InvalidOperationException>(() => service.RegisterTeacherCard(database.Connection, Admin, Waktu, "0001338254", 1));

        Assert.Equal("0001338253", new RepositoriKartu().CariUidAktifUntukPemilik(database.Connection, JenisPemilikKartu.Guru, 1));
        Assert.Null(service.CariPemilikAktif(database.Connection, "0001338254", 1));
        AssertAdaBarisAudit(database, "daftar_kartu", 1);
    }

    [Theory]
    [InlineData(1)]
    [InlineData(999)]
    public void RegisterTeacherCard_InactiveOrMissingTeacher_Rejects(int teacherId)
    {
        using var database = new MigratedDatabase();
        database.SeedKelasUntukRegistrasiKartu();
        using var command = database.Connection.CreateCommand();
        command.CommandText = "UPDATE guru SET aktif = 0 WHERE id = 1;";
        command.ExecuteNonQuery();
        var service = new LayananRegistrasiKartu();

        Assert.Throws<InvalidOperationException>(() => service.RegisterTeacherCard(database.Connection, Admin, Waktu, "0001338253", teacherId));

        Assert.Null(service.CariPemilikAktif(database.Connection, "0001338253", 1));
        AssertAdaBarisAudit(database, "daftar_kartu", 0);
    }

    [Fact]
    public void RegisterTeacherCard_HomeroomAccount_RejectsWithoutSaving()
    {
        using var database = new MigratedDatabase();
        database.SeedKelasUntukRegistrasiKartu();
        var service = new LayananRegistrasiKartu();

        Assert.Throws<InvalidOperationException>(() => service.RegisterTeacherCard(database.Connection, WaliKelas, Waktu, "0001338253", 1));

        Assert.Null(service.CariPemilikAktif(database.Connection, "0001338253", 1));
        AssertAdaBarisAudit(database, "daftar_kartu", 0);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(65)]
    public void RegisterTeacherCard_InvalidUidLength_Rejects(int length)
    {
        using var database = new MigratedDatabase();
        database.SeedKelasUntukRegistrasiKartu();
        var service = new LayananRegistrasiKartu();

        Assert.Throws<InvalidOperationException>(() => service.RegisterTeacherCard(database.Connection, Admin, Waktu, new string('0', length), 1));

        Assert.Null(new RepositoriKartu().CariUidAktifUntukPemilik(database.Connection, JenisPemilikKartu.Guru, 1));
        AssertAdaBarisAudit(database, "daftar_kartu", 0);
    }

    private static void AssertAdaBarisAudit(MigratedDatabase database, string aksi, int jumlahDiharapkan)
    {
        using var command = database.Connection.CreateCommand();
        command.CommandText = "SELECT COUNT(*) FROM audit WHERE aksi = $aksi;";
        command.Parameters.AddWithValue("$aksi", aksi);
        Assert.Equal(jumlahDiharapkan, Convert.ToInt32(command.ExecuteScalar()));
    }
}

