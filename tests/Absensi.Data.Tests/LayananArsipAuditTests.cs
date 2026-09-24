using Absensi.Data.Backup;
using Microsoft.Data.Sqlite;

namespace Absensi.Data.Tests;

// Master Bug & UI/UX Register A-39: arsip riwayat audit pra-restore.
public sealed class LayananArsipAuditTests : IDisposable
{
    private readonly string _folderArsip = Path.Combine(Path.GetTempPath(), $"absensi-arsip-{Guid.NewGuid():N}");

    [Fact]
    public void BuatDanVerifikasi_ArsipBerisiAuditDanAkunSaja_BukanSeluruhDatabase()
    {
        using var database = new MigratedDatabase();
        database.SeedMasterDataUntukUji();
        database.Execute("""
            INSERT INTO siswa (id, nisn, nama, jenis_kelamin) VALUES (1, '0001', 'Siswa Satu', 'L');
            """);
        var layanan = new LayananArsipAudit(_folderArsip);

        var pathArsip = layanan.BuatDanVerifikasi(database.Connection, new DateTime(2026, 9, 24, 8, 0, 0));

        using var arsip = new SqliteConnection($"Data Source={pathArsip}");
        arsip.Open();
        var namaTabel = new List<string>();
        using (var command = arsip.CreateCommand())
        {
            command.CommandText = "SELECT name FROM sqlite_master WHERE type='table' ORDER BY name;";
            using var reader = command.ExecuteReader();
            while (reader.Read())
            {
                namaTabel.Add(reader.GetString(0));
            }
        }

        Assert.Equal(new[] { "akun", "audit" }, namaTabel);
    }

    [Fact]
    public void BuatDanVerifikasi_JumlahBarisAuditSamaDenganSumber()
    {
        using var database = new MigratedDatabase();
        database.SeedMasterDataUntukUji();
        database.Execute("""
            INSERT INTO audit (waktu, akun_id, aksi, entitas, entitas_id, nilai_lama, nilai_baru, atas_permintaan, audit_uid)
            VALUES ('2026-09-20T08:00:00', 1, 'buat_akun', 'akun', 2, NULL, 'username=admin2', NULL, 'baris-uji-1');
            """);
        var layanan = new LayananArsipAudit(_folderArsip);

        var jumlahSumberSebelum = HitungBarisAudit(database.Connection);
        var pathArsip = layanan.BuatDanVerifikasi(database.Connection, new DateTime(2026, 9, 24, 8, 0, 0));

        using var arsip = new SqliteConnection($"Data Source={pathArsip}");
        arsip.Open();
        Assert.Equal(jumlahSumberSebelum, HitungBarisAudit(arsip));
    }

    [Fact]
    public void BuatDanVerifikasi_TidakMengubahDatabaseSumber()
    {
        using var database = new MigratedDatabase();
        database.SeedMasterDataUntukUji();
        var jumlahSebelum = HitungBarisAudit(database.Connection);
        var layanan = new LayananArsipAudit(_folderArsip);

        layanan.BuatDanVerifikasi(database.Connection, new DateTime(2026, 9, 24, 8, 0, 0));

        Assert.Equal(jumlahSebelum, HitungBarisAudit(database.Connection));
        using var command = database.Connection.CreateCommand();
        command.CommandText = "SELECT COUNT(*) FROM sqlite_master WHERE type='table' AND name='siswa';";
        Assert.Equal(1, Convert.ToInt32(command.ExecuteScalar()));
    }

    [Fact]
    public void BuatDanVerifikasi_TidakIkutRotasiTujuhBerkasBackupBiasa()
    {
        using var database = new MigratedDatabase();
        database.SeedMasterDataUntukUji();
        var layanan = new LayananArsipAudit(_folderArsip);

        layanan.BuatDanVerifikasi(database.Connection, new DateTime(2026, 9, 24, 8, 0, 0));

        // Folder arsip TERPISAH dari folder backup 7-berkas biasa --
        // rotasi LayananBackup.HapusBerkasSelainNTerbaru hanya memindai
        // folder induk langsung, tidak pernah menyentuh subfolder ini.
        Assert.Single(Directory.GetFiles(_folderArsip, "arsip-audit-*.db"));
    }

    [Fact]
    public void Verifikasi_IntegrityCheckGagal_MelemparDanRestoreBatal()
    {
        using var database = new MigratedDatabase();
        database.SeedMasterDataUntukUji();
        database.Execute("""
            INSERT INTO audit (waktu, akun_id, aksi, entitas, entitas_id, nilai_lama, nilai_baru, atas_permintaan, audit_uid)
            VALUES ('2026-09-20T08:00:00', 1, 'buat_akun', 'akun', 2, NULL, 'baris pengisi supaya berkas cukup besar untuk dipotong', NULL, 'baris-uji-integritas');
            """);
        var layanan = new LayananArsipAudit(_folderArsip);
        var pathArsip = layanan.Buat(database.Connection, new DateTime(2026, 9, 24, 8, 0, 0));
        SqliteConnection.ClearAllPools();

        // Rusak paksa: potong berkas jadi separuh ukuran aslinya -- merusak
        // struktur halaman SQLite secara andal (beda dari menimpa byte di
        // posisi tetap, yang kadang jatuh di ruang kosong dan tidak
        // terdeteksi), meniru berkas yang tersalin/tertulis tidak lengkap.
        var ukuranAsli = new FileInfo(pathArsip).Length;
        using (var stream = new FileStream(pathArsip, FileMode.Open, FileAccess.Write))
        {
            stream.SetLength(ukuranAsli / 2);
        }

        var ex = Assert.Throws<InvalidOperationException>(() => LayananArsipAudit.Verifikasi(pathArsip, 1));
        Assert.True(
            ex.Message.Contains("integritas") || ex.Message.Contains("diverifikasi"),
            $"Pesan tidak sesuai harapan: {ex.Message}");
        Assert.True(File.Exists(pathArsip), "Verifikasi() sendiri tidak boleh menghapus berkas -- itu tugas pemanggil (BuatDanVerifikasi).");
    }

