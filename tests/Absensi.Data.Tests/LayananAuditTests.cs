using Absensi.Data.Audit;
using Absensi.Data.Backup;
using Absensi.Data.Migrations;
using Absensi.Data.Repositori;
using Absensi.Domain.Akun;
using Microsoft.Data.Sqlite;

namespace Absensi.Data.Tests;

// Master Bug & UI/UX Register A-39 Tahap 2 + A-40. Kriteria selesai yang
// diminta eksplisit oleh pemilik proyek (2026-09-25): "catatan sebelum
// restore benar-benar muncul di layar Audit setelah restore berulang,
// tanpa duplikasi dan dengan nama pelaku yang benar" -- diuji langsung di
// GabunganAktifDanArsip_SetelahRestoreBerulang_TanpaDuplikasiDenganNamaPelakuBenar
// di bawah, bukan cuma diasumsikan dari Migration003TambahAuditUidTests.
public sealed class LayananAuditTests : IDisposable
{
    private static readonly AkunTersimpan Admin = new(1, "admin1", "x", PeranAkun.Admin, null, true);
    private static readonly AkunTersimpan Kepsek = new(1, "kepsek", "x", PeranAkun.KepalaSekolah, null, true);
    private static readonly AkunTersimpan WaliKelas = new(3, "wali_a", "x", PeranAkun.WaliKelas, 1, true);

    private readonly string _folderBackup = Path.Combine(Path.GetTempPath(), $"absensi-audit-{Guid.NewGuid():N}");
    private readonly string _folderArsip;
    private readonly LayananAudit _layananAudit = new();

    public LayananAuditTests()
    {
        _folderArsip = Path.Combine(_folderBackup, "arsip-audit");
    }

    [Theory]
    [MemberData(nameof(PeranBukanAdmin))]
    public void Baca_BukanAdmin_Ditolak(AkunTersimpan bukanAdmin)
    {
        using var database = new MigratedDatabase();
        database.SeedMasterDataUntukUji();

        Assert.Throws<InvalidOperationException>(() =>
            _layananAudit.Baca(database.Connection, bukanAdmin, _folderArsip, DateTime.MinValue, DateTime.MaxValue));
    }

    public static TheoryData<AkunTersimpan> PeranBukanAdmin => new() { Kepsek, WaliKelas };

    [Fact]
    public void Baca_BarisAktifDalamRentangTanggal_Tampil_DiLuarRentang_TidakTampil()
    {
        using var database = new MigratedDatabase();
        database.SeedMasterDataUntukUji();
        CatatAudit(database.Connection, 1, "perbarui_kelas", "kelas", new DateTime(2026, 6, 15, 8, 0, 0));

        var hasil = _layananAudit.Baca(database.Connection, Admin, _folderArsip,
            new DateTime(2026, 6, 1), new DateTime(2026, 6, 30, 23, 59, 59));
        Assert.Single(hasil.Baris);
        Assert.Equal("admin1", hasil.Baris[0].NamaPelaku);

        var kosong = _layananAudit.Baca(database.Connection, Admin, _folderArsip,
            new DateTime(2026, 7, 1), new DateTime(2026, 7, 31));
        Assert.Empty(kosong.Baris);
    }

