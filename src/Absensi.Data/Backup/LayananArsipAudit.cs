using Microsoft.Data.Sqlite;

namespace Absensi.Data.Backup;

// Master Bug & UI/UX Register A-39: arsip riwayat audit dibuat SEBELUM
// restore boleh menimpa apa pun. Isinya HANYA tabel `audit`+`akun` (bukan
// seluruh database) -- cukup untuk layar Audit nanti menampilkan riwayat
// dan nama pelaku tanpa perlu menghidupkan akun itu di database aktif,
// tanpa menggandakan seluruh data siswa/guru ke setiap arsip.
//
// Folder arsip SENGAJA terpisah dari folder backup 7-berkas biasa
// (LayananBackup.HapusBerkasSelainNTerbaru hanya memindai folder induk
// LANGSUNG, jadi subfolder ini otomatis tidak ikut rotasi) dan TIDAK
// PERNAH dihapus otomatis oleh kode ini -- keputusan pemilik proyek
// 2026-09-24: "jangan hapus arsip otomatis untuk tahap ini", retensi
// jangka panjang didokumentasikan terpisah (docs/SKEMA-DATABASE.md §9).
public sealed class LayananArsipAudit
{
    private readonly string _folderArsip;

    public LayananArsipAudit(string folderArsip)
    {
        _folderArsip = folderArsip;
    }

    // Membuat arsip lalu memverifikasinya lewat KONEKSI BARU (bukan
    // koneksi yang baru saja menulis -- supaya verifikasi benar-benar
    // membaca apa yang tersimpan di berkas, bukan cuma percaya proses
    // tulis tidak melempar exception). Melempar InvalidOperationException
    // dengan pesan Indonesia jelas kalau pembuatan ATAU verifikasi gagal.
    // Pemanggil (LayananBackup.SiapkanRestore) WAJIB berhenti kalau ini
    // melempar -- database sumber belum disentuh sama sekali di titik ini.
    public string BuatDanVerifikasi(SqliteConnection sumber, DateTime waktu)
    {
        var jumlahSumber = HitungBarisAudit(sumber);

        string pathArsip;
        try
        {
            pathArsip = Buat(sumber, waktu);
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException(
                "Gagal membuat arsip riwayat audit -- pemulihan dibatalkan, database tidak diubah.", ex);
        }

        try
        {
            Verifikasi(pathArsip, jumlahSumber);
        }
        catch
        {
            TandaiGagal(pathArsip);
            throw;
        }

        return pathArsip;
    }

    // Master Bug & UI/UX Register A-39 Tahap 2: baris `restore_backup`
    // (LayananBackup.SiapkanRestore) SENGAJA ditulis ke `sumber` SETELAH
    // arsip ini dibuat -- supaya kalau pengarsipan gagal, database sumber
    // TIDAK punya jejak restore sama sekali (jujur, tidak menyesatkan).
    // Konsekuensinya baris itu sendiri tidak ikut ter-snapshot ke arsip
    // (yang sudah lebih dulu dibuat) -- ditemukan lewat tes gagal
    // (LayananAuditTests), bukan dirancang di atas kertas. Method ini
    // menyalin SATU baris terakhir (marker yang baru saja berhasil
    // ditulis) ke arsip yang sudah ada. Aman: berkas arsip TIDAK ikut
    // menyalin trigger A-38 (CREATE TABLE AS SELECT hanya menyalin data),
    // jadi INSERT ke sana tidak melanggar batasan apa pun.
    public void TambahkanBarisTerakhir(string pathArsip, SqliteConnection sumber)
    {
        string waktu, aksi, entitas, auditUid;
        int akunId;
        object? entitasId, nilaiLama, nilaiBaru, atasPermintaan;

        using (var bacaTerakhir = sumber.CreateCommand())
        {
            bacaTerakhir.CommandText = """
                SELECT waktu, akun_id, aksi, entitas, entitas_id, nilai_lama, nilai_baru, atas_permintaan, audit_uid
                FROM audit ORDER BY id DESC LIMIT 1;
                """;
            using var reader = bacaTerakhir.ExecuteReader();
            if (!reader.Read())
            {
                return;
            }

            waktu = reader.GetString(0);
            akunId = reader.GetInt32(1);
            aksi = reader.GetString(2);
            entitas = reader.GetString(3);
            entitasId = reader.IsDBNull(4) ? null : reader.GetInt32(4);
            nilaiLama = reader.IsDBNull(5) ? null : reader.GetString(5);
            nilaiBaru = reader.IsDBNull(6) ? null : reader.GetString(6);
            atasPermintaan = reader.IsDBNull(7) ? null : reader.GetString(7);
            auditUid = reader.GetString(8);
        }

        SqliteConnection.ClearAllPools();
        using var arsip = new SqliteConnection($"Data Source={pathArsip}");
        arsip.Open();
        using var tulis = arsip.CreateCommand();
        tulis.CommandText = """
            INSERT INTO audit (waktu, akun_id, aksi, entitas, entitas_id, nilai_lama, nilai_baru, atas_permintaan, audit_uid)
            VALUES ($waktu, $akunId, $aksi, $entitas, $entitasId, $nilaiLama, $nilaiBaru, $atasPermintaan, $auditUid);
            """;
        tulis.Parameters.AddWithValue("$waktu", waktu);
        tulis.Parameters.AddWithValue("$akunId", akunId);
        tulis.Parameters.AddWithValue("$aksi", aksi);
        tulis.Parameters.AddWithValue("$entitas", entitas);
        tulis.Parameters.AddWithValue("$entitasId", (object?)entitasId ?? DBNull.Value);
        tulis.Parameters.AddWithValue("$nilaiLama", (object?)nilaiLama ?? DBNull.Value);
        tulis.Parameters.AddWithValue("$nilaiBaru", (object?)nilaiBaru ?? DBNull.Value);
        tulis.Parameters.AddWithValue("$atasPermintaan", (object?)atasPermintaan ?? DBNull.Value);
        tulis.Parameters.AddWithValue("$auditUid", auditUid);
        tulis.ExecuteNonQuery();
    }

