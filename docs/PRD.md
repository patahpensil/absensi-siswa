# PRD — Aplikasi Absensi Siswa RFID

**Versi dokumen:** 0.9 (draf, belum final)
**Tanggal:** 12 September 2026
**Sekolah rujukan:** UPT SMP Negeri 1 Pancarijang
**Status:** terbuka untuk perubahan

> **REVISI ARSITEKTUR 2026-11-03 (keputusan pemilik proyek): aplikasi berubah dari _standalone_ menjadi _online_.**
> Rincian, dampak per bagian, dan keputusan yang masih terbuka ada di **§21**. Sampai §21 selesai diputuskan dan
> bagian-bagian terkait diperbarui, pernyataan "standalone / satu PC / offline-first / SQLite lokal" di §1, §13, §15,
> §18, §19, dan §20 **sudah tidak berlaku sebagai arah** — dipertahankan apa adanya hanya sebagai catatan kondisi
> versi 0.9 yang sedang berjalan.

---

## 1. Ringkasan Produk

Aplikasi desktop standalone untuk mencatat kehadiran siswa menggunakan kartu RFID, dengan notifikasi otomatis ke orang tua melalui Telegram.

Aplikasi dibangun sebagai produk yang dapat dijual ke sekolah lain, bukan hanya untuk satu sekolah. Semua identitas sekolah, jadwal, penamaan kelas, dan kalender pendidikan bersifat konfigurasi, bukan nilai tertanam di kode.

| Aspek | Ketetapan |
|---|---|
| Bentuk | Satu file `.exe` standalone + file database terpisah |
| Deployment | 1 instalasi per sekolah |
| Koneksi | Offline-first; internet hanya untuk notifikasi, aktivasi, dan backup |
| Database | SQLite |
| Lisensi | Berbayar per sekolah, beli putus (opsional kontrak) |
| Kanal notifikasi | Telegram |

---

## 2. Tujuan

1. Menghapus absensi manual di buku dan rekap tulis tangan.
2. Memberi orang tua kepastian harian tentang kehadiran anaknya.
3. Menghasilkan laporan kehadiran yang siap diserahkan ke kepala sekolah dan dinas.
4. Menjadi produk yang bisa dipasang di sekolah lain tanpa pendampingan teknis berat.

---

## 3. Pengguna dan Hak Akses

Login memakai **username dan password**, satu akun per orang.

| Peran | Jumlah | Kewenangan |
|---|---|---|
| Kepala Sekolah | 1 | Penuh |
| Admin / TU | 2 | Penuh (setara kepala sekolah) |
| Wali Kelas | 1 per kelas | Koreksi status kehadiran siswa di kelasnya saja |

**Kewenangan penuh mencakup:** master siswa, master kelas, registrasi dan penggantian kartu, kalender pendidikan, naik kelas massal, kunci bulan, koreksi status, pembuatan akun, laporan, pengaturan.

**Wali kelas tidak dapat:** mengubah master data, mendaftarkan kartu, mengunci bulan, atau menyentuh data kelas lain.

### 3.1 Hierarki Pembuatan Akun

```
Wizard instalasi pertama  →  akun Kepala Sekolah
Kepala Sekolah            →  akun Admin
Admin                     →  akun Wali Kelas
```

Kepala sekolah dan admin sama-sama berwenang penuh, sehingga tidak terjadi kebuntuan bila salah satu berhalangan atau dimutasi.

### 3.2 Delegasi Koreksi (versi ini)

Wali kelas menyampaikan koreksi kepada admin. Admin mengeksekusi **memakai akunnya sendiri**, dengan mengisi kolom **"atas permintaan"** berisi nama wali kelas. Akun wali kelas tidak dipinjamkan.

---

## 4. Perangkat Keras

| Komponen | Ketetapan |
|---|---|
| Reader | 4 unit, USB keyboard-emulation |
| Penempatan | Seluruhnya di satu pintu gerbang |
| Fungsi reader | Sama untuk absen masuk dan pulang; dibedakan oleh logika aplikasi |
| Layar | 1 unit layar besar (TV) di gerbang |
| PC | 1 unit, menjalankan aplikasi |

**Catatan teknis:** reader USB keyboard-emulation tidak dapat dibedakan satu sama lain oleh sistem operasi. Aplikasi karena itu tidak melabeli tap berdasarkan nomor reader.

---

## 5. Master Data

### 5.1 Tahun Ajaran

Entitas tersendiri: nama (mis. 2026/2027), semester, tanggal mulai, tanggal berakhir, penanda aktif. Seluruh data harian terikat pada tahun ajaran aktif.

### 5.2 Master Kelas

- Nama kelas **bebas diedit oleh admin** (7A, VII-1, 7 Melati — menyesuaikan kebiasaan tiap sekolah)
- Kolom **tingkat**: pilihan 1–9
- Wali kelas ditugaskan per kelas per tahun ajaran

**Satu sumber data orang.** Master Guru adalah satu-satunya daftar orang di aplikasi. Penugasan wali kelas berupa kolom di Master Kelas yang **menunjuk ke baris guru**, bukan ketikan nama bebas. Akun login juga menunjuk ke baris guru yang sama.

Akibatnya: wali kelas adalah guru, tetapi guru belum tentu wali kelas. Perubahan nama, pensiun, atau mutasi cukup dilakukan di satu tempat.

Master Kelas harus diisi **sebelum** impor siswa, karena kolom KELAS divalidasi terhadapnya.

### 5.3 Master Siswa

Identitas utama: **NISN**.

Siswa yang keluar atau pindah **tidak dihapus**, cukup ditandai **tidak aktif** — berhenti muncul di daftar absensi harian, riwayatnya tetap utuh.

### 5.4 Impor Siswa dari Excel

Alur:

```
Pilih Tahun Ajaran → Pilih File Excel → Analisis File → Validasi
→ Tampilkan Error/Warning → Preview Data → Konfirmasi Operator
→ Import ke Master Siswa → Tampilkan Hasil → Registrasi RFID Siswa
```

Kolom template:

| Kolom | Contoh | Validasi |
|---|---|---|
| NISN | 0012345678 | Wajib, 10 digit, unik, **dibaca sebagai teks** |
| NAMA_LENGKAP | AHMAD FAUZI | Wajib |
| JENIS_KELAMIN | L | Wajib, hanya L atau P |
| TEMPAT_LAHIR | Makassar | Boleh kosong |
| TANGGAL_LAHIR | 2010-03-12 | Format YYYY-MM-DD |
| ALAMAT | Jl. ... | Boleh kosong |
| KELAS | 7A | Wajib, cocok dengan Master Kelas |
| NAMA_ORANG_TUA | Budi | Boleh kosong |
| NO_HP_ORANG_TUA | 08123456789 | Boleh kosong, format nomor valid |

> `NO_HP_ORANG_TUA` adalah data kontak saja. Nomor HP **tidak dapat** dipakai bot Telegram untuk mengirim pesan.

**Aturan impor ulang** — NISN menjadi kunci:

- NISN sudah ada → data diperbarui
- NISN baru → ditambahkan
- Siswa di database yang tidak ada di file → **dibiarkan, tidak dihapus**

### 5.5 Foto Siswa

- **Impor massal:** operator memilih satu folder berisi file foto dengan nama file sesuai NISN. Dijalankan **setelah** impor Excel.
- **Unggah per siswa:** untuk foto susulan dan siswa pindahan.
- Foto dikecilkan ke ±200×200 saat impor, bukan saat tampil.
- Dimuat ke memori sekali saat aplikasi dibuka.
- Siswa tanpa foto ditampilkan sebagai lingkaran inisial.

### 5.6 Kartu RFID

- **1 kartu aktif per siswa**
- Kartu RFID **merangkap sebagai kartu pelajar**
- Kelas **tidak dicetak** di kartu (agar tidak perlu cetak ulang tiap kenaikan kelas)
- Aplikasi **tidak memiliki fitur cetak kartu** — identitas dicetak sekolah di luar aplikasi
- **Kartu hilang:** siswa melapor ke wali kelas → **TU** menonaktifkan kartu lama dan mengaitkan kartu baru ke siswa yang sama → riwayat absensi tidak terputus

### 5.7 Registrasi Kartu

Alur:

