using System.IO.Compression;
using Absensi.Data.Backup;
using Microsoft.Data.Sqlite;
using Microsoft.JSInterop;

namespace Absensi.Web.Layanan;

// Cadangan web: database SQLite dan foto dikemas dalam satu berkas .zip yang
// diunduh pengguna. Browser adalah satu-satunya tempat data web, jadi berkas
// ini satu-satunya jalan memulihkan data setelah riwayat browser terhapus.
// Isi database divalidasi dengan layanan backup desktop sebelum dipakai.
public sealed partial class StatusAplikasi
{
    private const string BackupDatabaseEntry = "absensi.db";
    private const string BackupPhotoFolder = "foto/";
    public const long MaxBackupBytes = 200L * 1024 * 1024;

    public sealed record BerkasCadangan(string Name, byte[] Bytes);

    public async Task<BerkasCadangan?> CreateBackupAsync()
    {
        if (!WorkspaceView.CanManage(this) || Koneksi is null) return null;

        byte[] database;
        await _storage.WaitAsync();
        try
        {
            using (var command = Koneksi.CreateCommand())
            {
                command.CommandText = "PRAGMA wal_checkpoint(TRUNCATE);";
                command.ExecuteNonQuery();
            }

            database = File.ReadAllBytes(BerkasDb);
        }
        finally
        {
            _storage.Release();
        }

        using var output = new MemoryStream();
        using (var zip = new ZipArchive(output, ZipArchiveMode.Create, leaveOpen: true))
        {
            await WriteEntryAsync(zip, BackupDatabaseEntry, database);
            if (Directory.Exists(PhotoFolder))
            {
                foreach (var path in Directory.GetFiles(PhotoFolder).Order(StringComparer.Ordinal))
                    await WriteEntryAsync(zip, BackupPhotoFolder + Path.GetFileName(path), File.ReadAllBytes(path));
            }
        }

        return new BerkasCadangan($"absensi-cadangan-{Jam.Sekarang:yyyyMMdd-HHmm}.zip", output.ToArray());
    }

    // Null bila berhasil; selain itu pesan untuk pengguna. Seluruh data di
    // browser ini diganti isi cadangan dalam satu transaksi IndexedDB.
    public async Task<string?> RestoreBackupAsync(byte[] backup)
    {
        if (!WorkspaceView.CanManage(this)) return "Akun ini tidak berwenang memulihkan cadangan.";
        if (StorageConflict) return CatatanPenyimpanan;

        byte[]? database = null;
        var photos = new Dictionary<string, byte[]>(StringComparer.Ordinal);
        try
        {
            using var zip = new ZipArchive(new MemoryStream(backup), ZipArchiveMode.Read);
            foreach (var entry in zip.Entries)
            {
                if (entry.FullName == BackupDatabaseEntry) database = await ReadEntryAsync(entry);
                else if (entry.FullName.StartsWith(BackupPhotoFolder, StringComparison.Ordinal) && IsSafePhotoName(entry.FullName[BackupPhotoFolder.Length..]))
                    photos[entry.FullName[BackupPhotoFolder.Length..]] = await ReadEntryAsync(entry);
            }
        }
        catch (InvalidDataException)
        {
            return "Berkas bukan cadangan absensi atau sudah rusak.";
        }

        if (database is null) return "Berkas cadangan tidak berisi database absensi.";

        const string candidate = "/tmp/absensi-restore.db";
        try
        {
            DeleteDatabaseFiles(candidate);
            LayananBackup.TerapkanRestore(database, candidate);
            using (var connection = new SqliteConnection($"Data Source={candidate};Pooling=False"))
            {
                connection.Open();
                using var command = connection.CreateCommand();
                command.CommandText = "PRAGMA wal_checkpoint(TRUNCATE);";
                command.ExecuteNonQuery();
            }

            SqliteConnection.ClearAllPools();
            database = File.ReadAllBytes(candidate);
        }
        catch (InvalidOperationException ex)
        {
            return ex.Message;
        }
        finally
        {
            SqliteConnection.ClearAllPools();
            DeleteDatabaseFiles(candidate);
        }

        await _storage.WaitAsync();
        try
        {
            if (StorageConflict) return CatatanPenyimpanan;
            if (!await _js.InvokeAsync<bool>("absensiStorage.restoreDatabase", _storageVersion, database, photos))
            {
                MarkStorageConflict();
                return CatatanPenyimpanan;
            }

            Akun = null;
            Koneksi?.Dispose();
            Koneksi = null;
            SqliteConnection.ClearAllPools();
            DeleteDatabaseFiles(BerkasDb);
            _pendingPhotos.Clear();
            ClearLocalPhotos();
            StorageFailed = false;
        }
        finally
        {
            _storage.Release();
        }

        await MulaiAsync();
        return PesanGalat;
    }

    private static bool IsSafePhotoName(string name) =>
        name.Length is > 0 and <= 120 && name.IndexOfAny(['/', '\\']) < 0 && name != "." && name != "..";

    private static void DeleteDatabaseFiles(string path)
    {
        foreach (var file in new[] { path, path + "-wal", path + "-shm" })
            File.Delete(file);
    }

    private static async Task WriteEntryAsync(ZipArchive zip, string name, byte[] bytes)
    {
        await using var stream = zip.CreateEntry(name, CompressionLevel.Optimal).Open();
        await stream.WriteAsync(bytes);
    }

    private static async Task<byte[]> ReadEntryAsync(ZipArchiveEntry entry)
    {
        if (entry.Length > MaxBackupBytes) throw new InvalidDataException();
        using var output = new MemoryStream();
        await using var input = entry.Open();
        await input.CopyToAsync(output);
        return output.ToArray();
    }
}
