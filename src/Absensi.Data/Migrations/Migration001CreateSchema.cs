namespace Absensi.Data.Migrations;

// SQL disalin persis dari docs/SKEMA-DATABASE.md (bagian 3-9). Jangan diubah
// di sini tanpa mengubah dokumen skema terlebih dahulu.
internal sealed class Migration001CreateSchema : IMigration
{
    public int Number => 1;

    public string Sql => """
        CREATE TABLE skema_versi (
          versi        INTEGER PRIMARY KEY,
          diterapkan   TEXT NOT NULL
        );

        CREATE TABLE pengaturan (
          kunci        TEXT PRIMARY KEY,
          nilai        TEXT NOT NULL
        );

        CREATE TABLE sekolah (
          id           INTEGER PRIMARY KEY CHECK (id = 1),
          nama         TEXT NOT NULL,
          npsn         TEXT,
          alamat       TEXT,
          logo_path    TEXT
        );

        CREATE TABLE tahun_ajaran (
          id           INTEGER PRIMARY KEY,
          nama         TEXT NOT NULL,
          semester     TEXT NOT NULL CHECK (semester IN ('ganjil','genap')),
          tgl_mulai    TEXT NOT NULL,
          tgl_selesai  TEXT NOT NULL,
          aktif        INTEGER NOT NULL DEFAULT 0,
          UNIQUE (nama, semester)
        );

        CREATE TABLE jadwal_hari (
          id               INTEGER PRIMARY KEY,
          tahun_ajaran_id  INTEGER NOT NULL REFERENCES tahun_ajaran(id),
          hari             INTEGER NOT NULL CHECK (hari BETWEEN 1 AND 7),
          hari_sekolah     INTEGER NOT NULL DEFAULT 1,
          jam_masuk        TEXT,
          jam_rekap        TEXT,
          batas_tap_masuk  TEXT,
          mulai_pulang     TEXT,
          UNIQUE (tahun_ajaran_id, hari)
        );

        CREATE TABLE kalender (
          tanggal      TEXT PRIMARY KEY,
          jenis        TEXT NOT NULL CHECK (jenis IN ('sekolah','libur')),
          keterangan   TEXT,
          dibuat_oleh  INTEGER REFERENCES akun(id),
          dibuat_pada  TEXT
        );

        CREATE TABLE kunci_bulan (
          tahun        INTEGER NOT NULL,
          bulan        INTEGER NOT NULL CHECK (bulan BETWEEN 1 AND 12),
          dikunci_oleh INTEGER NOT NULL REFERENCES akun(id),
          dikunci_pada TEXT NOT NULL,
          PRIMARY KEY (tahun, bulan)
        );

        CREATE TABLE guru (
          id           INTEGER PRIMARY KEY,
          nip          TEXT NOT NULL UNIQUE,
          nama         TEXT NOT NULL,
          pangkat      TEXT,
          jabatan      TEXT,
          foto_path    TEXT,
          aktif        INTEGER NOT NULL DEFAULT 1,
          alasan_nonaktif TEXT
        );

        CREATE TABLE siswa (
          id            INTEGER PRIMARY KEY,
          nisn          TEXT NOT NULL UNIQUE,
          nama          TEXT NOT NULL,
          jenis_kelamin TEXT NOT NULL CHECK (jenis_kelamin IN ('L','P')),
          tempat_lahir  TEXT,
          tanggal_lahir TEXT,
          alamat        TEXT,
          nama_ortu     TEXT,
          no_hp_ortu    TEXT,
          foto_path     TEXT,
          aktif         INTEGER NOT NULL DEFAULT 1,
          alasan_nonaktif TEXT
        );

        CREATE TABLE kelas (
          id              INTEGER PRIMARY KEY,
          tahun_ajaran_id INTEGER NOT NULL REFERENCES tahun_ajaran(id),
          nama            TEXT NOT NULL,
          tingkat         INTEGER NOT NULL CHECK (tingkat BETWEEN 1 AND 9),
          wali_guru_id    INTEGER REFERENCES guru(id),
          UNIQUE (tahun_ajaran_id, nama)
        );

        CREATE TABLE siswa_kelas (
          id              INTEGER PRIMARY KEY,
          siswa_id        INTEGER NOT NULL REFERENCES siswa(id),
          kelas_id        INTEGER NOT NULL REFERENCES kelas(id),
          tahun_ajaran_id INTEGER NOT NULL REFERENCES tahun_ajaran(id),
          UNIQUE (siswa_id, tahun_ajaran_id)
        );

        CREATE TABLE kartu (
          id            INTEGER PRIMARY KEY,
          uid           TEXT NOT NULL,
          pemilik_tipe  TEXT NOT NULL CHECK (pemilik_tipe IN ('siswa','guru')),
          pemilik_id    INTEGER NOT NULL,
          aktif         INTEGER NOT NULL DEFAULT 1,
          didaftar_pada TEXT NOT NULL,
          dinonaktifkan_pada TEXT,
          alasan        TEXT
        );

        CREATE UNIQUE INDEX ix_kartu_uid_aktif
          ON kartu(uid) WHERE aktif = 1;

        CREATE INDEX ix_kartu_pemilik
          ON kartu(pemilik_tipe, pemilik_id);

        CREATE TABLE tap_log (
          id           INTEGER PRIMARY KEY,
          waktu        TEXT NOT NULL,
          uid          TEXT NOT NULL,
          hasil        TEXT NOT NULL CHECK (hasil IN
                        ('hadir','terlambat','pulang','masuk_guru','pulang_guru',
                         'diabaikan','ditolak_tidak_terdaftar','ditolak_jeda',
                         'ditolak_lewat_batas','ditolak_libur','registrasi')),
          pemilik_tipe TEXT,
          pemilik_id   INTEGER,
          catatan      TEXT
        );

        CREATE INDEX ix_tap_uid_waktu ON tap_log(uid, waktu);

        CREATE TABLE absensi_siswa (
          id            INTEGER PRIMARY KEY,
          tanggal       TEXT NOT NULL,
          siswa_id      INTEGER NOT NULL REFERENCES siswa(id),
          kelas_id      INTEGER NOT NULL REFERENCES kelas(id),
          status        TEXT NOT NULL CHECK (status IN
                         ('alpa','hadir','terlambat','bolos','izin','sakit')),
          jam_masuk     TEXT,
          jam_pulang    TEXT,
          keterangan    TEXT,
          sumber        TEXT NOT NULL DEFAULT 'tap'
                         CHECK (sumber IN ('tap','koreksi','sistem')),
          UNIQUE (tanggal, siswa_id)
        );

        CREATE INDEX ix_abs_siswa_tgl_kelas ON absensi_siswa(tanggal, kelas_id);

        CREATE TABLE absensi_guru (
          id            INTEGER PRIMARY KEY,
          tanggal       TEXT NOT NULL,
          guru_id       INTEGER NOT NULL REFERENCES guru(id),
          status        TEXT NOT NULL CHECK (status IN
                         ('alpa','masuk','hadir','tidak_lengkap',
                          'izin','sakit','dinas_luar','cuti')),
          jam_masuk     TEXT,
          jam_pulang    TEXT,
          keterangan    TEXT,
          sumber        TEXT NOT NULL DEFAULT 'tap'
                         CHECK (sumber IN ('tap','koreksi','sistem')),
          UNIQUE (tanggal, guru_id)
        );

        CREATE TABLE pairing (
          id            INTEGER PRIMARY KEY,
          subjek_tipe   TEXT NOT NULL CHECK (subjek_tipe IN ('siswa','guru')),
          subjek_id     INTEGER NOT NULL,
          token         TEXT NOT NULL UNIQUE,
          chat_id       TEXT,
          status        TEXT NOT NULL DEFAULT 'menunggu'
                         CHECK (status IN ('menunggu','tertaut','dibatalkan')),
          dibuat_pada   TEXT NOT NULL,
          tertaut_pada  TEXT,
          kedaluwarsa   TEXT
        );

        CREATE UNIQUE INDEX ix_pairing_subjek_aktif
          ON pairing(subjek_tipe, subjek_id) WHERE status = 'tertaut';

        CREATE TABLE outbox (
          id            INTEGER PRIMARY KEY,
          tanggal       TEXT NOT NULL,
          subjek_tipe   TEXT NOT NULL CHECK (subjek_tipe IN ('siswa','guru','kelas')),
          subjek_id     INTEGER NOT NULL,
          jenis         TEXT NOT NULL CHECK (jenis IN
                         ('hadir','terlambat','alpa','ralat',
                          'rekap_wali_belum_tap','bolos_wali','guru_tanpa_kabar')),
          chat_id       TEXT NOT NULL,
          isi           TEXT NOT NULL,
          status        TEXT NOT NULL DEFAULT 'antre'
                         CHECK (status IN ('antre','terkirim','gagal','dibatalkan')),
          percobaan     INTEGER NOT NULL DEFAULT 0,
          dibuat_pada   TEXT NOT NULL,
          terkirim_pada TEXT,
          galat         TEXT
        );

        CREATE UNIQUE INDEX ix_outbox_sekali
          ON outbox(tanggal, subjek_tipe, subjek_id, jenis)
          WHERE jenis <> 'ralat';

        CREATE INDEX ix_outbox_antre ON outbox(status) WHERE status = 'antre';

        CREATE TABLE akun (
          id            INTEGER PRIMARY KEY,
          username      TEXT NOT NULL UNIQUE,
          sandi_hash    TEXT NOT NULL,
          peran         TEXT NOT NULL CHECK (peran IN
                         ('kepala_sekolah','admin','wali_kelas')),
          guru_id       INTEGER REFERENCES guru(id),
          aktif         INTEGER NOT NULL DEFAULT 1,
          dibuat_oleh   INTEGER REFERENCES akun(id),
          dibuat_pada   TEXT NOT NULL
        );

        CREATE TABLE audit (
          id              INTEGER PRIMARY KEY,
          waktu           TEXT NOT NULL,
          akun_id         INTEGER NOT NULL REFERENCES akun(id),
          aksi            TEXT NOT NULL,
          entitas         TEXT NOT NULL,
          entitas_id      INTEGER,
          nilai_lama      TEXT,
          nilai_baru      TEXT,
          atas_permintaan TEXT
        );

        CREATE INDEX ix_audit_waktu ON audit(waktu);
        CREATE INDEX ix_audit_akun  ON audit(akun_id);
        """;
}