```
Aktifkan Mode Registrasi → Impor data (format & kolom baku) → Reader aktif via USB
→ Tap kartu → Validasi nama lengkap + NISN → Kaitkan kartu ke siswa
→ Simpan → Mode Registrasi mati otomatis
```

Ketentuan:

- Selama mode registrasi aktif, tap **tidak tercatat sebagai absensi**
- Tombol simpan berlaku **per sesi**, berapa pun jumlah siswa dalam sesi itu
- Mode registrasi mati sendiri setelah tombol simpan ditekan

### 5.8 Kenaikan Tahun Ajaran

- **Naik kelas massal:** pilih kelas asal → kelas tujuan, dengan **seleksi per siswa** (untuk siswa yang tidak naik)
- Siswa yang naik **tidak perlu tap ulang** — tetap memakai kartu lama, cukup diubah tingkat dan kelasnya
- Registrasi kartu dilakukan ulang setiap tahun; **siswa yang lulus otomatis kartunya tidak terpakai lagi**
- Tap ulang hanya untuk siswa baru

---

## 6. Kalender Pendidikan

Diinput **manual** oleh admin (tanggal, bulan, tahun). Tidak membaca file Excel. Diulang tiap semester.

Jenis hari:

| Jenis | Absensi | Laporan |
|---|---|---|
| Hari sekolah | Jalan normal | Kolom normal |
| Libur | Tidak ada | Kolom diarsir/diblok |

- Hari kegiatan seperti **MPLS dan Sumatif tetap absensi seperti biasa**
- **Libur mendadak** ditandai dengan memblok tanggalnya, disertai **kolom keterangan/alasan**
- Begitu sebuah tanggal diblok, **tidak akan pernah ada notifikasi** masuk ke orang tua pada tanggal itu

---

## 7. Jadwal Harian

| Hari | Jam masuk | Jam rekap | Batas akhir tap masuk | Mulai absen pulang |
|---|---|---|---|---|
| Senin–Kamis, Sabtu | 07.30 | 08.00 | 12.00 | 13.10 |
| Jumat | 07.30 | 08.00 | 10.00 | 11.30 |
| Minggu | — libur — | | | |

Seluruh nilai di atas adalah **konfigurasi per sekolah**, bukan nilai tetap.

**Pergantian hari: pukul 00.01.**

---

## 8. Aturan Status Kehadiran

Status awal seluruh siswa setiap hari adalah **Alpa**. Tap kartu yang mengubahnya.

| Status | Cara terjadi |
|---|---|
| Alpa | Status awal, sebelum ada tap |
| Hadir | Tap pertama sebelum jam masuk |
| Terlambat | Tap pertama antara jam masuk dan batas akhir tap masuk |
| **Bolos** | Sudah tap masuk, tetapi **tidak tap pada jam pulang** |
| Izin / Sakit | **Hanya** lewat koreksi manual, tidak pernah dari tap |
| Pulang | Bukan status baru — mengisi kolom jam pulang |

Status **Bolos** ditetapkan **satu jam setelah jam pulang**, bagi siswa yang tercatat masuk tetapi belum melakukan tap pulang. Statusnya dapat diperbaiki lewat koreksi bila ternyata siswa memang berizin pulang lebih awal atau kartunya gagal terbaca.

### 8.1 Aturan Tap

- **Jeda minimum 15 detik** antar tap untuk kartu yang sama; tap di bawah 15 detik **selalu ditolak sebagai error**
- Tap gagal terbaca → **bunyi berbeda + pesan error di layar**
- Tap baru dihitung sebagai **pulang** hanya setelah jam mulai absen pulang; sebelum jam itu, tap berulang diabaikan
- Siswa yang **belum punya catatan masuk** dan tap di atas batas akhir tap masuk → **ditolak dengan bunyi berbeda**, harus lewat koreksi
- Siswa yang **sudah punya catatan masuk** tetap dapat tap pulang jam berapa pun

### 8.2 Data yang Dipanggil Saat Tap

Hanya **NISN, nama, dan nomor kartu RFID**. Kelas tidak dipanggil saat tap. Foto diambil dari memori yang sudah dimuat saat aplikasi dibuka.

Pengelompokan absensi per kelas dan hak akses wali kelas dibaca dari data kelas di master saat laporan dibuka.

---

## 9. Notifikasi Telegram

Modul notifikasi termasuk **paket inti**, bukan add-on.

### 9.1 Prinsip Dasar

**Notifikasi hanya terkirim bila status sudah berubah.** Siswa yang masih Alpa ditahan — tidak ada pesan "belum tercatat hadir".

### 9.2 Titik Pengiriman

| Pemicu | Waktu | Isi |
|---|---|---|
| Siswa sudah tap | Pukul 08.00 | Notifikasi Hadir |
| Tap di atas 08.00 | Real-time | Terlambat, **dengan tanggal dan jam** |
| Batas akhir tap masuk | 12.00 / 10.00 | Tidak hadir tanpa keterangan (bagi yang masih Alpa) |
| Absen pulang | — | **Tidak ada notifikasi** (jam pulang tetap dicatat) |
| Status Bolos | 1 jam setelah jam pulang (14.10 / Jumat 12.30) | Dikirim ke **wali kelas**, bukan orang tua — satu pesan berisi **daftar siswa bolos di kelasnya** |

### 9.3 Notifikasi ke Wali Kelas

Wali kelas menerima dua pesan harian di Telegram-nya, keduanya berisi daftar per kelas:

| Waktu | Isi | Gunanya |
|---|---|---|
| 08.00 | Daftar siswa kelasnya yang **belum tap** | Wali kelas sempat memastikan dan menginput izin **sebelum** notifikasi "tidak hadir tanpa keterangan" terkirim ke orang tua pada batas akhir tap masuk |
| 1 jam setelah jam pulang | Daftar siswa berstatus **Bolos** | Memastikan penyebabnya sebelum orang tua dilibatkan |

Pesan pukul 08.00 inilah yang menutup celah utama sistem: tanpa itu, wali kelas baru mengetahui siswanya tidak hadir setelah orang tua terlanjur menerima pesan.

Wali kelas hanya **menerima informasi** lewat Telegram; perubahan status tetap melalui admin sesuai bagian 3.2.

> Notifikasi bolos sengaja diarahkan ke wali kelas lebih dulu. Wali kelas memastikan penyebabnya — benar-benar pulang tanpa izin, atau sekadar kartu gagal terbaca — sebelum orang tua dilibatkan.

### 9.4 Pengaman

- **PC/aplikasi tidak menyala** → tidak ada notifikasi; jadwal yang terlewat **dibuang**, tidak dikejar
- **Toleransi 15 menit:** bila aplikasi menyala dalam 15 menit setelah jadwal rekap, rekap tetap dikirim
- **PC hidup tapi internet putus** → tap tetap tercatat; pesan masuk antrean dan dikirim saat internet normal
- **Tanggal diblok** → nol notifikasi

### 9.5 Pairing Orang Tua

Bot Telegram tidak dapat menghubungi seseorang sebelum orang itu memulai chat. Karena itu setiap siswa memiliki **QR unik**:

```
t.me/<BotSekolah>?start=<token unik siswa>
```

- Username bot diambil dari **pengaturan**, bukan ditanam di kode (tiap sekolah membuat bot sendiri)
- Token acak, sekali pakai
- **1 orang tua tertaut per siswa**
- Aplikasi dapat **mencetak QR massal per kelas**
- QR yang hilang **dapat dibuatkan kembali**

**Layar pemantau pairing:** daftar siswa tertaut / belum tertaut, dengan ringkasan per kelas — agar tidak ada orang tua yang diam-diam tidak pernah menerima notifikasi.

---

## 10. Koreksi Status

- Wali kelas boleh mengoreksi status siswa **di kelasnya saja**, termasuk **tanggal lampau**
- Koreksi terbuka selama bulan bersangkutan **belum dikunci**
- **Kunci bulan** hanya dapat ditekan oleh **admin/TU**; setelah dikunci, bulan itu beku
- Seluruh koreksi tercatat di jejak audit

---

## 11. Laporan

Keluaran: **Excel dan PDF**.

### 11.1 Laporan Mingguan

- Rentang **Senin–Sabtu**, mengikuti hari sekolah
- Memuat **kolom keterangan per kejadian**: jam keterlambatan, keterangan izin/sakit
- Dipakai wali kelas untuk memantau dan menindaklanjuti

### 11.2 Laporan Bulanan

