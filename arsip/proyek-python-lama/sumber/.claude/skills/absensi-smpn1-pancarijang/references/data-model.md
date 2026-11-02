# Model Data

Basis data lokal SQLite. Baca berkas ini saat merancang skema, menulis model
SQLAlchemy, membuat migrasi Alembic, atau menyusun kueri laporan.

## Daftar isi

- Prinsip pemisahan peristiwa dan hasil
- Tabel inti
- Tabel absensi
- Tabel notifikasi
- Tabel sistem
- Catatan indeks dan integritas

---

## Prinsip pemisahan peristiwa dan hasil

Ada dua lapis yang tidak boleh dicampur.

`attendance_events` menyimpan fakta mentah: kartu ini ditempel pada detik ini di
pembaca ini. Baris di sini **tidak pernah diubah dan tidak pernah dihapus**, bahkan
ketika hasil pengolahannya berubah atau statusnya dikoreksi.

`attendance` menyimpan hasil pengolahan: satu baris per siswa per tanggal, berisi jam
masuk, status masuk, jam pulang. Baris di sini boleh dikoreksi, selalu disertai jejak
audit.

Kalau suatu saat aturan status berubah, seluruh isi `attendance` bisa dihitung ulang
dari `attendance_events` tanpa kehilangan apa pun.

---

## Tabel inti

**classes** — kelas atau rombongan belajar. Dibuat manual oleh admin, bisa diubah dan
dihapus. Simpan tingkat (7, 8, 9) terpisah dari nama kelas agar laporan per tingkat
mudah. Hapus hanya boleh bila tidak ada siswa aktif di dalamnya; kalau ada, tandai
tidak aktif.

**students** — id, NIS, NISN, nama lengkap, id kelas, jenis kelamin, status siswa
(aktif, pindah, lulus, keluar), status aktif. Jangan menghapus siswa secara fisik;
riwayat absensinya harus tetap terbaca.

**teachers** — id, NIP, nama lengkap, status kepegawaian, status aktif.

Aturan status kehadiran guru sudah ditetapkan: `HADIR`, `TERLAMBAT`, `DINAS_LUAR`,
`IZIN`, `SAKIT`, `CUTI`, `TIDAK_HADIR` — lihat SKILL.md bagian "Penentuan status
masuk guru". `teacher_schedules` tetap disiapkan strukturnya tapi belum diisi: yang
sudah diputuskan hanya satu angka (batas terlambat 09:31:00, disimpan sebagai
pengaturan `BATAS_TERLAMBAT_GURU`, bukan baris jadwal), bukan jam_masuk/jam_pulang
guru per hari.

**guardians** — orang tua atau wali. Nama, hubungan dengan siswa, nomor WhatsApp,
apakah menerima notifikasi. Relasi ke siswa bersifat banyak ke satu: satu siswa boleh
punya lebih dari satu penerima. Nomor disimpan dalam format `+628xxxxxxxxxx`,
dinormalkan saat disimpan, bukan saat ditampilkan.

**rfid_cards** — UID kartu, id pemilik, jenis pemilik (siswa atau guru), status
(aktif, hilang, diganti, nonaktif), tanggal terbit. UID unik di seluruh tabel. Satu
pemilik hanya boleh punya satu kartu aktif pada satu waktu. Kartu yang diganti
dinonaktifkan, tidak dihapus, supaya absensi lama tetap tertelusur.

---

## Tabel absensi

**school_schedules** — jadwal per hari dengan tanggal mulai berlaku:

```
hari (1-7)
jam_masuk
batas_terlambat
jam_pulang
berlaku_mulai
berlaku_sampai   (kosong berarti masih berlaku)
```

Perhitungan status selalu mencari baris yang berlaku pada tanggal absensi. Jangan
pernah memakai baris terbaru begitu saja.

**attendance_events** — peristiwa mentah:

```
id
uid_kartu
id_pemilik        (boleh kosong bila kartu tidak dikenal)
jenis_pemilik
waktu_scan
id_perangkat
jenis_event       (MASUK, PULANG, DUPLICATE, UNKNOWN_CARD, DI_LUAR_PERIODE)
metadata
```

**attendance** — hasil olahan, satu baris per orang per tanggal:

```
id
id_pemilik
jenis_pemilik
tanggal
jam_masuk
status_masuk      (siswa: HADIR, TERLAMBAT, ALPA, IZIN, SAKIT — guru: HADIR,
                   TERLAMBAT, DINAS_LUAR, IZIN, SAKIT, CUTI, TIDAK_HADIR)
jam_pulang
status_pulang     (BOLOS, atau kosong — siswa saja, lihat "Status BOLOS" di SKILL.md)
id_jadwal         (jadwal yang dipakai menghitung)
dikoreksi         (boolean)
```

