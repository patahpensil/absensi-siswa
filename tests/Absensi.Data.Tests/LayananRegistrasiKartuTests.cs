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

    private static void AssertAdaBarisAudit(MigratedDatabase database, string aksi, int jumlahDiharapkan)
    {
        using var command = database.Connection.CreateCommand();
        command.CommandText = "SELECT COUNT(*) FROM audit WHERE aksi = $aksi;";
        command.Parameters.AddWithValue("$aksi", aksi);
        Assert.Equal(jumlahDiharapkan, Convert.ToInt32(command.ExecuteScalar()));
    }
}
