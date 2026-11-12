using System.Globalization;
using Absensi.Data.Akun;
using Absensi.Data.Migrations;
using Absensi.Data.Repositori;
using Absensi.Domain.Akun;
using Microsoft.Data.Sqlite;

namespace Absensi.Data.Backup;

// PRD §18: dua titik pemicu (pembukaan aplikasi, setelah batas akhir tap
// masuk — App.xaml.cs dan Penjadwal yang memutuskan KAPAN, kelas ini hanya
// tahu CARA), retensi 7 berkas lokal terakhir, dan enkripsi sebelum
// diunggah ke cloud (lihat IPenyimpanCloudBackup untuk status server-nya).
public sealed class LayananBackup
{
    private const int JumlahBerkasDisimpan = 7;
    private const string FormatWaktu = "yyyyMMdd-HHmmss";

    private readonly string _folderBackup;
    private readonly IPenyimpanCloudBackup _penyimpanCloud;
    private readonly JalurPerubahanData _jalurPerubahanData = new();
    // Master Bug & UI/UX Register A-39: subfolder "arsip-audit" di bawah
    // folderBackup yang sama -- sengaja diturunkan di sini (bukan
    // parameter konstruktor baru) supaya seluruh pemanggil yang sudah ada
    // (App.xaml.cs, test) tidak perlu diubah.
    private readonly LayananArsipAudit _layananArsipAudit;
    private readonly Func<byte[]>? _archiveKey;

    public LayananBackup(string folderBackup, IPenyimpanCloudBackup penyimpanCloud, Func<byte[]>? archiveKey = null)
    {
        _folderBackup = folderBackup;
        _penyimpanCloud = penyimpanCloud;
        _archiveKey = archiveKey;
        _layananArsipAudit = new LayananArsipAudit(Path.Combine(folderBackup, "arsip-audit"));
    }

    // Titik normal (PRD §18.1): snapshot lokal, lalu — bila lisensi sudah
    // aktif — versi terenkripsinya "diunggah" (lihat catatan stub di
    // IPenyimpanCloudBackup). Ketentuan baseline desktop (PRD §21.9
    // meninjaunya untuk server, D-02): berbeda dari notifikasi, titik
    // ini DIKEJAR walau aplikasi baru dibuka lama setelah jadwalnya —
    // pemanggil (Penjadwal) karena itu tidak membuang panggilan ini
    // walau sudah lewat toleransi, tidak seperti titik notifikasi lain.
    public async Task JalankanAsync(SqliteConnection sumber, DateTime waktu, string? kunciLisensiAktif)
    {
        var data = BuatSnapshotLokal(sumber, waktu, kunciLisensiAktif);

        if (!string.IsNullOrWhiteSpace(kunciLisensiAktif))
        {
            var terenkripsi = EnkripsiBackup.Enkripsi(data, kunciLisensiAktif);
            await _penyimpanCloud.UnggahAsync(kunciLisensiAktif, terenkripsi, waktu);
        }
    }

    public IReadOnlyList<BerkasBackup> DaftarBackupLokal()
    {
        if (!Directory.Exists(_folderBackup))
        {
            return Array.Empty<BerkasBackup>();
        }

        return Directory.GetFiles(_folderBackup, "*.db")
            .Select(path => new BerkasBackup(path, WaktuDariNamaBerkas(path)))
            .OrderByDescending(b => b.WaktuDibuat)
            .ToList();
    }

    // PRD §18.4 langkah 1 & 2: cadangan kondisi saat ini dibuat DULU, dan
    // aksi restore tercatat di audit — keduanya selagi koneksi ke database
    // yang AKAN diganti masih terbuka. Isi berkas sumber restore dibaca ke
    // memori DI SINI (bukan belakangan) supaya rotasi 7-berkas di atas
    // tidak pernah bisa menghapusnya duluan sebelum sungguh dipakai.
    //
    // PRD §24.3: pemeriksaan peran di lapisan layanan ini,
    // bukan hanya menyembunyikan tombol Pulihkan di PengaturanViewModel.
    public byte[] SiapkanRestore(SqliteConnection sumber, AkunTersimpan akun, DateTime waktu, BerkasBackup berkasSumberRestore)
    {
        if (akun.Peran == PeranAkun.WaliKelas)
        {
            throw new InvalidOperationException("Wali kelas tidak berwenang memulihkan backup.");
        }

        var kunciLisensi = new Absensi.Data.Lisensi.RepositoriLisensi().BacaStatus(sumber)?.Kunci;
        var dataSumberRestore = EnkripsiBackup.BukaLokal(File.ReadAllBytes(berkasSumberRestore.PathBerkas), kunciLisensi);
        WithValidatedBackup(dataSumberRestore, _folderBackup, _ => { });

        var archiveKey = _archiveKey?.Invoke()
            ?? throw new InvalidOperationException("Kunci arsip audit tidak tersedia. Pemulihan dibatalkan.");
        BuatSnapshotLokal(sumber, waktu, kunciLisensi);
        var namaCadangan = $"{waktu.ToString(FormatWaktu)}.db";
        string? archivePath = null;
        try
        {
            _jalurPerubahanData.Jalankan(sumber, akun, waktu, "restore_backup", "sekolah", _ => null,
                nilaiLama: namaCadangan, nilaiBaru: berkasSumberRestore.NamaBerkas,
                setelahAudit: transaction => archivePath = _layananArsipAudit.Write(sumber, transaction, waktu, archiveKey));
        }
        catch
        {
            if (archivePath is not null) LayananArsipAudit.TryDelete(archivePath);
            throw;
        }

        return dataSumberRestore;
    }

