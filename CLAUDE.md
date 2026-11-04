# CLAUDE.md — Konteks dan Aturan Proyek

Berkas ini dibaca pada setiap sesi. Letakkan di akar repositori.

---

## 1. Apa yang Dibangun

Layanan absensi RFID multi sekolah dengan desktop C#/WPF, backend C#/ASP.NET Core dan frontend terpisah. Rancangan resmi mengikuti `docs/PRD.md` terbaru, khususnya §21–§25: isolasi runtime/database per sekolah, penyimpanan sensitif di Indonesia dan target verifikasi ASVS v5.0.0 L3.

Kode yang ada masih baseline desktop standalone/SQLite, bukan implementasi arsitektur online yang telah selesai. Telegram adalah integrasi lama; pemilihan kanal online dan P-16 ditangguhkan. Jangan menganggap tabel teknologi/aturan lama di bawah sebagai izin mengesampingkan PRD terbaru. Pembangunan kode tidak memberi izin deployment atau migrasi data sekolah.

**Dokumen rujukan — baca sebelum mengerjakan tugas apa pun:**

| Berkas | Isi |
|---|---|
| `docs/PRD.md` | Seluruh keputusan produk, alur, dan aturan |
| `docs/SKEMA-DATABASE.md` | Struktur tabel dan aturan yang ditegakkan database |
| `docs/PANDUAN-UI-UX.md` | Warna, huruf, jarak, komponen, daftar layar |

Bila ada pertentangan antara berkas ini dan PRD, **PRD yang menang**. Laporkan pertentangannya, jangan diam-diam memilih salah satu.

---

## 2. Tumpukan Teknologi Baseline yang Sudah Ada

| Aspek | Ketetapan |
|---|---|
| Bahasa | C# (.NET 8) |
| Antarmuka | WPF |
| Database | SQLite, akses lewat Microsoft.Data.Sqlite |
| Keluaran | Satu `.exe` self-contained, tanpa proses install |
| Target | Windows 10/11, layar minimal 1366×768, antarmuka utuh pada 1280 px |

Jangan menambahkan kerangka kerja, ORM, atau pustaka besar tanpa persetujuan. Setiap pustaka baru harus disebutkan alasannya lebih dulu.

---

## 3. Struktur Repositori

```
/src
  /Absensi.Domain        ← logika murni, tanpa UI dan tanpa database
  /Absensi.Data          ← akses SQLite, migrasi, repositori
  /Absensi.Telegram      ← pemanggilan Bot API, outbox
  /Absensi.Laporan       ← Excel dan PDF
  /Absensi.App           ← WPF, seluruh layar
/tests
  /Absensi.Domain.Tests  ← pengujian aturan status dan jadwal
/docs                    ← PRD, skema, panduan UI/UX
```

`Absensi.Domain` **tidak boleh** mereferensikan proyek lain. Seluruh aturan jam dan status berada di sana.

---

## 4. Aturan yang Tidak Boleh Dilanggar

Melanggar salah satu dari ini berarti pekerjaan harus diulang.

### 4.1 Arsitektur

1. **Logika absensi terpisah dari UI dan database.** Seluruh penentuan status berada di satu kelas di `Absensi.Domain` yang menerima nomor kartu + waktu + konfigurasi jadwal, lalu mengembalikan keputusan. Kelas ini tidak mengenal WPF dan tidak mengenal SQLite.
2. **Tidak ada nilai tertanam di kode.** Jam masuk, jam rekap, batas tap, jam pulang, nama sekolah, toleransi, jeda tap — semua dibaca dari database. Tidak boleh ada angka `07:30` atau kata `Pancarijang` di dalam kode.
3. **Tidak ada logika di code-behind XAML.** Pakai pola MVVM.
4. **Seluruh perubahan data melewati satu jalur** yang sekaligus menulis jejak audit.
5. **Migrasi database bernomor**, dijalankan otomatis saat aplikasi dibuka. Tidak pernah menghapus atau mengubah tabel yang sudah ada tanpa migrasi.

### 4.2 Data

