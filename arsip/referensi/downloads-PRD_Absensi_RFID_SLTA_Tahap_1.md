# PRD — Sistem Absensi RFID Siswa SLTA
## Versi 1.0 — Tahap 1

---

## 1. Ringkasan Produk

Sistem Absensi RFID Siswa SLTA adalah aplikasi absensi berbasis desktop yang diinstal pada komputer lokal sekolah dalam bentuk file executable (`.exe`).

Fokus Tahap 1:

1. Absensi masuk sekolah.
2. Absensi pulang sekolah.
3. Pencatatan status kehadiran siswa.
4. Penyimpanan data secara lokal.
5. Pengiriman notifikasi absensi ke nomor WhatsApp orang tua/wali.

Sistem dirancang dengan prinsip **local-first**. Proses utama absensi tetap berjalan ketika internet tidak tersedia. Internet digunakan untuk layanan eksternal seperti pengiriman notifikasi WhatsApp, sinkronisasi/backup online, dan pembaruan aplikasi.

---

## 2. Tujuan Produk

Sistem bertujuan menyediakan absensi sekolah yang:

- cepat;
- sederhana;
- mudah digunakan operator;
- tetap dapat mencatat absensi tanpa internet;
- menyimpan data secara lokal;
- dapat mengirim notifikasi otomatis kepada orang tua/wali;
- memiliki rekam data absensi yang dapat diaudit.

### Sasaran Tahap 1

Memastikan dua proses inti berjalan stabil:

```text
RFID → IDENTIFIKASI SISWA → ABSEN MASUK
RFID → IDENTIFIKASI SISWA → ABSEN PULANG
```

Fitur pergerakan siswa keluar-masuk di tengah hari belum termasuk Tahap 1.

---

## 3. Platform

### Aplikasi utama

Format aplikasi:

```text
.exe
```

Aplikasi dapat:

- diinstal pada PC sekolah;
- dijalankan sebagai aplikasi desktop;
- berkomunikasi dengan RFID Reader;
- menggunakan database lokal;
- memiliki antarmuka GUI;
- memiliki menu navigasi;
- menggunakan internet untuk fitur online tertentu.

### Konsep umum

```text
PC SEKOLAH
│
├── Aplikasi Absensi.exe
├── Database Lokal
├── RFID Reader
└── Internet
       │
       └── Layanan WhatsApp
```

---

## 4. Arsitektur Sistem

```text
                 ┌───────────────┐
                 │   RFID CARD   │
                 └───────┬───────┘
                         │
                         ▼
                 ┌───────────────┐
                 │ RFID READER   │
                 └───────┬───────┘
                         │
                         ▼
┌─────────────────────────────────────────────────┐
│                 PC SEKOLAH                      │
│                                                 │
│             APLIKASI ABSENSI (.EXE)             │
│                                                 │
│  ┌──────────────┐       ┌───────────────────┐  │
│  │ RFID Handler │──────▶│ Attendance Engine │  │
│  └──────────────┘       └─────────┬─────────┘  │
│                                   │            │
│                                   ▼            │
│                         ┌─────────────────┐    │
│                         │ Local Database  │    │
│                         └────────┬────────┘    │
│                                  │             │
└──────────────────────────────────┼─────────────┘
                                   │
                              INTERNET
                                   │
                                   ▼
                         ┌────────────────────┐
                         │ WhatsApp Service   │
                         └─────────┬──────────┘
                                   │
                                   ▼
                              ORANG TUA
```

### Prinsip arsitektur

- Database lokal adalah sumber operasional utama pada Tahap 1.
- Proses absensi tidak bergantung pada internet.
- Notifikasi WhatsApp adalah proses terpisah dari pencatatan absensi.
- Kegagalan WhatsApp tidak boleh menggagalkan atau menghapus absensi.

---

## 5. Prinsip Offline-First

### Internet tersedia

```text
RFID
 ↓
Absensi tersimpan lokal
 ↓
Notifikasi WhatsApp dikirim
```

### Internet tidak tersedia

```text
RFID
 ↓
Absensi tersimpan lokal
 ↓
Notifikasi = PENDING
 ↓
Internet kembali
 ↓
Sistem mencoba mengirim ulang
```

Internet bukan syarat untuk mencatat kehadiran.

---

