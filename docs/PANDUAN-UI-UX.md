# Panduan UI/UX — Aplikasi Absensi Siswa

**Dokumen pendamping PRD v0.9**
**Tanggal:** 12 September 2026
**Status:** draf, terbuka untuk perubahan

---

## 1. Siapa yang Memakai dan Dalam Keadaan Apa

Keputusan desain di dokumen ini diturunkan dari tiga keadaan nyata, bukan dari selera:

| Pengguna | Keadaan | Akibat pada desain |
|---|---|---|
| Operator TU | Pagi hari, 4 antrean siswa, layar TV dilihat dari 3–5 meter | Huruf besar, kontras tinggi, warna sebagai penanda utama |
| Wali kelas | Sela mengajar, terburu-buru, kadang tidak terbiasa komputer | Sedikit pilihan, label jelas, tidak ada istilah teknis |
| Kepala sekolah | Sesekali, ingin angka dan laporan | Ringkasan di depan, rincian di belakang |

PC sekolah umumnya kelas menengah ke bawah dengan layar 1366×768. **Antarmuka harus utuh pada lebar 1280 piksel.**

---

## 2. Arah Visual

Aplikasi ini dipakai di sekolah negeri, dilihat guru dan kepala sekolah, dan hasil cetaknya masuk berkas dinas. Arah visualnya diambil dari benda-benda yang memang ada di sekolah: seragam biru tua, papan pengumuman, kertas laporan, dan stempel merah.

Tiga prinsip:

**Tenang, bukan meriah.** Aplikasi yang dipakai setiap pagi selama bertahun-tahun tidak boleh melelahkan. Warna kuat hanya untuk status, sisanya netral.

**Warna adalah data.** Hijau, kuning, dan merah tidak pernah dipakai sebagai hiasan — hanya untuk Hadir, Terlambat, dan Alpa. Begitu warna dipakai untuk dekorasi, artinya hilang.

**Satu hal menonjol per layar.** Di layar gerbang: nama dan foto. Di dashboard: siapa yang belum tap. Di laporan: angka persentase. Sisanya tenang.

---

## 3. Warna

### 3.1 Palet Dasar

| Nama | Hex | Dipakai untuk |
|---|---|---|
| Biru seragam | `#1B3A6B` | Warna utama: header, tombol utama, menu aktif |
| Biru muda | `#E7EDF6` | Latar menu aktif, latar sorotan |
| Tinta | `#14181F` | Teks utama |
| Abu tulis | `#5B6472` | Teks sekunder, label |
| Kertas | `#F5F6F7` | Latar halaman |
| Putih | `#FFFFFF` | Latar kartu dan tabel |
| Garis | `#DFE3E8` | Pembatas, bingkai kolom |
| Bahaya | `#9B2C2C` | Tombol aksi berisiko (hapus, kunci bulan, pulihkan backup) |

Biru tua dipilih karena itu warna seragam SMP — bukan biru korporat yang bisa dipakai aplikasi apa pun.

### 3.2 Warna Status

Setiap status punya tiga nilai: teks, latar, dan garis.

| Status | Teks | Latar | Garis |
|---|---|---|---|
| Hadir | `#166534` | `#E8F5EC` | `#B7DFC4` |
| Terlambat | `#92610D` | `#FDF3E0` | `#EBD39B` |
| Izin / Sakit | `#1B3A6B` | `#E7EDF6` | `#BFD0E6` |
| Alpa / Ditolak | `#9B2C2C` | `#FDEDED` | `#F0C0C0` |
| Tidak lengkap | `#5B6472` | `#EFF1F3` | `#DFE3E8` |

Seluruh pasangan teks-latar di atas melewati rasio kontras 4.5:1.

### 3.3 Aturan Pemakaian

- Jangan memakai warna status untuk tombol, tautan, atau hiasan. Satu-satunya pengecualian: warna Bahaya (§3.1) sama dengan teks Alpa dan hanya dipakai untuk tombol aksi berisiko (keputusan pemilik 12 November 2026)
- Jangan mengandalkan warna saja: selalu ada teks status di sebelahnya, karena sebagian guru buta warna
- Latar halaman selalu Kertas, kartu selalu Putih — perbedaan tipis ini yang membuat kartu terlihat terangkat tanpa bingkai tebal

