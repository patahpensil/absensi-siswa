using System.Globalization;
using System.IO;
using System.Linq;
using Absensi.Data.Backup;
using Absensi.Data.Repositori;
using Absensi.Domain.Akun;
using Microsoft.Data.Sqlite;

namespace Absensi.Data.Audit;

// Master Bug & UI/UX Register A-39 Tahap 2: layar Audit minimum -- baca
// gabungan riwayat dari database aktif + seluruh arsip pra-restore (A-39
// Tahap 1, LayananArsipAudit), dedup berdasarkan audit_uid (identitas
// permanen, kebal terhadap `id` SQLite yang dipakai ulang setelah restore
// -- lihat Migration003TambahAuditUid.cs). Method ini HANYA MEMBACA --
// tidak pernah menulis balik ke tabel audit manapun. Desain "arsip dibaca
// saat ditampilkan, tidak pernah digabung fisik ke tabel aktif" dipilih
// setelah membandingkan dengan opsi salin-ke-tabel-aktif (risiko
// menghidupkan kembali akun yang seharusnya tidak ada di snapshot restore,
// atau gagal FK kalau pelakunya sendiri tidak ada di backup) -- lihat
// riwayat keputusan proyek 2026-09-24/25.
public sealed class LayananAudit
{
    private readonly Func<byte[]>? _archiveKey;

    public LayananAudit(Func<byte[]>? archiveKey = null) => _archiveKey = archiveKey;

    // A-40 (keputusan pemilik proyek 2026-09-25): HANYA Admin boleh
    // membaca riwayat audit, di rilis awal ini -- diperiksa DI SINI
    // (lapisan layanan), bukan cuma disembunyikan di UI. Pola sama seperti
    // LayananBackup.SiapkanRestore menolak Wali Kelas. Kepala Sekolah
    // SENGAJA belum diberi akses (keputusan eksplisit, bukan kelalaian) --
    // menunggu aturan akses per peran diputuskan dan diuji terpisah.
    public IReadOnlyList<BarisAuditGabungan> ReadActive(SqliteConnection connection, AkunTersimpan account,
        DateTime from, DateTime to)
    {
        EnsureAdmin(account);
        var entries = new Dictionary<string, BarisAuditGabungan>();
        BacaDariSumber(connection, "Aktif", from, to, entries);
        return entries.Values.OrderByDescending(entry => entry.Waktu).ToList();
    }

    private static void EnsureAdmin(AkunTersimpan account)
    {
        if (account.Peran != PeranAkun.Admin)
            throw new InvalidOperationException("Hanya Admin yang dapat membuka layar Audit.");
    }

    public HasilAudit Baca(
        SqliteConnection sumber,
        AkunTersimpan akun,
        string folderArsipAudit,
        DateTime dariTanggal,
        DateTime sampaiTanggal)
    {
        EnsureAdmin(akun);

        var baris = new Dictionary<string, BarisAuditGabungan>();
        BacaDariSumber(sumber, "Aktif", dariTanggal, sampaiTanggal, baris);

        var arsipDirujuk = AmbilArsipDirujuk(sumber);
        var arsipBermasalah = new List<ArsipTidakBisaDibaca>();

        var arsipAda = Directory.Exists(folderArsipAudit)
            ? Directory.GetFiles(folderArsipAudit, "arsip-audit-*.db")
                .Select(Path.GetFileName)
                .Where(nama => nama is not null)
                .Cast<string>()
                .ToHashSet()
            : new HashSet<string>();

        foreach (var namaBerkas in arsipAda)
        {
            var pathArsip = Path.Combine(folderArsipAudit, namaBerkas);
            try
            {
                BacaDariArsip(pathArsip, namaBerkas, dariTanggal, sampaiTanggal, baris);
            }
            catch (Exception ex) when (ex is SqliteException or InvalidOperationException or IOException)
            {
                var waktu = arsipDirujuk.TryGetValue(namaBerkas, out var w) ? w : DateTime.MinValue;
                arsipBermasalah.Add(new ArsipTidakBisaDibaca(namaBerkas, waktu, $"Gagal dibaca: {ex.Message}"));
            }
        }

        foreach (var (namaBerkas, waktuRestore) in arsipDirujuk)
        {
            if (!arsipAda.Contains(namaBerkas))
            {
                arsipBermasalah.Add(new ArsipTidakBisaDibaca(namaBerkas, waktuRestore, "Berkas arsip tidak ditemukan"));
            }
        }

        var urut = baris.Values.OrderByDescending(b => b.Waktu).ToList();
        var masalahUrut = arsipBermasalah.OrderByDescending(a => a.WaktuRestore).ToList();
        return new HasilAudit(urut, masalahUrut);
    }

