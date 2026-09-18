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

            INSERT INTO pairing (subjek_tipe, subjek_id, token, status, chat_id, dibuat_pada, tertaut_pada)
            VALUES ('siswa', 1, 'TOKUJI1', 'tertaut', 'chat-siswa-uji', '2026-09-01T00:00:00', '2026-09-01T00:00:00');
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

    // Sekolah kecil untuk pengujian master data dan impor (Tugas 7): satu
    // tahun ajaran aktif, satu kelas (7A), dan satu akun admin untuk dipakai
    // sebagai aktor JalurPerubahanData.
    public void SeedMasterDataUntukUji()
    {
        Execute("""
            INSERT INTO tahun_ajaran (id, nama, semester, tgl_mulai, tgl_selesai, aktif)
            VALUES (1, '2026/2027', 'ganjil', '2026-07-01', '2026-12-19', 1);

            INSERT INTO kelas (id, tahun_ajaran_id, nama, tingkat) VALUES (1, 1, '7A', 7);
            """);

        TambahAkunDenganSandi(1, "admin1", SandiKepsek, "admin", guruId: null);
    }

    // Sekolah kecil untuk pengujian Registrasi Kartu (Tugas 8, PRD §16.5/
    // §16.11/§17.9): kelas 7A dengan tiga siswa — satu sudah punya kartu
    // (Siswa Sudah Kartu), dua belum (Siswa Belum Satu/Dua) — plus satu
    // guru tanpa kartu (untuk uji akses §17.10) dan satu guru YANG SUDAH
    // punya kartu di master lain (Guru Sudah Kartu, UID-GURU-LAMA) untuk
    // uji penolakan UID lintas master.
    public void SeedKelasUntukRegistrasiKartu()
    {
        Execute("""
            INSERT INTO tahun_ajaran (id, nama, semester, tgl_mulai, tgl_selesai, aktif)
            VALUES (1, '2026/2027', 'ganjil', '2026-07-01', '2026-12-19', 1);

            INSERT INTO kelas (id, tahun_ajaran_id, nama, tingkat, wali_guru_id) VALUES (1, 1, '7A', 7, NULL);

            INSERT INTO siswa (id, nisn, nama, jenis_kelamin, aktif) VALUES (1, '0001', 'Siswa Sudah Kartu', 'L', 1);
            INSERT INTO siswa (id, nisn, nama, jenis_kelamin, aktif) VALUES (2, '0002', 'Siswa Belum Satu', 'L', 1);
            INSERT INTO siswa (id, nisn, nama, jenis_kelamin, aktif) VALUES (3, '0003', 'Siswa Belum Dua', 'L', 1);

            INSERT INTO siswa_kelas (siswa_id, kelas_id, tahun_ajaran_id) VALUES (1, 1, 1);
            INSERT INTO siswa_kelas (siswa_id, kelas_id, tahun_ajaran_id) VALUES (2, 1, 1);
            INSERT INTO siswa_kelas (siswa_id, kelas_id, tahun_ajaran_id) VALUES (3, 1, 1);

            INSERT INTO kartu (uid, pemilik_tipe, pemilik_id, aktif, didaftar_pada)
            VALUES ('UID-SISWA-LAMA', 'siswa', 1, 1, '2026-09-01T00:00:00');

            INSERT INTO guru (id, nip, nama, aktif) VALUES (1, '111', 'Guru Belum Kartu', 1);
            INSERT INTO guru (id, nip, nama, aktif) VALUES (2, '222', 'Guru Sudah Kartu', 1);

            INSERT INTO kartu (uid, pemilik_tipe, pemilik_id, aktif, didaftar_pada)
            VALUES ('UID-GURU-LAMA', 'guru', 2, 1, '2026-09-01T00:00:00');
            """);

        TambahAkunDenganSandi(1, "admin1", SandiKepsek, "admin", guruId: null);
        TambahAkunDenganSandi(2, "wali_a", SandiKepsek, "wali_kelas", guruId: 1);
    }

    // Sekolah kecil untuk pengujian LayananNotifikasi (Tugas 10, PRD
    // §9.2/§9.3): jadwal Kamis 07.30/08.00/12.00/13.10 (CLAUDE.md §7).
    // Kelas 7A wali guru id=1 (tertaut Telegram). Siswa: id=1 Hadir
    // (tertaut), id=2 Terlambat (tertaut), id=3 Alpa/belum tap (tertaut),
    // id=4 Hadir+sudah tap pulang (tertaut, BUKAN kandidat bolos), id=5
    // Hadir+belum tap pulang (tertaut, kandidat bolos). Guru id=2 "Guru
    // Alpa" belum tap sama sekali, tertaut sendiri (untuk guru_tanpa_kabar).
    public void SeedSekolahUntukNotifikasi()
    {
        Execute("""
            INSERT INTO tahun_ajaran (id, nama, semester, tgl_mulai, tgl_selesai, aktif)
            VALUES (1, '2026/2027', 'ganjil', '2026-07-01', '2026-12-19', 1);

            INSERT INTO jadwal_hari (tahun_ajaran_id, hari, hari_sekolah, jam_masuk, jam_rekap, batas_tap_masuk, mulai_pulang)
            VALUES (1, 4, 1, '07:30', '08:00', '12:00', '13:10');

            INSERT INTO guru (id, nip, nama, aktif) VALUES (1, '111', 'Wali Kelas A', 1);
            INSERT INTO guru (id, nip, nama, aktif) VALUES (2, '222', 'Guru Alpa', 1);

            INSERT INTO kelas (id, tahun_ajaran_id, nama, tingkat, wali_guru_id) VALUES (1, 1, '7A', 7, 1);

            INSERT INTO siswa (id, nisn, nama, jenis_kelamin, aktif) VALUES (1, '0001', 'Siswa Hadir', 'L', 1);
            INSERT INTO siswa (id, nisn, nama, jenis_kelamin, aktif) VALUES (2, '0002', 'Siswa Terlambat', 'L', 1);
            INSERT INTO siswa (id, nisn, nama, jenis_kelamin, aktif) VALUES (3, '0003', 'Siswa Alpa', 'L', 1);
            INSERT INTO siswa (id, nisn, nama, jenis_kelamin, aktif) VALUES (4, '0004', 'Siswa Sudah Pulang', 'L', 1);
            INSERT INTO siswa (id, nisn, nama, jenis_kelamin, aktif) VALUES (5, '0005', 'Siswa Bolos', 'L', 1);

            INSERT INTO siswa_kelas (siswa_id, kelas_id, tahun_ajaran_id) VALUES (1, 1, 1);
            INSERT INTO siswa_kelas (siswa_id, kelas_id, tahun_ajaran_id) VALUES (2, 1, 1);
            INSERT INTO siswa_kelas (siswa_id, kelas_id, tahun_ajaran_id) VALUES (3, 1, 1);
            INSERT INTO siswa_kelas (siswa_id, kelas_id, tahun_ajaran_id) VALUES (4, 1, 1);
            INSERT INTO siswa_kelas (siswa_id, kelas_id, tahun_ajaran_id) VALUES (5, 1, 1);

            INSERT INTO absensi_siswa (tanggal, siswa_id, kelas_id, status, jam_masuk, jam_pulang, sumber)
            VALUES ('2026-09-17', 1, 1, 'hadir', '07:20', '13:15', 'tap');
            INSERT INTO absensi_siswa (tanggal, siswa_id, kelas_id, status, jam_masuk, jam_pulang, sumber)
            VALUES ('2026-09-17', 2, 1, 'terlambat', '07:45', NULL, 'tap');
            INSERT INTO absensi_siswa (tanggal, siswa_id, kelas_id, status, jam_masuk, jam_pulang, sumber)
            VALUES ('2026-09-17', 3, 1, 'alpa', NULL, NULL, 'sistem');
            INSERT INTO absensi_siswa (tanggal, siswa_id, kelas_id, status, jam_masuk, jam_pulang, sumber)
            VALUES ('2026-09-17', 4, 1, 'hadir', '07:20', '13:15', 'tap');
            INSERT INTO absensi_siswa (tanggal, siswa_id, kelas_id, status, jam_masuk, jam_pulang, sumber)
            VALUES ('2026-09-17', 5, 1, 'hadir', '07:20', NULL, 'tap');

            INSERT INTO absensi_guru (tanggal, guru_id, status, sumber)
            VALUES ('2026-09-17', 1, 'masuk', 'tap');
            INSERT INTO absensi_guru (tanggal, guru_id, status, sumber)
            VALUES ('2026-09-17', 2, 'alpa', 'sistem');

            INSERT INTO pairing (subjek_tipe, subjek_id, token, status, chat_id, dibuat_pada, tertaut_pada)
            VALUES ('guru', 1, 'TOKWALI', 'tertaut', 'chat-wali-1', '2026-09-01T00:00:00', '2026-09-01T00:00:00');
            INSERT INTO pairing (subjek_tipe, subjek_id, token, status, chat_id, dibuat_pada, tertaut_pada)
            VALUES ('guru', 2, 'TOKGURU2', 'tertaut', 'chat-guru-2', '2026-09-01T00:00:00', '2026-09-01T00:00:00');
            INSERT INTO pairing (subjek_tipe, subjek_id, token, status, chat_id, dibuat_pada, tertaut_pada)
            VALUES ('siswa', 1, 'TOKS1', 'tertaut', 'chat-siswa-1', '2026-09-01T00:00:00', '2026-09-01T00:00:00');
            INSERT INTO pairing (subjek_tipe, subjek_id, token, status, chat_id, dibuat_pada, tertaut_pada)
            VALUES ('siswa', 2, 'TOKS2', 'tertaut', 'chat-siswa-2', '2026-09-01T00:00:00', '2026-09-01T00:00:00');
            INSERT INTO pairing (subjek_tipe, subjek_id, token, status, chat_id, dibuat_pada, tertaut_pada)
            VALUES ('siswa', 3, 'TOKS3', 'tertaut', 'chat-siswa-3', '2026-09-01T00:00:00', '2026-09-01T00:00:00');
            INSERT INTO pairing (subjek_tipe, subjek_id, token, status, chat_id, dibuat_pada, tertaut_pada)
            VALUES ('siswa', 4, 'TOKS4', 'tertaut', 'chat-siswa-4', '2026-09-01T00:00:00', '2026-09-01T00:00:00');
            INSERT INTO pairing (subjek_tipe, subjek_id, token, status, chat_id, dibuat_pada, tertaut_pada)
            VALUES ('siswa', 5, 'TOKS5', 'tertaut', 'chat-siswa-5', '2026-09-01T00:00:00', '2026-09-01T00:00:00');
            """);
    }

    // Sekolah kecil untuk pengujian Koreksi/Kunci Bulan/Status Guru (Tugas
    // 11, PRD §10, §16.10, §16.17): kelas 7A wali guru id=1 (tertaut,
    // chat-wali-1), kelas 7B wali guru id=2 (TIDAK tertaut — untuk uji
    // pesan ralat/notifikasi yang gagal karena belum ada chat_id). Siswa 1
    // (kelas 7A, Terlambat, tertaut chat-siswa-1), siswa 2 (kelas 7B, Alpa,
    // tidak tertaut). Akun: admin1(1)/kepsek(2) kewenangan penuh,
    // wali_a(3, guru_id=1)/wali_b(4, guru_id=2) masing-masing wali kelasnya.
    public void SeedSekolahUntukKoreksi()
    {
        Execute("""
            INSERT INTO tahun_ajaran (id, nama, semester, tgl_mulai, tgl_selesai, aktif)
            VALUES (1, '2026/2027', 'ganjil', '2026-07-01', '2026-12-19', 1);

            INSERT INTO guru (id, nip, nama, aktif) VALUES (1, '111', 'Wali A', 1);
            INSERT INTO guru (id, nip, nama, aktif) VALUES (2, '222', 'Wali B', 1);
            INSERT INTO guru (id, nip, nama, aktif) VALUES (3, '333', 'Guru Biasa', 1);

            INSERT INTO kelas (id, tahun_ajaran_id, nama, tingkat, wali_guru_id) VALUES (1, 1, '7A', 7, 1);
            INSERT INTO kelas (id, tahun_ajaran_id, nama, tingkat, wali_guru_id) VALUES (2, 1, '7B', 7, 2);

            INSERT INTO siswa (id, nisn, nama, jenis_kelamin, aktif) VALUES (1, '0001', 'Siswa Satu', 'L', 1);
            INSERT INTO siswa (id, nisn, nama, jenis_kelamin, aktif) VALUES (2, '0002', 'Siswa Dua', 'L', 1);

            INSERT INTO absensi_siswa (tanggal, siswa_id, kelas_id, status, jam_masuk, sumber)
            VALUES ('2026-09-17', 1, 1, 'terlambat', '07:45', 'tap');
            INSERT INTO absensi_siswa (tanggal, siswa_id, kelas_id, status, sumber)
            VALUES ('2026-09-17', 2, 2, 'alpa', 'sistem');

            INSERT INTO pairing (subjek_tipe, subjek_id, token, status, chat_id, dibuat_pada, tertaut_pada)
            VALUES ('siswa', 1, 'TOKS1', 'tertaut', 'chat-siswa-1', '2026-09-01T00:00:00', '2026-09-01T00:00:00');
            INSERT INTO pairing (subjek_tipe, subjek_id, token, status, chat_id, dibuat_pada, tertaut_pada)
            VALUES ('guru', 1, 'TOKW1', 'tertaut', 'chat-wali-1', '2026-09-01T00:00:00', '2026-09-01T00:00:00');
            """);

        TambahAkunDenganSandi(1, "admin1", SandiKepsek, "admin", guruId: null);
        TambahAkunDenganSandi(2, "kepsek", SandiKepsek, "kepala_sekolah", guruId: null);
        TambahAkunDenganSandi(3, "wali_a", SandiKepsek, "wali_kelas", guruId: 1);
        TambahAkunDenganSandi(4, "wali_b", SandiKepsek, "wali_kelas", guruId: 2);
    }

    // Sekolah kecil untuk pengujian LayananLaporan (Tugas 12, PRD §11):
    // tahun ajaran 1-30 September 2026 (dipersempit jadi semenjana supaya
    // batas semester gampang diuji), jadwal Senin-Sabtu hari sekolah
    // (CLAUDE.md §7), satu kalender override 17 September (Kamis) jadi
    // libur mendadak. Baris absensi_siswa/absensi_guru SENGAJA tidak
    // diisi untuk 17 September — meniru PembukaanHari yang melewati hari
    // libur — supaya hari efektif (dihitung dari kalender) dan jumlah
    // baris absensi (dihitung dari data) tetap konsisten satu sama lain.
    // 14-19 Sept = Senin..Sabtu, 6 hari sekolah - 1 libur mendadak = 5
    // hari efektif; siswa1 5 baris (Hadir,Terlambat,Sakit,Bolos,Hadir),
    // siswa2 5 baris (Alpa,Hadir,Hadir,Hadir,Izin), guru1 5 baris
    // (Hadir,DinasLuar,Hadir,TidakLengkap,Cuti).
    public void SeedSekolahUntukLaporan()
    {
        Execute("""
            INSERT INTO tahun_ajaran (id, nama, semester, tgl_mulai, tgl_selesai, aktif)
            VALUES (1, '2026/2027', 'ganjil', '2026-09-01', '2026-09-30', 1);

            INSERT INTO guru (id, nip, nama, aktif) VALUES (1, '111', 'Wali A', 1);

            INSERT INTO kelas (id, tahun_ajaran_id, nama, tingkat, wali_guru_id) VALUES (1, 1, '7A', 7, 1);

            INSERT INTO siswa (id, nisn, nama, jenis_kelamin, aktif) VALUES (1, '0001', 'Siswa Satu', 'L', 1);
            INSERT INTO siswa (id, nisn, nama, jenis_kelamin, aktif) VALUES (2, '0002', 'Siswa Dua', 'P', 1);

            INSERT INTO siswa_kelas (siswa_id, kelas_id, tahun_ajaran_id) VALUES (1, 1, 1);
            INSERT INTO siswa_kelas (siswa_id, kelas_id, tahun_ajaran_id) VALUES (2, 1, 1);

            INSERT INTO kalender (tanggal, jenis, keterangan, dibuat_pada)
            VALUES ('2026-09-17', 'libur', 'Libur mendadak banjir', '2026-09-16T18:00:00');

            INSERT INTO absensi_siswa (tanggal, siswa_id, kelas_id, status, jam_masuk, jam_pulang, keterangan, sumber) VALUES
                ('2026-09-14', 1, 1, 'hadir', '07:20', '13:10', NULL, 'tap'),
                ('2026-09-15', 1, 1, 'terlambat', '07:52', '13:10', NULL, 'tap'),
                ('2026-09-16', 1, 1, 'sakit', NULL, NULL, 'Demam', 'koreksi'),
                ('2026-09-18', 1, 1, 'bolos', '07:20', NULL, NULL, 'sistem'),
                ('2026-09-19', 1, 1, 'hadir', '07:15', '11:20', NULL, 'tap'),
                ('2026-09-14', 2, 1, 'alpa', NULL, NULL, NULL, 'sistem'),
                ('2026-09-15', 2, 1, 'hadir', '07:10', '13:10', NULL, 'tap'),
                ('2026-09-16', 2, 1, 'hadir', '07:10', '13:10', NULL, 'tap'),
                ('2026-09-18', 2, 1, 'hadir', '07:10', '13:10', NULL, 'tap'),
                ('2026-09-19', 2, 1, 'izin', NULL, NULL, 'Acara keluarga', 'koreksi');

            INSERT INTO absensi_guru (tanggal, guru_id, status, jam_masuk, jam_pulang, keterangan, sumber) VALUES
                ('2026-09-14', 1, 'hadir', '07:00', '14:00', NULL, 'tap'),
                ('2026-09-15', 1, 'dinas_luar', NULL, NULL, 'Rapat dinas', 'koreksi'),
                ('2026-09-16', 1, 'hadir', '07:00', '14:00', NULL, 'tap'),
                ('2026-09-18', 1, 'tidak_lengkap', '07:00', NULL, NULL, 'sistem'),
                ('2026-09-19', 1, 'cuti', NULL, NULL, 'Cuti tahunan', 'koreksi');
            """);

        for (var hari = 1; hari <= 7; hari++)
        {
            var hariSekolah = hari == 7 ? 0 : 1;
            using var command = Connection.CreateCommand();
            command.CommandText = """
                INSERT INTO jadwal_hari (tahun_ajaran_id, hari, hari_sekolah, jam_masuk, jam_rekap, batas_tap_masuk, mulai_pulang)
                VALUES (1, $hari, $hariSekolah, '07:30', '08:00', '12:00', '13:10');
                """;
            command.Parameters.AddWithValue("$hari", hari);
            command.Parameters.AddWithValue("$hariSekolah", hariSekolah);
            command.ExecuteNonQuery();
        }
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