6. **Kelas bukan kolom di tabel siswa.** Kelas adalah keanggotaan per tahun ajaran lewat `siswa_kelas`.
7. **Absensi menunjuk ke siswa atau guru, tidak pernah ke nomor kartu.**
8. **Tidak ada penghapusan data.** Siswa keluar → `aktif = 0`. Kartu hilang → `aktif = 0`. Baris audit tidak pernah dihapus.
9. **NISN dan NIP diperlakukan sebagai teks**, tidak pernah sebagai angka. Nol di depan wajib bertahan.
10. **Impor berjalan dalam satu transaksi.** Gagal satu baris berarti tidak ada yang tersimpan.

### 4.3 Absensi

11. **Status awal setiap siswa setiap hari adalah `alpa`.** Baris dibuat saat pembukaan hari, bukan saat tap.
12. **Urutan pemeriksaan tap mengikuti PRD bagian 16.8 persis.** Jangan menyusun ulang urutannya "agar lebih efisien".
13. **Guru butuh dua tap untuk berstatus `hadir`.** Tap masuk saja menghasilkan `masuk`, lalu `tidak_lengkap` di akhir hari.
14. **Mode registrasi mematikan pencatatan absensi.** Selama aktif, tidak ada baris absensi yang boleh tertulis.

### 4.4 Notifikasi

15. **Pesan tidak pernah dikirim langsung dari kode yang memicunya.** Selalu masuk tabel `outbox`, dikirim oleh satu pengirim terpisah.
16. **Jadwal yang terlewat karena aplikasi mati tidak dikejar.** Toleransi 15 menit, lewat dari itu jadwal dibuang. Ini berbeda dengan backup yang justru harus dikejar.
17. **Tanggal berjenis `libur` tidak menghasilkan notifikasi apa pun.**
18. **Notifikasi siswa ke orang tua; notifikasi guru ke guru itu sendiri; rekap dan bolos ke wali kelas.** Jangan tertukar.

### 4.5 Hak Akses

19. **Setiap operasi memeriksa peran sebelum dijalankan**, di lapisan layanan — bukan hanya dengan menyembunyikan tombol.
20. **Wali kelas hanya menyentuh absensi kelasnya sendiri.** Kelas ditentukan dari `akun.guru_id` yang cocok dengan `kelas.wali_guru_id`.

---

## 5. Standar Penulisan Kode

Kode ditulis seperti seorang software engineer senior yang tahu bahwa aplikasi ini akan dirawat bertahun-tahun oleh orang lain.

**Penamaan.** Bahasa Inggris untuk kode, bahasa Indonesia untuk teks yang dilihat pengguna. Nama menjelaskan maksud, bukan tipe. PRD §24.2: identifier **baru** (kelas, metode, variabel, kolom database, nama tes) wajib berbahasa Inggris walaupun berkas di sekitarnya masih memakai nama lama berbahasa Indonesia; nama lama tidak diganti massal tanpa lingkup yang disetujui.

**Fungsi.** Satu fungsi satu tanggung jawab. Bila sebuah fungsi butuh komentar untuk menjelaskan bagiannya, pecah jadi beberapa fungsi.

**Komentar.** Menjelaskan **mengapa**, bukan **apa**. Komentar yang mengulang kode adalah sampah. Setiap aturan yang tampak aneh wajib diberi komentar yang menyebut bagian PRD-nya.

**Kondisi tak terduga.** Jangan menelan galat diam-diam. Tidak ada `catch { }` kosong. Galat yang tidak bisa ditangani harus sampai ke pengguna dalam kalimat yang bisa ditindaklanjuti.

**Nilai kosong.** Bedakan "belum diisi" dan "kosong secara sah". Kolom yang boleh kosong diberi tipe nullable secara eksplisit.

**Waktu.** Seluruh waktu dari satu sumber yang bisa diganti saat pengujian. Tidak ada `DateTime.Now` tersebar di mana-mana — pakai antarmuka `IPenyediaWaktu`.

