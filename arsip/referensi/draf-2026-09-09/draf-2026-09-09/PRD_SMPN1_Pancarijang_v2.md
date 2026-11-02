> ## ⚠️ DOKUMEN INI SUDAH DIGANTIKAN — JANGAN DIJADIKAN RUJUKAN
>
> Digantikan sepenuhnya oleh **PRD_Absensi_RFID_SMPN1_Pancarijang.docx** (versi
> 2.0, format Word resmi untuk pengesahan sekolah) di `docs/` proyek utama,
> per 2026-09-10. Berkas ini dipindahkan ke arsip
> (`_archive/draf-2026-09-09/`) dan disimpan hanya sebagai riwayat draf awal.
>
> Perubahan paling penting dari dokumen ini: fitur **impor data Dapodik**
> (Bagian 20.1 di bawah) **dibatalkan sepenuhnya** — seluruh data master kini
> diinput manual satu per satu, tidak ada fitur unggah/impor berkas apa pun.
> Lihat SKILL.md proyek untuk daftar keputusan terkunci yang berlaku sekarang.

# PRD — Sistem Informasi Berbasis Digital UPT SMP Negeri 1 Pancarijang
## Versi 2.1 — Tahap 1: Modul Absensi (ARSIP — TIDAK BERLAKU)

**Sekolah:** UPT SMP Negeri 1 Pancarijang, Kabupaten Sidenreng Rappang, Sulawesi Selatan
**Status dokumen:** Spesifikasi Tahap 1, siap masuk tahap perancangan teknis
**Menggantikan:** PRD Sistem Absensi RFID Siswa SLTA v1.0

---

## 1. Ringkasan Produk

Sistem Informasi Berbasis Digital UPT SMP Negeri 1 Pancarijang adalah aplikasi desktop berbentuk file executable (`.exe`) yang dipasang pada komputer sekolah.

Nama aplikasi mencerminkan cakupan jangka panjang, tetapi Tahap 1 sengaja dibatasi hanya pada satu modul: **absensi**.

Cakupan Tahap 1:

1. Absensi masuk dan pulang siswa berbasis kartu RFID.
2. Absensi guru berbasis kartu RFID.
3. Pencatatan dan pengolahan status kehadiran.
4. Penyimpanan data di komputer lokal sekolah.
5. Rekapitulasi harian, mingguan, dan bulanan.
6. Dashboard monitoring kedisiplinan untuk admin dan kepala sekolah.
7. Modul notifikasi WhatsApp kepada orang tua/wali — dibangun sekarang, diaktifkan kemudian.
8. Pengelolaan kelas, jadwal, pengguna, dan konfigurasi oleh admin.

Sistem dirancang dengan prinsip **local-first**. Proses absensi berjalan penuh tanpa internet. Internet hanya dibutuhkan untuk layanan tambahan: pengiriman notifikasi WhatsApp, sinkronisasi cadangan daring, dan pembaruan aplikasi.

---

## 2. Tujuan Produk

Menyediakan sistem absensi sekolah yang:

- cepat dan tidak menghambat antrean siswa di pagi hari;
- sederhana dioperasikan oleh staf tata usaha tanpa pelatihan panjang;
- tetap mencatat kehadiran meskipun internet dan listrik jaringan bermasalah;
- menyimpan data secara lokal dan dapat dicadangkan dengan mudah;
- menghasilkan rekap yang dapat langsung dipakai wali kelas dan kepala sekolah;
- memiliki jejak audit atas setiap perubahan data;
- siap mengirim notifikasi otomatis kepada orang tua begitu anggaran tersedia.

### Sasaran Tahap 1

Memastikan dua alur inti berjalan stabil setiap hari sekolah:

```text
RFID → IDENTIFIKASI → ABSEN MASUK
RFID → IDENTIFIKASI → ABSEN PULANG
```

Pergerakan siswa keluar-masuk di tengah hari belum termasuk Tahap 1.

---

## 3. Platform

Format aplikasi:

```text
.exe
```

Aplikasi dapat dipasang pada PC sekolah, dijalankan sebagai aplikasi desktop, berkomunikasi dengan pembaca RFID, menggunakan basis data lokal, menampilkan antarmuka grafis dengan menu navigasi, dan memanfaatkan internet untuk fitur daring tertentu.

### Konsep umum

```text
PC SEKOLAH
│
├── Aplikasi Absensi.exe
├── Database Lokal (SQLite)
├── RFID Reader
└── Internet (opsional)
       │
       ├── Layanan WhatsApp
       └── Penyimpanan cadangan daring
```

---

## 4. Arsitektur Sistem

```text
                 ┌───────────────┐
                 │   RFID CARD   │
                 └───────┬───────┘
                         ▼
                 ┌───────────────┐
                 │  RFID READER  │
                 └───────┬───────┘
                         ▼
┌─────────────────────────────────────────────────┐
│                  PC SEKOLAH                     │
│              APLIKASI ABSENSI (.EXE)            │
│                                                 │
│  ┌──────────────┐       ┌───────────────────┐   │
│  │ RFID Handler │──────▶│ Attendance Engine │   │
│  └──────────────┘       └─────────┬─────────┘   │
│                                   ▼             │
│                         ┌─────────────────┐     │
│                         │ Local Database  │     │
│                         └────────┬────────┘     │
│                                  │              │
│              ┌───────────────────┼────────────┐ │
│              ▼                   ▼            │ │
│        ┌──────────┐      ┌──────────────┐     │ │
│        │Dashboard │      │Notif. Queue  │     │ │
│        └──────────┘      └──────┬───────┘     │ │
└────────────────────────────────┼─────────────┼─┘
                                 │             │
                            INTERNET      INTERNET
                                 ▼             ▼
                     ┌────────────────┐  ┌──────────────┐
                     │WhatsApp Service│  │Cloud Backup  │
                     └───────┬────────┘  └──────────────┘
                             ▼
                        ORANG TUA
```

### Prinsip arsitektur

- Basis data lokal adalah satu-satunya sumber kebenaran operasional.
- Proses absensi tidak bergantung pada internet.
- Notifikasi WhatsApp adalah proses terpisah dari pencatatan absensi.
- Kegagalan notifikasi tidak boleh menggagalkan, menunda, atau menghapus absensi.
- Mesin absensi tidak boleh bergantung pada lapisan antarmuka. Aturan status harus dapat diuji tanpa membuka jendela aplikasi.

