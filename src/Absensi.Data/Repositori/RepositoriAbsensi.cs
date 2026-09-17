using Absensi.Domain.Kehadiran;
using Microsoft.Data.Sqlite;

namespace Absensi.Data.Repositori;

public sealed class RepositoriAbsensi
{
    // CLAUDE.md §4.3 aturan 11: status awal setiap siswa setiap hari adalah
    // alpa, dibuat saat pembukaan hari. INSERT OR IGNORE bersandar pada
    // UNIQUE(tanggal, siswa_id) (docs/SKEMA-DATABASE.md §7) supaya
    // menjalankan pembukaan hari berulang tidak menggandakan baris.
    public void BukaHariSiswa(
        SqliteConnection connection,
        SqliteTransaction transaction,
        DateOnly tanggal,
        IReadOnlyList<AnggotaKelas> anggotaAktif)
    {
        using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = """
            INSERT OR IGNORE INTO absensi_siswa (tanggal, siswa_id, kelas_id, status, sumber)
            VALUES ($tanggal, $siswaId, $kelasId, 'alpa', 'sistem');
            """;
        var parameterTanggal = command.Parameters.Add("$tanggal", SqliteType.Text);
        var parameterSiswaId = command.Parameters.Add("$siswaId", SqliteType.Integer);
        var parameterKelasId = command.Parameters.Add("$kelasId", SqliteType.Integer);
        parameterTanggal.Value = tanggal.ToString("yyyy-MM-dd");

        foreach (var anggota in anggotaAktif)
        {
            parameterSiswaId.Value = anggota.SiswaId;
            parameterKelasId.Value = anggota.KelasId;
            command.ExecuteNonQuery();
        }
    }

    public void BukaHariGuru(
        SqliteConnection connection,
        SqliteTransaction transaction,
        DateOnly tanggal,
        IReadOnlyList<int> guruIdAktif)
    {
        using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = """
            INSERT OR IGNORE INTO absensi_guru (tanggal, guru_id, status, sumber)
            VALUES ($tanggal, $guruId, 'alpa', 'sistem');
            """;
        var parameterTanggal = command.Parameters.Add("$tanggal", SqliteType.Text);
        var parameterGuruId = command.Parameters.Add("$guruId", SqliteType.Integer);
        parameterTanggal.Value = tanggal.ToString("yyyy-MM-dd");

        foreach (var guruId in guruIdAktif)
        {
            parameterGuruId.Value = guruId;
            command.ExecuteNonQuery();
        }
    }

    public AbsensiSiswaHariIni? CariHariIniSiswa(SqliteConnection connection, DateOnly tanggal, int siswaId)
    {
        using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT status, jam_masuk, jam_pulang FROM absensi_siswa
            WHERE tanggal = $tanggal AND siswa_id = $siswaId;
            """;
        command.Parameters.AddWithValue("$tanggal", tanggal.ToString("yyyy-MM-dd"));
        command.Parameters.AddWithValue("$siswaId", siswaId);

        using var reader = command.ExecuteReader();
        if (!reader.Read())
        {
            return null;
        }

        return new AbsensiSiswaHariIni(
            PemetaanEnum.StatusSiswaDariTeks(reader.GetString(0)),
            BacaJam(reader, 1),
            BacaJam(reader, 2));
    }

    public AbsensiGuruHariIni? CariHariIniGuru(SqliteConnection connection, DateOnly tanggal, int guruId)
    {
        using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT status, jam_masuk, jam_pulang FROM absensi_guru
            WHERE tanggal = $tanggal AND guru_id = $guruId;
            """;
        command.Parameters.AddWithValue("$tanggal", tanggal.ToString("yyyy-MM-dd"));
        command.Parameters.AddWithValue("$guruId", guruId);

        using var reader = command.ExecuteReader();
        if (!reader.Read())
        {
            return null;
        }

        return new AbsensiGuruHariIni(
            PemetaanEnum.StatusGuruDariTeks(reader.GetString(0)),
            BacaJam(reader, 1),
            BacaJam(reader, 2));
    }

