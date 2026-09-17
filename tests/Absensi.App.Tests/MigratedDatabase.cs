using System.IO;
using Absensi.Data;
using Absensi.Data.Akun;
using Absensi.Data.Migrations;
using Microsoft.Data.Sqlite;

namespace Absensi.App.Tests;

// Salinan ringkas dari Absensi.Data.Tests.MigratedDatabase — sengaja tidak
// dibagi lintas proyek uji supaya masing-masing proyek uji berdiri sendiri.
internal sealed class MigratedDatabase : IDisposable
{
    private readonly string _path;

    public SqliteConnection Connection { get; }

    public MigratedDatabase()
    {
        _path = Path.Combine(Path.GetTempPath(), $"absensi-app-test-{Guid.NewGuid():N}.db");
        Connection = SqliteConnectionFactory.Open(_path);
        new MigrationRunner().Run(Connection);
    }

    public void Execute(string sql)
    {
        using var command = Connection.CreateCommand();
        command.CommandText = sql;
        command.ExecuteNonQuery();
    }

    // Satu tahun ajaran aktif, jadwal hari Kamis (hari=4) sebagai hari
    // sekolah biasa, satu siswa dan satu guru aktif dengan kartu masing-masing.
    public void SeedSekolahUntukGerbang()
    {
        Execute("""
            INSERT INTO tahun_ajaran (id, nama, semester, tgl_mulai, tgl_selesai, aktif)
            VALUES (1, '2026/2027', 'ganjil', '2026-07-01', '2026-12-19', 1);

            INSERT INTO jadwal_hari (tahun_ajaran_id, hari, hari_sekolah, jam_masuk, jam_rekap, batas_tap_masuk, mulai_pulang)
            VALUES (1, 4, 1, '07:30', '08:00', '12:00', '13:10');

            INSERT INTO guru (id, nip, nama, aktif) VALUES (1, '111', 'Guru Satu', 1);

            INSERT INTO siswa (id, nisn, nama, jenis_kelamin, aktif) VALUES (1, '0001', 'Siswa Satu', 'L', 1);
            INSERT INTO siswa (id, nisn, nama, jenis_kelamin, aktif) VALUES (2, '0002', 'Siswa Dua', 'L', 1);
            INSERT INTO siswa (id, nisn, nama, jenis_kelamin, aktif) VALUES (3, '0003', 'Siswa Tiga', 'L', 1);
            INSERT INTO siswa (id, nisn, nama, jenis_kelamin, aktif) VALUES (4, '0004', 'Siswa Empat', 'L', 1);
            INSERT INTO siswa (id, nisn, nama, jenis_kelamin, aktif) VALUES (5, '0005', 'Siswa Lima', 'L', 1);

            INSERT INTO kelas (id, tahun_ajaran_id, nama, tingkat, wali_guru_id) VALUES (1, 1, '7A', 7, 1);

            INSERT INTO siswa_kelas (siswa_id, kelas_id, tahun_ajaran_id) VALUES (1, 1, 1);
            INSERT INTO siswa_kelas (siswa_id, kelas_id, tahun_ajaran_id) VALUES (2, 1, 1);
            INSERT INTO siswa_kelas (siswa_id, kelas_id, tahun_ajaran_id) VALUES (3, 1, 1);
            INSERT INTO siswa_kelas (siswa_id, kelas_id, tahun_ajaran_id) VALUES (4, 1, 1);
            INSERT INTO siswa_kelas (siswa_id, kelas_id, tahun_ajaran_id) VALUES (5, 1, 1);

            INSERT INTO kartu (uid, pemilik_tipe, pemilik_id, aktif, didaftar_pada)
            VALUES ('UID-1', 'siswa', 1, 1, '2026-09-01T00:00:00');
            INSERT INTO kartu (uid, pemilik_tipe, pemilik_id, aktif, didaftar_pada)
            VALUES ('UID-2', 'siswa', 2, 1, '2026-09-01T00:00:00');
            INSERT INTO kartu (uid, pemilik_tipe, pemilik_id, aktif, didaftar_pada)
            VALUES ('UID-3', 'siswa', 3, 1, '2026-09-01T00:00:00');
            INSERT INTO kartu (uid, pemilik_tipe, pemilik_id, aktif, didaftar_pada)
            VALUES ('UID-4', 'siswa', 4, 1, '2026-09-01T00:00:00');
            INSERT INTO kartu (uid, pemilik_tipe, pemilik_id, aktif, didaftar_pada)
            VALUES ('UID-5', 'siswa', 5, 1, '2026-09-01T00:00:00');
            INSERT INTO kartu (uid, pemilik_tipe, pemilik_id, aktif, didaftar_pada)
            VALUES ('UID-GURU-1', 'guru', 1, 1, '2026-09-01T00:00:00');

            INSERT INTO absensi_guru (tanggal, guru_id, status, sumber)
            VALUES ('2026-09-17', 1, 'alpa', 'sistem');

            INSERT INTO absensi_siswa (tanggal, siswa_id, kelas_id, status, sumber)
            VALUES ('2026-09-17', 1, 1, 'alpa', 'sistem');
            INSERT INTO absensi_siswa (tanggal, siswa_id, kelas_id, status, sumber)
            VALUES ('2026-09-17', 2, 1, 'alpa', 'sistem');
            INSERT INTO absensi_siswa (tanggal, siswa_id, kelas_id, status, sumber)
            VALUES ('2026-09-17', 3, 1, 'alpa', 'sistem');
            INSERT INTO absensi_siswa (tanggal, siswa_id, kelas_id, status, sumber)
            VALUES ('2026-09-17', 4, 1, 'alpa', 'sistem');
            INSERT INTO absensi_siswa (tanggal, siswa_id, kelas_id, status, sumber)
            VALUES ('2026-09-17', 5, 1, 'alpa', 'sistem');
            """);
    }

