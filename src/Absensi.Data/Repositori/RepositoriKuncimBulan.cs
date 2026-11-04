using Microsoft.Data.Sqlite;

namespace Absensi.Data.Repositori;

public sealed class RepositoriKuncimBulan
{
    // Dipakai LayananKoreksi/LayananStatusGuru sebelum mengizinkan
    // perubahan (PRD §10: "Koreksi terbuka selama bulan bersangkutan
    // belum dikunci") dan Absensi.App/layar Kunci Bulan untuk menampilkan
    // status sekarang.
    public bool SudahDikunci(SqliteConnection connection, int tahun, int bulan)
    {
        using var command = connection.CreateCommand();
        command.CommandText = "SELECT COUNT(*) FROM kunci_bulan WHERE tahun = $tahun AND bulan = $bulan;";
        command.Parameters.AddWithValue("$tahun", tahun);
        command.Parameters.AddWithValue("$bulan", bulan);
        return Convert.ToInt32(command.ExecuteScalar()) > 0;
    }

    // Dipakai LayananKalender (rentang tanggal yang ditandai) dan
    // LayananJadwalHari (rentang tgl_mulai..tgl_selesai satu tahun ajaran —
    // jadwal_hari berdampak retroaktif ke semua bulan tahun ajaran itu,
    // bukan cuma bulan yang sedang dilihat admin). Dibandingkan sebagai
    // tahun*100+bulan supaya lintas tahun tetap benar urutannya.
    // dikunci_oleh diselesaikan ke nama tampil: nama guru bila akun itu
    // menunjuk ke baris guru (jarang untuk Kepsek/Admin), kalau tidak
    // username akun itu sendiri.
    public KuncimBulanInfo? CariPertamaDalamRentang(SqliteConnection connection, DateOnly tanggalAwal, DateOnly tanggalAkhir)
    {
        using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT k.tahun, k.bulan, COALESCE(g.nama, a.username), k.dikunci_pada
            FROM kunci_bulan k
            JOIN akun a ON a.id = k.dikunci_oleh
            LEFT JOIN guru g ON g.id = a.guru_id
            WHERE (k.tahun * 100 + k.bulan) BETWEEN $awal AND $akhir
            ORDER BY k.tahun, k.bulan
            LIMIT 1;
            """;
        command.Parameters.AddWithValue("$awal", tanggalAwal.Year * 100 + tanggalAwal.Month);
        command.Parameters.AddWithValue("$akhir", tanggalAkhir.Year * 100 + tanggalAkhir.Month);

        using var reader = command.ExecuteReader();
        if (!reader.Read())
        {
            return null;
        }

        return new KuncimBulanInfo(reader.GetInt32(0), reader.GetInt32(1), reader.GetString(2), DateTime.Parse(reader.GetString(3)));
    }

    // Hanya dipanggil lewat Absensi.Data.Koreksi.LayananKuncimBulan, yang
    // membungkusnya dengan JalurPerubahanData (PRD §12).
    // PRIMARY KEY (tahun, bulan) menolak kunci ganda pada level database —
    // pemanggil tetap memeriksa SudahDikunci dulu supaya pesan galatnya
    // jelas, bukan SqliteException mentah.
    public void Kunci(SqliteConnection connection, SqliteTransaction transaction, int tahun, int bulan, int akunId, DateTime waktu)
    {
        using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = """
            INSERT INTO kunci_bulan (tahun, bulan, dikunci_oleh, dikunci_pada)
            VALUES ($tahun, $bulan, $akunId, $waktu);
            """;
        command.Parameters.AddWithValue("$tahun", tahun);
        command.Parameters.AddWithValue("$bulan", bulan);
        command.Parameters.AddWithValue("$akunId", akunId);
        command.Parameters.AddWithValue("$waktu", waktu.ToString("o"));
        command.ExecuteNonQuery();
    }
}
