# Rencana Tugas — Diberikan Satu per Satu ke Agen

Setiap tugas adalah satu sesi. Jangan berikan dua tugas sekaligus.
Salin bagian **Perintah** ke agen, apa adanya.

Urutan ini disusun agar setiap tugas dapat diuji sebelum tugas berikutnya dimulai. Jangan melompat.

---

## Tugas 0 — Penyiapan Repositori

**Perintah:**

> Baca `docs/PRD.md`, `docs/SKEMA-DATABASE.md`, dan `docs/PANDUAN-UI-UX.md`. Buat struktur solusi .NET (kini .NET 10, menunggu persetujuan pemilik di PRD §25.5) sesuai bagian 3 CLAUDE.md: lima proyek di `/src` dan satu proyek pengujian di `/tests`. Atur `Absensi.Domain` tanpa referensi ke proyek mana pun. Tambahkan `.gitignore`, `README.md` singkat, dan pastikan `dotnet build` serta `dotnet test` berjalan tanpa galat. Jangan menulis logika apa pun.

**Selesai bila:** solusi terbangun, pengujian kosong berjalan, `Absensi.Domain` tidak punya referensi keluar.

---

## Tugas 1 — Skema dan Migrasi Database

**Perintah:**

> Terapkan seluruh tabel di `docs/SKEMA-DATABASE.md` sebagai migrasi bernomor di `Absensi.Data`. Migrasi 001 memuat seluruh tabel, indeks, dan CHECK persis seperti dokumen. Buat penjalan migrasi yang membaca tabel `skema_versi` dan menjalankan migrasi yang belum diterapkan saat aplikasi dibuka. Aktifkan `PRAGMA foreign_keys = ON` dan `journal_mode = WAL` pada setiap koneksi. Tulis pengujian yang membuat database kosong, menjalankan migrasi, lalu memverifikasi bahwa kedelapan aturan di bagian 10 dokumen skema benar-benar ditolak database ketika dilanggar.

**Selesai bila:** delapan pengujian pelanggaran gagal sebagaimana mestinya, migrasi idempoten.

---

## Tugas 2 — Logika Penentuan Status

**Perintah:**

> Bangun logika penentuan status di `Absensi.Domain` mengikuti PRD bagian 16.8, 16.8.a, dan 16.8.b **persis urutannya**. Masukan: nomor kartu, waktu, jenis pemilik, keadaan absensi hari itu, konfigurasi jadwal hari tersebut, dan status kalender. Keluaran: keputusan berupa jenis tindakan dan alasannya — bukan menulis ke database. Sediakan antarmuka `IPenyediaWaktu`. Tulis pengujian untuk seluruh kasus batas di bagian 5 CLAUDE.md, ditambah kasus Anda sendiri untuk setiap cabang keputusan.

**Selesai bila:** seluruh kasus batas lulus, dan tidak ada referensi ke SQLite maupun WPF di proyek ini.

**Jangan:** menyimpan apa pun ke database pada tugas ini.

---

## Tugas 3 — Repositori Data dan Pembukaan Hari

**Perintah:**

> Bangun lapisan repositori di `Absensi.Data` untuk siswa, guru, kelas, kartu, dan absensi. Terapkan proses pembukaan hari: saat aplikasi dibuka dan saat melewati pukul 00.01, buat baris `absensi_siswa` berstatus `alpa` untuk seluruh siswa aktif dan `absensi_guru` untuk seluruh guru aktif — kecuali tanggal itu berjenis `libur`. Proses harus aman dijalankan berulang kali pada hari yang sama. Sambungkan hasil keputusan dari Tugas 2 ke penyimpanan, dalam satu transaksi yang sekaligus menulis `tap_log`.

**Selesai bila:** menjalankan pembukaan hari dua kali tidak menggandakan baris; tap tercatat beserta jejaknya di `tap_log`.

---

## Tugas 4 — Akun, Peran, dan Jejak Audit

**Perintah:**

> Bangun autentikasi username dan sandi dengan hash yang layak. Terapkan tiga peran sesuai PRD bagian 3, termasuk aturan bahwa kepala sekolah dan admin berwenang penuh, dan wali kelas hanya menyentuh kelasnya sendiri lewat `akun.guru_id`. Pemeriksaan peran berada di lapisan layanan, bukan di UI. Bangun penulisan jejak audit sebagai satu jalur yang dilewati semua perubahan data, mencakup kolom `atas_permintaan`. Tulis pengujian yang memastikan wali kelas ditolak saat menyentuh kelas lain.