---

## 5. Prinsip Offline-First

**Saat internet tersedia:**

```text
RFID → Absensi tersimpan lokal → Notifikasi dikirim
```

**Saat internet tidak tersedia:**

```text
RFID → Absensi tersimpan lokal → Notifikasi = PENDING
                                        ↓
                                Internet kembali
                                        ↓
                                 Kirim ulang
```

Internet bukan syarat untuk mencatat kehadiran.

---

## 6. Jadwal Sekolah

Jadwal adalah konfigurasi sistem. Tidak boleh ditulis tetap di dalam kode.

| Hari | Jam Masuk | Batas Terlambat | Jam Pulang |
|---|---:|---:|---:|
| Senin | 07:15 | 07:20:59 | 13:30 |
| Selasa | 07:15 | 07:20:59 | 13:30 |
| Rabu | 07:15 | 07:20:59 | 13:30 |
| Kamis | 07:15 | 07:20:59 | 13:30 |
| Jumat | 07:15 | 07:20:59 | **11:10** |
| Sabtu | 07:15 | 07:20:59 | 13:30 |
| Minggu | Libur | — | Libur |

Jadwal siswa disimpan sebagai baris `school_schedules` per hari (bukan parameter
tunggal), persis karena jam pulang Jumat berbeda dari hari lain. Setiap baris punya
tanggal mulai berlaku sendiri — lihat "Riwayat jadwal" di bawah.

### Jadwal Guru

| Parameter | Nilai | Keterangan |
|---|---:|---|
| `BATAS_TERLAMBAT_GURU` | 09:31:00 | Scan pada atau setelah waktu ini berstatus TERLAMBAT |

Guru tidak punya jam masuk maupun jam pulang baku yang ditetapkan — hanya satu titik
pembanding keterlambatan. Guru boleh scan kapan pun sebelum 09:31:00 dan tetap
berstatus HADIR; tidak ada batas seberapa pagi. Lihat Bagian 13 untuk status kehadiran
guru selengkapnya.

### Parameter konfigurasi (siswa, disimpan di `settings`)

| Parameter | Nilai | Keterangan |
|---|---:|---|
| `ATTENDANCE_CUTOFF_TIME` | 07:30 | Waktu penetapan ALPA |
| `WAKTU_KIRIM_NOTIF_ALPA` | 09:15 | Saat jam pelajaran pertama berlangsung |
| `DUPLICATE_SCAN_WINDOW` | 10 detik | Jendela pengabaian scan berulang |
| `WAKTU_TUTUP_HARI_OTOMATIS` | 15:00 | Jaring pengaman bila admin belum Tutup Hari manual |

Seluruh nilai dapat diubah admin melalui menu Pengaturan. `JAM_MASUK`, `BATAS_TERLAMBAT`,
dan `JAM_PULANG` siswa **tidak** disimpan di `settings` seperti parameter di atas —
nilainya hidup di `school_schedules` karena berbeda per hari dan butuh riwayat tanggal
berlaku (lihat di bawah).

### Riwayat jadwal

Setiap perubahan jadwal (jam masuk, jam pulang, batas terlambat siswa) wajib memiliki
**tanggal mulai berlaku**, ditegakkan lewat baris `school_schedules`, bukan parameter
tunggal yang langsung berubah bagi semua tanggal. Perhitungan status selalu memakai
jadwal yang berlaku pada tanggal absensi bersangkutan, bukan jadwal terkini. Mengubah
jam masuk hari ini tidak boleh menggeser status absensi hari-hari sebelumnya.

---

## 7. Model Absensi Tahap 1

```text
BELUM ABSEN
      │ RFID pada periode masuk
      ▼
 ABSEN MASUK
      │ RFID pada periode pulang
      ▼
 ABSEN PULANG
```

Belum dimodelkan pada Tahap 1: `INSIDE`, `OUTSIDE`, `RETURN`, izin keluar, alasan keluar, `UNAUTHORIZED_EXIT`, dan `EARLY_EXIT`.

---

## 8. Absensi Masuk

```text
RFID SCAN
    ↓
Baca UID kartu
    ↓
Cari kartu di database
    ↓
Identifikasi siswa/guru
    ↓
Cek jadwal yang berlaku hari ini
    ↓
Cek scan berulang
    ↓
Cek status absensi hari ini
    ↓
Tentukan status (HADIR / TERLAMBAT)
    ↓
Catat ABSEN MASUK
    ↓
Antrekan notifikasi
```

Data minimal yang disimpan: ID pemilik, jenis pemilik, UID RFID, tanggal, waktu masuk, ID perangkat pembaca, status kehadiran, ID jadwal yang dipakai menghitung, dan timestamp sistem.

---

## 9. Absensi Pulang

```text
RFID SCAN
    ↓
Identifikasi pemilik kartu
    ↓
Cek absensi hari ini
    ↓
Sudah ada absen masuk?  ──TIDAK──▶ Catat sebagai peristiwa mentah, tidak
    ↓ YA                             membentuk baris absensi
Cek waktu
    ↓
Dalam periode pulang?  ──TIDAK──▶ Catat sebagai DI_LUAR_PERIODE
    ↓ YA
Catat ABSEN PULANG
    ↓
Antrekan notifikasi
```

Scan yang jatuh di luar periode masuk maupun periode pulang tetap disimpan di tabel peristiwa mentah agar dapat ditelusuri, tetapi tidak mengubah baris absensi.

---

## 10. Scan Berulang

Sistem harus mencegah satu penempelan kartu tercatat sebagai beberapa absensi.

```text
07:16:02 → Scan  → DICATAT
07:16:03 → Scan  → DUPLICATE
07:16:04 → Scan  → DUPLICATE
```

Hanya scan sah pertama yang membentuk absensi. Scan berikutnya dalam jendela 10 detik dicatat sebagai `DUPLICATE` pada tabel peristiwa. Nilai jendela dapat dikonfigurasi.

---

## 11. Status Kehadiran

### Status siswa

Status masuk yang dikenal sistem untuk siswa:

- `HADIR`
- `TERLAMBAT`
- `ALPA`
- `IZIN`
- `SAKIT`

`HADIR` dan `TERLAMBAT` ditentukan otomatis dari waktu scan masuk. `ALPA` ditentukan otomatis pada waktu cutoff. `IZIN` dan `SAKIT` **selalu diinput manual** oleh admin atau wali kelas, tidak pernah dihasilkan dari scan.

Selain status masuk, siswa juga punya penanda `BOLOS` pada sisi absen **pulang** —
lihat Bagian 12.1. `BOLOS` bukan pengganti status masuk; keduanya disimpan terpisah.

### Status guru

Status kehadiran guru terpisah dari siswa: `HADIR`, `TERLAMBAT`, `DINAS_LUAR`, `IZIN`,
`SAKIT`, `CUTI`, `TIDAK_HADIR`. `ALPA` dan `BOLOS` tidak pernah dipakai untuk guru.
Lihat Bagian 13.

Contoh dengan jadwal berlaku:

```text
Jam masuk       = 07:15
Batas terlambat = 07:21:00
```

Maka:

```text
07:05:00 → HADIR
07:15:00 → HADIR
07:20:59 → HADIR
07:21:00 → TERLAMBAT
07:45:00 → TERLAMBAT (dicatat, status ALPA tidak berubah otomatis — lihat Bagian 12)
```

---

## 12. Penentuan ALPA dan Koreksi

### Penetapan otomatis

Pada pukul 07:30, sistem memeriksa seluruh siswa aktif:

```text
Tidak ada absen masuk
        +
Tidak ada status IZIN atau SAKIT
        ↓
      ALPA
```

### Scan setelah cutoff

Siswa yang sudah berstatus ALPA lalu menempelkan kartu pukul 07:45 tetap dicatat scan-nya sebagai peristiwa. **Status kehadirannya tidak berubah otomatis.**

Ini keputusan sengaja. Sekolah menghendaki adanya penilaian manusia atas alasan keterlambatan, bukan pemutihan otomatis. Keterlambatan dengan alasan yang wajar dikoreksi oleh admin atau wali kelas.

### Koreksi manual

Admin dan wali kelas dapat mengubah status kehadiran. Setiap koreksi wajib menyimpan:

```text
WHO     — siapa yang mengubah
WHAT    — data apa yang diubah
WHEN    — kapan
BEFORE  — nilai sebelum
AFTER   — nilai sesudah
REASON  — alasan koreksi
```

Koreksi tanpa alasan ditolak sistem.

---

## 12.1 Status Bolos dan Tutup Hari

### Status BOLOS (siswa)

Siswa yang **sudah** absen masuk tetapi **tidak** absen pulang ditandai `BOLOS` saat
Tutup Hari (bukan pada saat itu juga saat jam pulang lewat — statusnya baru final
ketika Tutup Hari dijalankan). Ini penanda pada sisi absen pulang, terpisah total dari
status masuk: siswa yang `HADIR` atau `TERLAMBAT` paginya tetap tercatat begitu,
`BOLOS` tidak menimpa atau menggantikannya.

Siswa yang sama sekali tidak absen masuk sudah berstatus `ALPA` pada cutoff 07:30;
siswa itu **tidak** ditandai `BOLOS` lagi saat Tutup Hari. Satu ketidakhadiran tidak
mendapat dua penalti.

`BOLOS` dinotifikasi ke orang tua/wali dengan cara yang sama seperti `ALPA`.

```text
Sudah absen masuk?
   │
   ├─ TIDAK → sudah ALPA sejak cutoff 07:30 → tidak diproses lagi
   │
   └─ YA → Sudah absen pulang?
              │
              ├─ YA → tidak terjadi apa-apa
              │
              └─ TIDAK → status_pulang = BOLOS saat Tutup Hari
```

### Tutup Hari

Proses penetapan final harian, dipicu tombol "Tutup Hari" yang dioperasikan admin:

```text
TUTUP HARI (tanggal X)
   │
   ├─ Tanggal X hari libur?  ──YA──▶ Catat penutupan, tidak ada status yang difinalkan
   │        │
   │        NO
   │        ▼
   ├─ Finalkan BOLOS untuk siswa yang sudah absen masuk tapi belum absen pulang
   ├─ Finalkan TIDAK_HADIR untuk guru yang belum ada scan dan belum ada keterangan
   │  (DINAS_LUAR / IZIN / SAKIT / CUTI)
   └─ Antrekan notifikasi untuk baris yang baru difinalkan
```

Jaring pengaman: bila admin belum menekan Tutup Hari secara manual, sistem menutup
otomatis pukul 15:00 (`WAKTU_TUTUP_HARI_OTOMATIS`). Baik penutupan manual maupun
otomatis menjalankan proses finalisasi yang sama persis — bedanya hanya siapa/apa yang
memicunya, dicatat pada kolom `sumber` (`MANUAL` atau `OTOMATIS`).

Aturan penutupan:

- Wajib tercatat di audit log: siapa yang menutup (kosong bila otomatis) dan jam berapa.
- Antarmuka wajib meminta konfirmasi berlapis sebelum menjalankan — **tidak bisa
  dibatalkan** setelah dijalankan.
- Satu tanggal hanya boleh ditutup **sekali**. Percobaan menutup ulang tanggal yang
  sudah tertutup ditolak sistem, ditegakkan lewat kunci unik pada tabel `day_closures`,
  bukan sekadar validasi di antarmuka.

Pemicu waktu untuk cutoff ALPA (07:30) dan Tutup Hari otomatis (15:00) adalah tanggung
jawab `app/scheduler/`. Mesin absensi hanya menyediakan fungsi penentuan statusnya;
penjadwalannya sendiri disusun terpisah.

---

## 13. Absensi Guru

Guru menggunakan kartu RFID dan alur scan yang sama dengan siswa, tetapi dengan perbedaan:

- Jadwal guru dikonfigurasi terpisah dari jadwal siswa — lihat Bagian 6, "Jadwal Guru".
- Status kehadiran guru sudah ditetapkan (terpisah dari siswa): `HADIR`, `TERLAMBAT`,
  `DINAS_LUAR`, `IZIN`, `SAKIT`, `CUTI`, `TIDAK_HADIR`. `ALPA` dan `BOLOS` tidak
  dipakai untuk guru.