- Satu baris per siswa, satu kolom per tanggal, sel berisi kode status
- Rekap jumlah H/S/I/A dan jumlah keterlambatan
- Kolom hari Minggu dan hari libur **diarsir/diblok**, bukan dikosongkan
- Alasan libur mendadak tercetak sebagai catatan

### 11.3 Laporan Per Siswa

- Identitas (nama, NISN, kelas, foto), ringkasan H/S/I/A, jumlah terlambat, persentase kehadiran
- Rincian hari tidak hadir dan terlambat
- Dapat dicetak **per siswa** maupun **sekaligus satu kelas**, keluaran PDF

### 11.4 Laporan Semester

Format **per siswa**, mengikuti batas semester di kalender pendidikan — bukan batas bulan, sehingga bulan awal dan akhir semester terhitung sebagian.

Isi: nama, NISN, kelas, jumlah Hadir, Sakit, Izin, Alpa, jumlah keterlambatan, dan persentase kehadiran terhadap hari efektif.

**Perhitungan hari efektif** dilakukan aplikasi sendiri dari kalender pendidikan yang diinput admin:

| Dihitung efektif | Tidak dihitung |
|---|---|
| Seluruh hari sekolah, **termasuk Sabtu** | Hari Minggu |
| Hari kegiatan (MPLS, Sumatif) | Libur nasional |
| | Libur semester |
| | Libur mendadak |

> Angka hari efektif di kalender pendidikan dinas memakai pekan 5 hari. Karena sekolah tetap masuk Sabtu, aplikasi menghitung sendiri dan angkanya akan berbeda dari dokumen dinas.

### 11.5 Perhitungan dan Kolom Keterangan

**Terlambat dihitung sebagai hadir** untuk persentase kehadiran.

**Bolos juga dihitung sebagai hadir** — siswa memang datang dan mengikuti pelajaran; yang tidak dilakukannya hanya tap pulang.

Keduanya tidak hilang begitu saja: tiap laporan menyediakan **satu kolom keterangan** yang memuat catatan terlambat dan bolos.

| Laporan | Isi kolom keterangan |
|---|---|
| Harian | Jam keterlambatan; penanda bolos |
| Mingguan | Keterangan per kejadian: "Terlambat 07.52", "Bolos", "Sakit" |
| Bulanan | Rekap jumlah: jumlah terlambat dan jumlah bolos |
| Semester / rapor | Rekap jumlah terlambat dan bolos sepanjang semester |

Dengan cara ini, persentase kehadiran tetap adil, sementara pola siswa yang sering terlambat atau sering tidak tap pulang tetap terlihat oleh wali kelas dan orang tua.

---

## 12. Jejak Audit

Mencatat setiap perubahan data yang **tidak** berasal dari tap:

| Dicatat | Isi catatan |
|---|---|
| Koreksi status, izin/sakit | Waktu |
| Penggantian kartu | Akun pelaku |
| Kunci bulan | Apa yang diubah |
| Perubahan master | Nilai lama → nilai baru |
| Pemblokiran tanggal | Kolom "atas permintaan" |
| Pembuatan akun | |

- **Tidak dapat dihapus atau diedit siapa pun**, termasuk kepala sekolah
- Disimpan **selamanya**
- Layar audit menampilkan **90 hari terakhir** secara default; selebihnya lewat filter tanggal

---

## 13. Lisensi dan Instalasi

### 13.1 Model Lisensi

- Berbayar per sekolah, **beli putus** (opsional kontrak)
- **Biaya tambahan** untuk pembaruan fitur atau update aplikasi
- **Aktivasi sekali online** saat wizard; setelah itu aplikasi berjalan offline selamanya

### 13.2 Wizard Instalasi Pertama

```
1. Aktivasi lisensi
2. Identitas sekolah (nama, NPSN, alamat, logo)
3. Tahun ajaran aktif dan semester
4. Jadwal harian per hari
5. Akun Kepala Sekolah
6. Selesai
```

Token bot Telegram **tidak** masuk wizard — diisi belakangan lewat menu Pengaturan.

### 13.3 Mekanisme Update

Versi ini memakai **update manual**. Developer mengirim berkas `.exe` baru, TU mengganti berkas lama di folder aplikasi.

Karena database dan seluruh data sekolah berada di folder terpisah (`data/`, `config/`, `backup/`), penggantian `.exe` **tidak menyentuh data sekolah sama sekali**.

Ketentuan:

- Migrasi database bernomor berjalan otomatis saat versi baru pertama kali dibuka
- Pemasangan **tidak boleh dilakukan pada jam absensi** (06.00–14.10); lakukan sore atau di luar hari sekolah
- Sebelum mengganti `.exe`, TU disarankan menyalin folder `data/` sebagai cadangan
- Nomor versi ditampilkan di layar login dan menu Pengaturan, agar mudah dipastikan saat ada keluhan

Pemberitahuan update otomatis dari server ditunda ke tahap pengembangan berikutnya.

---

## 14. UI/UX

### 14.1 Layar Absensi (Gerbang)

- Tampil di **satu layar besar (TV)**
- Fullscreen, tanpa menu, tanpa mouse
- **Foto ditampilkan** saat tap — satu-satunya pengaman terhadap titip kartu
- **Label pembeda** di atas foto: `GURU` atau `SISWA`; nama sekolah tampil di header
- Kartu guru menampilkan **nama dan NIP**; kartu siswa menampilkan **nama dan NISN**
- Beberapa tap **dapat tampil bersamaan**; tidak ada label per reader
- Warna tegas: Hadir, Terlambat, Ditolak
- Ringkasan harian: Hadir / Terlambat / Belum tap / Guru

### 14.2 Layar Admin

Menu: Dashboard, Absensi, Siswa, Guru, Kelas, Koreksi, Kalender, Laporan, Notifikasi, Audit, Pengaturan.

Header memuat **nama sekolah, logo, dan tahun ajaran aktif**. Menampilkan tahun ajaran di header berfungsi sebagai pengaman — admin langsung sadar bila masih memakai tahun ajaran lama setelah Juli.

Dashboard menjawab satu pertanyaan utama: **siapa yang belum tap hari ini**. Isinya:

- Ringkasan harian: Hadir · Terlambat · Izin/Sakit · Alpa
- Status rekap notifikasi hari itu (mis. "Rekap 08.00 terkirim · 430 pesan")
- **Peringatan pairing** (mis. "42 siswa belum tertaut Telegram") — dipasang di dashboard agar terlihat, bukan disembunyikan di layar pemantau pairing
- Daftar siswa yang belum tap, dengan kelasnya
- Tombol cepat: Koreksi · Mode gerbang

### 14.3 Menu Absensi

Bukan layar gerbang, melainkan pengendalinya:

| Bagian | Isi |
|---|---|
| Status perangkat | Jumlah reader terhubung (mis. "4 dari 4") |
| Mode aktif | Absensi atau Registrasi — ditampilkan jelas agar mode registrasi yang lupa dimatikan langsung terlihat |
| Tombol | Buka layar gerbang · Registrasi |
| Pencarian | Cari nama / NISN, saring per kelas |
| Daftar harian | Seluruh siswa hari itu beserta status, jam masuk, jam pulang |

Siswa yang belum tap tetap muncul sebagai Alpa, bukan hilang dari daftar — sesuai aturan bahwa seluruh siswa punya baris sejak awal hari.

### 14.4 Menu Siswa

Empat sub-halaman:

| Sub-halaman | Isi |
|---|---|
| Daftar | Tabel siswa dengan foto, nama, NISN, jenis kelamin |
| Impor Excel | Alur 10 langkah |
| Impor Foto | Pencocokan folder dengan NISN (lihat catatan revisi di bawah) |
| Mutasi | Menandai siswa keluar/pindah menjadi tidak aktif |

Kolom utama tabel Daftar: Nama, NISN, JK, Kelas, Kartu, lalu kolom aksi (Ubah/Unggah foto/Ganti kartu/Mutasi).

- **Kartu** — menampilkan UID kartu RFID sebenarnya kalau sudah terdaftar, atau teks "Belum terdaftar" kalau belum (direvisi 2026-09-23 — sebelumnya hanya penanda ya/tidak, lihat §16.5)