**Selesai bila:** tidak ada jalur perubahan data yang bisa melewati audit.

---

## Tugas 5 — Kerangka UI dan Layar Login

**Perintah:**

> Terapkan `docs/PANDUAN-UI-UX.md` sebagai sumber daya WPF: warna, skala huruf, jarak kelipatan 4, tiga radius, dua tingkat bayangan, serta gaya untuk tombol, kolom isian, tabel, dan lencana status. Bangun kerangka jendela utama dengan header dan menu samping sesuai bagian 14.2 PRD, lalu layar login sesuai bagian 9 panduan. Pakai MVVM. Nomor versi tampil di layar login.

**Selesai bila:** seluruh nilai visual berasal dari satu berkas sumber daya; tidak ada warna atau ukuran yang ditulis langsung di XAML layar.

---

## Tugas 6 — Layar Gerbang

**Perintah:**

> Bangun layar gerbang fullscreen sesuai PRD bagian 14.1 dan panduan UI/UX bagian 8. Tangkap masukan reader keyboard-emulation dengan fokus yang tidak bisa lepas. Tampilkan hingga empat kartu bersamaan, bertahan 8 detik, lalu turun ke daftar ringkas. Bedakan label Guru dan Siswa. Muat seluruh foto ke memori saat layar dibuka. Beri bunyi berbeda untuk berhasil dan gagal. Layar tidak bisa ditutup tanpa sandi.

**Selesai bila:** empat tap beruntun dalam satu detik tampil seluruhnya tanpa ada yang hilang.

---

## Tugas 7 — Master Data dan Impor

**Perintah:**

> Bangun layar Siswa, Guru, dan Kelas sesuai PRD bagian 14.4 dan 5. Terapkan impor Excel mengikuti alur 16.3 persis: sepuluh langkah, validasi per baris dengan nomor baris dan alasan, pratinjau, konfirmasi, lalu impor dalam satu transaksi. NISN dan NIP dibaca sebagai teks. Terapkan impor foto dari folder dengan nama berkas sesuai NISN atau NIP, dikecilkan ke 200×200. Sediakan unggah foto per orang. Terapkan penandaan tidak aktif — tanpa penghapusan.

**Selesai bila:** impor berkas yang memuat baris salah tidak menyimpan apa pun; impor ulang berkas yang sama tidak menggandakan data.

---

## Tugas 8 — Registrasi Kartu

**Perintah:**

> Bangun mode registrasi sesuai PRD 16.5 dan 17. Selama mode aktif, pencatatan absensi mati sepenuhnya. Daftar siswa per kelas, kursor berpindah otomatis setelah satu kartu terdaftar, progres ditampilkan. Tolak UID yang sudah dipakai — periksa terhadap kartu siswa **dan** guru, tampilkan nama pemiliknya. Simpan per sesi, dan matikan mode registrasi setelah tombol simpan ditekan. Bangun juga penggantian kartu hilang sesuai 16.11.

**Selesai bila:** tidak ada satu pun baris absensi tertulis selama mode registrasi aktif dalam pengujian.

---

## Tugas 9 — Kalender dan Jadwal

**Perintah:**

> Bangun layar Kalender sesuai PRD bagian 6: input manual per tanggal atau per rentang, dua jenis hari, kolom keterangan wajib untuk libur mendadak. Bangun layar pengaturan jadwal harian sesuai bagian 7, seluruh nilai dapat diubah per hari. Saat sebuah tanggal ditandai libur, batalkan seluruh baris `outbox` hari itu yang masih berstatus antre.

**Selesai bila:** menandai hari ini sebagai libur membuat seluruh notifikasi hari itu berstatus dibatalkan.

---

## Tugas 10 — Telegram dan Outbox

**Perintah:**