- Guru tidak memiliki penerima notifikasi WhatsApp.
- Rekap absensi guru terpisah dari rekap siswa dan diperuntukkan bagi kepala sekolah dan tata usaha.

### Penentuan status masuk guru

Berlaku pada scan pertama yang sah di hari itu, tanpa batas bawah waktu (guru boleh
scan sepagi apa pun dan tetap HADIR):

```text
scan < 09:31:00    → HADIR
scan >= 09:31:00   → TERLAMBAT
tidak ada scan, tidak ada keterangan, sampai Tutup Hari → TIDAK_HADIR
```

`DINAS_LUAR`, `IZIN`, `SAKIT`, dan `CUTI` selalu diinput manual, tidak pernah
dihasilkan dari scan. `TIDAK_HADIR` **tidak** punya waktu cutoff sendiri di pagi hari
seperti ALPA siswa — statusnya baru final saat Tutup Hari (Bagian 12.1), karena guru
bisa saja bertugas di luar sekolah tanpa sempat scan pagi itu.

Struktur basis data sudah menyiapkan tabel dan relasi untuk guru sejak awal
(`teacher_schedules` tersedia strukturnya, belum diisi — jam masuk dan jam pulang guru
belum ditetapkan sekolah, hanya batas keterlambatannya).

---

## 14. Notifikasi WhatsApp

**Status modul: dibangun pada Tahap 1, dinonaktifkan secara bawaan.**

Sekolah belum menganggarkan biaya layanan WhatsApp resmi. Karena itu modul dibangun lengkap agar siap pakai, tetapi tidak mengirim apa pun sampai admin mengaktifkannya.

### Alur

```text
SCAN RFID
   ↓
ABSENSI TERCATAT (transaksi selesai)
   ↓
NOTIFICATION QUEUE (transaksi terpisah)
   ↓
QUEUE PROCESSOR
   ↓
PROVIDER (dapat ditukar)
   ↓
NOMOR ORANG TUA/WALI
```

Penyimpanan absensi dan pembuatan antrean notifikasi harus berada dalam transaksi terpisah. Kegagalan pada antrean tidak boleh menggagalkan penyimpanan absensi.

### Lapisan penyedia yang dapat ditukar

Modul dibangun di balik satu antarmuka standar, sehingga berpindah layanan tidak menuntut penulisan ulang aplikasi:

| Implementasi | Fungsi |
|---|---|
| `NullProvider` | Bawaan. Tidak mengirim apa pun, pesan ditandai terlewat |
| `LogProvider` | Menulis pesan ke berkas log untuk pengujian tanpa biaya |
| `MetaCloudProvider` | Kerangka untuk WhatsApp Cloud API resmi, dilengkapi kemudian |

### Jenis notifikasi

| Jenis | Waktu kirim |
|---|---|
| Absen masuk | Segera setelah tercatat |
| Absen pulang | Segera setelah tercatat |
| ALPA | Pukul 09:15 |

Notifikasi ALPA sengaja ditahan sampai 09:15, bukan dikirim pada saat cutoff 07:30. Sebelum dikirim, sistem memeriksa ulang apakah siswa akhirnya scan atau statusnya sudah dikoreksi. Bila ya, pesan dibatalkan dengan status `CANCELLED`.

### Contoh isi pesan

> Ananda [Nama Siswa] telah melakukan absensi masuk pada pukul 07:12. Status: Hadir.

> Ananda [Nama Siswa] telah melakukan absensi pulang pada pukul 13:34.

> Ananda [Nama Siswa] belum tercatat hadir di sekolah pada hari ini. Mohon konfirmasi kepada wali kelas.

Template dapat diubah admin, memakai variabel bernama seperti `{nama_siswa}`, `{jam}`, `{status}`, `{kelas}`. Versi template disimpan agar terlihat template mana yang dipakai pada pesan lama.

### Catatan biaya untuk perencanaan anggaran

Sejak 1 Juli 2025 Meta menagih per pesan terkirim. Notifikasi absensi termasuk kategori *utility* karena dikirim atas inisiatif sekolah. Tidak tersedia jalur resmi yang gratis untuk pesan semacam ini.

Bentuk biayanya perlu dipahami sebelum modul diaktifkan: jumlah siswa × jumlah pesan per hari × jumlah hari sekolah per bulan. Mengirim dua pesan harian untuk seluruh siswa menghasilkan volume puluhan ribu pesan per bulan.

Opsi penghematan yang disarankan: kirim notifikasi hanya untuk kejadian yang perlu diketahui orang tua — terlambat, alpa, izin, dan sakit. Orang tua siswa yang hadir tepat waktu umumnya tidak membutuhkan pesan harian.

Tarif resmi wajib diverifikasi langsung ke rate card Meta sebelum penyusunan anggaran.

---

## 15. Status Pengiriman Notifikasi

```text
PENDING → SENDING → SENT

PENDING → SENDING → FAILED → RETRY → SENT

PENDING → CANCELLED   (siswa ternyata hadir atau status dikoreksi)
```

Waktu kejadian absensi dan waktu pengiriman disimpan terpisah:

```text
Waktu absensi : 07:12:22
Waktu kirim   : 08:15:07
Status        : SENT
```

Setiap pesan memiliki kunci idempotensi yang unik, agar satu peristiwa absensi tidak pernah menghasilkan dua pesan meskipun proses antrean dijalankan ulang.

---

## 16. Data Siswa dan Orang Tua

```text
SISWA
 ├── NIS
 ├── NISN
 ├── Nama lengkap
 ├── Kelas
 ├── Jenis kelamin
 ├── Status siswa
 ├── Kartu RFID
 └── ORANG TUA/WALI  (satu siswa boleh punya lebih dari satu penerima)
       ├── Nama
       ├── Hubungan
       ├── Nomor WhatsApp
       └── Menerima notifikasi (ya/tidak)
```

Nomor WhatsApp disimpan dalam format terstandar `+628xxxxxxxxxx`, dinormalkan pada saat penyimpanan, bukan pada saat penampilan.

Siswa tidak dihapus secara fisik. Siswa yang pindah, lulus, atau keluar ditandai statusnya agar riwayat absensinya tetap terbaca.