Di atas tabel ditampilkan hitungan untuk kelas yang sedang dipilih, mis. "Kelas 7A · 6 siswa belum punya kartu · 14 belum berfoto". Siswa tidak aktif tetap muncul dengan nama berwarna redup, dengan alasannya tampil inline di sebelah nama (mis. "Budi (pindah)"), tetapi tidak ikut absensi harian.

**Revisi 2026-09-23 (instruksi eksplisit pemilik proyek, audit TG/Alasan):**
- Kolom **TG** (status pairing Telegram orang tua) dihapus dari tabel Daftar — audit mengonfirmasi field `TertautTelegram` di database/`SiswaDaftar` TIDAK dihapus (masih dipakai menu Notifikasi untuk hitungan siswa yang orang tuanya sudah/belum tertaut), hanya tampilannya di panel ini yang dihapus karena dinilai jarang dipakai di layar ini dan membuat tabel padat. Status pairing Telegram tetap bisa dilihat di menu Notifikasi.
- Kolom **Alasan** (dedicated, hampir selalu kosong untuk siswa aktif) dihapus sebagai kolom terpisah — diganti teks alasan inline di sebelah nama siswa tidak aktif (lihat paragraf di atas), field `AlasanNonaktif` TIDAK dihapus dari database (tetap dipakai fitur Mutasi).
- Tombol **Impor foto** (massal, pencocokan folder-ke-NISN) dihapus dari bagian atas panel — kelas `ImporFotoSiswa` yang mengimplementasikannya TETAP ADA di kode (tidak dihapus), tapi untuk saat ini tidak ada entry point UI yang memanggilnya. Unggah foto satu-per-satu (tombol "Unggah foto" per baris siswa) tetap dipertahankan dan diperbaiki (lihat §16.4 revisi).

### 14.5 Tampilan Hasil Impor

Layar impor menampilkan:

- Kemajuan langkah: Analisis → Validasi → Preview → Impor
- Ringkasan tiga angka: **Baru · Diperbarui · Bermasalah**
- Daftar kesalahan dengan **nomor baris dan alasannya**, mis. "Baris 17 — NISN hanya 9 digit"
- Pratinjau data sebelum konfirmasi
- Tombol impor menyebut jumlahnya, mis. "Impor 812 baris"

### 14.6 Panel Detail Siswa

Di menu Absensi, **nama siswa dapat diklik** untuk memunculkan panel detail tanpa berpindah halaman. Isi panel:

| Bagian | Isi |
|---|---|
| Identitas | Foto, nama, NISN, kelas, jenis kelamin, status hari ini |
| Kartu RFID | Nomor kartu dan status aktif/nonaktif |
| Orang tua | Nama orang tua dan status pairing Telegram (tertaut / belum) |
| Rekap bulan berjalan | Jumlah Hadir, Terlambat, Sakit/Izin, Alpa |
| Riwayat absensi | Per tanggal: status, jam tap, penanda bila berasal dari koreksi manual |
| Tindakan | Cetak laporan siswa · Koreksi status |

Penanda "koreksi" pada baris riwayat membedakan data yang berasal dari tap dan yang diubah manual.

### 14.7 Ketentuan Visual

- Setiap box, toggle, dan elemen berwarna memakai **shadow/overlay** — tidak flat
- Penempatan huruf **presisi dan proporsional**: perataan, jarak, dan ukuran konsisten

---

## 15. Ketentuan Teknis

| Aspek | Ketetapan |
|---|---|
| Keluaran | Satu file `.exe` standalone, tanpa proses install |
| Database | SQLite, file tersendiri di **PC yang sama** |
| Ukuran file | Tidak jadi pertimbangan saat ini |
| Konfigurasi | Disimpan di database, diubah lewat menu Pengaturan |

**Seluruh aplikasi berjalan di satu PC.** Yang dipisah adalah *file*, bukan komputer:

```
AbsensiSekolah/
│
├── AbsensiSekolah.exe
│
├── data/
│   ├── absensi.db
│   ├── foto/
│   ├── qr/
│   └── logo-sekolah
│
├── config/
│   └── app.config
│
├── logs/
│   └── application.log
│
├── backup/
│
├── templates/
│   └── template_import_siswa.xlsx
│
└── resources/
    ├── icon
    ├── logo
    └── assets
```

Pembagian isi folder:

| Folder | Isi | Saat update |
|---|---|---|
| `data/` | Database, foto siswa dan guru, berkas QR pairing, logo sekolah | **Tidak disentuh** |
| `config/` | Jalur file, token bot, pengaturan teknis | **Tidak disentuh** |
| `logs/` | Catatan teknis aplikasi | Tidak disentuh |
| `backup/` | Berkas cadangan | **Tidak disentuh** |
| `templates/` | Template Excel impor siswa | Diperbarui |
| `resources/` | Ikon, logo aplikasi, aset bawaan | Diperbarui |

Alasan pemisahan: bila database ditanam di dalam `.exe`, maka setiap kali sekolah mengganti `.exe` dengan versi baru, seluruh data siswa dan absensi ikut tertimpa dan hilang. Dengan dipisah, update cukup mengganti satu file `.exe` — folder `data` tidak disentuh dan data sekolah tetap aman.

### 15.1 Standar Penulisan Kode

Kode ditulis dengan standar seorang software engineer profesional. Tiga standar **wajib**:

1. **Logika absensi terpisah dari UI dan database.** Seluruh aturan jam dan status berada di satu tempat yang menerima nomor kartu + waktu dan mengembalikan status.
2. **Pengujian khusus untuk penentuan status.** Kasus batas wajib diuji: tap 07.29, tap 07.31, tap kedua dalam 10 detik, tap 13.15, tap Jumat 10.30.
3. **Migrasi database bernomor** yang berjalan otomatis saat update, agar data sekolah tidak rusak saat versi baru dipasang.

---

## 16. Alur Kerja Detail

### 16.1 Instalasi Pertama

```
1. Salin folder AbsensiSekolah ke PC sekolah
2. Jalankan AbsensiSekolah.exe
3. Aplikasi mendeteksi data/absensi.db belum ada → Wizard dijalankan
   3.1  Aktivasi lisensi (sekali online)
   3.2  Identitas sekolah: nama, NPSN, alamat, logo → disimpan ke data/
   3.3  Tahun ajaran aktif dan semester
   3.4  Jadwal harian per hari (masuk, rekap, batas tap, mulai pulang)
   3.5  Buat akun Kepala Sekolah (username + password)
4. Wizard selesai → aplikasi membuat data/absensi.db dan struktur tabel
5. Kepala Sekolah login → membuat akun Admin
6. Admin login → membuat akun Wali Kelas
```

Wizard tidak dapat dijalankan ulang setelah selesai.

### 16.2 Persiapan Tahun Ajaran

```
1. Admin membuka Master Kelas
   1.1  Tambah kelas: nama bebas + tingkat (1–9)
   1.2  Tugaskan wali kelas ke tiap kelas
2. Admin membuka Kalender Pendidikan
   2.1  Input tanggal libur satu per satu atau per rentang
   2.2  Tandai hari kegiatan (MPLS, Sumatif) sebagai hari sekolah biasa
3. Admin mengunduh template lewat tombol "Unduh Template" pada layar Siswa
   atau Guru — dibuatkan aplikasi saat diminta, bukan diambil dari folder
   statis (nama berkas default: template_import_siswa.xlsx /
   template_import_guru.xlsx)
4. Operator mengisi template dari data sekolah
5. Impor siswa (lihat 16.3)
6. Impor foto (lihat 16.4)
7. Registrasi kartu (lihat 16.5)
8. Cetak QR pairing per kelas (lihat 16.6)
```

### 16.3 Impor Siswa

```
1. Pilih Tahun Ajaran
2. Pilih File Excel
3. Analisis File
   3.1  Baca header, cocokkan dengan 9 kolom baku
   3.2  Baca NISN sebagai teks (nol di depan tidak hilang)
4. Validasi per baris
   4.1  NISN wajib, 10 digit, tidak duplikat di dalam file
   4.2  NISN tidak bentrok dengan data di database
   4.3  NAMA_LENGKAP wajib
   4.4  JENIS_KELAMIN hanya L atau P
   4.5  TANGGAL_LAHIR format YYYY-MM-DD bila diisi
   4.6  KELAS harus ada di Master Kelas
   4.7  NO_HP_ORANG_TUA format nomor valid bila diisi
5. Tampilkan Error/Warning — baris bermasalah ditunjukkan nomor barisnya
6. Preview Data
7. Konfirmasi Operator
8. Import ke Master Siswa
   8.1  NISN sudah ada  → data diperbarui
   8.2  NISN baru       → ditambahkan
   8.3  Tidak ada di file → dibiarkan, tidak dihapus
9. Tampilkan Hasil: jumlah ditambah, diperbarui, gagal
```