## 6. Jadwal Sekolah

Jadwal merupakan konfigurasi sistem dan tidak boleh hard-coded.

| Hari | Jam Masuk | Jam Pulang |
|---|---:|---:|
| Senin | 07:00 | 15:00 |
| Selasa | 07:00 | 15:00 |
| Rabu | 07:00 | 15:00 |
| Kamis | 07:00 | 15:00 |
| Jumat | 07:00 | 11:30 |
| Sabtu | 07:00 | 12:00 |
| Minggu | Libur | Libur |

Parameter tambahan yang perlu dikonfigurasi:

- toleransi keterlambatan;
- batas penetapan ALPA (`ATTENDANCE_CUTOFF_TIME`);
- duplicate scan window.

Nilai parameter tersebut ditetapkan pada tahap Business Rules.

---

## 7. Model Absensi Tahap 1

Tahap 1 menggunakan model sederhana:

```text
BELUM ABSEN
      │
      │ RFID pada periode masuk
      ▼
 ABSEN MASUK
      │
      │ RFID pada periode pulang
      ▼
 ABSEN PULANG
```

Sistem tidak terlebih dahulu memodelkan:

- `INSIDE`;
- `OUTSIDE`;
- `RETURN`;
- izin keluar;
- alasan keluar;
- unauthorized exit;
- early exit.

Fitur tersebut ditunda ke fase berikutnya.

---

## 8. Absensi Masuk

Ketika kartu RFID dibaca:

```text
RFID SCAN
    ↓
Baca UID kartu
    ↓
Cari kartu di database
    ↓
Identifikasi siswa
    ↓
Cek jadwal hari ini
    ↓
Cek status absensi siswa
    ↓
Catat ABSEN MASUK
```

Data minimal:

- ID siswa;
- UID RFID;
- tanggal;
- waktu masuk;
- perangkat/reader;
- status kehadiran;
- timestamp sistem.

---

## 9. Absensi Pulang

Scan RFID berikutnya digunakan sebagai absensi pulang sesuai periode dan aturan waktu yang ditetapkan.

```text
RFID SCAN
    ↓
Identifikasi siswa
    ↓
Cek absensi hari ini
    ↓
Sudah ABSEN MASUK?
    ↓
YA
    ↓
Cek waktu
    ↓
Periode pulang
    ↓
Catat ABSEN PULANG
```

Data minimal:

- ID siswa;
- UID RFID;
- tanggal;
- waktu pulang;
- perangkat/reader;
- timestamp sistem.

---

## 10. Duplicate Scan

Sistem harus mencegah scan berulang tercatat sebagai beberapa absensi.

Contoh:

```text
07:01:02 → Scan
07:01:03 → Scan
07:01:04 → Scan
```

Hanya scan valid pertama yang dicatat sebagai absensi.

Scan berikutnya dalam `duplicate window` dicatat sebagai `DUPLICATE` atau diabaikan sesuai aturan final.

Nilai awal yang dapat digunakan untuk pengujian: **10 detik**. Nilai ini harus dapat dikonfigurasi.

---

## 11. Status Kehadiran

Tahap 1 minimal mengenal:

- `HADIR`
- `TERLAMBAT`
- `ALPA`
- `IZIN`
- `SAKIT`

`HADIR` dan `TERLAMBAT` ditentukan berdasarkan waktu absensi masuk.

Contoh jika:

```text
Jam masuk = 07:00
Toleransi = 10 menit
```

Maka:

```text
06:55 → HADIR
07:00 → HADIR
07:10 → HADIR
07:11 → TERLAMBAT
```

Toleransi tidak boleh hard-coded.

---

## 12. Penentuan ALPA

Sistem tidak langsung memberikan ALPA pada awal jam masuk.

Diperlukan parameter:

```text
ATTENDANCE_CUTOFF_TIME
```

Contoh:

```text
09:00
```

Pada waktu cutoff:

```text
Tidak ada absensi masuk
+
Tidak ada status IZIN/SAKIT
        ↓
       ALPA
```

Aturan final cutoff ditetapkan dalam Business Rules.

---

## 13. Notifikasi WhatsApp

Setelah absensi tercatat, sistem membuat pekerjaan notifikasi.