    // Sekolah kecil untuk pengujian RegistrasiKartuViewModel (Tugas 8, PRD
    // §16.5): kelas 7A dengan tiga siswa — satu sudah punya kartu, dua
    // belum — plus satu kartu terpakai di kelas 7B lain untuk uji pesan
    // penolakan lintas kelas (§16.5 langkah 3.4).
    public void SeedKelasUntukRegistrasiKartu()
    {
        Execute("""
            INSERT INTO tahun_ajaran (id, nama, semester, tgl_mulai, tgl_selesai, aktif)
            VALUES (1, '2026/2027', 'ganjil', '2026-07-01', '2026-12-19', 1);

            INSERT INTO kelas (id, tahun_ajaran_id, nama, tingkat) VALUES (1, 1, '7A', 7);
            INSERT INTO kelas (id, tahun_ajaran_id, nama, tingkat) VALUES (2, 1, '7B', 7);

            INSERT INTO siswa (id, nisn, nama, jenis_kelamin, aktif) VALUES (1, '0001', 'Siswa Sudah Kartu', 'L', 1);
            INSERT INTO siswa (id, nisn, nama, jenis_kelamin, aktif) VALUES (2, '0002', 'Siswa Belum Satu', 'L', 1);
            INSERT INTO siswa (id, nisn, nama, jenis_kelamin, aktif) VALUES (3, '0003', 'Siswa Belum Dua', 'L', 1);
            INSERT INTO siswa (id, nisn, nama, jenis_kelamin, aktif) VALUES (4, '0004', 'Siswa Kelas Lain', 'L', 1);

            INSERT INTO siswa_kelas (siswa_id, kelas_id, tahun_ajaran_id) VALUES (1, 1, 1);
            INSERT INTO siswa_kelas (siswa_id, kelas_id, tahun_ajaran_id) VALUES (2, 1, 1);
            INSERT INTO siswa_kelas (siswa_id, kelas_id, tahun_ajaran_id) VALUES (3, 1, 1);
            INSERT INTO siswa_kelas (siswa_id, kelas_id, tahun_ajaran_id) VALUES (4, 2, 1);

            INSERT INTO kartu (uid, pemilik_tipe, pemilik_id, aktif, didaftar_pada)
            VALUES ('UID-SISWA-LAMA', 'siswa', 1, 1, '2026-09-01T00:00:00');
            INSERT INTO kartu (uid, pemilik_tipe, pemilik_id, aktif, didaftar_pada)
            VALUES ('UID-KELAS-LAIN', 'siswa', 4, 1, '2026-09-01T00:00:00');
            """);

        TambahAkun(1, "admin1", "admin", guruId: null);
    }

    public void TambahSekolah(string nama)
    {
        using var command = Connection.CreateCommand();
        command.CommandText = "INSERT INTO sekolah (id, nama) VALUES (1, $nama);";
        command.Parameters.AddWithValue("$nama", nama);
        command.ExecuteNonQuery();
    }

    public const string SandiUji = "Rahasia123!";

    // guruId hanya relevan untuk peran wali_kelas (akun.guru_id, dibandingkan
    // terhadap kelas.wali_guru_id — CLAUDE.md §4.5 aturan 20).
    public void TambahAkun(int id, string username, string peran, int? guruId)
    {
        using var command = Connection.CreateCommand();
        command.CommandText = """
            INSERT INTO akun (id, username, sandi_hash, peran, guru_id, aktif, dibuat_pada)
            VALUES ($id, $username, $sandiHash, $peran, $guruId, 1, '2026-09-01T00:00:00');
            """;
        command.Parameters.AddWithValue("$id", id);
        command.Parameters.AddWithValue("$username", username);
        command.Parameters.AddWithValue("$sandiHash", HashSandi.Buat(SandiUji));
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
            // File lock Windows kadang belum lepas persis setelah Dispose.
        }
    }
}