Impor bersifat **utuh**: bila gagal di tengah, tidak ada baris yang tersimpan.

### 16.4 Impor Foto

```
1. Operator menaruh seluruh file foto dalam satu folder
   Nama file = NISN, contoh 0012345678.jpg
2. Admin memilih folder tersebut di aplikasi
3. Aplikasi mencocokkan tiap file dengan NISN di Master Siswa
4. Foto yang cocok dikecilkan ke ±200×200 lalu disimpan ke data/foto/
5. Aplikasi menampilkan daftar file yang tidak cocok
6. Foto susulan diunggah satu per satu lewat layar Data Siswa
```

Harus dijalankan **setelah** impor siswa, karena pencocokan memakai NISN.

**Revisi 2026-09-23:** langkah 1-5 (impor massal lewat folder) sementara tidak punya entry point di UI Panel Siswa — tombolnya dihapus atas instruksi eksplisit pemilik proyek (lihat §14.4). Kelas `ImporFotoSiswa` tetap ada di kode. Langkah 6 (unggah satu per satu lewat tombol "Unggah foto" per baris) tetap berfungsi penuh dan diperbaiki: pesan "Foto [nama siswa] berhasil diunggah." hanya tampil SETELAH berkas berhasil disalin/diubah ukuran DAN database berhasil diperbarui (bukan lagi ditampilkan tanpa pengecekan); kegagalan (akses ditolak, berkas terkunci, format tidak dikenali) menampilkan alasan yang jelas, bukan pesan berhasil.

### 16.5 Registrasi Kartu (Siswa)

**Direvisi 2026-09-23** — menggantikan alur antrean-otomatis versi
sebelumnya (kursor lompat sendiri ke siswa berikutnya yang belum punya
kartu). UAT nyata menemukan alur lama menghasilkan Simpan yang "berhasil"
tanpa exception tapi tidak benar-benar menulis kartu ke database
(docs/MASTER-BUG-UIUX-REGISTER.md A-31) — diganti alur eksplisit satu
siswa satu kartu per aksi, dikonfirmasi pemilik proyek. **Registrasi kartu
Guru dan penggantian kartu hilang (§16.11) TIDAK berubah** — tetap satu
kartu per aksi lewat dialog terpisah, sudah begitu sejak awal.

```
1. Admin/TU mengaktifkan Mode Registrasi
   → Seluruh tap TIDAK dicatat sebagai absensi selama mode ini aktif
2. Pilih kelas → daftar siswa aktif kelas itu tampil di dropdown terpisah
3. Operator memilih SATU siswa secara eksplisit dari dropdown itu
   → Aplikasi menampilkan nama lengkap, NISN, kelas, dan status kartu
     siswa terpilih (UID kalau sudah punya, "Belum terdaftar" kalau belum)
   → Memilih siswa lain SEBELUM Simpan membatalkan UID sementara yang
     sedang ditahan (langkah 4) — tidak ada nilai yang "menempel" ke
     siswa yang salah
4. Operator menempelkan kartu di reader
   4.1  Aplikasi membaca nomor kartu (UID), diperiksa LANGSUNG ke database
        (bukan ke memori sesi — tidak ada lagi "sesi" multi-siswa)
   4.2  UID sudah dipakai siswa lain ATAU guru? → tolak, tampilkan
        pemiliknya. UID TIDAK ditahan sebagai data sementara, tombol
        Simpan TIDAK aktif
   4.3  Siswa terpilih SUDAH punya kartu aktif? → tolak, arahkan ke
        "Ganti kartu" (§16.11) — penggantian kartu tidak termasuk alur
        registrasi pertama ini
   4.4  UID bebas dan siswa belum punya kartu → UID ditampilkan sebagai
        data SEMENTARA ("Kartu terdeteksi: ..., belum disimpan") — BUKAN
        pesan "terdaftar", karena belum tersimpan ke database
5. Operator menekan Simpan
   → Tombol hanya aktif kalau kelas, siswa, dan UID sementara semuanya
     terisi valid
   → Satu klik menghubungkan TEPAT satu siswa dengan TEPAT satu kartu,
     dalam satu transaksi yang me-RE-VERIFIKASI keunikan UID dan "satu
     siswa satu kartu" persis sebelum menulis (menutup celah race
     condition antara langkah 4 dan langkah 5)
   → Setelah transaksi commit, aplikasi QUERY ULANG database untuk
     memastikan kartu benar-benar tersimpan sebelum menampilkan pesan
     berhasil — "tidak melempar exception" sendiri tidak pernah dianggap
     cukup sebagai bukti keberhasilan
   → Panel Siswa (kalau sudah pernah dibuka sesi ini) menunjukkan kolom
     Kartu terbaru begitu dialog registrasi ditutup, tanpa restart
     aplikasi
   → Dialog TIDAK menutup diri sendiri setelah Simpan — operator memilih
     siswa berikutnya di kelas yang sama tanpa membuka ulang dialog.
     Hanya tombol Batal/X yang menutup dialog dan mematikan Mode
     Registrasi
```

Kegagalan penyimpanan (database maupun verifikasi ulang) ditampilkan
sebagai pesan galat yang jelas — TIDAK PERNAH sebagai pesan berhasil.

### 16.6 Pairing Orang Tua

```
1. Sekolah membuat bot Telegram sendiri lewat BotFather
2. Admin memasukkan token bot dan username bot di menu Pengaturan
3. Aplikasi membuat token acak sekali pakai untuk tiap siswa
4. Admin mencetak QR massal per kelas
   Isi QR: t.me/<BotSekolah>?start=<token siswa>
5. QR dibagikan ke orang tua lewat surat edaran
6. Orang tua memindai QR
   6.1  Telegram terbuka, bot menerima token
   6.2  Aplikasi mencocokkan token dengan siswa
   6.3  chat_id disimpan, status berubah menjadi "tertaut"
   6.4  Token dimatikan
7. Orang tua kedua memindai QR yang sama → ditolak, siswa sudah tertaut
8. Layar Pemantau Pairing menampilkan siapa yang belum tertaut
9. QR hilang → dibuatkan kembali (token lama mati)
```

### 16.7 Alur Harian

```
00.01  Hari berganti
       → Aplikasi membuat baris absensi untuk seluruh siswa aktif
       → Status awal seluruhnya ALPA

       Bila tanggal ini diblok di kalender → tidak ada baris dibuat,
       tidak ada notifikasi sama sekali

06.00– Siswa mulai berdatangan, tap kartu (lihat 16.8)
07.30  Batas Hadir
08.00  Rekap terkirim: notifikasi Hadir untuk siswa yang sudah tap
       Siswa yang masih ALPA → ditahan, tidak ada pesan
       → Wali kelas menerima daftar siswa kelasnya yang BELUM TAP
08.00– Tap masuk menghasilkan notifikasi real-time (Terlambat)
12.00  Batas akhir tap masuk (Jumat 10.00)
       → Siswa yang masih ALPA → notifikasi tidak hadir tanpa keterangan
13.10  Mulai absen pulang (Jumat 11.30)
       → Tap mengisi jam pulang, tanpa notifikasi
14.10  Satu jam setelah jam pulang (Jumat 12.30)
       → Siswa yang tercatat masuk tetapi belum tap pulang
         ditetapkan BOLOS
       → Satu pesan per kelas berisi daftar nama dikirim ke wali kelas
24.00  Hari berakhir
```

### 16.8 Keputusan Saat Kartu Ditap

```
1.  Baca UID kartu
2.  Mode Registrasi aktif?
      Ya → tap diproses sebagai registrasi, bukan absensi. SELESAI
3.  Tanggal hari ini diblok / libur?
      Ya → tolak, bunyi error. SELESAI
4.  UID terdaftar di salah satu master?
      Tidak → tolak, bunyi error, tampilkan "Kartu tidak terdaftar". SELESAI
5.  UID terdaftar di master mana?
      Master Guru  → jalankan aturan GURU (16.8.b)
      Master Siswa → jalankan aturan SISWA (16.8.a)
```

