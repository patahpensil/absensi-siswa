# Skema Database — Aplikasi Absensi Siswa

**Dokumen pendamping PRD v0.9**
**Mesin:** SQLite
**Tanggal:** 12 September 2026

---

## 1. Prinsip yang Dipegang Skema Ini

Lima keputusan PRD yang membentuk struktur di bawah:

1. **Kelas bukan milik siswa.** Kelas adalah keanggotaan per tahun ajaran, sehingga laporan tahun lalu tetap menampilkan kelas yang benar saat siswa naik kelas.
2. **Absensi menunjuk ke orang, bukan ke kartu.** Kartu boleh diganti tanpa memutus riwayat.
3. **Master guru adalah satu-satunya daftar orang.** Wali kelas dan akun login menunjuk ke baris guru yang sama.
4. **Nomor kartu unik lintas master.** Satu tabel kartu menampung kartu siswa dan guru sekaligus.
5. **Setiap baris absensi dibuat di awal hari** dengan status Alpa, sehingga rekap cukup membaca tabel.

---

## 2. Daftar Tabel

| Kelompok | Tabel |
|---|---|
| Sistem | `skema_versi`, `pengaturan`, `sekolah` |
| Waktu | `tahun_ajaran`, `jadwal_hari`, `kalender`, `kunci_bulan` |
| Orang | `guru`, `siswa`, `kelas`, `siswa_kelas` |
| Kartu | `kartu`, `tap_log` |
| Kehadiran | `absensi_siswa`, `absensi_guru` |
| Telegram | `pairing`, `outbox` |
| Keamanan | `akun`, `audit` |

---

## 3. Sistem

```sql
CREATE TABLE skema_versi (
  versi        INTEGER PRIMARY KEY,
  diterapkan   TEXT NOT NULL          -- ISO-8601
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
```

`pengaturan` memuat nilai teknis: `token_bot`, `username_bot`, `versi_aplikasi`, `notif_bolos_aktif`, `toleransi_jadwal_menit` (bawaan 15), `jeda_tap_detik` (bawaan 15).

`sekolah` sengaja dibatasi satu baris — satu instalasi untuk satu sekolah.

---

## 4. Waktu

```sql
CREATE TABLE tahun_ajaran (
  id           INTEGER PRIMARY KEY,
  nama         TEXT NOT NULL,              -- '2026/2027'
  semester     TEXT NOT NULL CHECK (semester IN ('ganjil','genap')),
  tgl_mulai    TEXT NOT NULL,
  tgl_selesai  TEXT NOT NULL,
  aktif        INTEGER NOT NULL DEFAULT 0,
  UNIQUE (nama, semester)
);

CREATE TABLE jadwal_hari (
  id               INTEGER PRIMARY KEY,
  tahun_ajaran_id  INTEGER NOT NULL REFERENCES tahun_ajaran(id),
  hari             INTEGER NOT NULL CHECK (hari BETWEEN 1 AND 7), -- 1=Senin
  hari_sekolah     INTEGER NOT NULL DEFAULT 1,
  jam_masuk        TEXT,        -- '07:30'
  jam_rekap        TEXT,        -- '08:00'
  batas_tap_masuk  TEXT,        -- '12:00'  Jumat '10:00'
  mulai_pulang     TEXT,        -- '13:10'  Jumat '11:30'
  UNIQUE (tahun_ajaran_id, hari)
);

CREATE TABLE kalender (
  tanggal      TEXT PRIMARY KEY,           -- 'YYYY-MM-DD'
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
```

**Catatan.** `kalender` hanya memuat tanggal yang menyimpang dari pola mingguan — libur nasional, libur semester, libur mendadak. Hari biasa tidak perlu dicatat; aplikasi menurunkannya dari `jadwal_hari`. Hari kegiatan seperti MPLS dan Sumatif berjenis `sekolah`, sesuai keputusan bahwa absensi tetap berjalan.

Hari efektif untuk persentase = jumlah tanggal dalam rentang yang `hari_sekolah = 1` **dikurangi** tanggal berjenis `libur`.

---

## 5. Orang