    public void SimpanKeputusanSiswa(
        SqliteConnection connection,
        SqliteTransaction transaction,
        DateOnly tanggal,
        int siswaId,
        DateTime waktu,
        KeputusanTap keputusan)
    {
        switch (keputusan.Tindakan)
        {
            case JenisTindakanTap.IsiJamMasuk:
                Eksekusi(connection, transaction, """
                    UPDATE absensi_siswa SET status = $status, jam_masuk = $jam
                    WHERE tanggal = $tanggal AND siswa_id = $siswaId;
                    """,
                    ("$status", PemetaanEnum.TeksDariStatusSiswa(keputusan.StatusSiswaBaru!.Value)),
                    ("$jam", JamTeks(waktu)),
                    ("$tanggal", TanggalTeks(tanggal)),
                    ("$siswaId", siswaId));
                break;

            case JenisTindakanTap.IsiJamPulang:
                Eksekusi(connection, transaction, """
                    UPDATE absensi_siswa SET jam_pulang = $jam
                    WHERE tanggal = $tanggal AND siswa_id = $siswaId;
                    """,
                    ("$jam", JamTeks(waktu)),
                    ("$tanggal", TanggalTeks(tanggal)),
                    ("$siswaId", siswaId));
                break;
        }
    }

    public void SimpanKeputusanGuru(
        SqliteConnection connection,
        SqliteTransaction transaction,
        DateOnly tanggal,
        int guruId,
        DateTime waktu,
        KeputusanTap keputusan)
    {
        switch (keputusan.Tindakan)
        {
            case JenisTindakanTap.IsiJamMasuk:
                Eksekusi(connection, transaction, """
                    UPDATE absensi_guru SET status = $status, jam_masuk = $jam
                    WHERE tanggal = $tanggal AND guru_id = $guruId;
                    """,
                    ("$status", PemetaanEnum.TeksDariStatusGuru(keputusan.StatusGuruBaru!.Value)),
                    ("$jam", JamTeks(waktu)),
                    ("$tanggal", TanggalTeks(tanggal)),
                    ("$guruId", guruId));
                break;

            case JenisTindakanTap.IsiJamPulang:
                Eksekusi(connection, transaction, """
                    UPDATE absensi_guru SET status = $status, jam_pulang = $jam
                    WHERE tanggal = $tanggal AND guru_id = $guruId;
                    """,
                    ("$status", PemetaanEnum.TeksDariStatusGuru(keputusan.StatusGuruBaru!.Value)),
                    ("$jam", JamTeks(waktu)),
                    ("$tanggal", TanggalTeks(tanggal)),
                    ("$guruId", guruId));
                break;
        }
    }

    // Ringkasan harian layar gerbang (PRD §14.1: "Hadir / Terlambat / Belum
    // tap / Guru").
    public int HitungSiswaBerdasarkanStatus(SqliteConnection connection, DateOnly tanggal, StatusAbsensiSiswa status)
    {
        using var command = connection.CreateCommand();
        command.CommandText = "SELECT COUNT(*) FROM absensi_siswa WHERE tanggal = $tanggal AND status = $status;";
        command.Parameters.AddWithValue("$tanggal", TanggalTeks(tanggal));
        command.Parameters.AddWithValue("$status", PemetaanEnum.TeksDariStatusSiswa(status));
        return Convert.ToInt32(command.ExecuteScalar());
    }

    // "Guru" pada ringkasan harian gerbang berarti guru yang sudah tap
    // masuk hari ini (status masuk atau hadir) — PRD tidak memecah guru
    // per status seperti siswa, hanya satu angka.
    public int HitungGuruSudahTapMasuk(SqliteConnection connection, DateOnly tanggal)
    {
        using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT COUNT(*) FROM absensi_guru
            WHERE tanggal = $tanggal AND status IN ('masuk', 'hadir');
            """;
        command.Parameters.AddWithValue("$tanggal", TanggalTeks(tanggal));
        return Convert.ToInt32(command.ExecuteScalar());
    }

    private static void Eksekusi(
        SqliteConnection connection,
        SqliteTransaction transaction,
        string sql,
        params (string Nama, object Nilai)[] parameter)
    {
        using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = sql;
        foreach (var (nama, nilai) in parameter)
        {
            command.Parameters.AddWithValue(nama, nilai);
        }

        command.ExecuteNonQuery();
    }

    private static string TanggalTeks(DateOnly tanggal) => tanggal.ToString("yyyy-MM-dd");

    private static string JamTeks(DateTime waktu) => TimeOnly.FromDateTime(waktu).ToString("HH:mm");

    private static TimeOnly? BacaJam(SqliteDataReader reader, int kolom) =>
        reader.IsDBNull(kolom) ? null : TimeOnly.Parse(reader.GetString(kolom));
}
