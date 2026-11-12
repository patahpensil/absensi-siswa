# Panduan Pemasangan Aplikasi Absensi

Panduan ini untuk Tata Usaha (TU) sekolah. Tidak perlu latar belakang komputer — ikuti saja urutannya.

---

## 1. Menyalin Aplikasi ke Komputer Sekolah

1. Salin seluruh folder **AbsensiSekolah** (isinya `Absensi.App.exe` dan beberapa file pendukung) dari flashdisk/link yang diberikan developer ke komputer sekolah.
2. Taruh di lokasi yang mudah diingat, misalnya `D:\AbsensiSekolah`.
3. **Jangan** memindahkan hanya file `.exe`-nya saja — seluruh isi folder harus ikut disalin bersamaan, aplikasi tidak akan berjalan bila file pendukungnya tertinggal.

`[GAMBAR: folder AbsensiSekolah disalin ke Windows Explorer]`

> **Catatan:** komputer sekolah **tidak perlu** menginstal apa pun terlebih dahulu (tidak perlu instal .NET, tidak perlu instal apa pun dari internet). Paket uji Windows x64 sudah membawa runtime .NET 10. Cocokkan commit pada `BUILD.txt` dan SHA-256 paket dengan hasil CI sebelum pengujian.

---

## 2. Menjalankan Aplikasi Pertama Kali (Wizard)

1. Buka folder AbsensiSekolah, klik dua kali `Absensi.App.exe`.
2. Karena ini pertama kali dijalankan, layar **Instalasi Pertama** akan muncul. Layar ini hanya muncul SEKALI saja — setelah selesai, layar ini tidak akan muncul lagi.
3. Ikuti langkahnya satu per satu:
   - **Langkah 1 — Lisensi**: masukkan kunci lisensi yang diberikan developer. Pilih **"Instalasi Baru"** (pilih **"Pulihkan dari PC Lama"** hanya bila ini pemasangan ulang di komputer pengganti — lihat bagian 7).
   - **Langkah 2 — Sekolah**: isi nama sekolah, NPSN, alamat, dan logo (opsional).
   - **Langkah 3 — Tahun Ajaran**: isi nama tahun ajaran (contoh `2026/2027`), semester, dan tanggal mulai/selesai.
   - **Langkah 4 — Jadwal Harian**: isi jam masuk, jam daftar belum tap (wali kelas), jam rekap orang tua, batas akhir tap, dan jam mulai pulang untuk tiap hari, dengan urutan jam masuk < rekap < batas akhir ≤ mulai pulang. Kosongkan jam pada hari yang libur (contoh: Minggu). Setelah instalasi, perubahan jadwal di Pengaturan berlaku mulai tanggal yang dipilih (paling cepat besok); jadwal untuk hari yang sudah lewat tidak berubah.
   - **Langkah 5 — Akun Kepala Sekolah**: buat username dan sandi untuk Kepala Sekolah. **Catat baik-baik.**
   - **Langkah 6 — Selesai**: klik **"Buka Halaman Masuk"**.

`[GAMBAR: layar Instalasi Pertama, langkah 1]`

4. Setelah wizard selesai, layar **Masuk** akan tampil. Masuk dengan akun Kepala Sekolah yang baru dibuat.

`[GAMBAR: layar Masuk]`

5. **Kepala Sekolah login → buat akun Admin:**
   - Buka menu **Pengaturan** → tab **Akun**.
   - Klik **"Tambah Akun"**.
   - Pilih peran **Admin**, isi username dan sandi (guru terkait boleh dikosongkan untuk Admin).
   - Klik **Simpan**.

`[GAMBAR: layar Pengaturan → Akun, tombol Tambah Akun]`

6. **Admin login → buat akun Wali Kelas** (untuk tiap kelas):
   - Buka menu **Pengaturan** → tab **Akun** → **"Tambah Akun"**.
   - Pilih peran **Wali Kelas**, isi username dan sandi, lalu **pilih guru yang bersangkutan** dari daftar (wajib untuk Wali Kelas — pastikan data guru sudah diimpor lebih dulu, lihat bagian 4).
   - Klik **Simpan**.

> **Catatan:** Admin hanya bisa membuat akun Wali Kelas, tidak bisa membuat Admin lain — hanya Kepala Sekolah yang bisa membuat akun Admin. Akun bisa dinonaktifkan (bukan dihapus) dan sandinya bisa direset dari layar yang sama.

---

## 3. Memasukkan Token Bot Telegram

Notifikasi ke orang tua dan guru dikirim lewat bot Telegram milik sekolah sendiri (bukan milik developer).