    // ---------- Kriteria selesai A-39 (diminta eksplisit) ----------
    [Fact]
    public async Task GabunganAktifDanArsip_SetelahRestoreBerulang_TanpaDuplikasiDenganNamaPelakuBenar()
    {
        Directory.CreateDirectory(_folderBackup);
        var penyimpanCloud = new PenyimpanCloudBackupLokal(Path.Combine(_folderBackup, "cloud"));
        var layananBackup = new LayananBackup(_folderBackup, penyimpanCloud);

        // T0: database baseline (akun admin1 id=1), jadi backup lama B1
        // yang akan dipakai sebagai TARGET restore berkali-kali di bawah.
        using var databaseB1 = new MigratedDatabase();
        databaseB1.SeedMasterDataUntukUji();
        await layananBackup.JalankanAsync(databaseB1.Connection, new DateTime(2026, 9, 1, 6, 0, 0), null);
        var berkasB1 = layananBackup.DaftarBackupLokal().Single();

        // Database AKTIF: baseline + akun kedua ("admin2") yang MELAKUKAN
        // aksinya sendiri (bukan cuma dibuat) -- supaya nama pelakunya
        // sungguh perlu di-resolve dari akun arsip nanti, bukan cuma dari
        // akun admin1 yang toh tetap ada di database aktif.
        using var databaseAktif = new MigratedDatabase();
        databaseAktif.SeedMasterDataUntukUji();
        databaseAktif.Execute("INSERT INTO akun (id, username, sandi_hash, peran, aktif, dibuat_pada) " +
                               "VALUES (2, 'admin2', 'x', 'admin', 1, '2026-09-05T00:00:00Z');");
        var waktuAksiAdmin2 = new DateTime(2026, 9, 10, 9, 0, 0);
        CatatAudit(databaseAktif.Connection, 2, "perbarui_kelas", "kelas", waktuAksiAdmin2);

        // RESTORE #1 ke B1 -- mengarsipkan baris admin2 di atas (satu-
        // satunya tempat baris itu akan tersisa setelah overwrite).
        var waktuRestore1 = new DateTime(2026, 9, 15, 8, 0, 0);
        var dataRestore1 = layananBackup.SiapkanRestore(databaseAktif.Connection, Admin, waktuRestore1, berkasB1);
        var pathDb = databaseAktif.Connection.DataSource;
        databaseAktif.Connection.Dispose();
        SqliteConnection.ClearAllPools();
        LayananBackup.TerapkanRestore(dataRestore1, pathDb);

        using var connSetelahRestore1 = SqliteConnectionFactory.Open(pathDb);
        new MigrationRunner().Run(connSetelahRestore1); // upgrade skema B1 (versi lama) di lini masa ini

        // ID DIPAKAI ULANG setelah restore #1: akun baru "wali_c" beraksi,
        // besar kemungkinan mendapat id=2 lagi (max id sekarang balik ke 1).
        JalankanSql(connSetelahRestore1, "INSERT INTO akun (id, username, sandi_hash, peran, aktif, dibuat_pada) " +
                                          "VALUES (2, 'wali_c', 'x', 'wali_kelas', 1, '2026-09-16T00:00:00Z');");
        var waktuAksiWaliC = new DateTime(2026, 9, 17, 9, 0, 0);
        CatatAudit(connSetelahRestore1, 2, "koreksi_status", "absensi_siswa", waktuAksiWaliC);

        // RESTORE #2 (BERULANG) ke B1 LAGI -- mengarsipkan baris wali_c
        // sebagai arsip KEDUA, terpisah dari arsip pertama.
        var waktuRestore2 = new DateTime(2026, 9, 20, 8, 0, 0);
        var dataRestore2 = layananBackup.SiapkanRestore(connSetelahRestore1, Admin, waktuRestore2, berkasB1);
        connSetelahRestore1.Dispose();
        SqliteConnection.ClearAllPools();
        LayananBackup.TerapkanRestore(dataRestore2, pathDb);

        using var connFinal = SqliteConnectionFactory.Open(pathDb);
        new MigrationRunner().Run(connFinal);

        // ---------- Baca gabungan lewat LayananAudit sungguhan ----------
        var hasil = _layananAudit.Baca(connFinal, Admin, _folderArsip, DateTime.MinValue, DateTime.MaxValue);

        Assert.Empty(hasil.ArsipBermasalah); // kedua arsip harus terbaca sehat

        var baris = hasil.Baris;
        Assert.Equal(baris.Count, baris.Select(b => b.AuditUid).Distinct().Count()); // TANPA DUPLIKASI (audit_uid unik)

        var barisAdmin2 = Assert.Single(baris, b => b.Waktu == waktuAksiAdmin2);
        Assert.Equal("admin2", barisAdmin2.NamaPelaku); // NAMA PELAKU BENAR, di-resolve dari arsip #1
        Assert.StartsWith("Arsip", barisAdmin2.Asal);

        var barisWaliC = Assert.Single(baris, b => b.Waktu == waktuAksiWaliC);
        Assert.Equal("wali_c", barisWaliC.NamaPelaku); // NAMA PELAKU BENAR, di-resolve dari arsip #2 (BUKAN admin2 walau id sama-sama 2)
        Assert.StartsWith("Arsip", barisWaliC.Asal);
        Assert.NotEqual(barisAdmin2.Asal, barisWaliC.Asal); // dua arsip BERBEDA, tidak tertukar

        // Kedua baris restore_backup (marker Tahap 1) sendiri juga CATATAN
        // SEBELUM RESTORE yang harus muncul -- masing-masing hanya ada di
        // arsipnya sendiri (ditulis SEBELUM overwrite).
        Assert.Contains(baris, b => b.Aksi == "restore_backup" && b.Waktu == waktuRestore1);
        Assert.Contains(baris, b => b.Aksi == "restore_backup" && b.Waktu == waktuRestore2);
    }

