using Absensi.Data.Akun;
using Absensi.Data.Migrations;
using Microsoft.Data.Sqlite;

namespace Absensi.Data.Tests;

// Database SQLite sementara yang sudah dijalankan migrasinya, dipakai ulang
// oleh setiap kelas pengujian. Satu berkas per instans agar PRAGMA
// journal_mode = WAL (yang tidak berlaku pada ":memory:") benar-benar diuji.
internal sealed class MigratedDatabase : IDisposable
{
    private readonly string _path;

    public SqliteConnection Connection { get; }

    public MigratedDatabase()
    {
        _path = Path.Combine(Path.GetTempPath(), $"absensi-test-{Guid.NewGuid():N}.db");
        Connection = SqliteConnectionFactory.Open(_path);
        new MigrationRunner().Run(Connection);
    }

    public void Execute(string sql)
    {
        using var command = Connection.CreateCommand();
        command.CommandText = sql;
        command.ExecuteNonQuery();
    }

    public void SeedBaselineReferenceRows()
    {
        Execute("""
            INSERT INTO tahun_ajaran (id, nama, semester, tgl_mulai, tgl_selesai, aktif)
            VALUES (1, '2026/2027', 'ganjil', '2026-07-01', '2026-12-19', 1);

            INSERT INTO guru (id, nip, nama) VALUES (1, '111', 'Guru Satu');

            INSERT INTO siswa (id, nisn, nama, jenis_kelamin) VALUES (1, '0001', 'Siswa Satu', 'L');

            INSERT INTO kelas (id, tahun_ajaran_id, nama, tingkat, wali_guru_id)
            VALUES (1, 1, '7A', 7, 1);

            INSERT INTO kelas (id, tahun_ajaran_id, nama, tingkat, wali_guru_id)
            VALUES (2, 1, '7B', 7, 1);
            """);
    }

    // Sekolah kecil lengkap untuk pengujian pembukaan hari dan pemrosesan
    // tap (Tugas 3): satu tahun ajaran aktif, jadwal_hari 1-7 (Minggu
    // libur, Jumat beda batas tap dan jam pulang sesuai CLAUDE.md §7), satu
    // guru dan satu siswa aktif plus satu masing-masing yang nonaktif, dan
    // kartu untuk keempatnya.
    public void SeedSekolahUntukTap()
    {
        Execute("""
            INSERT INTO tahun_ajaran (id, nama, semester, tgl_mulai, tgl_selesai, aktif)
            VALUES (1, '2026/2027', 'ganjil', '2026-07-01', '2026-12-19', 1);
            """);

        for (var hari = 1; hari <= 7; hari++)
        {
            var hariSekolah = hari == 7 ? 0 : 1;
            var batasTapMasuk = hari == 5 ? "10:00" : "12:00";
            var mulaiPulang = hari == 5 ? "11:30" : "13:10";

            using var command = Connection.CreateCommand();
            command.CommandText = """
                INSERT INTO jadwal_hari
                    (tahun_ajaran_id, hari, hari_sekolah, jam_masuk, jam_rekap, batas_tap_masuk, mulai_pulang)
                VALUES (1, $hari, $hariSekolah, '07:30', '08:00', $batasTapMasuk, $mulaiPulang);
                """;
            command.Parameters.AddWithValue("$hari", hari);
            command.Parameters.AddWithValue("$hariSekolah", hariSekolah);
            command.Parameters.AddWithValue("$batasTapMasuk", hariSekolah == 1 ? batasTapMasuk : DBNull.Value);
            command.Parameters.AddWithValue("$mulaiPulang", hariSekolah == 1 ? mulaiPulang : DBNull.Value);
            command.ExecuteNonQuery();
        }

        Execute("""
            INSERT INTO guru (id, nip, nama, aktif) VALUES (1, '111', 'Guru Aktif', 1);
            INSERT INTO guru (id, nip, nama, aktif) VALUES (2, '222', 'Guru Nonaktif', 0);

            INSERT INTO siswa (id, nisn, nama, jenis_kelamin, aktif) VALUES (1, '0001', 'Siswa Aktif', 'L', 1);
            INSERT INTO siswa (id, nisn, nama, jenis_kelamin, aktif) VALUES (2, '0002', 'Siswa Nonaktif', 'L', 0);

            INSERT INTO kelas (id, tahun_ajaran_id, nama, tingkat, wali_guru_id) VALUES (1, 1, '7A', 7, 1);

            INSERT INTO siswa_kelas (siswa_id, kelas_id, tahun_ajaran_id) VALUES (1, 1, 1);
            INSERT INTO siswa_kelas (siswa_id, kelas_id, tahun_ajaran_id) VALUES (2, 1, 1);

            INSERT INTO kartu (uid, pemilik_tipe, pemilik_id, aktif, didaftar_pada)
            VALUES ('UID-SISWA-1', 'siswa', 1, 1, '2026-09-01T00:00:00');
            INSERT INTO kartu (uid, pemilik_tipe, pemilik_id, aktif, didaftar_pada)
            VALUES ('UID-SISWA-2', 'siswa', 2, 1, '2026-09-01T00:00:00');
            INSERT INTO kartu (uid, pemilik_tipe, pemilik_id, aktif, didaftar_pada)
            VALUES ('UID-GURU-1', 'guru', 1, 1, '2026-09-01T00:00:00');
            INSERT INTO kartu (uid, pemilik_tipe, pemilik_id, aktif, didaftar_pada)
            VALUES ('UID-GURU-2', 'guru', 2, 1, '2026-09-01T00:00:00');
            """);
    }