```text
SISWA SCAN RFID
      ↓
ABSENSI TERCATAT
      ↓
DATABASE LOKAL
      ↓
NOTIFICATION QUEUE
      ↓
INTERNET
      ↓
WHATSAPP SERVICE
      ↓
NOMOR ORANG TUA/WALI
```

### Notifikasi masuk

Contoh:

> Ananda [Nama Siswa] telah melakukan absensi masuk sekolah pada pukul 06:58. Status: Hadir.

### Notifikasi pulang

Contoh:

> Ananda [Nama Siswa] telah melakukan absensi pulang sekolah pada pukul 15:03.

Template pesan harus dapat dikonfigurasi oleh admin.

---

## 14. Status Pengiriman WhatsApp

Setiap notifikasi memiliki status:

```text
PENDING
   ↓
SENDING
   ↓
SENT
```

Jika gagal:

```text
FAILED
   ↓
RETRY
   ↓
SENT
```

Contoh ketika internet mati:

```text
Waktu absensi : 07:01:22
WhatsApp      : PENDING
```

Setelah internet kembali:

```text
Waktu absensi : 07:01:22
Waktu kirim   : 08:15:07
WhatsApp      : SENT
```

Waktu kejadian absensi dan waktu pengiriman harus disimpan secara terpisah.

Sistem harus menggunakan ID unik/idempotency agar tidak terjadi pengiriman ganda.

---

## 15. Data Siswa dan Orang Tua

Relasi dasar:

```text
SISWA
 │
 ├── NIS
 ├── NISN
 ├── Nama
 ├── Kelas
 ├── Status
 ├── RFID UID
 └── ORANG TUA/WALI
       │
       ├── Nama
       ├── Nomor WhatsApp
       └── Status Notifikasi
```

Nomor WhatsApp harus disimpan dalam format standar, misalnya:

```text
+628xxxxxxxxxx
```

Satu siswa dapat memiliki konfigurasi penerima sesuai kebutuhan sistem.

---

## 16. Menu Aplikasi

```text
DASHBOARD
│
├── ABSENSI
│   ├── Absensi Masuk
│   ├── Absensi Pulang
│   └── Riwayat Absensi
│
├── DATA MASTER
│   ├── Siswa
│   ├── Kelas
│   ├── Orang Tua/Wali
│   └── Kartu RFID
│
├── JADWAL
│   └── Jadwal Sekolah
│
├── NOTIFIKASI
│   ├── WhatsApp
│   ├── Terkirim
│   ├── Pending
│   └── Gagal
│
├── LAPORAN
│   ├── Harian
│   ├── Mingguan
│   └── Bulanan
│
├── PERANGKAT
│   └── RFID Reader
│
└── PENGATURAN
```

---

## 17. Dashboard

Dashboard hari berjalan minimal menampilkan:

```text
ABSENSI HARI INI

Total Siswa        : 420
Sudah Masuk        : 397
Terlambat          : 18
Belum Absen        : 23
Izin               : 5
Sakit              : 2
Alpa               : -
```

Status layanan:

```text
RFID READER : CONNECTED
DATABASE    : ONLINE
INTERNET    : CONNECTED
WHATSAPP    : CONNECTED
```

Angka di atas hanya contoh UI, bukan data nyata.

---

## 18. Data Master Siswa

Data minimal:

- ID siswa;
- NIS;
- NISN;
- nama lengkap;
- kelas;
- jurusan;
- status siswa;
- UID RFID;
- nama orang tua/wali;
- nomor WhatsApp orang tua/wali;
- status aktif.

---

## 19. Manajemen RFID

Admin dapat:

- mendaftarkan kartu;
- menghubungkan kartu dengan siswa;
- mengganti kartu;
- menonaktifkan kartu;
- melihat UID;
- melihat status kartu.

Contoh:

```text
UID RFID : A7:32:F1:09
Siswa    : Ahmad Fauzan
Kelas    : XI IPA 1
Status   : ACTIVE
```

Kartu tidak dikenal:

```text
UNKNOWN CARD
```

Tidak boleh membuat absensi siswa dari kartu yang belum terdaftar.

---

## 20. RFID Reader

Aplikasi minimal dapat mengetahui:

- `Reader Connected`;
- `Reader Disconnected`;
- `Card Detected`;
- `Card UID`;
- timestamp.

