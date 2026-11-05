using Absensi.Domain.Kehadiran;
using Microsoft.Data.Sqlite;

namespace Absensi.Data.Repositori;

public sealed class RepositoriAbsensi
{
    // PRD §8: status awal setiap siswa setiap hari adalah
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

    // Dipakai Absensi.Data.Notifikasi.LayananNotifikasi (PRD §9.2/§9.3) —
    // satu kueri untuk seluruh titik pemicu (rekap, batas akhir tap, bolos)
    // supaya masing-masing tinggal menyaring di memori, bukan menambah
    // kueri sempit per titik pemicu.
    public IReadOnlyList<AbsensiSiswaUntukNotifikasi> DaftarSiswaUntukNotifikasi(SqliteConnection connection, DateOnly tanggal)
    {
        using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT a.siswa_id, s.nama, a.kelas_id, k.nama, a.status, a.jam_masuk, a.jam_pulang
            FROM absensi_siswa a
            JOIN siswa s ON s.id = a.siswa_id
            JOIN kelas k ON k.id = a.kelas_id
            WHERE a.tanggal = $tanggal;
            """;
        command.Parameters.AddWithValue("$tanggal", TanggalTeks(tanggal));

        using var reader = command.ExecuteReader();
        var daftar = new List<AbsensiSiswaUntukNotifikasi>();
        while (reader.Read())
        {
            daftar.Add(new AbsensiSiswaUntukNotifikasi(
                reader.GetInt32(0),
                reader.GetString(1),
                reader.GetInt32(2),
                reader.GetString(3),
                PemetaanEnum.StatusSiswaDariTeks(reader.GetString(4)),
                BacaJam(reader, 5),
                BacaJam(reader, 6)));
        }

        return daftar;
    }

    public IReadOnlyList<AbsensiGuruUntukNotifikasi> DaftarGuruUntukNotifikasi(SqliteConnection connection, DateOnly tanggal)
    {
        using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT a.guru_id, g.nama, a.status
            FROM absensi_guru a
            JOIN guru g ON g.id = a.guru_id
            WHERE a.tanggal = $tanggal;
            """;
        command.Parameters.AddWithValue("$tanggal", TanggalTeks(tanggal));

        using var reader = command.ExecuteReader();
        var daftar = new List<AbsensiGuruUntukNotifikasi>();
        while (reader.Read())
        {
            daftar.Add(new AbsensiGuruUntukNotifikasi(reader.GetInt32(0), reader.GetString(1), PemetaanEnum.StatusGuruDariTeks(reader.GetString(2))));
        }

        return daftar;
    }

    // PRD §16.7 (14.10/Jumat 12.30): siswa tercatat masuk tapi belum tap
    // pulang ditetapkan Bolos (Absensi.Domain.Kehadiran.PenentuStatusKehadiran
    // .TentukanStatusAkhirHariSiswa menentukan KEPUTUSANnya; ini yang MENULIS).
    public void TandaiBolos(SqliteConnection connection, SqliteTransaction transaction, DateOnly tanggal, int siswaId)
    {
        Eksekusi(connection, transaction, """
            UPDATE absensi_siswa SET status = 'bolos' WHERE tanggal = $tanggal AND siswa_id = $siswaId;
            """,
            ("$tanggal", TanggalTeks(tanggal)),
            ("$siswaId", siswaId));
    }