    internal string Buat(SqliteConnection sumber, DateTime waktu)
    {
        Directory.CreateDirectory(_folderArsip);
        var pathArsip = Path.Combine(_folderArsip, $"arsip-audit-{waktu:yyyyMMdd-HHmmss}.db");

        using var buat = sumber.CreateCommand();
        buat.CommandText = """
            ATTACH DATABASE $pathArsip AS arsip;
            CREATE TABLE arsip.audit AS SELECT * FROM audit;
            CREATE TABLE arsip.akun AS SELECT * FROM akun;
            DETACH DATABASE arsip;
            """;
        buat.Parameters.AddWithValue("$pathArsip", pathArsip);
        buat.ExecuteNonQuery();

        return pathArsip;
    }

    // internal (bukan private) supaya bisa diuji langsung terhadap arsip
    // yang sengaja dirusak setelah dibuat -- lihat LayananArsipAuditTests.
    internal static void Verifikasi(string pathArsip, long jumlahSumberDiharapkan)
    {
        SqliteConnection.ClearAllPools();

        try
        {
            using var koneksi = new SqliteConnection($"Data Source={pathArsip}");
            koneksi.Open();

            using (var cekIntegritas = koneksi.CreateCommand())
            {
                cekIntegritas.CommandText = "PRAGMA integrity_check;";
                var hasil = (string)cekIntegritas.ExecuteScalar()!;
                if (hasil != "ok")
                {
                    throw new InvalidOperationException(
                        $"Arsip riwayat audit gagal pemeriksaan integritas ({hasil}) -- pemulihan dibatalkan, database tidak diubah.");
                }
            }

            var jumlahArsip = HitungBarisAudit(koneksi);
            if (jumlahArsip != jumlahSumberDiharapkan)
            {
                throw new InvalidOperationException(
                    $"Jumlah baris arsip ({jumlahArsip}) tidak cocok dengan sumber ({jumlahSumberDiharapkan}) -- pemulihan dibatalkan, database tidak diubah.");
            }
        }
        catch (Exception ex) when (ex is not InvalidOperationException)
        {
            throw new InvalidOperationException(
                "Arsip riwayat audit gagal diverifikasi -- pemulihan dibatalkan, database tidak diubah.", ex);
        }
        finally
        {
            SqliteConnection.ClearAllPools();
        }
    }

    private static long HitungBarisAudit(SqliteConnection connection)
    {
        using var command = connection.CreateCommand();
        command.CommandText = "SELECT COUNT(*) FROM audit;";
        return (long)command.ExecuteScalar()!;
    }

    // Berkas yang gagal verifikasi TIDAK dihapus (punya nilai forensik
    // untuk diperiksa manual) -- hanya ditandai jelas lewat akhiran nama
    // supaya tidak pernah tertukar dengan arsip yang sah. Upaya terbaik:
    // kegagalan menandai tidak boleh menutupi galat verifikasi asli.
    // internal (bukan private) supaya bisa diuji langsung -- lihat
    // LayananArsipAuditTests.
    internal static void TandaiGagal(string pathArsip)
    {
        try
        {
            SqliteConnection.ClearAllPools();
            var pathGagal = pathArsip + ".gagal-verifikasi";
            if (File.Exists(pathGagal))
            {
                File.Delete(pathGagal);
            }

            if (File.Exists(pathArsip))
            {
                File.Move(pathArsip, pathGagal);
            }
        }
        catch
        {
            // Lihat catatan di atas method -- sengaja ditelan.
        }
    }
}