---

## 4. Huruf

### 4.1 Keluarga

Satu keluarga untuk seluruh aplikasi: **IBM Plex Sans**, dibundel bersama `.exe` agar tampil sama di PC mana pun.

Alasan: angka tabularnya sejajar (penting untuk kolom jam dan rekap), bentuk hurufnya jelas pada ukuran kecil, dan mendukung tanda baca Indonesia dengan baik.

Untuk angka di tabel dan laporan, aktifkan **tabular figures** agar kolom jam tidak bergoyang.

### 4.2 Skala

Dasar 14 px, rasio 1.25:

| Peran | Ukuran | Tebal | Tinggi baris |
|---|---|---|---|
| Judul halaman | 22 px | 600 | 1.3 |
| Judul bagian | 18 px | 600 | 1.35 |
| Teks utama | 14 px | 400 | 1.5 |
| Label dan teks kecil | 12 px | 400 | 1.4 |
| Angka ringkasan | 28 px | 600 | 1.2 |

### 4.3 Aturan

- **Sentence case untuk semua label.** Tidak ada huruf kapital semua — "Belum tap", bukan "BELUM TAP". Ini juga berlaku untuk label GURU/SISWA di layar gerbang: tulis "Guru" dan "Siswa".
- Nama orang ditulis apa adanya sesuai data, tidak dipaksa huruf besar
- Panjang baris teks maksimal 80 karakter
- Tidak ada huruf miring untuk penekanan; pakai tebal bila perlu

---

## 5. Jarak dan Bentuk

### 5.1 Kisi 4 Piksel

Seluruh jarak adalah kelipatan 4: **4, 8, 12, 16, 24, 32, 48**. Tidak ada angka di luar daftar ini.

| Tempat | Jarak |
|---|---|
| Dalam tombol | 8 atas-bawah, 16 kiri-kanan |
| Dalam kartu | 16 |
| Antar kartu | 12 |
| Antar bagian | 24 |
| Tepi halaman | 24 |
| Tinggi baris tabel | 40 |

### 5.2 Sudut

| Elemen | Radius |
|---|---|
| Tombol, kolom isian | 6 px |
| Kartu, panel, dialog | 10 px |
| Lencana status | 999 px |

Tiga tingkat saja. Memakai satu radius untuk semua membuat hierarki hilang.

### 5.3 Bayangan

Dua tingkat, memakai warna tinta — bukan hitam murni, karena hitam murni terlihat kotor di atas latar kertas:

```
Tingkat 1 (kartu, tabel, kolom isian)
  0 1px 2px rgba(20, 24, 31, 0.06)

Tingkat 2 (dialog, panel detail, menu turun, tombol utama saat ditekan)
  0 4px 12px rgba(20, 24, 31, 0.12)
```

Aturannya: bayangan menandakan **sesuatu berada di atas sesuatu yang lain**. Elemen yang menempel pada halaman tidak diberi bayangan. Jangan memberi bayangan pada setiap kotak — justru itu yang membuat aplikasi terlihat murah.

### 5.4 Overlay

Dialog dan panel detail muncul di atas lapisan gelap: `rgba(20, 24, 31, 0.45)`. Klik di luar menutup panel, kecuali pada dialog yang meminta keputusan.

---

## 6. Komponen

### 6.1 Tombol

| Jenis | Tampilan | Dipakai untuk |
|---|---|---|
| Utama | Latar Biru seragam, teks putih | Satu tindakan utama per layar |
| Sekunder | Latar putih, garis Garis, teks Tinta | Tindakan pendukung |
| Bahaya | Latar putih, garis dan teks merah | Hapus, nonaktifkan, restore |
| Teks | Tanpa latar dan garis | Batal, tutup |

Tinggi 36 px. Satu layar hanya punya **satu** tombol utama.

Tulisan tombol menyebut tindakannya: "Simpan sesi", "Impor 812 baris", "Kunci bulan September" — bukan "OK" atau "Submit".

### 6.2 Kolom Isian

Tinggi 36 px, garis `#DFE3E8`, radius 6. Saat difokus: garis Biru seragam 2 px. Saat salah: garis merah dengan pesan di bawahnya.

