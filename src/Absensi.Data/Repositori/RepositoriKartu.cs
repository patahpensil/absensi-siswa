using Microsoft.Data.Sqlite;

namespace Absensi.Data.Repositori;

public sealed class RepositoriKartu
{
    // Kartu tidak aktif (hilang/rusak/lulus) sengaja tidak ditemukan di
    // sini — ix_kartu_uid_aktif menjamin paling banyak satu kartu aktif per
    // UID (docs/SKEMA-DATABASE.md §6), jadi "tidak ditemukan" berarti tidak
    // terdaftar bagi PRD 16.8 langkah 4, sama seperti UID yang belum pernah
    // didaftarkan.
    public KartuAktif? CariAktifBerdasarkanUid(SqliteConnection connection, string uid)
    {
        using var command = connection.CreateCommand();
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
}