```sql
CREATE TABLE guru (
  id           INTEGER PRIMARY KEY,
  nip          TEXT NOT NULL UNIQUE,
  nama         TEXT NOT NULL,
  pangkat      TEXT,
  jabatan      TEXT,
  foto_path    TEXT,
  aktif        INTEGER NOT NULL DEFAULT 1,
  alasan_nonaktif TEXT                     -- 'pensiun', 'pindah'
);

CREATE TABLE siswa (
  id            INTEGER PRIMARY KEY,
  nisn          TEXT NOT NULL UNIQUE,      -- disimpan sebagai TEXT
  nama          TEXT NOT NULL,
  jenis_kelamin TEXT NOT NULL CHECK (jenis_kelamin IN ('L','P')),
  tempat_lahir  TEXT,
  tanggal_lahir TEXT,                      -- 'YYYY-MM-DD'
  alamat        TEXT,
  nama_ortu     TEXT,
  no_hp_ortu    TEXT,
  foto_path     TEXT,
  aktif         INTEGER NOT NULL DEFAULT 1,
  alasan_nonaktif TEXT                     -- 'lulus', 'pindah', 'keluar'
);

CREATE TABLE kelas (
  id              INTEGER PRIMARY KEY,
  tahun_ajaran_id INTEGER NOT NULL REFERENCES tahun_ajaran(id),
  nama            TEXT NOT NULL,           -- bebas: '7A', 'VII-1'
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
```

**Catatan.** `wali_guru_id` menunjuk ke baris guru — bukan nama yang diketik. Inilah yang membuat "wali kelas adalah guru, guru belum tentu wali kelas" terjaga di tingkat data.

`siswa_kelas` dibatasi satu kelas per siswa per tahun ajaran. Naik kelas berarti menambah baris baru untuk tahun ajaran berikutnya, bukan menimpa baris lama.

---

## 6. Kartu

```sql
CREATE TABLE kartu (
  id            INTEGER PRIMARY KEY,
  uid           TEXT NOT NULL,             -- nomor unik kartu RFID
  pemilik_tipe  TEXT NOT NULL CHECK (pemilik_tipe IN ('siswa','guru')),
  pemilik_id    INTEGER NOT NULL,
  aktif         INTEGER NOT NULL DEFAULT 1,
  didaftar_pada TEXT NOT NULL,
  dinonaktifkan_pada TEXT,
  alasan        TEXT                       -- 'hilang', 'rusak', 'lulus'
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
```

**Catatan penting.** Indeks parsial `ix_kartu_uid_aktif` adalah yang menegakkan dua aturan sekaligus: satu kartu aktif per UID, dan keunikan lintas master — karena siswa dan guru berbagi satu tabel. Kartu lama tidak dihapus, hanya `aktif = 0`, sehingga riwayat tetap utuh dan kartu hilang tidak bisa dipakai.

`tap_log` mencatat **semua** tap termasuk yang ditolak. Inilah sumber data untuk aturan jeda 15 detik, dan untuk menelusuri keluhan "kartu saya tidak terbaca".

---

## 7. Kehadiran

```sql
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
```

**Catatan.** `kelas_id` disalin ke baris absensi saat baris dibuat. Ini disengaja: laporan tidak perlu menelusuri keanggotaan kelas untuk tiap baris, dan siswa yang pindah kelas di tengah tahun tidak mengubah riwayat lamanya.

`sumber` membedakan data dari tap dan data hasil koreksi — inilah yang memberi penanda "koreksi" di panel riwayat siswa.

---

## 8. Telegram

```sql
CREATE TABLE pairing (
  id            INTEGER PRIMARY KEY,
  subjek_tipe   TEXT NOT NULL CHECK (subjek_tipe IN ('siswa','guru')),
  subjek_id     INTEGER NOT NULL,
  token         TEXT NOT NULL UNIQUE,      -- 6 karakter acak
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
```

**Catatan.** Indeks `ix_outbox_sekali` adalah penegak aturan anti-kirim-ganda: satu jenis pesan hanya boleh ada satu kali per subjek per tanggal. Pesan `ralat` dikecualikan karena bisa terjadi lebih dari sekali bila status dikoreksi berulang.

Pesan tidak pernah dikirim langsung — selalu masuk `outbox` lalu diproses pengirim terpisah. Inilah yang membuat aturan "internet putus → antre, kirim saat normal" bekerja tanpa kode khusus.

Satu pairing tertaut per subjek ditegakkan oleh `ix_pairing_subjek_aktif`, sesuai keputusan satu orang tua per siswa.

---

## 9. Keamanan