    public static void TerapkanRestore(byte[] dataSumberRestore, SqliteConnection target)
    {
        // SQLite mengunci tujuan dan mengganti isinya dalam satu transaksi.
        // Bila penyalinan gagal, SQLite membatalkan transaksi tersebut.
        WithValidatedBackup(dataSumberRestore, Path.GetDirectoryName(Path.GetFullPath(target.DataSource))!,
            candidate => candidate.BackupDatabase(target));
    }

    public static void TerapkanRestore(byte[] dataSumberRestore, string pathDatabaseTujuan)
    {
        using var target = SqliteConnectionFactory.Open(pathDatabaseTujuan);
        TerapkanRestore(dataSumberRestore, target);
    }

    private static void WithValidatedBackup(byte[] data, string directory, Action<SqliteConnection> apply)
    {
        var path = Path.Combine(directory, $"restore-{Guid.NewGuid():N}.tmp");
        try
        {
            if (!data.AsSpan().StartsWith("SQLite format 3\0"u8))
                throw new InvalidOperationException("Berkas backup rusak atau bukan database absensi.");

            File.WriteAllBytes(path, data);
            var settings = new SqliteConnectionStringBuilder { DataSource = path, Mode = SqliteOpenMode.ReadWrite, Pooling = false };
            using var candidate = new SqliteConnection(settings.ToString());
            candidate.Open();

            using var command = candidate.CreateCommand();
            command.CommandText = "PRAGMA integrity_check;";
            if (!Equals(command.ExecuteScalar(), "ok"))
                throw new InvalidOperationException("Pemeriksaan integritas backup gagal.");

            new MigrationRunner().UpgradeBackup(candidate);
            command.CommandText = """
                SELECT COUNT(*) FROM sqlite_schema WHERE type = 'table' AND name IN
                ('sekolah', 'pengaturan', 'akun', 'audit', 'tahun_ajaran', 'kelas', 'siswa', 'guru',
                 'siswa_kelas', 'absensi_siswa', 'absensi_guru', 'opened_school_day', 'schedule_change');
                """;
            if (!Equals(command.ExecuteScalar(), 13L))
                throw new InvalidOperationException("Tabel wajib pada backup tidak lengkap.");

            command.CommandText = "PRAGMA foreign_key_check;";
            if (command.ExecuteScalar() is not null)
                throw new InvalidOperationException("Data pada backup memiliki rujukan yang tidak valid.");

            apply(candidate);
        }
        catch (SqliteException ex)
        {
            throw new InvalidOperationException("Backup tidak dapat dipulihkan. Periksa berkas dan pastikan database dapat ditulis.", ex);
        }
        finally
        {
            LayananArsipAudit.TryDelete(path);
        }
    }

    // Mengembalikan isi SQLite polos snapshot. Berkas lokal ditulis
    // terenkripsi bila kunci lisensi tersedia; tanpa lisensi aktif (sebelum
    // aktivasi) tetap polos seperti sebelumnya.
    private byte[] BuatSnapshotLokal(SqliteConnection sumber, DateTime waktu, string? kunciLisensi)
    {
        Directory.CreateDirectory(_folderBackup);
        var pathTujuan = Path.Combine(_folderBackup, $"{waktu.ToString(FormatWaktu)}.db");
        var pathSementara = pathTujuan + ".tmp";

        using (var tujuan = new SqliteConnection($"Data Source={pathSementara};Pooling=False"))
        {
            tujuan.Open();
            sumber.BackupDatabase(tujuan);
        }

        // Microsoft.Data.Sqlite mengumpulkan (pool) koneksi native walau
        // sudah Dispose() — tanpa ini, Windows masih mengunci berkas yang
        // baru saja ditulis sehingga baca/hapus berikutnya gagal.
        SqliteConnection.ClearAllPools();

        var data = File.ReadAllBytes(pathSementara);
        File.WriteAllBytes(pathTujuan, string.IsNullOrWhiteSpace(kunciLisensi) ? data : EnkripsiBackup.BungkusLokal(data, kunciLisensi));
        File.Delete(pathSementara);

        HapusBerkasSelainNTerbaru(JumlahBerkasDisimpan);
        return data;
    }

    private void HapusBerkasSelainNTerbaru(int jumlahDisimpan)
    {
        // Nama berkas yyyyMMdd-HHmmss.db terurut leksikografis = terurut
        // kronologis, jadi tidak perlu mem-parse tanggalnya lagi di sini.
        var berkasLama = Directory.GetFiles(_folderBackup, "*.db")
            .OrderByDescending(path => path)
            .Skip(jumlahDisimpan);

        foreach (var path in berkasLama)
        {
            File.Delete(path);
        }
    }

    private static DateTime WaktuDariNamaBerkas(string path) =>
        DateTime.ParseExact(Path.GetFileNameWithoutExtension(path), FormatWaktu, CultureInfo.InvariantCulture);
}