    // RENCANA-TUGAS.md Tugas 11 — layar Koreksi (PRD §16.10): satu kelas,
    // satu tanggal, sekaligus jam tap dan keterangan untuk ditampilkan.
    public IReadOnlyList<SiswaUntukKoreksi> DaftarUntukKoreksi(SqliteConnection connection, DateOnly tanggal, int kelasId)
    {
        using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT s.id, s.nisn, s.nama, a.status, a.jam_masuk, a.jam_pulang, a.keterangan
            FROM absensi_siswa a
            JOIN siswa s ON s.id = a.siswa_id
            WHERE a.tanggal = $tanggal AND a.kelas_id = $kelasId
            ORDER BY s.nama;
            """;
        command.Parameters.AddWithValue("$tanggal", TanggalTeks(tanggal));
        command.Parameters.AddWithValue("$kelasId", kelasId);

        using var reader = command.ExecuteReader();
        var daftar = new List<SiswaUntukKoreksi>();
        while (reader.Read())
        {
            daftar.Add(new SiswaUntukKoreksi(
                reader.GetInt32(0),
                reader.GetString(1),
                reader.GetString(2),
                PemetaanEnum.StatusSiswaDariTeks(reader.GetString(3)),
                BacaJam(reader, 4),
                BacaJam(reader, 5),
                reader.IsDBNull(6) ? null : reader.GetString(6)));
        }

        return daftar;
    }

    // Hanya dipanggil lewat Absensi.Data.Koreksi.LayananKoreksi, yang
    // membungkusnya dengan JalurPerubahanData (PRD §12).
    // jam_masuk/jam_pulang sengaja tidak disentuh — riwayat tap asli tetap
    // utuh, hanya status+keterangan yang berubah (PRD §16.10 langkah 5).
    public void KoreksiStatusSiswa(SqliteConnection connection, SqliteTransaction transaction, DateOnly tanggal, int siswaId, StatusAbsensiSiswa status, string? keterangan)
    {
        Eksekusi(connection, transaction, """
            UPDATE absensi_siswa SET status = $status, keterangan = $keterangan, sumber = 'koreksi'
            WHERE tanggal = $tanggal AND siswa_id = $siswaId;
            """,
            ("$status", PemetaanEnum.TeksDariStatusSiswa(status)),
            ("$keterangan", (object?)keterangan ?? DBNull.Value),
            ("$tanggal", TanggalTeks(tanggal)),
            ("$siswaId", siswaId));
    }

    // RENCANA-TUGAS.md Tugas 11 — layar Guru→Kehadiran (PRD §16.17). LEFT
    // JOIN karena tanggal mendatang belum tentu punya baris (pembukaan hari
    // belum berjalan untuk tanggal itu) — status null di situ wajar.
    public IReadOnlyList<GuruUntukKehadiran> DaftarGuruUntukKehadiran(SqliteConnection connection, DateOnly tanggal)
    {
        using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT g.id, g.nip, g.nama, a.status, a.keterangan
            FROM guru g
            LEFT JOIN absensi_guru a ON a.guru_id = g.id AND a.tanggal = $tanggal
            WHERE g.aktif = 1
            ORDER BY g.nama;
            """;
        command.Parameters.AddWithValue("$tanggal", TanggalTeks(tanggal));

        using var reader = command.ExecuteReader();
        var daftar = new List<GuruUntukKehadiran>();
        while (reader.Read())
        {
            daftar.Add(new GuruUntukKehadiran(
                reader.GetInt32(0),
                reader.GetString(1),
                reader.GetString(2),
                reader.IsDBNull(3) ? null : PemetaanEnum.StatusGuruDariTeks(reader.GetString(3)),
                reader.IsDBNull(4) ? null : reader.GetString(4)));
        }

        return daftar;
    }

    // Hanya dipanggil lewat Absensi.Data.Koreksi.LayananStatusGuru. Upsert
    // (bukan UPDATE biasa) karena menaungi DUA ARAH PRD §16.17: "DI MUKA"
    // untuk tanggal mendatang (belum ada baris — pembukaan hari belum
    // berjalan) memasukkan baris baru; "SETELAH" untuk tanggal yang
    // sudah punya baris memperbaruinya. jam_masuk/jam_pulang tidak
    // disentuh saat baris sudah ada, supaya tap asli (kalau ada) tetap utuh.
    public void TetapkanStatusGuru(SqliteConnection connection, SqliteTransaction transaction, DateOnly tanggal, int guruId, StatusAbsensiGuru status, string? keterangan)
    {
        using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = """
            INSERT INTO absensi_guru (tanggal, guru_id, status, keterangan, sumber)
            VALUES ($tanggal, $guruId, $status, $keterangan, 'koreksi')
            ON CONFLICT(tanggal, guru_id) DO UPDATE SET
                status = excluded.status,
                keterangan = excluded.keterangan,
                sumber = 'koreksi';
            """;
        command.Parameters.AddWithValue("$tanggal", TanggalTeks(tanggal));
        command.Parameters.AddWithValue("$guruId", guruId);
        command.Parameters.AddWithValue("$status", PemetaanEnum.TeksDariStatusGuru(status));
        command.Parameters.AddWithValue("$keterangan", (object?)keterangan ?? DBNull.Value);
        command.ExecuteNonQuery();
    }

    // RENCANA-TUGAS.md Tugas 11 — PRD §16.16 (24.00, akhir hari): guru
    // dengan tap masuk saja menjadi Tidak Lengkap. Dipanggil Absensi.App
    // .Penjadwal saat mendeteksi pergantian tanggal, untuk tanggal yang
    // BARU SAJA berakhir — bukan lewat LayananStatusGuru (bukan tindakan
    // Kepala Sekolah, tidak diaudit, sama seperti PembukaanHari).
    public IReadOnlyList<int> DaftarGuruMasukTanpaPulang(SqliteConnection connection, DateOnly tanggal)
    {
        using var command = connection.CreateCommand();
        command.CommandText = "SELECT guru_id FROM absensi_guru WHERE tanggal = $tanggal AND status = 'masuk';";
        command.Parameters.AddWithValue("$tanggal", TanggalTeks(tanggal));

        using var reader = command.ExecuteReader();
        var daftar = new List<int>();
        while (reader.Read())
        {
            daftar.Add(reader.GetInt32(0));
        }

        return daftar;
    }