---

## 17. Peran dan Hak Akses

| Peran | Kewenangan |
|---|---|
| **Admin** | Seluruh pengaturan, data master, kelas, jadwal, template pesan, pengguna, menyalakan dan mematikan modul, koreksi absensi seluruh kelas |
| **Wali kelas** | Koreksi absensi hanya untuk kelas yang diampunya. Melihat rekap kelasnya. Tidak dapat mengubah pengaturan sistem |
| **Operator** | Menjalankan layar absensi dan melihat riwayat. Tidak dapat mengoreksi |
| **Kepala sekolah** | Akses baca ke dashboard dan seluruh laporan |

Pembatasan kelas bagi wali kelas ditegakkan di lapisan data, bukan sekadar disembunyikan di antarmuka.

Kata sandi disimpan dalam bentuk hash. Tidak pernah disimpan polos.

---

## 18. Menu Aplikasi

```text
DASHBOARD
│
├── ABSENSI
│   ├── Layar Absensi
│   ├── Riwayat Absensi
│   └── Koreksi Absensi
│
├── DATA MASTER
│   ├── Siswa
│   ├── Guru
│   ├── Kelas
│   ├── Orang Tua/Wali
│   └── Kartu RFID
│
├── JADWAL
│   ├── Jadwal Siswa
│   ├── Jadwal Guru
│   └── Hari Libur
│
├── LAPORAN
│   ├── Harian
│   ├── Mingguan
│   ├── Bulanan
│   ├── Rekap per Kelas
│   └── Rekap Guru
│
├── NOTIFIKASI
│   ├── Pengaturan Layanan
│   ├── Template Pesan
│   ├── Terkirim
│   ├── Pending
│   └── Gagal
│
├── PERANGKAT
│   └── RFID Reader
│
└── PENGATURAN
    ├── Jam dan Toleransi
    ├── Pengguna dan Peran
    ├── Modul Aktif
    ├── Backup dan Restore
    └── Sinkronisasi Daring
```

---

## 19. Dashboard Monitoring

Dashboard melayani dua kebutuhan berbeda dan keduanya harus terpenuhi.

### Pemantauan hari berjalan

```text
ABSENSI HARI INI — Selasa, 8 September 2026

Total siswa aktif   : ___
Sudah absen masuk   : ___
Terlambat           : ___
Belum absen         : ___
Izin                : ___
Sakit               : ___
Alpa                : ___
```

Angka yang menuntut tindakan — belum absen dan terlambat — ditampilkan lebih menonjol daripada angka total.

### Pemantauan kedisiplinan

Untuk kepala sekolah, yang berguna adalah pola, bukan angka harian:

- Grafik tren keterlambatan per kelas dalam rentang waktu yang dipilih.
- Perbandingan tingkat kehadiran antar kelas dan antar tingkat.
- Daftar siswa dengan alpa terbanyak pada rentang waktu tertentu, dapat langsung ditindaklanjuti wali kelas.
- Tren kehadiran guru.

Daftar yang dapat ditindaklanjuti lebih berguna daripada banyak grafik dekoratif.

### Status layanan

```text
RFID READER : CONNECTED
DATABASE    : ONLINE
INTERNET    : DISCONNECTED
WHATSAPP    : NONAKTIF
```

---

## 20. Data Master

### Siswa

ID, NIS, NISN, nama lengkap, kelas, jenis kelamin, status siswa, UID kartu RFID, data orang tua/wali, nomor WhatsApp, status aktif.

### Guru

ID, NIP, nama lengkap, status kepegawaian, UID kartu RFID, status aktif.

### Kelas

Dibuat, diubah, dan dihapus manual oleh admin. Menyimpan tingkat (7, 8, 9) terpisah dari nama kelas agar rekap per tingkat mudah disusun. Kelas hanya boleh dihapus bila tidak ada siswa aktif di dalamnya; bila ada, kelas ditandai tidak aktif.

---

## 20.1 Impor Data Dapodik

Data awal siswa dan guru diisi lewat impor file Dapodik, bukan diketik satu per satu.
Fitur impornya sendiri **menyusul** — menunggu contoh berkas dari sekolah sebelum
pemetaan kolom bisa dipastikan. Lihat Bagian 34.

UID kartu RFID **tidak ada** di data Dapodik. Pemasangan kartu ke siswa/guru tetap
dilakukan manual di menu Manajemen RFID setelah data pemiliknya masuk lewat impor.

Skala data: kurang lebih 1.000 siswa dan guru — dipakai sebagai acuan perkiraan biaya
notifikasi WhatsApp dan pengujian beban, bukan angka pasti dari sekolah.

---

## 21. Manajemen RFID

Admin dapat mendaftarkan kartu, menghubungkan kartu dengan siswa atau guru, mengganti kartu, menonaktifkan kartu, serta melihat UID dan status kartu.

```text
UID RFID : A7:32:F1:09
Pemilik  : Ahmad Fauzan
Kelas    : 8A
Status   : AKTIF
```

Kartu tidak dikenal:

```text
KARTU TIDAK TERDAFTAR
```

UID yang tidak terdaftar **tidak boleh membentuk absensi dalam keadaan apa pun**. Sistem menampilkan peringatan jelas, mencatat peristiwanya, lalu langsung siap menerima scan berikutnya tanpa perlu menutup dialog secara manual. Antrean pagi tidak boleh macet karena satu kartu asing.

Satu pemilik hanya boleh memiliki satu kartu aktif pada satu waktu. Kartu yang diganti dinonaktifkan, tidak dihapus, agar absensi lama tetap tertelusur.

---

## 22. RFID Reader

Aplikasi minimal mengetahui: `Reader Connected`, `Reader Disconnected`, `Card Detected`, `Card UID`, dan timestamp.

Banyak pembaca RFID USB berperilaku sebagai papan ketik (HID keyboard wedge) — UID "diketikkan" ke bidang yang sedang fokus. Aplikasi harus mendukung dua mode, serial dan HID, dan mode dipilih melalui menu Perangkat.

Bila pembaca terputus, peringatan harus muncul dalam hitungan detik dan terlihat jelas. Kabel tercabut tidak boleh baru ketahuan setelah puluhan siswa lewat tanpa tercatat.