**16.8.a — Kartu Siswa**

```
1.  Siswa berstatus aktif?
      Tidak → tolak, bunyi error. SELESAI
2.  Jarak dari tap terakhir kartu ini < 15 detik?
      Ya → tolak, bunyi error. SELESAI
3.  Waktu sekarang >= jam mulai absen pulang?
      Ya  → 3.1  Siswa sudah punya catatan masuk?
                   Ya    → isi jam pulang. TANPA NOTIFIKASI. SELESAI
                   Tidak → tolak, bunyi error, arahkan ke koreksi. SELESAI
      Tidak → lanjut ke 4
4.  Siswa sudah punya catatan masuk hari ini?
      Ya → abaikan (tap berulang). Tidak mengubah apa pun. SELESAI
5.  Waktu sekarang < jam masuk?
      Ya → status HADIR
6.  Waktu sekarang <= batas akhir tap masuk?
      Ya    → status TERLAMBAT
      Tidak → tolak, bunyi error, arahkan ke koreksi. SELESAI
7.  Simpan status + jam tap
8.  Tampilkan di layar gerbang: label SISWA, foto, nama, NISN, status, jam
9.  Antrekan notifikasi sesuai aturan 16.9
```

**16.8.b — Kartu Guru**

```
1.  Guru berstatus aktif (belum pensiun/pindah)?
      Tidak → tolak, bunyi error. SELESAI
2.  Jarak dari tap terakhir kartu ini < 15 detik?
      Ya → tolak, bunyi error. SELESAI
3.  Status hari ini sudah diisi di muka (Sakit/Izin/Dinas Luar/Cuti)?
      Ya → tap tidak mengubah status. Tampilkan status tersebut. SELESAI
4.  Waktu sekarang >= jam mulai tap pulang (13.10 / Jumat 11.30)?
      Ya  → 4.1  Guru sudah punya catatan masuk?
                   Ya    → isi jam pulang, status menjadi HADIR. SELESAI
                   Tidak → tolak, bunyi error, arahkan ke Kepala Sekolah. SELESAI
      Tidak → lanjut ke 5
5.  Guru sudah punya catatan masuk hari ini?
      Ya → abaikan (tap berulang). SELESAI
6.  Simpan status MASUK + jam tap
7.  Tampilkan di layar gerbang: label GURU, foto, nama, NIP, status, jam
8.  Tidak ada notifikasi pada tahap ini
```

> Status guru menjadi **HADIR** hanya setelah tap pulang. Bila sampai akhir hari hanya ada tap masuk, status menjadi **Tidak lengkap** dan menunggu keputusan Kepala Sekolah.

### 16.9 Alur Notifikasi

```
1. Notifikasi dibuat HANYA bila status berubah dari ALPA
2. Tentukan waktu kirim:
     Status HADIR sebelum 08.00     → dikirim saat rekap 08.00
     Status TERLAMBAT setelah 08.00 → dikirim real-time
     Masih ALPA di batas akhir tap  → dikirim saat batas akhir tap
     Perubahan status oleh koreksi  → dikirim saat koreksi disimpan
3. Sebelum mengirim, periksa penanda "sudah terkirim"
     Belum terkirim → kirim seperti biasa
     Sudah terkirim → kirim PESAN RALAT bila status berubah karena koreksi
                      (mis. "Koreksi: Ananda tercatat Izin hari ini")
4. Periksa koneksi internet
     Terhubung  → kirim ke chat_id orang tua
     Terputus   → masuk antrean, kirim ulang saat internet normal
5. Setelah terkirim, tandai baris absensi sebagai "sudah terkirim"
6. Pengaman jadwal:
     Aplikasi menyala <= 15 menit setelah jadwal → jadwal tetap dijalankan
     Aplikasi menyala  > 15 menit setelah jadwal → jadwal DIBUANG
     Tanggal diblok                              → seluruh jadwal dibatalkan
```

### 16.10 Koreksi Status

```
1. Wali kelas menyampaikan koreksi kepada Admin
2. Admin membuka menu Koreksi, login dengan akunnya sendiri
3. Pilih kelas dan tanggal
4. Bulan tersebut sudah dikunci?
     Ya → koreksi ditolak. SELESAI
5. Ubah status siswa (Hadir / Terlambat / Izin / Sakit / Alpa)
6. Isi kolom "atas permintaan" dengan nama wali kelas
7. Simpan
     7.1  Jejak audit mencatat: waktu, akun, siswa, status lama → baru,
          atas permintaan siapa
     7.2  Bila status berubah dari ALPA dan belum pernah ada notifikasi
          → notifikasi dikirim
     7.3  Bila sudah pernah ada notifikasi → dikirim PESAN RALAT
          yang menyatakan status terbaru
```

### 16.11 Penggantian Kartu Hilang

```
1. Siswa melapor kepada wali kelas
2. Wali kelas meneruskan ke TU
3. TU membuka Data Siswa → pilih siswa
4. Nonaktifkan kartu lama (tidak dihapus)
5. Aktifkan Mode Registrasi
6. Tap kartu baru → dikaitkan ke siswa yang sama
7. Simpan → Mode Registrasi mati
8. Riwayat absensi siswa tetap utuh karena absensi menunjuk ke siswa,
   bukan ke nomor kartu
```

### 16.12 Libur Mendadak

```
1. Admin membuka Kalender Pendidikan
2. Pilih tanggal hari ini (atau tanggal lain)
3. Tandai sebagai libur → isi kolom keterangan/alasan
4. Simpan
     4.1  Seluruh jadwal notifikasi hari itu dibatalkan
     4.2  Tap pada tanggal itu ditolak
     4.3  Kolom tanggal diarsir di laporan bulanan
     4.4  Alasan tercetak sebagai catatan di laporan
5. Jejak audit mencatat siapa yang memblok dan kapan
```

### 16.13 Akhir Bulan

```
1. Wali kelas menyelesaikan koreksi bulan berjalan
2. Admin mencetak laporan bulanan per kelas (Excel dan PDF)
3. Laporan diserahkan ke Kepala Sekolah
4. Admin menekan Kunci Bulan
     → Seluruh data bulan itu beku, tidak dapat diubah siapa pun
     → Jejak audit mencatat penguncian
```

### 16.14 Kenaikan Tahun Ajaran

```
1. Admin membuat Tahun Ajaran baru dan menetapkannya aktif
2. Admin membuat Master Kelas untuk tahun ajaran baru
3. Jalankan Naik Kelas Massal
     3.1  Pilih kelas asal → kelas tujuan
     3.2  Seluruh siswa tercentang secara bawaan
     3.3  Hilangkan centang untuk siswa yang tidak naik
     3.4  Jalankan → keanggotaan kelas baru dibuat
4. Tingkat tertinggi (kelas 9) ditandai LULUS
     → Status menjadi tidak aktif
     → Kartu otomatis tidak dapat dipakai lagi
5. Siswa yang naik TIDAK perlu tap ulang — kartu lama tetap berlaku
6. Impor siswa baru (kelas 7) → registrasi kartu → pairing orang tua
```

### 16.15 Onboarding Data Guru

```
1. TU atau Kepala Sekolah membuka Master Guru
2. Impor Excel guru (kolom: NAMA, NIP, PANGKAT, JABATAN, NO_KARTU)
     2.1  Validasi: NIP wajib dan unik, NAMA wajib
     2.2  Preview → konfirmasi → impor
3. Impor foto guru dari satu folder, nama file sesuai NIP
4. Aktifkan Mode Registrasi → tap kartu tiap guru
     4.1  Pemeriksaan duplikat dilakukan terhadap master guru DAN master siswa
5. Simpan → Mode Registrasi mati
6. Tugaskan wali kelas: pada Master Kelas, pilih guru dari daftar
     (bukan mengetik nama bebas)
7. Cetak QR pairing guru → guru memindai dari HP masing-masing
```

