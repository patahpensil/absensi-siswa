using Absensi.Domain.Kehadiran;
using Microsoft.Data.Sqlite;

namespace Absensi.Data.Repositori;

public sealed class RepositoriKartu
{
    // Kartu tidak aktif (hilang/rusak/lulus) sengaja tidak ditemukan di
    // sini — ix_kartu_uid_aktif menjamin paling banyak satu kartu aktif per
    // UID (docs/SKEMA-DATABASE.md §6), jadi "tidak ditemukan" berarti tidak
    // terdaftar bagi PRD 16.8 langkah 4, sama seperti UID yang belum pernah
    // didaftarkan.
    // UAT Bug #11 (2026-09-23, redesain fundamental): `transaction` opsional
    // ditambahkan (default null, jadi seluruh pemanggil lama tetap
    // kompilasi tanpa perubahan) supaya method ini bisa dipanggil ULANG DI
    // DALAM transaksi Simpan — re-verifikasi keunikan UID persis sebelum
    // INSERT, menutup celah race condition antara pengecekan awal (tap) dan
    // penyimpanan (dua tap UID yang sama nyaris bersamaan).
    public KartuAktif? CariAktifBerdasarkanUid(SqliteConnection connection, string uid, SqliteTransaction? transaction = null)
    {
        using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = """
            SELECT pemilik_tipe, pemilik_id FROM kartu WHERE uid = $uid AND aktif = 1;
            """;
        command.Parameters.AddWithValue("$uid", uid);

        using var reader = command.ExecuteReader();
        if (!reader.Read())
        {
            return null;
        }

        return new KartuAktif(
            PemetaanEnum.PemilikTipeDariTeks(reader.GetString(0)),
            reader.GetInt32(1));
    }

    // UAT Bug #11 (2026-09-23): "satu siswa/guru satu kartu aktif" — dipakai
    // pra-cek (TapUid, di luar transaksi) DAN re-verifikasi (Simpan, di
    // dalam transaksi lewat parameter transaction) untuk pemilik yang
    // SUDAH punya kartu, supaya kartu kedua ditolak sebelum sempat tersimpan.
    public string? CariUidAktifUntukPemilik(SqliteConnection connection, JenisPemilikKartu pemilikTipe, int pemilikId, SqliteTransaction? transaction = null)
    {
        using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = """
            SELECT uid FROM kartu WHERE pemilik_tipe = $pemilikTipe AND pemilik_id = $pemilikId AND aktif = 1 LIMIT 1;
            """;
        command.Parameters.AddWithValue("$pemilikTipe", PemetaanEnum.TeksDariPemilikTipe(pemilikTipe));
        command.Parameters.AddWithValue("$pemilikId", pemilikId);

        return command.ExecuteScalar() as string;
    }

    // Hanya dipanggil lewat Absensi.Data.Kartu.LayananRegistrasiKartu, yang
    // membungkusnya dengan JalurPerubahanData (CLAUDE.md §4.1 aturan 4).
    // Pemanggil wajib memeriksa duplikat lintas master lebih dulu (PRD
    // §17.9) — ix_kartu_uid_aktif di sini hanya jaring pengaman terakhir.
    public void Daftarkan(
        SqliteConnection connection,
        SqliteTransaction transaction,
        string uid,
        JenisPemilikKartu pemilikTipe,
        int pemilikId,
        DateTime waktu)
    {
        using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = """
            INSERT INTO kartu (uid, pemilik_tipe, pemilik_id, aktif, didaftar_pada)
            VALUES ($uid, $pemilikTipe, $pemilikId, 1, $waktu);
            """;
        command.Parameters.AddWithValue("$uid", uid);
        command.Parameters.AddWithValue("$pemilikTipe", PemetaanEnum.TeksDariPemilikTipe(pemilikTipe));
        command.Parameters.AddWithValue("$pemilikId", pemilikId);
        command.Parameters.AddWithValue("$waktu", waktu.ToString("o"));
        command.ExecuteNonQuery();
    }

    // PRD §16.11: kartu lama dinonaktifkan, tidak pernah dihapus (CLAUDE.md
    // §4.2 aturan 8) — riwayat absensi tetap utuh karena menunjuk ke
    // pemilik, bukan ke kartu (aturan 7).
    public void Nonaktifkan(
        SqliteConnection connection,
        SqliteTransaction transaction,
        JenisPemilikKartu pemilikTipe,
        int pemilikId,
        DateTime waktu,
        string alasan)
    {
        using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = """
            UPDATE kartu SET aktif = 0, dinonaktifkan_pada = $waktu, alasan = $alasan
            WHERE pemilik_tipe = $pemilikTipe AND pemilik_id = $pemilikId AND aktif = 1;
            """;
        command.Parameters.AddWithValue("$waktu", waktu.ToString("o"));
        command.Parameters.AddWithValue("$alasan", alasan);
        command.Parameters.AddWithValue("$pemilikTipe", PemetaanEnum.TeksDariPemilikTipe(pemilikTipe));
        command.Parameters.AddWithValue("$pemilikId", pemilikId);
        command.ExecuteNonQuery();
    }
}