1. Minta seseorang (TU/Admin) membuat bot Telegram lewat aplikasi Telegram, cari akun **@BotFather**, ikuti instruksinya untuk mendapatkan **token bot**.
2. Buka aplikasi Absensi → menu **Pengaturan**.
3. Isi kolom **Token bot** dan **Username bot** (tanpa tanda `@`).
4. Klik **"Simpan Bot Telegram"**.

`[GAMBAR: layar Pengaturan, bagian Bot Telegram]`

> Notifikasi baru bisa terkirim setelah langkah ini selesai. Sebelum ini diisi, aplikasi tetap berjalan normal untuk absensi — hanya notifikasinya yang belum jalan.

---

## 4. Mengimpor Data Siswa dan Guru

### Siapkan berkas Excel dulu

Cara termudah: klik **"Unduh Template"** di layar **Siswa** atau **Guru**, lalu isi data sekolah ke dalamnya **tanpa mengubah nama atau urutan kolom header**. Setelah terisi, lanjut ke bagian "Impor lewat aplikasi" di bawah.

Kalau membuat berkas sendiri dari nol, buat berkas Excel (`.xlsx`) dengan kolom **persis** seperti ini (huruf besar semua, urutan sesuai):

**Untuk siswa** (9 kolom):
`NISN | NAMA_LENGKAP | JENIS_KELAMIN | TEMPAT_LAHIR | TANGGAL_LAHIR | ALAMAT | KELAS | NAMA_ORANG_TUA | NO_HP_ORANG_TUA`

**Untuk guru** (4 kolom):
`NAMA | NIP | PANGKAT | JABATAN`

Catatan pengisian:
- NISN dan NIP ditulis apa adanya, termasuk angka 0 di depan (jangan sampai Excel menghilangkannya — atur format kolom sebagai **Teks**, bukan Angka).
- JENIS_KELAMIN diisi `L` atau `P` saja.
- TANGGAL_LAHIR format `2015-03-17` (tahun-bulan-tanggal), boleh dikosongkan.
- Kolom KELAS harus sudah ada di menu **Kelas** sebelum impor (buat kelasnya dulu bila belum ada).

### Impor lewat aplikasi

1. Buka menu **Siswa** (atau **Guru**).
2. Klik **"Impor Excel"**.
3. Pilih berkas Excel yang sudah disiapkan.
4. Aplikasi akan memeriksa isinya dan menunjukkan baris mana yang bermasalah (kalau ada) beserta alasannya — perbaiki di Excel, lalu impor ulang.
5. Bila semua sudah benar, klik tombol impor untuk menyimpan.
6. (Fitur "Impor foto" massal sudah dihapus dari layar, A-35; foto per orang diunggah lewat form Siswa/Guru. Entry point impor foto massal belum ada — lihat PROGRES-DAN-UTANG.md.)

`[GAMBAR: layar Siswa, tombol Impor Excel]`

---

## 5. Cara Backup Manual

Aplikasi ini sudah membuat backup **otomatis** setiap kali dibuka dan setelah batas akhir tap masuk — TU tidak perlu melakukan apa pun untuk itu.

Untuk cadangan tambahan (misalnya sebelum mengganti komputer), TU bisa membackup manual:

1. Tutup aplikasi Absensi terlebih dahulu.
2. Buka folder AbsensiSekolah.
3. Salin (copy-paste) folder **`data`** ke tempat lain yang aman — flashdisk, hard disk eksternal, atau folder cloud (Google Drive/OneDrive).
4. Beri nama folder salinannya dengan tanggal, misalnya `data-2026-09-18`, supaya mudah dicari kalau dibutuhkan.

`[GAMBAR: folder data disalin]`

> Folder `backup` di dalam folder AbsensiSekolah juga menyimpan 7 backup harian terakhir secara otomatis — folder ini boleh ikut disalin juga sebagai cadangan tambahan.
>
> Sejak 12 November 2026, berkas di folder `backup` dienkripsi dengan kunci lisensi sekolah (bila lisensi sudah aktif). Berkas itu hanya bisa dipulihkan oleh aplikasi dengan kunci lisensi yang sama — **simpan kunci lisensi di tempat aman, terpisah dari PC**. Salinan folder `data` hasil langkah manual di atas TIDAK terenkripsi; simpan di tempat yang hanya bisa diakses orang berwenang karena berisi data pribadi siswa.
>
> Bila PC mati sebelum jam finalisasi (mis. 14.10), status Bolos siswa dan Tidak lengkap guru untuk hari itu ditetapkan otomatis saat aplikasi dibuka lagi; notifikasi yang terlewat tidak dikirim.