```sql
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
  aksi            TEXT NOT NULL,           -- 'koreksi_status', 'ganti_kartu', ...
  entitas         TEXT NOT NULL,           -- 'absensi_siswa', 'kartu', ...
  entitas_id      INTEGER,
  nilai_lama      TEXT,
  nilai_baru      TEXT,
  atas_permintaan TEXT                     -- nama wali kelas pada koreksi delegasi
);

CREATE INDEX ix_audit_waktu ON audit(waktu);
CREATE INDEX ix_audit_akun  ON audit(akun_id);

-- Migrasi 002 (Master Bug & UI/UX Register A-38): PRD §12 "tidak dapat
-- dihapus atau diedit siapa pun" ditegakkan langsung di database, bukan
-- cuma diandalkan dari perilaku RepositoriAudit yang cuma pernah INSERT.
-- Berlaku ke SELURUH baris (lama maupun baru) -- tidak butuh migrasi data.
CREATE TRIGGER trg_audit_cegah_update
BEFORE UPDATE ON audit
BEGIN
    SELECT RAISE(ABORT, 'Baris audit tidak dapat diubah (PRD §12).');
END;

CREATE TRIGGER trg_audit_cegah_delete
BEFORE DELETE ON audit
BEGIN
    SELECT RAISE(ABORT, 'Baris audit tidak dapat dihapus (PRD §12).');
END;

-- Migrasi 003 (Master Bug & UI/UX Register A-39, Tahap 1): identitas
-- permanen per baris audit, supaya baris yang SAMA tetap dikenali sama
-- walau `id` (INTEGER PRIMARY KEY tanpa AUTOINCREMENT) dipakai ulang oleh
-- peristiwa LAIN setelah database di-restore ke backup lama -- dibuktikan
-- lewat prototipe + pengujian terhadap salinan database UAT nyata sebelum
-- migrasi ini ditulis (bukan diasumsikan). Trigger di atas dilepas lalu
-- dipasang ulang PERSIS sama di dalam migrasi ini (satu transaksi) supaya
-- UPDATE backfill di bawah bisa berjalan -- lihat komentar lengkap di
-- Migration003TambahAuditUid.cs untuk kenapa ini perlu (ditemukan lewat
-- tes gagal, bukan dirancang di atas kertas).
ALTER TABLE audit ADD COLUMN audit_uid TEXT NOT NULL DEFAULT '';

UPDATE audit SET audit_uid =
    waktu || '|' || akun_id || '|' || aksi || '|' || entitas || '|' ||
    COALESCE(entitas_id, '') || '|' || COALESCE(nilai_lama, '') || '|' ||
    COALESCE(nilai_baru, '') || '|' || COALESCE(atas_permintaan, '')
WHERE audit_uid = '';

CREATE UNIQUE INDEX ix_audit_uid ON audit(audit_uid);
```

**Catatan.** `guru_id` pada akun wali kelas menunjuk ke baris guru yang sama dengan `kelas.wali_guru_id` — dari sinilah aplikasi tahu kelas mana yang boleh disentuh akun tersebut.

Tabel `audit` tidak pernah diubah atau dihapus oleh aplikasi. Tidak ada layar yang menyediakan tombol hapus untuk tabel ini — dan sejak migrasi 002, UPDATE/DELETE terhadap tabel ini ditolak langsung oleh database (trigger di atas), bukan cuma oleh konvensi kode. **Batasnya:** ini pencegah lewat SQL biasa, bukan kebal-rusak — akses langsung ke berkas `.db` di luar aplikasi (mis. DB Browser, atau `DROP TRIGGER`) tetap bisa melewatinya.

**`audit_uid` (migrasi 003).** Baris LAMA (sudah ada sebelum migrasi) diisi **fingerprint deterministik** dari isi baris itu sendiri (rumus di atas) — SENGAJA bukan GUID acak, supaya baris yang sama tetap dikenali sama walau di-backfill ulang secara independen di lini masa/berkas berbeda (mis. backup lama yang suatu saat di-restore lalu database hasilnya dimigrasi ulang). Baris BARU (dibuat setelah migrasi ini ada) diisi GUID acak oleh `RepositoriAudit.Catat` saat `INSERT`. **Jangan ubah rumus fingerprint di atas setelah dirilis** — mengubahnya membuat baris lama yang sama dihitung ulang jadi `audit_uid` berbeda di lini masa berbeda, merusak jaminan identitas yang jadi tujuan kolom ini.

### Arsip riwayat audit pra-restore (A-39, Tahap 1)