    [Fact]
    public void Verifikasi_JumlahBarisTidakCocok_Melempar()
    {
        using var database = new MigratedDatabase();
        database.SeedMasterDataUntukUji();
        database.Execute("""
            INSERT INTO audit (waktu, akun_id, aksi, entitas, entitas_id, nilai_lama, nilai_baru, atas_permintaan, audit_uid)
            VALUES ('2026-09-20T08:00:00', 1, 'buat_akun', 'akun', 2, NULL, 'username=admin2', NULL, 'baris-uji-2');
            """);
        var layanan = new LayananArsipAudit(_folderArsip);
        var pathArsip = layanan.Buat(database.Connection, new DateTime(2026, 9, 24, 8, 0, 0));

        var ex = Assert.Throws<InvalidOperationException>(() => LayananArsipAudit.Verifikasi(pathArsip, 999));
        Assert.Contains("Jumlah baris arsip", ex.Message);
    }

    [Fact]
    public void BuatDanVerifikasi_GagalTulis_PesanJelasDanTidakMengubahSumber()
    {
        using var database = new MigratedDatabase();
        database.SeedMasterDataUntukUji();
        var jumlahSebelum = HitungBarisAudit(database.Connection);

        // Folder arsip "diblokir" oleh BERKAS bernama sama persis --
        // Directory.CreateDirectory akan gagal, meniru disk penuh/izin
        // ditolak tanpa perlu memanipulasi izin sungguhan di CI.
        var folderTerhalang = Path.Combine(Path.GetTempPath(), $"absensi-arsip-terhalang-{Guid.NewGuid():N}");
        File.WriteAllText(folderTerhalang, "bukan folder");
        try
        {
            var layanan = new LayananArsipAudit(folderTerhalang);

            var ex = Assert.Throws<InvalidOperationException>(
                () => layanan.BuatDanVerifikasi(database.Connection, new DateTime(2026, 9, 24, 8, 0, 0)));
            Assert.Contains("Gagal membuat arsip", ex.Message);
            Assert.Equal(jumlahSebelum, HitungBarisAudit(database.Connection));
        }
        finally
        {
            File.Delete(folderTerhalang);
        }
    }

    // TandaiGagal diuji langsung (bukan lewat BuatDanVerifikasi) karena
    // memicu kegagalan Verifikasi() SPESIFIK DI ANTARA pembuatan dan
    // verifikasi di dalam SATU pemanggilan BuatDanVerifikasi butuh
    // menyuntik galat di tengah method itu -- tidak ada celah yang aman
    // untuk itu tanpa mocking berlebihan. Kabelnya sendiri (catch {
    // TandaiGagal(path); throw; } di BuatDanVerifikasi) cuma 3 baris,
    // diperiksa lewat pembacaan kode; dua potongannya (Verifikasi
    // melempar, TandaiGagal menandai) masing-masing diuji utuh di sini.
    [Fact]
    public void TandaiGagal_MenggantiNama_BukanMenghapus()
    {
        var pathArsip = Path.Combine(_folderArsip, "arsip-uji.db");
        Directory.CreateDirectory(_folderArsip);
        File.WriteAllText(pathArsip, "isi arsip yang gagal verifikasi");

        LayananArsipAudit.TandaiGagal(pathArsip);

        Assert.False(File.Exists(pathArsip));
        Assert.True(File.Exists(pathArsip + ".gagal-verifikasi"));
        Assert.Equal("isi arsip yang gagal verifikasi", File.ReadAllText(pathArsip + ".gagal-verifikasi"));
    }

    [Fact]
    public void TandaiGagal_SudahAdaBerkasGagalSebelumnya_MenimpaBukanMenumpuk()
    {
        var pathArsip = Path.Combine(_folderArsip, "arsip-uji.db");
        Directory.CreateDirectory(_folderArsip);
        File.WriteAllText(pathArsip + ".gagal-verifikasi", "gagal lama");
        File.WriteAllText(pathArsip, "gagal baru");

        LayananArsipAudit.TandaiGagal(pathArsip);

        Assert.Equal("gagal baru", File.ReadAllText(pathArsip + ".gagal-verifikasi"));
    }

    private static long HitungBarisAudit(SqliteConnection connection)
    {
        using var command = connection.CreateCommand();
        command.CommandText = "SELECT COUNT(*) FROM audit;";
        return (long)command.ExecuteScalar()!;
    }

    public void Dispose()
    {
        if (Directory.Exists(_folderArsip))
        {
            Directory.Delete(_folderArsip, recursive: true);
        }
    }
}

file static class SqliteConnectionExtensions
{
    public static void Execute(this SqliteConnection connection, string sql)
    {
        using var command = connection.CreateCommand();
        command.CommandText = sql;
        command.ExecuteNonQuery();
    }
}