Kunci unik gabungan pada `(id_pemilik, jenis_pemilik, tanggal)`.

Simpan `id_jadwal` agar terlihat jelas jadwal mana yang dipakai. Ini menyelamatkan
banyak waktu saat menelusuri sengketa status.

`status_pulang` terpisah dari `status_masuk` dengan sengaja: siswa yang HADIR atau
TERLAMBAT paginya tetap begitu paginya walau `BOLOS` sore harinya. Nilainya diisi saat
Tutup Hari, bukan saat scan pulang terjadi.

**day_closures** — penanda Tutup Hari, satu baris per tanggal:

```
id
tanggal            unik
ditutup_oleh       (id pengguna; kosong bila ditutup otomatis oleh penjadwal)
waktu_tutup
sumber             (MANUAL, OTOMATIS)
```

Kunci unik pada `tanggal` menegakkan "satu hari hanya bisa ditutup sekali" di lapisan
data, bukan cuma di antarmuka. Tidak ada kolom maupun operasi untuk membatalkan
penutupan — begitu ada baris untuk suatu tanggal, tanggal itu tertutup permanen.

---

## Tabel notifikasi

**notification_queue**:

```
id
id_absensi
id_penerima
nomor_tujuan
isi_pesan
jenis            (MASUK, PULANG, ALPA)
status           (PENDING, SENDING, SENT, FAILED, CANCELLED)
kunci_idempotensi   unik
waktu_kejadian
waktu_dijadwalkan
waktu_kirim
jumlah_percobaan
pesan_galat
```

`waktu_kejadian` dan `waktu_kirim` wajib terpisah. Saat internet mati semalaman,
laporan harus tetap bisa menunjukkan bahwa siswa scan pukul 07:01 meski pesannya baru
terkirim pukul 08:15.

Status `CANCELLED` dipakai untuk notifikasi ALPA yang batal karena siswa ternyata
datang atau statusnya dikoreksi sebelum 09:15.

**message_templates** — template per jenis notifikasi, dengan variabel bernama seperti
`{nama_siswa}`, `{jam}`, `{status}`. Bisa diubah admin. Simpan versinya agar terlihat
template mana yang dipakai pada pesan lama.

---

## Tabel sistem

**users** — id, nama pengguna, hash kata sandi, peran (admin, wali_kelas, operator),
id kelas yang diampu (hanya untuk wali kelas), status aktif, waktu login terakhir.

**devices** — pembaca RFID yang terdaftar: nama, mode (serial atau HID), port, lokasi
pemasangan, status aktif.

**settings** — pasangan kunci dan nilai untuk seluruh konfigurasi: toleransi, jendela
scan berulang, waktu cutoff ALPA, batas terlambat guru, waktu tutup hari otomatis,
waktu kirim notifikasi ALPA, penyedia WhatsApp yang dipakai, dan feature flag untuk
menyalakan atau mematikan modul. Jam masuk/batas terlambat/jam pulang siswa per hari
disimpan di `school_schedules`, bukan di sini, karena nilainya berbeda per hari dan
butuh riwayat tanggal berlaku.

**audit_logs** — siapa, apa, kapan, nilai sebelum, nilai sesudah, alasan. Wajib terisi
untuk perubahan data siswa, nomor WhatsApp, jadwal, penggantian kartu, koreksi
absensi, perubahan status siswa, dan perubahan konfigurasi.

---

## Catatan indeks dan integritas

Indeks yang benar-benar dibutuhkan sejak awal:

- `rfid_cards.uid_kartu` — unik. Ini kueri terpanas; dijalankan setiap kali kartu
  ditempel, ratusan kali dalam sepuluh menit.
- `attendance (id_pemilik, jenis_pemilik, tanggal)` — unik.
- `attendance_events (waktu_scan)` untuk laporan.
- `notification_queue (status, waktu_dijadwalkan)` untuk pemroses antrean.

Aktifkan mode WAL pada SQLite supaya pembacaan dashboard tidak mengunci penulisan
absensi. Nyalakan juga penegakan kunci asing, karena SQLite mematikannya secara
bawaan.

Backup adalah penyalinan berkas basis data. Lakukan otomatis setiap hari setelah jam
pulang, simpan beberapa salinan bergilir, dan sediakan tombol backup manual serta
restore di menu pengaturan.