Label selalu di atas kolom, bukan di dalamnya — placeholder yang hilang saat diketik membuat pengguna lupa kolom apa yang sedang diisi.

### 6.3 Tabel

- Kepala tabel: latar Kertas, teks 12 px, tebal 600
- Baris: tinggi 40 px, pembatas 1 px `#DFE3E8`
- Baris disorot saat kursor di atasnya: latar `#F5F6F7`
- Kolom angka rata kanan, kolom teks rata kiri
- Kepala tabel menempel saat digulir

### 6.4 Lencana Status

Teks 12 px, radius 999, padding 2×10, memakai pasangan warna dari bagian 3.2. Selalu berisi kata statusnya, tidak pernah hanya titik berwarna.

### 6.5 Pemberitahuan Ringkas

Muncul di kanan bawah, bertahan 4 detik. Kalimatnya menyebut hasil, bukan proses: "Sesi tersimpan · 28 kartu terdaftar".

### 6.6 Dialog

Lebar 480 px, bayangan tingkat 2. Berisi judul, penjelasan singkat, dan dua tombol. Dialog yang menghapus atau mengunci wajib menyebut akibatnya:

> Kunci bulan September? Setelah dikunci, tidak ada yang bisa mengubah data September — termasuk kepala sekolah.

---

## 7. Keadaan Layar

Tiap layar wajib punya empat keadaan, bukan hanya keadaan normal.

**Kosong.** Berisi ajakan, bukan keluhan. "Belum ada siswa. Impor dari Excel untuk memulai." disertai tombolnya.

**Memuat.** Kerangka abu-abu berbentuk seperti isinya, bukan lingkaran berputar di tengah layar.

**Salah.** Menyebut apa yang terjadi dan apa yang harus dilakukan. "Kartu tidak terdaftar. Daftarkan lewat menu Absensi → Registrasi." Tidak minta maaf, tidak menyalahkan pengguna.

**Terkunci.** Menjelaskan sebabnya. "Bulan September sudah dikunci pada 3 Oktober oleh Rahmat."

---

## 8. Layar Gerbang

Layar ini punya aturan sendiri karena dilihat dari jarak 3–5 meter oleh orang yang sedang berjalan.

### 8.1 Ukuran pada Layar 1920×1080

| Elemen | Ukuran |
|---|---|
| Nama | 64 px, tebal 600 |
| Status dan jam | 40 px, tebal 600 |
| NISN atau NIP | 28 px |
| Label Guru/Siswa | 24 px |
| Foto | rasio 3:4, tinggi minimal 320 px |

### 8.2 Ketentuan

- Tanpa menu, tanpa kursor, tidak bisa ditutup tanpa sandi
- Latar kartu memakai warna status penuh, bukan tipis — dari jarak jauh, warna tipis tidak terbaca
- Empat kartu dapat tampil bersamaan; kartu lama turun ke daftar ringkas di bawah
- Kartu bertahan **8 detik** sebelum meredup
- Bunyi berbeda untuk berhasil dan gagal, karena siswa sering tidak melihat layar
- Jam besar di pojok, agar siswa tahu dia terlambat berapa menit

### 8.3 Yang Tidak Boleh Ada

Animasi masuk-keluar yang berlebihan, gambar latar, logo besar, dan teks berjalan. Semuanya mengganggu tugas tunggal layar ini: memastikan siswa melihat namanya muncul lalu berjalan terus.

---

## 9. Daftar Layar

Tanda ✓ berarti rancangannya sudah dibahas; sisanya mengikuti pola komponen di dokumen ini.