Jika reader terputus, aplikasi menampilkan peringatan.

---

## 21. Database Lokal

Entitas minimal:

```text
students
classes
parents
rfid_cards
school_schedules
attendance
attendance_events
notification_queue
users
devices
audit_logs
```

### attendance_events

Menyimpan fakta mentah:

- UID;
- siswa;
- waktu scan;
- reader;
- event;
- sumber/event metadata.

### attendance

Menyimpan hasil pengolahan:

- siswa;
- tanggal;
- jam masuk;
- status masuk;
- jam pulang;
- status pulang.

Event mentah tidak boleh diubah hanya karena hasil pengolahan berubah.

---

## 22. Audit Trail

Perubahan penting harus tercatat:

- perubahan data siswa;
- perubahan nomor WhatsApp;
- perubahan jadwal;
- penggantian kartu RFID;
- koreksi absensi;
- perubahan status siswa;
- perubahan konfigurasi.

Minimal:

```text
WHO
WHAT
WHEN
BEFORE
AFTER
REASON
```

---

## 23. Keamanan

Minimal:

- login pengguna;
- password di-hash;
- role/permission;
- session/token;
- validasi input;
- validasi UID RFID;
- validasi nomor WhatsApp;
- audit log;
- backup database.

---

## 24. Backup

Karena database utama berada di PC lokal, backup merupakan kebutuhan wajib.

Minimal:

```text
Backup Manual
Backup Otomatis
Restore Database
```

Tahap berikutnya dapat menambahkan backup cloud.

---

## 25. Kebutuhan Internet

| Fitur | Internet |
|---|---|
| Scan RFID | Tidak wajib |
| Simpan absensi | Tidak wajib |
| Dashboard lokal | Tidak wajib |
| Riwayat lokal | Tidak wajib |
| Kirim WhatsApp | Wajib |
| Update aplikasi | Wajib |
| Backup cloud | Wajib jika digunakan |
| Sinkronisasi cloud | Wajib jika digunakan |

---

## 26. Penanganan Internet Putus

Jika internet putus:

```text
ABSENSI
  ↓
TERSIMPAN LOKAL
  ↓
NOTIFIKASI = PENDING
```

Ketika internet kembali:

```text
NETWORK CONNECTED
       ↓
QUEUE PROCESSOR
       ↓
Kirim notifikasi pending
       ↓
Update status
```

Kegagalan pengiriman tidak boleh menghapus data absensi.

---

## 27. Skenario Utama

### A. Masuk Normal

```text
06:55
↓
Siswa scan RFID
↓
Siswa ditemukan
↓
Belum absen masuk
↓
ABSEN MASUK
↓
HADIR
↓
Simpan lokal
↓
Buat notifikasi WhatsApp
```

### B. Terlambat

```text
07:11
↓
RFID
↓
Siswa ditemukan
↓
ABSEN MASUK
↓
TERLAMBAT
↓
Simpan
↓
Buat notifikasi WhatsApp
```

### C. Pulang

```text
15:02
↓
RFID
↓
Siswa ditemukan
↓
Sudah absen masuk
↓
Waktu memenuhi periode pulang
↓
ABSEN PULANG
↓
Buat notifikasi WhatsApp
```

### D. Internet Mati

```text
RFID
↓
ABSEN
↓
DATABASE LOKAL
↓
WhatsApp = PENDING

Internet kembali
↓
Kirim
↓
SENT
```

### E. Kartu Tidak Dikenal

```text
RFID
↓
UID tidak ditemukan
↓
UNKNOWN CARD
↓
Tidak membuat absensi
↓
Tampilkan peringatan
```

---

## 28. Di Luar Scope Tahap 1

Belum termasuk:

- izin keluar sekolah;
- alasan keluar;
- siswa keluar kemudian kembali;
- tracking `INSIDE/OUTSIDE`;
- `UNAUTHORIZED_EXIT`;
- `EARLY_EXIT`;
- face recognition;
- aplikasi orang tua;
- WhatsApp chatbot;
- pembayaran;
- akademik;
- nilai;
- GPS;
- jadwal pelajaran detail;
- cloud dashboard penuh.

---

## 29. Target MVP Tahap 1

### RFID