---

## 23. Database Lokal

Entitas minimal:

```text
students
teachers
classes
guardians
rfid_cards
school_schedules
teacher_schedules
holidays
attendance_events
attendance
day_closures
notification_queue
message_templates
users
devices
settings
audit_logs
```

### Pemisahan peristiwa dan hasil

`attendance_events` menyimpan fakta mentah: kartu ini ditempel pada detik ini di pembaca ini. Baris di sini **tidak pernah diubah dan tidak pernah dihapus**, meskipun hasil pengolahannya berubah atau statusnya dikoreksi.

`attendance` menyimpan hasil pengolahan: satu baris per orang per tanggal, berisi jam masuk, status masuk, jam pulang, status pulang, dan ID jadwal yang dipakai menghitung. Baris di sini boleh dikoreksi, selalu disertai jejak audit.

`status_pulang` menyimpan `BOLOS` untuk siswa yang sudah absen masuk tapi tidak absen pulang, diisi saat Tutup Hari — terpisah dari `status_masuk` supaya status pagi tidak pernah tertimpa oleh kejadian sore.

`day_closures` mencatat satu baris per tanggal yang sudah ditutup (Bagian 12.1), dengan kunci unik pada `tanggal` yang menegakkan "satu hari hanya bisa ditutup sekali" di lapisan data.

Bila aturan status berubah di kemudian hari, seluruh isi `attendance` dapat dihitung ulang dari `attendance_events` tanpa kehilangan apa pun.

### Catatan teknis

Kunci unik gabungan pada `attendance (id_pemilik, jenis_pemilik, tanggal)`. Kunci unik pada `day_closures.tanggal`.

Indeks wajib: `rfid_cards.uid_kartu` (kueri terpanas, dijalankan ratusan kali dalam sepuluh menit), `attendance_events.waktu_scan`, dan `notification_queue (status, waktu_dijadwalkan)`.

Mode WAL diaktifkan agar pembacaan dashboard tidak mengunci penulisan absensi. Penegakan kunci asing diaktifkan secara eksplisit, karena SQLite mematikannya secara bawaan.

Seluruh perbandingan waktu memakai waktu lokal WITA, bukan UTC.

---

## 24. Sinkronisasi Daring

Basis data lokal adalah sumber kebenaran. Salinan daring berfungsi sebagai cadangan, bukan sebagai sumber operasional.

```text
DATABASE LOKAL  ──(saat internet tersedia)──▶  PENYIMPANAN DARING
```

Sinkronisasi berjalan satu arah pada Tahap 1: dari lokal ke daring. Kegagalan sinkronisasi tidak boleh mempengaruhi operasi absensi.

Layanan penyimpanan yang dipakai belum ditentukan. Lihat Bagian 34.

---

## 25. Audit Trail

Perubahan yang wajib tercatat: data siswa, data guru, nomor WhatsApp, jadwal, penggantian kartu RFID, koreksi absensi, status siswa, pengguna dan peran, serta konfigurasi sistem.

Format minimal:

```text
WHO
WHAT
WHEN
BEFORE
AFTER
REASON
```

---

## 26. Keamanan

- Login pengguna dengan kata sandi ter-hash.
- Peran dan izin ditegakkan di lapisan data.
- Sesi dengan batas waktu tidak aktif.
- Validasi masukan pada seluruh formulir.
- Validasi format UID RFID.
- Validasi dan normalisasi nomor WhatsApp.
- Audit log untuk seluruh perubahan penting.
- Backup rutin.

---

## 27. Backup

Karena basis data berada di PC lokal, backup adalah kebutuhan wajib, bukan pelengkap.

```text
Backup Manual     — tombol di menu Pengaturan
Backup Otomatis   — harian, setelah jam pulang
Restore Database  — dengan konfirmasi berlapis
```

Simpan beberapa salinan bergilir, bukan hanya satu. Backup berupa penyalinan berkas basis data.

---

## 28. Kebutuhan Internet

| Fitur | Internet |
|---|---|
| Scan RFID | Tidak wajib |
| Simpan absensi | Tidak wajib |
| Penentuan status dan ALPA | Tidak wajib |
| Dashboard | Tidak wajib |
| Laporan dan rekap | Tidak wajib |
| Backup lokal | Tidak wajib |
| Kirim WhatsApp | Wajib |
| Sinkronisasi daring | Wajib |
| Pembaruan aplikasi | Wajib |

---

## 29. Penanganan Internet Putus

```text
ABSENSI → TERSIMPAN LOKAL → NOTIFIKASI = PENDING

Internet kembali
       ↓
QUEUE PROCESSOR
       ↓
Kirim notifikasi pending
       ↓
Update status
```

Kegagalan pengiriman tidak boleh menghapus atau mengubah data absensi.

---

## 30. Stack Teknis

```text
Python 3.11+
PySide6              antarmuka
SQLAlchemy + SQLite  basis data lokal
Alembic              migrasi skema
pyserial             pembaca RFID mode serial
APScheduler          cutoff ALPA, jadwal notifikasi, backup otomatis
PyQtGraph            grafik dashboard
argon2-cffi          hash kata sandi
PyInstaller          membungkus menjadi .exe
```

Struktur modul:

```text
app/
├── ui/           layar PySide6
├── core/         mesin absensi, aturan status
├── data/         model SQLAlchemy, repositori
├── rfid/         pembacaan kartu (serial + HID)
├── notify/       antrean dan penyedia WhatsApp
├── scheduler/    tugas terjadwal
└── config/       pengaturan dan feature flag
```

Modul `core/` tidak boleh mengimpor apa pun dari `ui/`.

Pembacaan RFID dan pengiriman notifikasi berjalan di thread terpisah. Antarmuka tidak boleh membeku saat kartu ditempel.

---

## 31. Skenario Utama

### A. Masuk tepat waktu

```text
07:08 → Scan → Siswa ditemukan → Belum absen → ABSEN MASUK → HADIR
      → Simpan lokal → Antrekan notifikasi
```

### B. Terlambat

```text
07:23 → Scan → Siswa ditemukan → ABSEN MASUK → TERLAMBAT
      → Simpan → Antrekan notifikasi
```

### C. Alpa