**Transaksi.** Operasi yang mengubah lebih dari satu tabel selalu dalam satu transaksi.

**Pengujian.** Setiap aturan di `Absensi.Domain` punya pengujian. Minimal kasus batas berikut wajib ada:

```
tap 07.29            → hadir
tap 07.30.00         → hadir
tap 07.30.01         → terlambat
tap 07.31            → terlambat
tap kedua 10 detik   → ditolak
tap kedua 20 detik   → diabaikan (belum jam pulang)
tap 12.01 tanpa masuk→ ditolak
tap 13.15 sudah masuk→ mengisi jam pulang
Jumat tap 10.30      → ditolak
Jumat tap 11.35      → mengisi jam pulang
tanggal libur        → semua tap ditolak
guru tap masuk saja  → tidak_lengkap
siswa Izin/Sakit tap → status dan jam resmi tetap; percobaan dicatat
```

Waktu di atas adalah konfigurasi contoh. P-06 menetapkan rekap orang tua 08.10 dan daftar belum tap wali kelas 08.00 sebagai dua jadwal berbeda; pemisahannya masih merupakan utang implementasi, bukan alasan mengubah batas Hadir. Perubahan jadwal tidak boleh diam-diam menulis ulang riwayat (P-15). Ketentuan offline/replay, kalender libur/ralat dan hak akses historis memakai PRD terbaru; rincian terbuka pada §25.1 tidak boleh ditebak dari perilaku baseline.

---

## 6. Cara Bekerja per Sesi

1. Baca `docs/PRD.md` pada bagian yang relevan dengan tugas
2. Kerjakan **hanya** tugas yang diberikan — jangan mengerjakan tugas berikutnya
3. Tulis pengujian bersamaan dengan kode, bukan setelahnya
4. Jalankan seluruh pengujian sebelum menyatakan selesai
5. Laporkan: apa yang dibuat, keputusan apa yang diambil, dan apa yang masih kurang

**Jangan pernah:**

- Menambah fitur yang tidak diminta
- Mengubah keputusan PRD karena menurut Anda ada cara yang lebih baik — laporkan saja, biarkan pemilik proyek yang memutuskan
- Membuat data contoh yang terlihat seperti data sungguhan tanpa menandainya jelas
- Menyentuh berkas di `docs/` tanpa diminta
- Menambahkan atribusi platform pada commit, PR, kode, komentar, UI, atau dokumentasi produk, misalnya trailer `Co-Authored-By` untuk bot/platform, tautan sesi percakapan, atau kalimat "Generated by ..." (PRD §24.5). Riwayat commit lama tidak ditulis ulang.

**Selalu tanyakan bila:** PRD tidak menyebutkan kasus yang Anda hadapi, atau dua bagian PRD tampak bertentangan.

---

## 7. Hal yang Sudah Ditetapkan dan Sering Salah Ditebak

| Hal | Ketetapan |
|---|---|
| Hari sekolah | Senin–Kamis, **Sabtu**, dan Jumat. Minggu libur |
| Jumat | Jam masuk sama (07.30); yang beda batas tap (10.00) dan jam pulang (11.30) |
| Pergantian hari | Pukul 00.01 |
| Terlambat dan bolos | **Dihitung hadir** untuk persentase, ditampilkan di kolom keterangan |
| Rekap 08.00 | Hanya untuk siswa yang **sudah tap**; yang masih alpa ditahan |
| Reader | 4 unit USB keyboard-emulation, **tidak dapat dibedakan** satu sama lain |
| Kartu siswa dan guru | Satu tabel, UID unik lintas keduanya |
| Backup | Dikejar bila terlewat. Notifikasi tidak |

---

## 8. Yang Tidak Dikerjakan pada Versi Ini

Jangan membangun ini walau terasa mudah ditambahkan:

- Akses wali kelas lewat HP atau jaringan
- Notifikasi WhatsApp
- Pemberitahuan update otomatis dari server
- Cetak kartu dari aplikasi
- Absensi per mata pelajaran atau per jam pelajaran
- Fitur apa pun yang tidak ada di PRD