    // Nama arsip mengikuti format PERSIS LayananArsipAudit.Buat --
    // "arsip-audit-{waktu:yyyyMMdd-HHmmss}.db" -- dan waktu itu SAMA
    // dengan waktu baris restore_backup (LayananBackup.SiapkanRestore
    // memanggil keduanya dengan parameter waktu yang identik). Keterkaitan
    // ini SENGAJA implisit lewat konvensi, bukan kolom tersimpan terpisah
    // -- JANGAN ubah salah satu format tanpa yang lain.
    private static Dictionary<string, DateTime> AmbilArsipDirujuk(SqliteConnection sumber)
    {
        var hasil = new Dictionary<string, DateTime>();
        using var command = sumber.CreateCommand();
        command.CommandText = "SELECT waktu FROM audit WHERE aksi = 'restore_backup';";
        using var reader = command.ExecuteReader();
        while (reader.Read())
        {
            var waktu = DateTime.Parse(reader.GetString(0), CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind);
            hasil[$"arsip-audit-{waktu:yyyyMMdd-HHmmss}.db"] = waktu;
        }

        return hasil;
    }

    private static void BacaDariSumber(
        SqliteConnection koneksi, string asal, DateTime dari, DateTime sampai, Dictionary<string, BarisAuditGabungan> tujuan)
    {
        foreach (var entry in ReadRows(koneksi, asal, dari, sampai))
            tujuan.TryAdd(entry.AuditUid, entry);
    }

    internal static List<BarisAuditGabungan> ReadRows(SqliteConnection koneksi, string asal,
        DateTime? dari = null, DateTime? sampai = null, SqliteTransaction? transaction = null)
    {
        using var command = koneksi.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = """
            SELECT a.audit_uid, a.waktu, a.aksi, a.entitas, a.entitas_id, a.nilai_lama, a.nilai_baru, a.atas_permintaan, k.username
            FROM audit a
            LEFT JOIN akun k ON k.id = a.akun_id
            WHERE ($dari IS NULL OR a.waktu >= $dari) AND ($sampai IS NULL OR a.waktu <= $sampai)
            ORDER BY a.id;
            """;
        command.Parameters.AddWithValue("$dari", (object?)dari?.ToString("o") ?? DBNull.Value);
        command.Parameters.AddWithValue("$sampai", (object?)sampai?.ToString("o") ?? DBNull.Value);

        using var reader = command.ExecuteReader();
        var rows = new List<BarisAuditGabungan>();
        while (reader.Read())
        {
            rows.Add(new BarisAuditGabungan(
                reader.GetString(0),
                DateTime.Parse(reader.GetString(1), CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind),
                reader.GetString(2),
                reader.GetString(3),
                reader.IsDBNull(4) ? null : reader.GetInt32(4),
                reader.IsDBNull(5) ? null : reader.GetString(5),
                reader.IsDBNull(6) ? null : reader.GetString(6),
                reader.IsDBNull(7) ? null : reader.GetString(7),
                reader.IsDBNull(8) ? "(akun tidak ditemukan)" : reader.GetString(8),
                asal));
        }

        return rows;
    }

    // Koneksi BARU terpisah ke berkas arsip (bukan ATTACH ke koneksi
    // database aktif yang sedang dipakai sesi berjalan) -- lebih aman,
    // tidak mengubah keadaan koneksi utama sama sekali. ReadOnly murni
    // untuk jaga-jaga (LayananAudit tidak pernah menulis, tapi menegaskan
    // niat itu di tingkat koneksi juga).
    private void BacaDariArsip(
        string pathArsip, string namaBerkas, DateTime dari, DateTime sampai, Dictionary<string, BarisAuditGabungan> tujuan)
    {
        if (LayananArsipAudit.ReadProtected(pathArsip, _archiveKey) is { } entries)
        {
            foreach (var entry in entries.Where(entry => entry.Waktu >= dari && entry.Waktu <= sampai))
                tujuan.TryAdd(entry.AuditUid, entry);
            return;
        }

        var settings = new SqliteConnectionStringBuilder { DataSource = pathArsip, Mode = SqliteOpenMode.ReadOnly, Pooling = false };
        using var koneksi = new SqliteConnection(settings.ToString());
        koneksi.Open();

        using (var cekIntegritas = koneksi.CreateCommand())
        {
            cekIntegritas.CommandText = "PRAGMA integrity_check;";
            var hasil = (string)cekIntegritas.ExecuteScalar()!;
            if (hasil != "ok")
            {
                throw new InvalidOperationException($"integrity_check gagal ({hasil})");
            }
        }

        BacaDariSumber(koneksi, $"Arsip {namaBerkas}", dari, sampai, tujuan);
    }
}