```text
07:30 → Cutoff → Tidak ada scan, tidak ada izin/sakit → ALPA
09:15 → Periksa ulang → Masih ALPA → Kirim notifikasi
```

### D. Datang setelah cutoff

```text
07:45 → Scan → Siswa berstatus ALPA
      → Scan dicatat sebagai peristiwa
      → Status TIDAK berubah otomatis
      → Wali kelas mengoreksi manual dengan alasan
      → Notifikasi ALPA pukul 09:15 dibatalkan
```

### E. Pulang

```text
13:34 → Scan → Sudah absen masuk → Dalam periode pulang → ABSEN PULANG
      → Antrekan notifikasi
```

### F. Internet mati

```text
Scan → Absen tersimpan → Notifikasi PENDING
Internet kembali → Kirim → SENT
```

### G. Kartu tidak dikenal

```text
Scan → UID tidak ditemukan → KARTU TIDAK TERDAFTAR
     → Tidak membentuk absensi → Peringatan tampil
     → Siap menerima scan berikutnya
```

### H. Bolos

```text
07:08 → Scan masuk → HADIR
13:34 → Tidak ada scan pulang
15:00 → Tutup Hari (otomatis, belum ditutup manual)
      → Belum ada absen pulang → status_pulang = BOLOS
      → Antrekan notifikasi BOLOS
      → Status masuk (HADIR) tidak berubah
```

### I. Guru tidak hadir tanpa keterangan

```text
Sepanjang hari → Tidak ada scan, tidak ada DINAS_LUAR/IZIN/SAKIT/CUTI
Tutup Hari (manual atau otomatis) → status_masuk = TIDAK_HADIR
```

---

## 32. Pedoman Antarmuka

Tampilan harus ramah, profesional, mudah dimengerti, dan tidak terkesan dihasilkan secara otomatis.

### Prinsip

Pakai istilah sekolah Indonesia yang sesungguhnya: NIS, NISN, wali kelas, rombel, tahun pelajaran, semester ganjil dan genap. Bukan istilah generik hasil terjemahan.

Susun layar mengikuti alur kerja nyata, bukan mengikuti struktur tabel basis data. Operator tidak berpikir "buka tabel siswa"; dia berpikir "anak ini kartunya hilang".

Beri bobot visual berbeda pada hal yang berbeda pentingnya. Jumlah siswa belum absen jauh lebih penting daripada total siswa terdaftar.

Rapatkan tabel. Daftar ratusan siswa dengan baris renggang menyulitkan, bukan mempercantik.

Satu keluarga font. Satu warna aksen. Warna status konsisten di seluruh aplikasi: hijau hadir, kuning terlambat, merah alpa, biru izin, ungu sakit.

Hindari emoji sebagai ikon, gradien dekoratif, kartu seragam berjajar tanpa hierarki, dan persentase perubahan tanpa pembanding yang jelas.

### Layar absensi

Layar ini dipakai setiap pagi saat ratusan siswa mengantre, dan aturannya berbeda dari layar lain.

Harus bekerja **tanpa disentuh sama sekali**. Tidak ada dialog yang perlu ditutup, tidak ada tombol konfirmasi, tidak ada fokus bidang isian yang bisa hilang.

Umpan balik terbaca dari jarak dua meter dalam waktu kurang dari sedetik: nama besar, kelas, jam scan, status, dan foto siswa bila tersedia. Foto membantu operator menangkap kartu yang dipinjamkan.

Hasil dibedakan lewat warna sekaligus bentuk, tidak warna saja.

Bunyi berbeda untuk berhasil dan gagal. Operator tidak selalu menatap layar.

Riwayat beberapa scan terakhir tetap terlihat, agar operator yakin alat bekerja. Status pembaca RFID selalu terpampang.

---

## 33. Di Luar Cakupan Tahap 1

Izin keluar sekolah, alasan keluar, siswa keluar lalu kembali, pelacakan `INSIDE/OUTSIDE`, `UNAUTHORIZED_EXIT`, `EARLY_EXIT`, pengenalan wajah, aplikasi untuk orang tua, chatbot WhatsApp, pembayaran, nilai akademik, jadwal pelajaran detail, GPS, dan portal daring penuh.

---

## 34. Hal yang Belum Diputuskan

Butir-butir ini harus dijawab sebelum masuk tahap konstruksi. Tidak boleh diisi dengan asumsi.

| No | Hal | Dampak bila tertunda |
|---|---|---|
| 1 | Tanggal libur konkret dan kalender akademik | Tabel `holidays` dan pemeriksaannya di mesin absensi sudah ada, tapi tanpa tanggal terisi, tidak ada tanggal yang diperlakukan sebagai libur |
| 2 | Skema kolom impor Dapodik | Fitur impor data awal (Bagian 20.1) tidak dapat diselesaikan sebelum ada contoh berkas dari sekolah |
| 3 | Layanan penyimpanan daring | Modul sinkronisasi tidak dapat diselesaikan |

Butir-butir berikut sudah **selesai diputuskan** sejak versi 2.1 dan dipindahkan dari
daftar ini ke bagian terkait: jam pulang Jumat/Sabtu (Bagian 6), status dan toleransi
keterlambatan guru (Bagian 6 dan 13), status BOLOS dan proses Tutup Hari (Bagian
12.1), skala data (~1.000 siswa dan guru, Bagian 20.1), dan sumber data awal lewat
impor Dapodik (Bagian 20.1).

---

## 35. Target MVP Tahap 1

### RFID

- [ ] Pembaca terhubung, mode serial dan HID
- [ ] UID terbaca
- [ ] UID terkait dengan siswa dan guru
- [ ] Kartu tidak dikenal terdeteksi dan tidak membentuk absensi
- [ ] Scan berulang tertangani

### Absensi