| Layar | Isi utama |
|---|---|
| Login ✓ | Logo, nama sekolah, username, sandi, status lisensi |
| Wizard instalasi | Enam langkah, satu layar per langkah, kemajuan di atas |
| Dashboard ✓ | Ringkasan harian, status rekap, peringatan pairing, daftar belum tap |
| Absensi ✓ | Status reader, mode aktif, daftar harian, tombol layar gerbang |
| Layar gerbang ✓ | Kartu tap, ringkasan harian |
| Siswa ✓ | Daftar, Unduh Template, Impor Excel, Impor Foto, Mutasi |
| Panel detail siswa ✓ | Identitas, kartu, pairing, rekap, riwayat |
| Guru | Daftar, Unduh Template, impor, kehadiran, penetapan status oleh kepala sekolah |
| Kelas | Daftar kelas, tingkat, wali kelas (pilih dari daftar guru) |
| Koreksi | Pilih kelas dan tanggal, ubah status, kolom "atas permintaan" |
| Kalender | Tampilan bulanan, blok tanggal libur, kolom alasan |
| Laporan | Pilih jenis, rentang, kelas; pratinjau; ekspor Excel dan PDF |
| Notifikasi | Status bot, antrean, riwayat kirim, pemantau pairing |
| Audit | Daftar perubahan 90 hari, saring per akun dan tanggal |
| Pengaturan | Identitas sekolah, jadwal harian, tahun ajaran, token bot, backup |

---

## 10. Alur Papan Ketik

Operator TU mengetik lebih cepat daripada mengeklik.

- `Tab` berpindah antar kolom sesuai urutan baca
- `Enter` menjalankan tombol utama
- `Esc` menutup panel dan dialog
- `F11` membuka dan menutup layar gerbang
- Cincin fokus selalu terlihat: garis Biru seragam 2 px dengan jarak 2 px

Pada mode registrasi, fokus **selalu** berada di kolom pembaca kartu. Bila pengguna mengeklik tempat lain, fokus kembali sendiri — kalau tidak, tap kartu akan hilang tanpa jejak.

---

## 11. Hasil Cetak

Laporan adalah wajah aplikasi ini di meja kepala sekolah dan dinas.

- A4, laporan bulanan memakai orientasi **lanskap**
- Kepala halaman: logo, nama sekolah, NPSN, judul laporan, rentang tanggal
- Kaki halaman: nomor halaman, tanggal cetak, nama pencetak
- Kepala tabel diulang di tiap halaman
- Kolom hari libur diarsir abu-abu muda dengan alasannya di catatan bawah
- Ruang tanda tangan kepala sekolah di halaman terakhir
- Cetak hitam-putih tetap terbaca: status dibedakan huruf (H, T, S, I, A), bukan hanya warna

---

## 12. Daftar Periksa Sebelum Layar Dianggap Selesai

Perbedaan antara aplikasi profesional dan aplikasi murahan hampir tidak pernah terletak pada fiturnya — melainkan pada konsistensi hal-hal kecil. Periksa setiap layar terhadap daftar ini.

| Hindari | Pastikan |
|---|---|
| Bayangan tebal di semua kotak | Bayangan hanya pada elemen yang benar-benar mengambang |
| Lebih dari tiga radius sudut | Tiga radius saja: 6, 10, 999 |
| Warna cerah sebagai hiasan | Warna kuat hanya untuk status |
| Label huruf kapital semua | Sentence case di seluruh antarmuka |
| Ikon dari berbagai gaya | Satu keluarga ikon, satu ketebalan garis |
| Jarak 13 px di satu tempat, 15 px di tempat lain | Seluruh jarak kelipatan 4 |
| Tombol berbeda ukuran di satu layar | Tinggi tombol seragam 36 px |
| Angka rata kiri di kolom tabel | Angka rata kanan, angka tabular |
| Pesan galat teknis seperti "Error: NullReference" | Pesan menyebut penyebab dan langkah perbaikan |
| Layar kosong yang benar-benar kosong | Keadaan kosong berisi ajakan dan tombolnya |
| Dua tombol utama bersaing di satu layar | Satu tombol utama per layar |
| Tulisan tombol "OK" atau "Submit" | Tulisan tombol menyebut tindakannya |

Tiga hal yang paling cepat membuat aplikasi terlihat matang:

1. **Kolom angka yang sejajar** — jam, jumlah, dan persentase yang rapi terbaca sebagai ketelitian
2. **Jarak yang konsisten** — mata menangkap ketidakteraturan jarak sebelum menyadari penyebabnya
3. **Pesan galat yang membantu** — ini yang membedakan aplikasi yang dirawat dari aplikasi yang sekadar jadi

---

*Dokumen ini belum final dan masih terbuka untuk perubahan.*