> Bangun `Absensi.Telegram` memakai Bot API lewat HTTPS langsung, tanpa pustaka pihak ketiga. Dua panggilan: `sendMessage` dan `getUpdates`. Terapkan pairing sesuai PRD 16.6 dengan token acak enam karakter sekali pakai, termasuk pembuatan QR dan cetak massal per kelas. Terapkan pengirim outbox: membaca antrean, menghormati batas kirim, mengulang saat gagal, menandai terkirim. Terapkan seluruh titik pengiriman di PRD bagian 9.2 dan 9.3, termasuk aturan toleransi 15 menit dan jadwal terlewat yang dibuang. Pesan hanya dibuat, tidak pernah dikirim langsung oleh kode pemicunya.

**Selesai bila:** mematikan internet tidak menghilangkan pesan; menyalakan kembali mengirim antrean; jadwal yang terlewat lebih dari 15 menit tidak terkirim.

---

## Tugas 11 — Koreksi Status dan Kunci Bulan

**Perintah:**

> Bangun layar Koreksi sesuai PRD 16.10, termasuk kolom "atas permintaan" dan penolakan bila bulan sudah dikunci. Terapkan pesan ralat: bila status berubah setelah notifikasi terkirim, buat pesan ralat di outbox. Bangun kunci bulan yang hanya dapat ditekan admin dan kepala sekolah, dengan dialog yang menyebut akibatnya. Bangun layar penetapan status guru oleh kepala sekolah sesuai 16.17, dua arah — di muka dan setelah.

**Selesai bila:** koreksi pada bulan terkunci ditolak di lapisan layanan, bukan hanya di UI.

---

## Tugas 12 — Laporan

**Perintah:**

> Bangun laporan sesuai PRD bagian 11: harian, mingguan Senin–Sabtu dengan keterangan per kejadian, bulanan berupa grid tanggal dengan hari libur diarsir, per siswa, dan semester dengan hari efektif dihitung sendiri. Terapkan aturan bahwa terlambat dan bolos dihitung hadir dengan kolom keterangan. Keluaran Excel dan PDF. PDF bulanan memakai orientasi lanskap dengan kepala tabel berulang tiap halaman, sesuai panduan UI/UX bagian 11. Laporan per siswa dapat dicetak per orang maupun satu kelas sekaligus.

**Selesai bila:** persentase kehadiran sesuai hari efektif yang dihitung dari kalender, bukan jumlah hari kalender.

---

## Tugas 13 — Wizard, Lisensi, dan Backup

**Perintah:**

> Bangun wizard instalasi pertama sesuai PRD 16.1, enam langkah, hanya berjalan bila database belum ada. Terapkan aktivasi lisensi sekali online. Terapkan backup sesuai bagian 18: dua titik — saat aplikasi dibuka dan setelah batas akhir tap masuk — berjalan di latar belakang, menyimpan tujuh berkas terakhir di folder `backup/`, terenkripsi sebelum diunggah. Terapkan restore dua jalur dengan ketiga pengaman di 18.4.

**Selesai bila:** restore membuat cadangan kondisi saat ini lebih dulu, dan tercatat di audit.

---

## Tugas 14 — Pengerasan dan Serah Terima

**Perintah:**

> Telusuri seluruh kode terhadap bagian 4 CLAUDE.md dan laporkan setiap pelanggaran. Periksa tidak ada nilai tertanam, tidak ada `catch` kosong, tidak ada `DateTime.Now` di luar penyedia waktu. Periksa seluruh layar terhadap daftar periksa panduan UI/UX bagian 12. Bangun `.exe` self-contained, uji di Windows bersih tanpa .NET terpasang. Tulis panduan pemasangan singkat untuk TU dalam bahasa Indonesia.

**Selesai bila:** aplikasi berjalan di PC tanpa .NET, dan seluruh pengujian lulus.

---

## Catatan Pemakaian

**Bila agen menyimpang,** hentikan dan minta dia membandingkan pekerjaannya dengan bagian PRD yang relevan sebelum melanjutkan.

**Bila agen bertanya,** jawab dari PRD. Bila PRD memang tidak menyebutkannya, itu keputusan Anda — dan sebaiknya ditambahkan ke PRD supaya tidak ditanyakan lagi di sesi berikutnya.

**Setelah tiap tugas selesai,** jalankan sendiri aplikasinya sebentar. Bug yang ditemukan pada tugas ke-3 jauh lebih murah daripada yang ditemukan pada tugas ke-12.
