using System.Security.Cryptography;
using System.Text.Json;
using Absensi.Data.Audit;
using Microsoft.Data.Sqlite;

namespace Absensi.Data.Backup;

// Arsip riwayat audit pra-restore. Isinya hanya baris audit beserta nama
// akun pelaku (tanpa hash sandi). Keputusan pemilik 12 November 2026
// (PRD §25.5): tidak memakai DPAPI. Arsip dienkripsi dengan kunci lisensi
// sekolah, sama seperti backup lokal; bila lisensi belum aktif arsip
// disimpan polos, sama seperti backup lokal tanpa lisensi.
// Nama berkas tetap mengikuti waktu restore agar layar Audit bisa
// mencocokkannya dengan baris restore_backup.
internal sealed class LayananArsipAudit(string folder)
{
    // Semua penanda 16 bita supaya cukup satu kali baca di awal berkas.
    private static ReadOnlySpan<byte> HeaderDpapi => "ABSENSI-AUDIT-1\n"u8;
    private static ReadOnlySpan<byte> HeaderEncrypted => "ABSENSI-AUDIT-2\n"u8;
    private static ReadOnlySpan<byte> HeaderPlain => "ABSENSI-AUDIT-P\n"u8;
    private const int HeaderLength = 16;

    public string Write(SqliteConnection source, SqliteTransaction transaction, DateTime time, string? licenseKey)
    {
        var name = $"arsip-audit-{time:yyyyMMdd-HHmmss}.db";
        var path = Path.Combine(folder, name);
        var temp = path + $".{Guid.NewGuid():N}.tmp";
        try
        {
            Directory.CreateDirectory(folder);
            var entries = LayananAudit.ReadRows(source, $"Arsip {name}", transaction: transaction);
            var json = JsonSerializer.SerializeToUtf8Bytes(entries);
            var encrypted = !string.IsNullOrWhiteSpace(licenseKey);
            using (var stream = new FileStream(temp, FileMode.CreateNew, FileAccess.Write, FileShare.None))
            {
                stream.Write(encrypted ? HeaderEncrypted : HeaderPlain);
                stream.Write(encrypted ? EnkripsiBackup.Enkripsi(json, licenseKey!) : json);
                stream.Flush(flushToDisk: true);
            }

            if (Read(temp, licenseKey) is not { } verified || !entries.SequenceEqual(verified))
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

    // Mengembalikan null untuk arsip lama berbentuk SQLite (tanpa penanda);
    // pemanggil membacanya lewat jalur SQLite. Semua kegagalan lain menjadi
    // InvalidOperationException berpesan jelas agar layar Audit bisa
    // mencantumkannya sebagai arsip yang tidak bisa dibaca.
    public static IReadOnlyList<BarisAuditGabungan>? Read(string path, string? licenseKey)
    {
        byte[] content;
        try
        {
            content = File.ReadAllBytes(path);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            throw new InvalidOperationException("Berkas arsip audit tidak bisa dibuka.", ex);
        }

        var header = content.AsSpan(0, Math.Min(HeaderLength, content.Length));
        if (header.SequenceEqual(HeaderDpapi))
            throw new InvalidOperationException("Arsip ini dibuat dengan kunci DPAPI yang tidak dipakai lagi, sehingga isinya tidak bisa dibuka.");

        var body = content.AsSpan(Math.Min(HeaderLength, content.Length)).ToArray();
        try
        {
            if (header.SequenceEqual(HeaderPlain))
                return Deserialize(body);

            if (!header.SequenceEqual(HeaderEncrypted))
                return null;

            if (string.IsNullOrWhiteSpace(licenseKey))
                throw new InvalidOperationException("Arsip ini terenkripsi, tetapi lisensi pada aplikasi ini belum aktif.");

            return Deserialize(EnkripsiBackup.Dekripsi(body, licenseKey));
        }
        catch (CryptographicException ex)
        {
            throw new InvalidOperationException("Arsip ini dienkripsi dengan kunci lisensi lain atau berkasnya rusak.", ex);
        }
        catch (JsonException ex)
        {
            throw new InvalidOperationException("Isi arsip audit rusak.", ex);
        }
    }

    private static List<BarisAuditGabungan> Deserialize(byte[] json) =>
        JsonSerializer.Deserialize<List<BarisAuditGabungan>>(json)
            ?? throw new InvalidOperationException("Isi arsip audit kosong atau tidak sah.");

    internal static void TryDelete(string path)
    {
        try { File.Delete(path); }
        catch (IOException) { }
        catch (UnauthorizedAccessException) { }
    }
}