- [ ] Reader dapat terhubung.
- [ ] UID dapat dibaca.
- [ ] UID dapat dikaitkan dengan siswa.
- [ ] Kartu tidak dikenal dapat dideteksi.
- [ ] Duplicate scan dapat ditangani.

### Absensi

- [ ] Absensi masuk berjalan.
- [ ] Absensi pulang berjalan.
- [ ] HADIR dapat ditentukan.
- [ ] TERLAMBAT dapat ditentukan.
- [ ] ALPA dapat ditentukan setelah cutoff.
- [ ] Jadwal berbeda setiap hari dapat digunakan.

### Lokal

- [ ] Aplikasi berjalan sebagai `.exe`.
- [ ] Database lokal berjalan.
- [ ] Absensi tetap berjalan tanpa internet.
- [ ] Backup tersedia.
- [ ] Restore tersedia.

### WhatsApp

- [ ] Nomor orang tua/wali tersimpan.
- [ ] Notifikasi masuk dapat dikirim.
- [ ] Notifikasi pulang dapat dikirim.
- [ ] Notifikasi pending dapat dikirim ulang.
- [ ] Status `SENT/PENDING/FAILED` tersimpan.
- [ ] Tidak terjadi pengiriman ganda.

### Operasional

- [ ] Dashboard tersedia.
- [ ] Riwayat absensi tersedia.
- [ ] Laporan tersedia.
- [ ] Operator dapat mengelola siswa.
- [ ] Admin dapat mengelola RFID.

---

## 30. Prinsip Desain Utama

> **ABSENSI HARUS TETAP BERJALAN TANPA INTERNET.**

> **DATA ABSENSI DISIMPAN TERLEBIH DAHULU SECARA LOKAL.**

> **NOTIFIKASI WHATSAPP ADALAH PROSES TERPISAH DARI PENCATATAN ABSENSI.**

> **KEGAGALAN WHATSAPP TIDAK BOLEH MENGGAGALKAN ABSENSI.**

> **SETIAP RFID SCAN HARUS DAPAT DIAUDIT.**

---

## 31. Roadmap

### FASE 1 — ABSENSI DASAR

```text
RFID
 ↓
ABSEN MASUK
 ↓
ABSEN PULANG
 ↓
DATABASE LOKAL
 ↓
WHATSAPP NOTIFICATION
```

### FASE 2 — KONTROL PERGERAKAN

```text
MASUK
 ↓
KELUAR
 ↓
MASUK
 ↓
KELUAR
```

Kemungkinan fitur:

- izin;
- alasan keluar;
- return;
- early exit;
- unauthorized exit.

### FASE 3 — ONLINE & ORANG TUA

- portal orang tua;
- dashboard online;
- riwayat absensi;
- notifikasi lebih lengkap;
- cloud backup.

### FASE 4 — SMART SCHOOL

- analitik kehadiran;
- deteksi pola;
- integrasi akademik;
- integrasi pembayaran;
- integrasi sistem sekolah lainnya.

---

## 32. Definisi Produk Tahap 1

> **Sistem Absensi RFID Siswa SLTA Tahap 1 adalah aplikasi desktop `.exe` yang berjalan pada PC lokal sekolah untuk membaca kartu RFID, mengidentifikasi siswa, mencatat absensi masuk dan pulang berdasarkan jadwal sekolah, menyimpan data secara lokal, serta mengirimkan notifikasi absensi secara otomatis ke WhatsApp orang tua/wali melalui koneksi internet.**

Internet bukan bagian dari mesin absensi, tetapi merupakan jalur layanan notifikasi dan layanan online.

---

## 33. Alur Sistem Utama

```text
             ┌───────────────┐
             │   RFID CARD   │
             └───────┬───────┘
                     ↓
             ┌───────────────┐
             │ RFID READER   │
             └───────┬───────┘
                     ↓
             ┌───────────────┐
             │ APLIKASI .EXE │
             └───────┬───────┘
                     ↓
             ┌───────────────┐
             │ LOCAL DATABASE│
             └───────┬───────┘
                     │
              ┌──────┴───────┐
              ↓              ↓
         DASHBOARD       NOTIFICATION
                              │
                           INTERNET
                              │
                              ↓
                           WHATSAPP
                              │
                              ↓
                           ORANG TUA
```

**Status dokumen:** PRD Tahap 1 — Draft untuk validasi Business Rules.