---

## 6. Cara Update ke Versi Baru

1. Tutup aplikasi Absensi.
2. Developer akan mengirim `Absensi.App.exe` versi baru (dan file pendukungnya bila ada perubahan).
3. Ganti **hanya** file-file yang dikirim developer di folder AbsensiSekolah.
4. **JANGAN** sentuh, hapus, atau timpa folder **`data`**, **`backup`**, atau **`config`** — di situlah seluruh data sekolah tersimpan.
5. Buka aplikasi seperti biasa. Struktur data akan diperbarui otomatis bila diperlukan.

`[GAMBAR: mengganti file .exe, folder data tidak disentuh]`

> Jangan memasang versi baru pada jam sekolah aktif (06.00–14.10) — lakukan sore hari atau saat libur, supaya tidak mengganggu jam absensi.

---

## 7. Cara Restore Kalau Komputer Rusak

Bila komputer sekolah rusak total atau harus diganti:

1. Pasang aplikasi di komputer baru (ulangi bagian 1).
2. Jalankan `Absensi.App.exe` — layar **Instalasi Pertama** akan muncul lagi (karena komputer baru belum punya data sama sekali).
3. Di **Langkah 1 — Lisensi**, masukkan kunci lisensi sekolah yang sama seperti sebelumnya.
4. Pilih **"Pulihkan dari PC Lama"**.
5. Aplikasi akan mengambil backup terakhir dari penyimpanan cadangan sekunder (SAAT INI masih folder lokal di PC, bukan server di luar PC — A-43) secara otomatis — seluruh data sekolah (siswa, guru, absensi, pengaturan) akan kembali seperti semula tanpa perlu mengisi ulang wizard dari awal.

`[GAMBAR: layar Instalasi Pertama, tombol Pulihkan dari PC Lama]`

> Jalur ini hanya berhasil bila folder cadangan sekunder dari komputer lama masih bisa diakses (belum ada penyimpanan di luar PC). Bila ragu, hubungi developer terlebih dahulu sebelum memasang di komputer baru.

---

## 8. Butuh Bantuan?

Bila ada masalah, kendala, atau pertanyaan yang tidak terjawab di panduan ini, hubungi developer:

- **Nama:** `[ISI NAMA DEVELOPER]`
- **Telepon/WhatsApp:** `[ISI NOMOR]`
- **Email:** `[ISI EMAIL]`

Saat menghubungi, sebutkan:
1. Apa yang sedang dilakukan saat masalah muncul
2. Pesan yang tampil di layar (boleh difoto)
3. Nomor versi aplikasi (tampil di layar Masuk, pojok bawah)

---

*Panduan ini dibuat untuk versi awal aplikasi. Simpan berkas ini untuk referensi TU berikutnya.*

## Uji reader dan TV pada Windows

Reader USB dalam mode keyboard menerima tap ketika **Layar Gerbang** aktif. Saat fokus berpindah, layar menampilkan status dijeda. Kembali ke Gerbang sebelum melanjutkan tap. Pada dua monitor, gunakan `Win+Shift+←/→` saat Gerbang aktif untuk memindahkannya ke TV.

Untuk menerima tap ketika operator memakai Dashboard, gunakan reader yang menyediakan mode **USB serial/COM** dan nonaktifkan mode keyboard pada reader tersebut. Periksa dukungannya di manual perangkat. Di folder `data` yang berada di samping `Absensi.App.exe`, buat `rfid-serial.json`, misalnya `{"ports":["COM3","COM4"],"baudRate":9600}`. Gunakan port dan baud rate dari perangkat. Mode ini menerima 1–4 port berbeda, 8N1, UID alfanumerik 8–64 karakter, dan terminator CR/LF. Gunakan reader keyboard terpisah untuk formulir registrasi jika diperlukan. Periksa status siap saat Gerbang dibuka. Setelah gangguan sambungan, buka ulang Gerbang setelah port tersedia.

Arsip riwayat audit yang dibuat saat pemulihan backup disimpan di `backup/arsip-audit`. Bila lisensi aktif, arsip dienkripsi dengan kunci lisensi sekolah, sama seperti backup lokal, sehingga tetap bisa dibaca setelah aplikasi dipindah ke PC lain atau Windows dipasang ulang selama lisensinya sama. Tidak ada file kunci terpisah yang perlu dijaga. Arsip lama berbentuk database tetap terbaca.