    [Fact]
    public void Baca_ArsipDirujukTapiBerkasHilang_MasukArsipBermasalah_BarisAktifTetapTampil()
    {
        Directory.CreateDirectory(_folderArsip);
        using var database = new MigratedDatabase();
        database.SeedMasterDataUntukUji();
        // Baris restore_backup PALSU (tanpa benar-benar menjalankan restore)
        // -- cukup untuk menguji deteksi "arsip dirujuk tapi tidak ada".
        var waktuRestore = new DateTime(2026, 9, 20, 8, 0, 0);
        CatatAudit(database.Connection, 1, "restore_backup", "sekolah", waktuRestore);

        var hasil = _layananAudit.Baca(database.Connection, Admin, _folderArsip, DateTime.MinValue, DateTime.MaxValue);

        var masalah = Assert.Single(hasil.ArsipBermasalah);
        Assert.Equal($"arsip-audit-{waktuRestore:yyyyMMdd-HHmmss}.db", masalah.NamaBerkas);
        Assert.Contains("tidak ditemukan", masalah.Alasan);
        Assert.Contains(hasil.Baris, b => b.Aksi == "restore_backup"); // baris aktif tetap tampil, tidak crash
    }

    [Fact]
    public void Baca_ArsipRusak_MasukArsipBermasalah_TidakCrash()
    {
        using var database = new MigratedDatabase();
        database.SeedMasterDataUntukUji();
        var layananArsip = new LayananArsipAudit(_folderArsip);
        var pathArsip = layananArsip.Buat(database.Connection, new DateTime(2026, 9, 20, 8, 0, 0));
        SqliteConnection.ClearAllPools();

        // Rusak paksa: potong berkas jadi separuh (lihat pola sama di
        // LayananArsipAuditTests -- merusak struktur halaman SQLite secara andal).
        var ukuranAsli = new FileInfo(pathArsip).Length;
        using (var stream = new FileStream(pathArsip, FileMode.Open, FileAccess.Write))
        {
            stream.SetLength(ukuranAsli / 2);
        }

        var hasil = _layananAudit.Baca(database.Connection, Admin, _folderArsip, DateTime.MinValue, DateTime.MaxValue);

        Assert.Single(hasil.ArsipBermasalah);
        Assert.Contains("Gagal dibaca", hasil.ArsipBermasalah[0].Alasan);
    }

    private static void CatatAudit(SqliteConnection connection, int akunId, string aksi, string entitas, DateTime waktu)
    {
        using var transaksi = connection.BeginTransaction();
        new RepositoriAudit().Catat(connection, transaksi, waktu, akunId, aksi, entitas, null, null, "nilai-uji", null);
        transaksi.Commit();
    }

    private static void JalankanSql(SqliteConnection connection, string sql)
    {
        using var command = connection.CreateCommand();
        command.CommandText = sql;
        command.ExecuteNonQuery();
    }

    public void Dispose()
    {
        if (Directory.Exists(_folderBackup))
        {
            Directory.Delete(_folderBackup, recursive: true);
        }
    }
}