    // Sekolah kecil untuk pengujian akun, peran, dan audit (Tugas 4): dua
    // wali kelas masing-masing wali satu kelas berbeda, satu kelas tanpa
    // wali, dan akun kepala sekolah/admin dengan sandi sungguhan supaya
    // Autentikasi bisa diuji ujung ke ujung.
    public const string SandiKepsek = "Rahasia123!";

    public void SeedAkunUntukUji()
    {
        Execute("""
            INSERT INTO tahun_ajaran (id, nama, semester, tgl_mulai, tgl_selesai, aktif)
            VALUES (1, '2026/2027', 'ganjil', '2026-07-01', '2026-12-19', 1);

            INSERT INTO guru (id, nip, nama) VALUES (1, '111', 'Wali Kelas Satu');
            INSERT INTO guru (id, nip, nama) VALUES (2, '222', 'Wali Kelas Dua');

            INSERT INTO kelas (id, tahun_ajaran_id, nama, tingkat, wali_guru_id) VALUES (1, 1, '7A', 7, 1);
            INSERT INTO kelas (id, tahun_ajaran_id, nama, tingkat, wali_guru_id) VALUES (2, 1, '7B', 7, 2);
            INSERT INTO kelas (id, tahun_ajaran_id, nama, tingkat, wali_guru_id) VALUES (3, 1, '7C', 7, NULL);

            INSERT INTO akun (id, username, sandi_hash, peran, guru_id, aktif, dibuat_pada)
            VALUES (2, 'wali_a', 'tidak-terpakai', 'wali_kelas', 1, 1, '2026-09-01T00:00:00');
            INSERT INTO akun (id, username, sandi_hash, peran, guru_id, aktif, dibuat_pada)
            VALUES (3, 'wali_b_nonaktif', 'tidak-terpakai', 'wali_kelas', 2, 0, '2026-09-01T00:00:00');
            """);

        TambahAkunDenganSandi(1, "kepsek", SandiKepsek, "kepala_sekolah", guruId: null);
    }

    private void TambahAkunDenganSandi(int id, string username, string sandi, string peran, int? guruId)
    {
        using var command = Connection.CreateCommand();
        command.CommandText = """
            INSERT INTO akun (id, username, sandi_hash, peran, guru_id, aktif, dibuat_pada)
            VALUES ($id, $username, $sandiHash, $peran, $guruId, 1, '2026-09-01T00:00:00');
            """;
        command.Parameters.AddWithValue("$id", id);
        command.Parameters.AddWithValue("$username", username);
        command.Parameters.AddWithValue("$sandiHash", HashSandi.Buat(sandi));
        command.Parameters.AddWithValue("$peran", peran);
        command.Parameters.AddWithValue("$guruId", (object?)guruId ?? DBNull.Value);
        command.ExecuteNonQuery();
    }

    public void Dispose()
    {
        Connection.Dispose();
        SqliteConnection.ClearAllPools();

        try
        {
            File.Delete(_path);
        }
        catch (IOException)
        {
            // File lock Windows kadang belum lepas persis setelah Dispose;
            // folder temp OS yang membersihkannya, bukan kegagalan pengujian.
        }
    }
}