Sebelum restore database menimpa apa pun, `LayananArsipAudit` membuat salinan **tabel `audit`+`akun` saja** (bukan seluruh database) ke `backup/arsip-audit/arsip-audit-<yyyyMMdd-HHmmss>.db`, memverifikasinya (`PRAGMA integrity_check` + jumlah baris cocok dengan sumber lewat koneksi BARU, bukan koneksi yang baru menulis), baru mengizinkan restore lanjut. Kalau pembuatan atau verifikasi gagal, restore **dibatalkan** dan database sumber tidak disentuh; berkas arsip yang gagal verifikasi ditandai `.gagal-verifikasi` (tidak dihapus — punya nilai forensik).

- **Lokasi:** `backup/arsip-audit/`, folder terpisah dari 7 berkas backup biasa di `backup/` langsung — rotasi `LayananBackup.HapusBerkasSelainNTerbaru` hanya memindai folder induk, jadi subfolder ini otomatis tidak pernah ikut terhitung/terhapus.
- **Retensi (tahap ini):** **tidak dihapus otomatis sama sekali.** Setiap restore menambah satu berkas baru; jumlahnya bertambah seiring waktu tanpa batas built-in. Ini keputusan sengaja (pemilik proyek, 2026-09-24) untuk tahap ini — bukan jaminan "aman selamanya tanpa perlu diawasi".
- **Ruang penyimpanan:** satu arsip berisi hanya `audit`+`akun` (jauh lebih kecil dari backup database penuh ~180 KB pada database UAT saat ini) — perkiraan kasar, tumbuh sebanding jumlah baris audit di titik restore itu, bukan sebanding ukuran seluruh database. Cara memeriksa: lihat ukuran folder `backup/arsip-audit/` lewat Properti folder di File Explorer (tidak ada layar aplikasi yang menampilkan ini di tahap ini — dicatat sebagai kebutuhan potensial untuk layar Audit/Pengaturan nanti).
- **Belum dikerjakan (dicatat, bukan diklaim selesai):** salinan arsip ke media/server di luar PC ini + uji pemulihan sungguhan adalah pekerjaan terpisah (lihat catatan `FolderBackupCloudStub` di `LokasiData.cs` — "cloud" saat ini adalah folder cadangan SEKUNDER LOKAL, bukan di luar PC).

---

## 10. Hal yang Ditegakkan Database, Bukan Kode

Delapan aturan PRD yang dijamin oleh struktur di atas, sehingga tidak bisa dilanggar walau ada bug di lapisan aplikasi:

| Aturan | Penegak |
|---|---|
| Satu kartu aktif per UID, unik lintas master | `ix_kartu_uid_aktif` |
| Satu baris absensi per siswa per hari | `UNIQUE (tanggal, siswa_id)` |
| Satu baris absensi per guru per hari | `UNIQUE (tanggal, guru_id)` |
| Satu kelas per siswa per tahun ajaran | `UNIQUE (siswa_id, tahun_ajaran_id)` |
| Satu pairing tertaut per subjek | `ix_pairing_subjek_aktif` |
| Satu pesan per jenis per subjek per hari | `ix_outbox_sekali` |
| Status hanya dari daftar yang sah | `CHECK (status IN ...)` |
| Tingkat kelas hanya 1–9 | `CHECK (tingkat BETWEEN 1 AND 9)` |
| Baris `audit` tidak dapat diubah/dihapus lewat SQL biasa | `trg_audit_cegah_update`, `trg_audit_cegah_delete` (migrasi 002) |
| Setiap baris `audit` punya identitas unik, kebal terhadap `id` yang dipakai ulang setelah restore | `ix_audit_uid` (migrasi 003) |

---

## 11. Catatan Pelaksanaan

**Tanggal dan waktu** disimpan sebagai TEXT ISO-8601 (`YYYY-MM-DD` dan `HH:MM`). SQLite tidak punya tipe tanggal, dan format ini terurut benar secara leksikografis.

**`PRAGMA foreign_keys = ON`** harus dijalankan setiap kali koneksi dibuka — SQLite mematikannya secara bawaan.

**`PRAGMA journal_mode = WAL`** agar proses penulisan tap tidak terhalang proses pembacaan laporan.

**Pembukaan hari** dijalankan saat aplikasi dibuka dan saat melewati pukul 00.01: buat baris `absensi_siswa` berstatus `alpa` untuk seluruh siswa aktif, kecuali tanggal itu berjenis `libur`.

**Impor** dijalankan dalam satu transaksi — gagal di baris mana pun berarti tidak ada yang tersimpan.

**Migrasi** memakai tabel `skema_versi`: setiap perubahan struktur diberi nomor urut, dan aplikasi menjalankan migrasi yang nomornya lebih besar dari versi tersimpan.

---

*Dokumen ini belum final dan mengikuti perubahan pada PRD.*