    public void FinalisasiAkhirHariGuru(SqliteConnection connection, SqliteTransaction transaction, DateOnly tanggal, int guruId, StatusAbsensiGuru status)
    {
        Eksekusi(connection, transaction, """
            UPDATE absensi_guru SET status = $status WHERE tanggal = $tanggal AND guru_id = $guruId;
            """,
            ("$status", PemetaanEnum.TeksDariStatusGuru(status)),
            ("$tanggal", TanggalTeks(tanggal)),
            ("$guruId", guruId));
    }

    // RENCANA-TUGAS.md Tugas 12: sumber baris mentah untuk seluruh jenis
    // laporan siswa (Harian/Mingguan/Bulanan/Per Siswa/Semester). Diambil
    // langsung dari absensi_siswa, bukan dari roster siswa aktif — supaya
    // siswa yang sudah mutasi/lulus tetap muncul di laporan periode saat
    // dia masih aktif (PRD §5.3: riwayat tetap utuh). kelasId null berarti
    // seluruh kelas.
    public IReadOnlyList<BarisAbsensiSiswaMentah> DaftarSiswaUntukRentang(
        SqliteConnection connection, DateOnly tanggalAwal, DateOnly tanggalAkhir, int? kelasId)
    {
        using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT a.tanggal, a.siswa_id, s.nama, s.nisn, a.kelas_id, k.nama, a.status, a.jam_masuk, a.jam_pulang, a.keterangan, a.sumber
            FROM absensi_siswa a
            JOIN siswa s ON s.id = a.siswa_id
            JOIN kelas k ON k.id = a.kelas_id
            WHERE a.tanggal BETWEEN $awal AND $akhir
              AND ($kelasId IS NULL OR a.kelas_id = $kelasId)
            ORDER BY a.tanggal, k.nama, s.nama;
            """;
        command.Parameters.AddWithValue("$awal", TanggalTeks(tanggalAwal));
        command.Parameters.AddWithValue("$akhir", TanggalTeks(tanggalAkhir));
        command.Parameters.AddWithValue("$kelasId", (object?)kelasId ?? DBNull.Value);

        using var reader = command.ExecuteReader();
        var daftar = new List<BarisAbsensiSiswaMentah>();
        while (reader.Read())
        {
            daftar.Add(new BarisAbsensiSiswaMentah(
                DateOnly.Parse(reader.GetString(0)),
                reader.GetInt32(1),
                reader.GetString(2),
                reader.GetString(3),
                reader.GetInt32(4),
                reader.GetString(5),
                PemetaanEnum.StatusSiswaDariTeks(reader.GetString(6)),
                BacaJam(reader, 7),
                BacaJam(reader, 8),
                reader.IsDBNull(9) ? null : reader.GetString(9),
                reader.GetString(10)));
        }

        return daftar;
    }

    // Guru tidak dikelompokkan per kelas (§17.7/§17.8 tidak menyebut
    // pengelompokan) — selalu seluruh guru yang punya baris di rentang itu.
    public IReadOnlyList<BarisAbsensiGuruMentah> DaftarGuruUntukRentang(
        SqliteConnection connection, DateOnly tanggalAwal, DateOnly tanggalAkhir)
    {
        using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT a.tanggal, a.guru_id, g.nama, g.nip, a.status, a.jam_masuk, a.jam_pulang, a.keterangan, a.sumber
            FROM absensi_guru a
            JOIN guru g ON g.id = a.guru_id
            WHERE a.tanggal BETWEEN $awal AND $akhir
            ORDER BY a.tanggal, g.nama;
            """;
        command.Parameters.AddWithValue("$awal", TanggalTeks(tanggalAwal));
        command.Parameters.AddWithValue("$akhir", TanggalTeks(tanggalAkhir));

        using var reader = command.ExecuteReader();
        var daftar = new List<BarisAbsensiGuruMentah>();
        while (reader.Read())
        {
            daftar.Add(new BarisAbsensiGuruMentah(
                DateOnly.Parse(reader.GetString(0)),
                reader.GetInt32(1),
                reader.GetString(2),
                reader.GetString(3),
                PemetaanEnum.StatusGuruDariTeks(reader.GetString(4)),
                BacaJam(reader, 5),
                BacaJam(reader, 6),
                reader.IsDBNull(7) ? null : reader.GetString(7),
                reader.GetString(8)));
        }

        return daftar;
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

    private static string JamTeks(DateTime waktu) => StoredTime.Seconds(TimeOnly.FromDateTime(waktu));

    private static TimeOnly? BacaJam(SqliteDataReader reader, int kolom) =>
        reader.IsDBNull(kolom) ? null : StoredTime.Parse(reader.GetString(kolom));
}