- [ ] Absensi masuk berjalan
- [ ] Absensi pulang berjalan
- [ ] HADIR dan TERLAMBAT ditentukan otomatis
- [ ] ALPA ditetapkan pada cutoff
- [ ] IZIN dan SAKIT dapat diinput manual
- [ ] Koreksi manual berjalan lengkap dengan alasan dan audit
- [ ] Jadwal berbasis tanggal berlaku bekerja benar
- [ ] Hari libur tidak menghasilkan ALPA, BOLOS, maupun TIDAK_HADIR
- [ ] BOLOS ditetapkan saat Tutup Hari untuk siswa yang absen masuk tapi tidak absen pulang
- [ ] Siswa yang sama sekali tidak absen masuk (sudah ALPA) tidak ditandai BOLOS lagi
- [ ] Tutup Hari manual berjalan, dengan konfirmasi berlapis dan tidak bisa dibatalkan
- [ ] Tutup Hari otomatis berjalan pukul 15:00 bila belum ditutup manual
- [ ] Satu tanggal tidak bisa ditutup dua kali

### Guru

- [ ] Absensi guru berjalan
- [ ] HADIR dan TERLAMBAT guru ditentukan otomatis dari batas 09:31:00
- [ ] TIDAK_HADIR guru ditetapkan saat Tutup Hari, bukan pada cutoff pagi
- [ ] DINAS_LUAR, IZIN, SAKIT, CUTI dapat diinput manual
- [ ] Rekap guru tersedia

### Data awal

- [ ] Impor data Dapodik (menyusul, menunggu contoh berkas dan skema kolom)
- [ ] Pemasangan kartu RFID tetap manual setelah data pemilik masuk lewat impor

### Lokal

- [ ] Aplikasi berjalan sebagai `.exe`
- [ ] Basis data lokal berjalan
- [ ] Absensi berjalan penuh tanpa internet
- [ ] Backup manual dan otomatis tersedia
- [ ] Restore tersedia

### Notifikasi

- [ ] Antrean notifikasi berjalan
- [ ] Kunci idempotensi mencegah pengiriman ganda
- [ ] Status PENDING, SENT, FAILED, CANCELLED tersimpan
- [ ] Waktu kejadian dan waktu kirim tersimpan terpisah
- [ ] Template dapat diubah admin
- [ ] Penyedia dapat ditukar tanpa mengubah kode lain

### Operasional

- [ ] Dashboard hari berjalan tersedia
- [ ] Dashboard kedisiplinan dengan grafik tersedia
- [ ] Laporan harian, mingguan, dan bulanan tersedia
- [ ] Admin dapat mengelola siswa, guru, kelas, jadwal, dan pengguna
- [ ] Wali kelas hanya dapat mengoreksi kelasnya sendiri
- [ ] Modul dapat dinyalakan dan dimatikan admin

---

## 36. Prinsip Desain Utama

> **ABSENSI HARUS TETAP BERJALAN TANPA INTERNET.**

> **DATA ABSENSI DISIMPAN LEBIH DAHULU SECARA LOKAL.**

> **NOTIFIKASI ADALAH PROSES TERPISAH DARI PENCATATAN ABSENSI.**

> **KEGAGALAN NOTIFIKASI TIDAK BOLEH MENGGAGALKAN ABSENSI.**

> **SETIAP SCAN RFID HARUS DAPAT DIAUDIT.**

> **TIDAK ADA NILAI WAKTU YANG DITULIS TETAP DI DALAM KODE.**

> **KOREKSI STATUS SELALU MELIBATKAN PENILAIAN MANUSIA.**

---

## 37. Roadmap

### Tahap 1 — Absensi

```text
RFID → ABSEN MASUK → ABSEN PULANG → DATABASE LOKAL → DASHBOARD
                                                   → ANTREAN NOTIFIKASI
```

### Tahap 2 — Aktivasi Notifikasi

Verifikasi Meta Business, penyiapan nomor khusus, pengajuan template, uji coba terbatas pada satu atau dua kelas, lalu perluasan bertahap.

### Tahap 3 — Kontrol Pergerakan

Izin keluar, alasan keluar, siswa kembali, pulang lebih awal, keluar tanpa izin.

### Tahap 4 — Layanan Daring dan Orang Tua

Portal orang tua, dashboard daring, riwayat absensi daring, cadangan awan penuh.

### Tahap 5 — Sistem Informasi Sekolah

Analitik kehadiran, deteksi pola, integrasi akademik, integrasi pembayaran, dan modul lain dari sistem informasi sekolah.

---

## 38. Definisi Produk Tahap 1

> **Sistem Informasi Berbasis Digital UPT SMP Negeri 1 Pancarijang Tahap 1 adalah aplikasi desktop `.exe` berbasis Python yang berjalan pada PC lokal sekolah untuk membaca kartu RFID, mengidentifikasi siswa dan guru, mencatat absensi masuk dan pulang berdasarkan jadwal yang dapat dikonfigurasi, menentukan status kehadiran secara otomatis, menyimpan seluruh data secara lokal, menyajikan rekap dan dashboard kedisiplinan, serta menyiapkan modul notifikasi WhatsApp yang dapat diaktifkan ketika sekolah siap.**

Internet bukan bagian dari mesin absensi. Internet adalah jalur layanan tambahan.

---

**Riwayat dokumen**

| Versi | Perubahan |
|---|---|
| 1.0 | Draf awal, konteks SLTA umum |
| 2.0 | Disesuaikan untuk UPT SMP Negeri 1 Pancarijang. Jam sekolah ditetapkan. Absensi guru, dashboard monitoring, sinkronisasi daring, peran wali kelas, hari libur, dan stack teknis ditambahkan. Modul WhatsApp diubah menjadi arsitektur penyedia yang dapat ditukar dengan status nonaktif. Aturan koreksi ALPA ditetapkan manual |
| 2.1 | Jam pulang Jumat ditetapkan 11:10 (hari lain tetap 13:30), disimpan per hari lewat `school_schedules`. Status kehadiran guru ditetapkan: HADIR, TERLAMBAT, DINAS_LUAR, IZIN, SAKIT, CUTI, TIDAK_HADIR, dengan batas terlambat 09:31:00. Status BOLOS (siswa) dan proses Tutup Hari ditambahkan (Bagian 12.1), termasuk jaring pengaman otomatis pukul 15:00 dan tabel `day_closures`. Sumber data awal ditetapkan: impor Dapodik (fitur menyusul), UID RFID tetap dipasang manual, skala ~1.000 siswa dan guru. Hal yang belum diputuskan dipersempit menjadi tanggal libur konkret, skema kolom Dapodik, dan layanan penyimpanan daring |
