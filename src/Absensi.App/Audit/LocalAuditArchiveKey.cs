using System.IO;
using System.Security.Cryptography;

namespace Absensi.App.Audit;

internal static class LocalAuditArchiveKey
{
    private static string KeyPath => Path.Combine(LokasiData.FolderData, "audit-archive-key.dpapi");

    public static byte[] Read() => ProtectedData.Unprotect(File.ReadAllBytes(KeyPath), null, DataProtectionScope.LocalMachine);

    public static byte[] GetOrCreate()
    {
        if (File.Exists(KeyPath)) return Read();
        if (Directory.Exists(LokasiData.FolderArsipAudit) &&
            Directory.EnumerateFiles(LokasiData.FolderArsipAudit, "arsip-audit-*.db").Any(IsEncryptedArchive))
        {
            throw new InvalidOperationException("Kunci arsip audit hilang. Pulihkan kunci pemasangan sebelum melanjutkan.");
        }

        var key = RandomNumberGenerator.GetBytes(32);
        var protectedKey = ProtectedData.Protect(key, null, DataProtectionScope.LocalMachine);
        var temp = KeyPath + $".{Guid.NewGuid():N}.tmp";
        try
        {
            using (var stream = new FileStream(temp, FileMode.CreateNew, FileAccess.Write, FileShare.None))
            {
                stream.Write(protectedKey);
                stream.Flush(flushToDisk: true);
            }
            try { File.Move(temp, KeyPath); }
            catch (IOException) when (File.Exists(KeyPath)) { return Read(); }
            return key;
        }
        finally
        {
            if (File.Exists(temp)) File.Delete(temp);
        }
    }

    private static bool IsEncryptedArchive(string path)
    {
        using var stream = File.OpenRead(path);
        Span<byte> header = stackalloc byte[16];
        return stream.ReadAtLeast(header, header.Length, false) == header.Length
            && header.StartsWith("ABSENSI-AUDIT-1\n"u8);
    }
}