**Revisi 2026-09-24 (Panel Guru, instruksi eksplisit pemilik proyek — mengikuti revisi Panel Siswa §14.4/§16.4):**
- Langkah 3 (impor foto guru massal dari folder): tombol **Impor foto** dihapus dari bagian atas Panel Guru. Kelas `ImporFotoGuru` TETAP ada di kode, tanpa entry point UI untuk saat ini. Foto guru diunggah satu per satu lewat tombol **Unggah foto** pada baris guru (pesan berhasil hanya setelah berkas dan database sama-sama tersimpan; kegagalan menampilkan alasan jelas).
- Tabel Data Guru: kolom **Nama, NIP, Kartu, Status**, lalu aksi (Ubah · Unggah foto · Daftarkan kartu/Ganti kartu · Nonaktifkan). **Kartu** menampilkan UID RFID aktif sebenarnya atau "Belum terdaftar" (bukan ya/tidak). **Status** menampilkan "Aktif", atau alasan tidak aktif ("Pensiun"/"Pindah"); guru tidak aktif tampil dengan nama redup dan hanya punya aksi Ubah dan Unggah foto.
- Kolom **TG** (status pairing Telegram guru) dihapus dari tabel Data Guru. Field `TertautTelegram` TIDAK dihapus — masih dipakai menu Notifikasi (pairing guru, langkah 7 di atas); status pairing tetap dilihat di sana.
- Formulir tambah/ubah guru dipindah dari panel kanan ke bilah di atas tabel agar tabel memakai seluruh lebar panel (tanpa scroll horizontal di 1366×768). NIP hanya bisa diisi saat menambah guru; setelah tersimpan NIP hanya-baca (NIP kembar ditolak dengan pesan di formulir).

### 16.16 Alur Harian Guru

```
Pagi    Guru tap → status MASUK, jam tap dicatat
        Tidak ada notifikasi

12.00   Batas akhir tap masuk (Jumat 10.00)
        Guru yang belum tap sama sekali dan tanpa kabar
        → notifikasi dikirim ke Telegram guru itu sendiri
        Guru berstatus Sakit/Izin/Dinas Luar/Cuti → tidak dikirim

13.10   Mulai tap pulang (Jumat 11.30)
        Guru tap kedua → status menjadi HADIR

24.00   Hari berakhir
        Guru dengan tap masuk saja → status TIDAK LENGKAP
        → menunggu keputusan Kepala Sekolah
```

### 16.17 Penetapan Status Guru oleh Kepala Sekolah

```
1. Kepala Sekolah membuka menu Guru → Kehadiran
2. Pilih tanggal
3. Dua arah pencatatan:
     3.1  DI MUKA  — tandai Izin / Dinas Luar / Cuti untuk tanggal mendatang
                     → saat hari itu tiba, status sudah benar tanpa perlu tap
     3.2  SETELAH  — ubah status Alpa atau Tidak lengkap menjadi kategori lain
                     setelah laporan datang
4. Isi kolom keterangan
5. Simpan → jejak audit mencatat waktu, akun, status lama → baru
```

---

## 17. Modul Absensi Guru

### 17.1 Master Guru

| Kolom | Keterangan |
|---|---|
| NAMA | Wajib |
| NIP | Wajib, identitas utama guru |
| PANGKAT | |
| JABATAN | |
| NO_KARTU | Nomor unik kartu RFID |
| FOTO | Diimpor terpisah, bukan lewat Excel |

Dikelola oleh **TU dan Kepala Sekolah**.

### 17.2 Impor Data Guru

Mengikuti model yang sama persis dengan impor siswa: pilih file Excel → analisis → validasi → tampilkan error → preview → konfirmasi → impor → tampilkan hasil.

Foto guru diimpor dari satu folder dengan **nama file sesuai NIP**, sama seperti foto siswa yang memakai NISN.

### 17.3 Aturan Status Guru

Guru memerlukan **dua tap** untuk dihitung hadir:

```
Tap jam masuk   → status MASUK
Tap jam pulang  → status HADIR
```

| Kondisi | Status |
|---|---|
| Tidak tap sama sekali | Alpa |
| Tap masuk saja, tidak tap pulang | **Tidak lengkap** — menunggu keputusan Kepala Sekolah |
| Tap masuk dan tap pulang | Hadir |

Jam mulai tap pulang guru **sama dengan siswa**: 13.10, dan 11.30 pada hari Jumat.

### 17.4 Kategori Status

Hadir · Masuk · Tidak lengkap · Alpa · Sakit · Izin · Dinas Luar · Cuti

Setiap status disertai **kolom keterangan**.

Sakit, Izin, Dinas Luar, dan Cuti tidak pernah berasal dari tap — hanya diinput manual.

### 17.5 Dua Arah Pencatatan

- **Di muka:** Kepala Sekolah menandai izin, dinas luar, atau cuti sebelum hari pelaksanaan. Saat hari itu tiba, status sudah benar tanpa perlu tap.
- **Setelah:** Kepala Sekolah mengubah status Alpa menjadi kategori lain bila laporan datang belakangan.

Seluruh perubahan tercatat di jejak audit.

### 17.6 Notifikasi Guru

Dikirim ke **akun Telegram guru itu sendiri**.

| Kondisi | Notifikasi |
|---|---|
| Tidak tap sama sekali dan tanpa kabar | **Dikirim** pada batas akhir tap masuk (12.00 / Jumat 10.00) |
| Tap masuk saja | Tidak dikirim |
| Hadir lengkap | Tidak dikirim |
| Sudah berstatus Sakit/Izin/Dinas Luar/Cuti | Tidak dikirim |

Notifikasi berfungsi sebagai pengingat, bukan laporan harian — sehingga tidak diabaikan guru.

Pairing guru memakai mekanisme QR yang sama dengan orang tua, namun jauh lebih ringan karena jumlahnya sedikit dan guru melakukannya sendiri.

### 17.7 Siklus Hidup Data Guru

- Guru **tidak ikut registrasi kartu ulang setiap tahun**
- Hanya guru baru yang didaftarkan kartunya
- Tersedia fitur **edit** untuk guru yang pensiun, pindah, atau berganti tugas wali kelas
- **Kartu guru merangkap kartu pegawai**
- **Pairing Telegram guru** memakai alur yang sama dengan pairing orang tua siswa: QR unik per guru, dipindai sekali dari HP masing-masing

### 17.8 Laporan Guru

Mengikuti format laporan siswa, dengan tambahan kolom **Dinas Luar** dan **Cuti**. Keluaran Excel dan PDF.

### 17.9 Reader Bersama dan Keunikan Kartu

Guru memakai **reader yang sama** dengan siswa di gerbang. Aplikasi membedakan keduanya dari nomor kartu: kartu guru terdaftar di master guru, kartu siswa di master siswa.

**Nomor kartu wajib unik lintas master.** Kartu yang sudah terdaftar di master siswa tidak boleh dipakai di master guru, dan sebaliknya. Pemeriksaan duplikat saat registrasi dilakukan terhadap **kedua master**, bukan hanya master yang sedang dibuka.

### 17.10 Hak Akses Modul Guru

| Tindakan | TU / Admin | Kepala Sekolah | Wali Kelas |
|---|---|---|---|
| Kelola master guru | Ya | Ya | Tidak |
| Registrasi kartu guru | Ya | Ya | Tidak |
| Ubah status kehadiran guru | Tidak | **Ya** | Tidak |
| Lihat laporan guru | Ya | Ya | Tidak |

---

## 18. Backup dan Restore

### 18.1 Kapan Backup Dijalankan

Dua titik, keduanya jatuh pada waktu PC pasti menyala:

| Titik | Waktu | Isi |
|---|---|---|
| Pembukaan aplikasi | Setiap aplikasi dibuka | Data hari sebelumnya yang sudah final |
| Setelah batas akhir tap masuk | 12.00 / Jumat 10.00 | Data hari berjalan yang praktis sudah final |

Tidak memakai jadwal jam tetap, karena PC sekolah tidak menyala 24 jam. Backup berjalan di latar belakang sehingga operator tidak menunggu.

### 18.2 Penyimpanan

- **Lokal:** folder `backup/`, menyimpan **7 berkas harian terakhir**
- **Cloud:** server developer untuk tahap awal, kemudian pindah ke cloud milik sekolah
- Berkas **dienkripsi** sebelum diunggah, karena memuat data ribuan siswa
- Retensi cloud dibatasi agar biaya penyimpanan tidak tumbuh tanpa batas

### 18.3 Restore

Dua jalur sesuai penyebabnya:

| Jalur | Untuk kasus | Cara |
|---|---|---|
| File lokal | Database rusak, salah hapus | Admin memilih berkas dari folder `backup/` |
| Cloud | PC rusak total atau diganti | Pasang aplikasi di PC baru → masukkan kunci lisensi → ambil backup terakhir |

