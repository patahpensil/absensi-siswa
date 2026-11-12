using System.Security.Cryptography;
using System.Text.Json;
using Absensi.Data.Audit;
using Microsoft.Data.Sqlite;

namespace Absensi.Data.Backup;

// Arsip baru hanya membawa baris audit dan nama akun yang tampil di layar Audit.
// Nama berkas tetap mengikuti referensi waktu restore agar arsip lama terbaca.
internal sealed class LayananArsipAudit(string folder)
{
    private static ReadOnlySpan<byte> Header => "ABSENSI-AUDIT-1\n"u8;

    public string Write(SqliteConnection source, SqliteTransaction transaction, DateTime time, byte[] key)
    {
        var name = $"arsip-audit-{time:yyyyMMdd-HHmmss}.db";
        var path = Path.Combine(folder, name);
        var temp = path + $".{Guid.NewGuid():N}.tmp";
        try
        {
            Directory.CreateDirectory(folder);
            var entries = LayananAudit.ReadRows(source, $"Arsip {name}", transaction: transaction);
            var json = JsonSerializer.SerializeToUtf8Bytes(entries);
            var sealedArchive = EnkripsiBackup.Enkripsi(json, Convert.ToBase64String(key));
            using (var stream = new FileStream(temp, FileMode.CreateNew, FileAccess.Write, FileShare.None))
            {
                stream.Write(Header);
                stream.Write(sealedArchive);
                stream.Flush(flushToDisk: true);
            }

            if (ReadProtected(temp, () => key) is not { } verified || !entries.SequenceEqual(verified))
                throw new InvalidOperationException("Isi arsip audit tidak sama dengan sumbernya.");

            File.Move(temp, path);
            return path;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or InvalidOperationException)
        {
            throw new InvalidOperationException("Gagal membuat arsip riwayat audit. Pemulihan dibatalkan.", ex);
        }
        finally
        {
            TryDelete(temp);
        }
    }

    public static IReadOnlyList<BarisAuditGabungan>? ReadProtected(string path, Func<byte[]>? keyProvider)
    {
        using var stream = File.OpenRead(path);
        Span<byte> header = stackalloc byte[Header.Length];
        if (stream.ReadAtLeast(header, header.Length, false) != header.Length || !header.SequenceEqual(Header))
            return null;

        if (keyProvider is null)
            throw new InvalidOperationException("Kunci arsip audit tidak tersedia pada pemasangan ini.");

        try
        {
            using var package = new MemoryStream();
            stream.CopyTo(package);
            var json = EnkripsiBackup.Dekripsi(package.ToArray(), Convert.ToBase64String(keyProvider()));
            return JsonSerializer.Deserialize<List<BarisAuditGabungan>>(json)
                ?? throw new InvalidOperationException("Isi arsip audit kosong atau tidak sah.");
        }
        catch (Exception ex) when (ex is CryptographicException or JsonException or UnauthorizedAccessException)
        {
            throw new InvalidOperationException("Arsip audit rusak atau kuncinya tidak sesuai.", ex);
        }
    }

    internal static void TryDelete(string path)
    {
        try { File.Delete(path); }
        catch (IOException) { }
        catch (UnauthorizedAccessException) { }
    }
}