Kunci lisensi sekolah menjadi penanda backup mana yang miliknya.

### 18.4 Pengaman Restore

1. Sebelum restore berjalan, aplikasi **membuat backup kondisi saat ini** — agar restore yang salah berkas masih bisa dibatalkan
2. Restore hanya dapat dilakukan akun berwenang penuh, dan **tercatat di jejak audit**
3. Aplikasi menampilkan **tanggal dan jumlah data** di berkas backup sebelum tombol ditekan, sehingga admin tahu berapa hari data yang akan hilang

---

## 19. Poin Terbuka

Seluruh poin utama telah ditetapkan. Yang masih dapat berubah seiring pengembangan:

| No | Poin | Status |
|---|---|---|
| 1 | Pemindahan backup dari server developer ke cloud milik sekolah | Arah sudah ditetapkan, waktu pelaksanaan belum |
| 2 | Akses wali kelas lewat HP | Direncanakan untuk versi berikutnya |

---

## 20. Di Luar Lingkup Versi Ini

Direncanakan untuk versi berikutnya, sebagian menjadi dasar update berbayar:

- **Akses wali kelas lewat HP** atau versi desktop di HP
- **Pemberitahuan update otomatis dari server** — versi ini masih update manual
- Notifikasi via WhatsApp (ditolak untuk versi ini karena biaya ±Rp7 juta/bulan untuk 800 siswa)
- Migrasi ke database jaringan bila akses multi-perangkat diperlukan
- Optimasi ukuran file `.exe`

---

## 21. Revisi 2026-11-03 — Perubahan Arsitektur: Standalone → Online

**Sifat revisi:** perubahan arah produk atas keputusan eksplisit pemilik proyek (2026-11-03). Bagian ini
**baru mencatat keputusan dan dampaknya**; rancangan teknis online belum dibuat. Tidak ada kode yang diubah oleh
revisi ini. Daftar "Keputusan terbuka" (21.3) **wajib** dijawab pemilik sebelum rancangan dan kode dimulai —
kebutuhan tidak ditebak.

### 21.1 Keputusan
1. Aplikasi tidak lagi dirancang sebagai aplikasi desktop **standalone** (satu PC, database SQLite lokal, offline-first).
2. Aplikasi akan beroperasi sebagai sistem **online**.

### 21.2 Dampak pada isi PRD ini (hasil telaah dokumen; pengganti belum diputuskan)
| Bagian | Isi versi 0.9 | Status setelah revisi |
|---|---|---|
| §1 Ringkasan | "desktop standalone", 1 instalasi per sekolah, offline-first, SQLite | **Berubah** — menunggu 21.3 |
| §3, §3.1 Hak akses & hierarki akun | Akun lokal di database lokal | Perlu ditinjau: identitas/sesi/akun lintas perangkat |
| §9 Notifikasi Telegram, §9.4 Pengaman | Outbox lokal, "internet putus → antre" | Perlu ditinjau: pengirim di server atau tetap di klien |
| §12 Jejak audit | Tabel audit lokal, tak bisa diubah/dihapus (trigger SQLite) | Perlu ditinjau: audit terpusat di server |
| §13 Lisensi & instalasi | "Aktivasi sekali online, setelah itu offline selamanya"; wizard instalasi; update manual | **Berubah** — lisensi dan pembaruan bisa diperiksa terus-menerus |
| §14.1 Layar Gerbang | Tap RFID diproses lokal seketika | **Risiko tertinggi**: perilaku saat koneksi putus harus ditentukan (21.3 butir 3) |
| §15 Ketentuan teknis | `.exe` tanpa install, SQLite di PC yang sama, folder `data/ config/ logs/ backup/` | **Berubah** |
| §16.1 Instalasi pertama | Wizard 6 langkah di PC sekolah | **Berubah** |
| §18 Backup & Restore | Backup lokal + "cloud" (saat ini folder lokal) + restore dari berkas | **Berubah** — backup menjadi tanggung jawab server |
| §19 Poin terbuka #1, #2 | Pemindahan backup ke cloud sekolah; akses wali kelas lewat HP | Sebagian terjawab/terambil oleh arsitektur online |
| §20 Di luar lingkup | Akses HP wali kelas, database jaringan multi-perangkat, update otomatis | Butir-butir ini **menjadi relevan** — tinjau ulang |

Tidak terdampak secara konsep (aturan bisnis tetap): aturan status kehadiran (§8, §16.8), kalender (§6),
jadwal (§7), master data (§5), koreksi (§10), laporan (§11), modul guru (§17). Yang berubah adalah *tempat* aturan
ini dijalankan dan disimpan, bukan aturannya.

### 21.3 Keputusan terbuka (perlu jawaban pemilik sebelum rancangan)
1. **Bentuk "online":** (a) server pusat + klien desktop WPF yang sudah ada tersambung ke server; (b) aplikasi web
   (browser); (c) gabungan (gerbang desktop + admin/wali kelas lewat web/HP)?
2. **Model sewa/hosting:** satu server untuk banyak sekolah (multi-tenant) atau satu server per sekolah? Di mana
   dihosting dan siapa yang membayar/mengelola? Dampaknya ke model lisensi §13.1 (beli putus vs langganan).
3. **Perilaku Layar Gerbang saat internet putus:** tap tetap diterima dan disinkronkan nanti (butuh antrean/buffer
   lokal; status kehadiran dihitung di klien atau di server?) atau tap ditolak/ditunda sampai online? Ini menentukan
   apakah aturan §16.8 dijalankan di klien, di server, atau keduanya.
4. **Sumber waktu:** server atau jam PC gerbang sebagai acuan keterlambatan?
5. **Database:** SQLite diganti (mis. PostgreSQL)? Bagaimana nasib data dan instalasi versi 0.9 yang sudah ada
   (migrasi satu kali)?
6. **Notifikasi Telegram:** dikirim dari server — token bot per sekolah atau satu bot untuk semua?
7. **Akun & keamanan:** login lewat internet berarti butuh kebijakan sandi, pembatasan percobaan masuk, sesi/token,
   HTTPS, dan perlindungan data pribadi siswa (NISN/NIK/alamat/nomor orang tua) di server, termasuk kepatuhan
   (izin orang tua, lokasi data).
8. **Audit (§12):** diteruskan sebagai audit terpusat tak-ubah di server?
9. **Backup/restore (§18):** menjadi layanan server; apakah fitur restore dari berkas di klien masih diperlukan?
10. **Perangkat RFID & akses wali kelas lewat HP (§19 #2, §20):** masuk lingkup rilis online pertama atau menyusul?
11. **Urutan pengerjaan:** apakah versi standalone 0.9 yang sedang UAT diselesaikan dan dirilis dulu, atau pengembangan
    dialihkan penuh ke arsitektur online sekarang?

### 21.4 Dampak ke pekerjaan yang sedang berjalan (catatan, bukan keputusan)
- Rancangan dan kode yang sudah ada (Domain murni, aturan status, laporan) dipertahankan sebisa mungkin; lapisan
  `Absensi.Data` (SQLite lokal, `JalurPerubahanData`, trigger audit, backup/restore, lisensi lokal), penjadwal lokal,
  dan pengirim outbox adalah bagian yang paling terdampak.
- Utang teknis yang terkait arsitektur lama (lihat `PROGRES-DAN-UTANG.md`) perlu dinilai ulang: sebagian
  (backup lokal tak terenkripsi, lisensi stub, "cloud" folder lokal, Bolos saat PC mati) bisa menjadi tidak
  relevan atau berubah bentuk.
- Dokumen turunan yang harus mengikuti begitu 21.3 diputuskan: `CLAUDE.md`, `RENCANA-TUGAS.md`, `SKEMA-DATABASE.md`,
  `PANDUAN-PEMASANGAN.md`, `PANDUAN-TESTER.md`, `MASTER-BUG-UIUX-REGISTER.md`.

### 21.5 Riwayat revisi
| Tanggal | Perubahan | Sumber |
|---|---|---|
| 2026-11-03 | Dicatat keputusan standalone → online; dibuat §21 (dampak + keputusan terbuka). Belum ada rancangan teknis. | Instruksi pemilik proyek |

---

*Dokumen ini belum final dan masih terbuka untuk perubahan.*
