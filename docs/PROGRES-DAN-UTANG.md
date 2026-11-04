# Progres dan Utang — implementasi awal PRD 2026-11-04

Satu-satunya folder kerja proyek: `D:\Project Absensi SMP 1`. Berkas ini adalah
ringkasan status terkini; rincian per butir ada di `MASTER-BUG-UIUX-REGISTER.md`,
`TODO-VERSI-BERIKUTNYA.md`, dan `RENCANA-TUGAS.md`.

> **Checkpoint kode 2026-11-04:** pemilik telah menginstruksikan kelanjutan kode yang ada mengacu pada PRD terbaru. Implementasi awal P-04 dan batas Hadir P-06 beserta hasil pengujian dicatat pada §7. Catatan §1–§5 serta checkpoint dokumentasi di bawah merupakan riwayat, bukan keadaan implementasi baru. Belum ada deployment.

> **2026-11-04 — PEMBARUAN DOKUMENTASI:** rancangan online rinci dan hasil pembahasan dicatat pada [PRD](PRD.md). Pemilik menginstruksikan commit/push dokumen PRD serta catatan utang ini. P-16 (verifikasi penautan dan pemilihan kanal Telegram/WhatsApp) tetap ditangguhkan. Utang PRD online ada pada §6 di bawah dan PRD §25.1.
>
> **Status historis:** §1–§5 di bawah dipertahankan sebagai checkpoint implementasi 2026-11-02. Angka 592 tes hijau, keadaan remote Git lokal dan rincian UAT merupakan catatan saat checkpoint tersebut, bukan pengujian ulang pada pembaruan dokumen ini. Repositori GitHub `patahpensil/absensi-smp1` sudah ada; konfigurasi remote pada PC pemilik tidak diperiksa/diubah. Kode aplikasi dan seluruh utang bug lama belum diubah oleh commit dokumentasi ini.

## 1. Kondisi
- Tes Release: **592 hijau** (Domain 54, Laporan 32, Data 241, Telegram 10, App 255).
- Repo git lokal, cabang `master`. **Belum ada remote GitHub** (`git remote -v` kosong) —
  tidak ada yang bisa di-push sampai pemilik menambahkan remote.

## 2. Selesai pada checkpoint ini (hasil pembacaan menyeluruh 2026-11-02)
| # | Perbaikan | Tes |
|---|---|---|
| T1 | `Penjadwal.DetakAsync` kini dilindungi bendera re-entrancy: detak yang tumpang tindih dilewati, pesan outbox tidak terkirim ganda. | `DetakAsync_DetakSebelumnyaMasihBerjalan_DetakBerikutnyaDilewati` |
| T2 | `IKlienBotTelegram.KirimPesanAsync` mengembalikan `HasilKirimPesan` (Berhasil / GagalJaringan / Ditolak). Tanpa internet atau 429/5xx **tidak** lagi menghabiskan jatah 5 percobaan dan menghentikan siklus; hanya penolakan nyata (4xx) yang dihitung (PRD §9.4). | 2 tes baru `PengirimOutboxTests` |
| T4 | `token_bot` (dan kunci berisi "token"/"sandi") tidak lagi ditulis ke `audit.nilai_baru`; yang tercatat `(disamarkan)`. | `LayananPengaturanAuditTests` (2) |
| T5 | `LayananAkun.ResetSandi`/`NonaktifkanAkun` memeriksa peran **target**: Admin hanya atas Wali Kelas (PRD §3.1); tidak ada yang bisa menonaktifkan dirinya sendiri; target tak ada ditolak. | 6 tes baru `LayananAkunTests` |
| T7 | Foto siswa/guru dan logo di PDF/Cetak laporan kini di-resolve dari `data/foto` / `data/` (sebelumnya `File.Exists` pada nama polos → tak pernah tercetak). | — (butuh cek visual UAT) |
| T9 | `GerbangViewModel.ProsesTap` menangkap galat yang bisa dipulihkan (tahun ajaran hilang, SQLite sibuk): kartu "Tap belum tercatat", bunyi gagal, aplikasi tidak tertutup. | — (butuh UAT gerbang) |
| doc | Register A-38/A-39 disinkronkan dengan commit; `PANDUAN-PEMASANGAN` tidak lagi menyebut "Impor foto" / restore "dari cloud". | |

## 3. Utang yang MASIH TERBUKA (urut prioritas)
**Dari pembacaan kode, belum dikerjakan:**
1. **T3** Status Bolos hanya ditulis di job notifikasi (ikut toleransi 15 menit); `FinalisasiAkhirHariGuru` hanya jalan saat pergantian tanggal di dalam proses. PC mati pada jamnya → status tidak pernah ditetapkan. Perlu keputusan produk (final saat start untuk hari-hari lampau).
2. **T6** Cek peran di lapisan layanan untuk `LayananSiswa/Guru/Kelas/Pengaturan/Pairing/Impor` (CLAUDE.md §4.5 aturan 19; A-42 hanya menyebut `LayananGuru`).
3. **T8** Libur ditandai setelah hari dibuka meninggalkan baris `alpa` yang ikut rekap (belum diuji).
4. Jaga `GuruViewModel.Simpan` dengan `PenangananGalat.Coba`; `TapKartuTunggalViewModel.TapUid` dan `AkunViewModel.Tambah/Nonaktifkan` hanya menangkap `InvalidOperationException`.
5. `HashSandi.Verifikasi` tak menjaga hash rusak; backup lokal tak terenkripsi; cadangan pra-restore ikut rotasi 7.
6. Token bot lama yang **sudah** tertulis di audit instalasi yang ada tidak bisa dihapus (audit immutable) — sarankan ganti token lewat BotFather.
7. Tes yang belum ada: foto/logo di PDF (T7), `ProsesTap` galat (T9).

**Terbuka dari register (tidak berubah):** Bug #11 UAT alur lengkap; A-41/A-42 UAT; A-44 tap RFID hilang di dua monitor; A-45 Playbook; A-7/D-1 crash `STATUS_STACK_OVERFLOW` belum tereproduksi (bukti di `arsip/bukti-uji-c/`); A-4/5/6; A-14/15/16/17 redesain UI; A-19 tes `KlienBotTelegram`; A-21/22/23 (A-23 logo header masih placeholder); A-24 Dashboard belum dibangun; TODO B.2; cadangan sekunder masih lokal; `ImporFotoSiswa/Guru` tanpa entry point UI; lisensi masih stub (PRD: aktivasi online); `README.md` perlu memuat 5 projek tes.

## 4. Proyek Python lama (DIBEKUKAN) — diarsipkan di `arsip/`
Disalin ke `arsip/proyek-python-lama/` (`sumber/` + `riwayat-git-lengkap.bundle` = seluruh riwayat git;
pulihkan dengan `git clone riwayat-git-lengkap.bundle`). Referensi (PRD .docx v2.0, draf, `smp.txt`,
skema) di `arsip/referensi/`. Bukti uji dari build uji lama C: di `arsip/bukti-uji-c/` (catatan,
berkas uji, log; dump crash `.dmp` 1,2 GB tidak ikut git, SHA-256:
`10108` = DBE823A1DEBC114234925CD75F8E0D3A5311C28ED819BA3C0DD83634DB01E2B4,
`7020` = D1D711BF172F72A76E724621743EE28DD09DB1EA5D461D1F0A98695A91EE238C).

Utang proyek Python (jika suatu hari dilanjutkan): Slice 14 Pencadangan; semantik ALPA scan terbuffer
(wajib sebelum WhatsApp aktif); pemicu notifikasi nyata (`antrekan()` tak pernah dipanggil);
ambang koreksi massal hardcode 5000; lampiran guru tak terenkripsi; laporan guru; tabel `devices` mati;
antrean lobi terblokir item yang ditolak server. **Bug terbukti 2026-11-02 (belum diperbaiki):**
(a) edit sel apa pun pada baris siswa di tabel Data Master menghapus NIK (NIK dimuat kosong lalu ditulis
`None`); (b) nomor WA orang tua yang ditambahkan lewat edit tidak mengaktifkan `menerima_notifikasi`.
Lainnya (bacaan): lobi hanya memakai `MockReader`, layar koreksi/laporan meminta ID angka,
Pengaturan bisa menimpa jam dengan 00:00 bila nilai belum ada, PDF Helvetica gagal pada karakter non-Latin-1.

## 5. Pembersihan
Folder di luar proyek yang terkait absensi dihapus setelah isinya diarsipkan dan diverifikasi
(jumlah berkas + SHA-256 untuk dump): `D:\Absensi SMP 1`, `D:\smp.txt`, `D:\master pembuatan aplikasi
absensi Final.zip`, `C:\AbsensiSekolah-*` (10 build uji, semua data uji sintetis), dan 4 berkas absensi di Downloads. **Tidak dihapus:** `C:\c` -- isinya bukan absensi (kerangka profil Hermes + `binance_raw.json` 1,9 MB yang BERBEDA dari `C:\Users\edgar\binance_raw.json`); menunggu keputusan pemilik. **GitHub:** akun `gh` (patahpensil) login, tetapi repo ini tidak punya remote dan belum ada repo absensi di akun; tidak ada yang di-push -- menunggu keputusan pemilik (nama repo, privat/publik). Folder proyek lain di `D:\` (ICT Screening, Malomo, NAHKODA, Ayuma, dst.) **tidak disentuh**.


## 6. Utang pembahasan PRD online

**Tanggal pencatatan:** 4 November 2026 (WITA). **Acuan:** [PRD §25](PRD.md#25-keputusan-terbuka-migrasi-dan-gerbang-rilis).

Tabel ini mencatat keputusan yang masih harus dibahas atau dibuktikan. Prinsip yang sudah disepakati tidak dibuka ulang: desktop C#/WPF, backend C#/ASP.NET Core, frontend terpisah, isolasi sekolah, lokasi penyimpanan utama Indonesia dan target verifikasi ASVS v5.0.0 L3. Target keamanan/biaya belum terbukti dan tidak boleh dinyatakan lulus atau layak hanya karena dokumen sudah di-commit.

### 6.1 Keputusan terbuka

| ID | Keputusan yang dibutuhkan | Dampak/batas sebelum diputuskan |
|---|---|---|
| D-01 | P-08 memilih antrean tap lokal terenkripsi; P-09 menetapkan waktu tap terverifikasi sebagai dasar Hadir/Terlambat dan Perlu pemeriksaan bila waktu tidak dapat dipercaya. Metode bukti waktu, ambang penyimpangan, batas offline, data minimum, kunci, retensi, alur pemeriksaan dan rekonsiliasi masih harus ditetapkan | Pilihan antrean dan prinsip waktu tap tidak dibuka ulang; gerbang produksi menunggu rincian dan bukti pengujian yang relevan |
| D-02 | P-10 menetapkan penahanan notifikasi Alpa/Bolos yang belum pasti akibat gangguan sinkronisasi, peringatan admin dan evaluasi setelah sinkronisasi/pemeriksaan selesai. Deteksi kelengkapan, cakupan terdampak, batas tunggu/eskalasi, finalisasi, replay job setelah downtime, toleransi jadwal dan ralat data terlambat masih harus dirinci | Prinsip penahanan tidak dibuka ulang; notifikasi/finalisasi produksi menunggu rincian dan bukti pengujian yang relevan |
| D-03 | Pemilihan kanal notifikasi online belum final; Telegram merupakan baseline dan WhatsApp dipertimbangkan. P-16 tentang verifikasi penautan Telegram ditangguhkan. Isi pesan, batas data keluar, identitas penerima serta aliran pihak ketiga masih harus diputuskan | Tidak mengimplementasikan WhatsApp atau usulan P-16 tanpa keputusan baru; tidak mengklaim semua salinan data siswa hanya di Indonesia sambil mengirim detail lewat pihak ketiga |
| D-04 | Model komersial/tarif/kontrak, masa aktif, akses saat menunggak/berakhir, ekspor dan bantuan | Tidak menetapkan harga atau menjalankan suspend/penghapusan otomatis |
| D-05 | Retensi per kelas data, audit/log, cache lokal, laporan sementara, backup dan pascakontrak; dasar dan prosedur pemusnahan | Tidak mengaktifkan penghapusan; konflik retensi "selamanya" belum ditutup |
| D-06 | Provider Indonesia, pembagian VM/cluster/instance, HSM, perlindungan memori, biaya dan pihak pengelola | Tidak membeli atau menyediakan infrastruktur; tidak mengklaim kelayakan L3 |
| D-07 | Target beban, latensi p95/p99, throughput, uptime, RPO/RTO dan batas anggaran pemilik layanan | Tidak menjanjikan kapasitas, SLA atau kecukupan dana pemilik tanpa bukti |
| D-08 | Teknologi frontend web dan model hosting/autentikasi, domain/subdomain serta daftar fitur pilot | Tidak mengganti bahasa backend/desktop atau menambah fitur web di luar kebutuhan yang ditinjau |
| D-09 | Strategi transisi versi 0.9, keberadaan data nyata, migrasi satu kali, pilot, cutover dan rollback | Tidak mengonversi database sekolah atau meninggalkan dua sumber kebenaran |
| D-10 | Pendaftaran hardware MFA, autentikator cadangan, pemulihan, posture perangkat, nilai umur sesi dan tata kelola bantuan | Tidak membuat login password saja sebagai pengecualian otomatis L3 |
| D-11 | Tanggung jawab penyedia/sekolah, kewenangan pihak sekolah, pengaturan privasi/aliran data, dan proses permintaan data | Tidak menganggap hosting Indonesia atau ASVS membuktikan seluruh kepatuhan hukum |
| D-12 | **Lingkungan uji sementara di GitHub** (Actions/runner di luar Indonesia) dipakai untuk menguji tooling dan kinerja aplikasi atas instruksi pemilik, 4 November 2026. Kebijakan data dan keamanan data lingkungan uji, serta pemindahan ke lingkungan uji di Indonesia, **belum dibahas** | Hanya data sintetis yang jelas ditandai. Tidak ada data siswa/guru nyata, kredensial produksi, atau token bot sungguhan di repo, log CI, maupun artefak. Bukan bukti kelayakan lokasi data, ASVS, atau kinerja produksi |

Prioritas pembahasan berikutnya yang disarankan: kapasitas awal dan dana yang harus disiapkan pemilik layanan (D-06/D-07/PRD §23), lalu rincian offline/replay (D-01/D-02), backup/retensi dan pemulihan akun. Ini urutan usulan, bukan pemilihan provider, angka anggaran atau persetujuan pengadaan. Evaluasi biaya menghitung pengeluaran penyedia; tarif/tagihan sekolah belum ditentukan.

### 6.2 Keputusan pembahasan yang sudah dicatat

P-01–P-15, P-17 dan P-18 dicatat pada PRD §25.5: koreksi berwenang, kartu tetap lintas tahun, tiga alur registrasi siswa, prioritas Izin/Sakit atas tap, notifikasi Alpa di batas akhir, batas Hadir 07.30/rekap orang tua 08.10, riwayat kelas, antrean offline terenkripsi, waktu tap terverifikasi, penahanan notifikasi tidak pasti, kelengkapan sebelum kunci bulan, pemulihan antrean rusak, libur setelah tap, koreksi kalender, riwayat jadwal, pergantian wali kelas dan acuan kalender akademik dengan penyesuaian siswa masuk/keluar tengah periode.

**P-16 ditangguhkan:** verifikasi tambahan penautan Telegram belum disetujui; WhatsApp masih dipertimbangkan, belum dipilih atau diimplementasikan. Aturan waktu/status notifikasi yang disepakati tetap tercatat. Jangan menganggap penangguhan sebagai persetujuan usulan tersebut.

### 6.3 Utang sinkronisasi dokumen dan bukti implementasi

- **DOC-01 — terbuka:** selaraskan `CLAUDE.md`, `README.md`, `SKEMA-DATABASE.md`, `RENCANA-TUGAS.md`, panduan pemasangan/tester dan dokumen turunan lain dengan PRD baru sebelum tugas implementasi terkait. Contoh konflik yang masih ada: standalone/SQLite sebagai penyimpanan resmi, migrasi setiap desktop, rekap 08.00, contoh tap 07.30 menjadi Terlambat, larangan notifikasi libur tanpa pengecualian koreksi kalender, serta akses wali yang hanya mengikuti penunjuk master saat ini. PRD tetap menjadi rujukan tertinggi; jangan mengubah aturan yang telah disepakati dari dokumen lama.
- **T3/T6/T8 dan bug/UAT lama tetap terbuka** sesuai §3: perubahan PRD tidak memperbaiki kode finalisasi, otorisasi, kalender libur atau bug gerbang secara otomatis. Kebutuhan produk terkait sudah diperjelas, bukti implementasinya belum tersedia.
- **VALIDASI-01 — belum dilakukan:** implementasi/migrasi, build dan regresi aturan baru, verifikasi ASVS lengkap, pengujian kapasitas/sinkronisasi, uji backup/restore, penawaran provider serta perhitungan biaya aktual. Skenario U-01–U-28 pada PRD merupakan kriteria penerimaan yang harus diuji, bukan hasil tes yang telah lulus.

### 6.4 Lingkup checkpoint dokumentasi

Pembaruan ini hanya menyimpan `docs/PRD.md` dan `docs/PROGRES-DAN-UTANG.md` ke repositori atas instruksi pemilik. Tidak mengubah source code, database, data sekolah, konfigurasi runtime, atau deployment. Daftar bug dan arsip historis pada §1–§5 tetap dipertahankan. Keputusan implementasi, migrasi data nyata, pengadaan dan deployment tetap memerlukan instruksi tersendiri.

## 7. Implementasi awal P-04 dan batas Hadir P-06

**Acuan:** PRD §8.3, §16.8.a dan §24. **Basis kode:** commit `6df9dee5978f2cb359062c80cefb2bb4dd63103a` pada `master`. Perubahan melanjutkan Domain, Data dan WPF yang sudah ada; tidak menambahkan dependency atau mengubah skema/database sekolah.

### 7.1 Perubahan yang ditulis

- Konteks tap siswa wajib memuat status resmi saat ini. Izin/Sakit mendahului tap masuk/pulang sesuai urutan PRD; status dan jam resmi tetap utuh, percobaan masuk ke `tap_log`, dan tap itu tidak menambah notifikasi Hadir/Terlambat.
- Pembacaan konteks, penyimpanan keputusan, antrean notifikasi dan tap log berada dalam satu transaksi SQLite dengan penguncian penulis sejak awal. Transaksi kartu diteruskan eksplisit. Koreksi dari koneksi lain tidak dapat menyela pembacaan status dan penyimpanan tap. Mekanisme ini hanya berlaku pada adapter SQLite yang ada, bukan bukti implementasi backend PostgreSQL.
- Perbandingan batas Hadir mengikuti konfigurasi sekolah dengan operator `<=`: pada konfigurasi 07.30, tap 07.30.00 Hadir dan 07.30.01 Terlambat; detik tidak dibulatkan.
- Gerbang menampilkan Izin/Sakit dengan arahan menghubungi wali kelas atau admin. Teks arahan opsional tidak ditampilkan untuk tap normal/guru.
- `CLAUDE.md` menandai teknologi lama sebagai baseline, merujuk arsitektur PRD baru, dan memperbaiki contoh batas Hadir. DOC-01 masih terbuka untuk dokumen/aturan lain.

### 7.2 Bukti pemeriksaan pada perubahan ini

| Pemeriksaan | Hasil |
|---|---|
| Restore/build Release seluruh `Absensi.sln`, termasuk WPF dan proyek tes Windows | Berhasil, SDK 8.0.427; lintas target dengan `EnableWindowsTargeting=true` pada Linux |
| Domain | 66 lulus, 0 gagal, 0 dilewati |
| Data | 247 lulus, 0 gagal, 0 dilewati |
| Telegram | 10 lulus, 0 gagal, 0 dilewati |
| Laporan | 32 lulus, 0 gagal, 0 dilewati |
| Total tes yang benar-benar dijalankan | **355 lulus**, 0 gagal, 0 dilewati |
| Tes WPF/App dan UAT visual/RFID | **Belum dijalankan**, memerlukan Windows; build tes berhasil |
| `git diff --check` | Lulus |

Tes Data mencakup Izin/Sakit sebelum masuk, setelah rekap dan saat pulang, termasuk jam resmi yang sudah terisi; tidak ada outbox baru dan percobaan tercatat. Uji kegagalan insert tap log membuktikan perubahan absensi/outbox ikut rollback dan pemrosesan berikutnya tetap dapat berhasil. Tes Domain mencakup batas detik, jadwal sekolah yang berbeda dan prioritas jeda tap. Tes pemetaan pesan dan layout petunjuk pada empat ukuran layar ditambahkan ke proyek App, tetapi belum dinyatakan lulus runtime.

CLI `dotnet test`/VSTest terhalang pembacaan informasi proses pada lingkungan ini. Build dijalankan melalui `MSBuild.dll` SDK yang sama; 355 tes di atas dijalankan memakai console runner resmi xUnit 2.5.3 dengan `-noshadow -parallel none`. Runner hanya alat pemeriksaan lokal, bukan dependency proyek. Pada Windows, jalankan `dotnet test Absensi.sln -c Release` sebelum menerima perubahan UI. Angka historis 592 pada §1 bukan hasil ulang seluruh suite dalam checkpoint ini.

### 7.3 Pekerjaan berikutnya yang belum selesai

1. Jalankan seluruh tes App/WPF dan UAT arahan gerbang pada Windows; pastikan tata letak, DPI dan input RFID benar.
2. Selesaikan P-06: pisahkan konfigurasi/job daftar belum tap wali kelas 08.00 dari rekap orang tua 08.10. Baseline masih memakai satu `jam_rekap`; perubahan ini **belum** memisahkannya atau mengganti jadwal tersimpan.
3. Bangun backend ASP.NET Core, adapter PostgreSQL dan identitas/isolasi tenant mengikuti PRD; desktop saat ini masih menggunakan adapter SQLite. Belum ada portal online, provisioning atau migrasi data nyata.
4. Lanjutkan DOC-01, T3/T6/T8, P-07–P-15/P-17/P-18 dan verifikasi keamanan sesuai cakupan masing-masing. D-01–D-11 tetap membutuhkan rincian/bukti sebagaimana §6; P-16 tetap ditangguhkan.

Perubahan ini tidak membuktikan ASVS L3, kapasitas, lokasi hosting, sinkronisasi offline atau kelayakan biaya. Tidak ada deployment, pengadaan maupun perubahan data sekolah nyata.


## 8. Pratinjau UI/UX HTML — 4 November 2026

Pratinjau terbaru disimpan di [docs/ui-preview/index.html](ui-preview/index.html), dengan [petunjuk penggunaan dan batas pemeriksaan](ui-preview/README.md).

- Navigasi atas dan susunan panel mengadaptasi Binance Advanced; bilah perintah, pintasan keyboard, angka monospace dan header panel mengadaptasi pola terminal. Warna utama biru seragam, putih dan abu muda dipertahankan.
- Tersedia pantauan gerbang, daftar absensi, riwayat tap, filter siswa belum tap/Izin/Sakit, filter aktivitas dan simulasi kartu.
- Bilah perintah menerima GERBANG, HARIAN/SISWA, LOG/RIWAYAT, UJI, TAP, BANTUAN serta pencarian nama/NISN. Ctrl+K memfokuskan perintah dan F2 membuka simulasi.
- Pemeriksaan browser terakhir: 14 aturan HTML lulus; perintah, pencarian, bantuan dan pintasan diperiksa; empat ukuran bingkai diperiksa tanpa luapan horizontal/kartu terpotong. Sintaks dua blok JavaScript diperiksa kembali sebelum commit.
- **Batas implementasi:** masih prototipe HTML dengan data contoh di memori browser. Belum mengubah tampilan WPF, menambahkan backend online, menghubungkan RFID/database, atau mengirim pesan. Utang pada §6–§7 tetap terbuka.
- Commit/push pratinjau diminta pemilik. Penyimpanan berkas ini ke GitHub tidak merupakan deployment aplikasi.


## 9. Checkpoint kode 4 November 2026 (sore) — T6, P-06, T8, CI dan build uji

Dikerjakan atas instruksi pemilik untuk melanjutkan kode yang ada sampai dapat diuji lewat GitHub. Seluruh perubahan pada baseline desktop WPF/SQLite; belum ada backend online. Data uji tetap sintetis (D-12).

| Butir | Perubahan | PR |
|---|---|---|
| T6 | `PenjagaKewenangan`: master siswa/guru/kelas, registrasi & ganti kartu, impor, pengaturan dan pairing hanya Kepala Sekolah/Admin, diperiksa di lapisan layanan sebelum transaksi. Unggah/impor foto memeriksa peran sebelum berkas disalin. Dialog tap kartu tunggal menampilkan penolakan sebagai pesan. **Perubahan perilaku:** wali kelas tidak lagi bisa mendaftarkan kartu siswa (tes lama mengizinkannya, bertentangan dengan PRD §3). | #13 |
| P-06 | Migrasi 004 `jadwal_hari.jam_daftar_belum_tap` (baris lama diisi dari `jam_rekap`, tidak mengubah perilaku instalasi lama). Daftar belum tap wali kelas dan rekap orang tua dijalankan terpisah. Pengaturan/wizard punya kolom "Belum tap (wali)" dan "Rekap orang tua"; isian awal wizard 08.00/08.10. | #14 |
| T8 | Baris absensi pada tanggal yang ditetapkan libur setelah hari dibuka tetap tersimpan, tetapi tidak dihitung pada seluruh laporan (P-13). | #15 |
| CI | Workflow `CI`: build dan seluruh tes di Windows untuk setiap push/PR. Workflow `Rilis build uji`: setelah CI `master` lulus, `.exe` self-contained diterbitkan ke prarilis `uji-terbaru`. Dependabot mingguan aktif. | #1, #4, PR ini |

**Bukti:** CI `master` commit `0b8151c` lulus — Domain 66, Data 266, Telegram 10, Laporan 32, App 266 (**640 tes**, 0 gagal), dijalankan di runner Windows GitHub.

**Belum dikerjakan / tetap terbuka:**
- T3 (finalisasi Bolos/guru saat PC mati), DOC-01 (penyelarasan CLAUDE.md, SKEMA-DATABASE.md — kolom baru migrasi 004 belum tercatat di sana), P-14 (pemberitahuan koreksi kalender), butir 4–7 §3, register UAT, serta D-01–D-12.
- UAT manual pada Windows dengan reader RFID sungguhan untuk perubahan T6 (pesan penolakan) dan P-06 (dua kolom jadwal) belum dilakukan.
- PR Dependabot #2–#12 belum ditinjau; sebagian versi mayor (mis. Microsoft.Data.Sqlite 10, xunit runner 4) perlu keputusan pemilik.

## 10. Simulator browser dan perubahan setelah tinjauan PRD §24 — 4 November 2026

- **Identifier berbahasa Inggris (§24.2):** identifier baru dari T6/P-06/T8 diganti ke bahasa Inggris. Migrasi 005 mengganti nama kolom `jam_daftar_belum_tap` menjadi `not_tapped_list_time`; migrasi 004 tidak diubah.
- **Atribusi (§24.5):** commit baru tanpa trailer atribusi. Deskripsi PR #1, #4, #13–#16 tidak bisa dibersihkan dari lingkungan kerja ini (proxy menambahkan kembali baris atribusi); perlu disunting manual oleh pemilik bila diinginkan. Aturan dicatat di CLAUDE.md §5–§6.
- **Build .exe dihapus** atas keputusan pemilik; workflow `Rilis build uji` dihapus. Prarilis `uji-terbaru` dan tag-nya masih ada di GitHub dan perlu dihapus manual (penghapusan ditolak dari lingkungan kerja ini).
- **Simulator browser (keputusan pemilik):** `Absensi.Simulator.Core` + `Absensi.Simulator.Web` (Blazor WebAssembly) menjalankan `PenentuStatusKehadiran` asli pada satu hari di memori, dengan data contoh. Pilihan Blazor ini untuk simulator uji; teknologi frontend portal produksi tetap keputusan D-08. Workflow `Simulator browser` menyimpan hasil publish di cabang `simulator-build`; halaman uji diterbitkan sebagai artifact pribadi pemilik dan diperbarui manual dari cabang itu.
- **Bukti:** CI `feat/simulator-browser` lulus (Domain 66, Data 266, Telegram 10, Laporan 32, App 266, Simulator 8). Uji Chromium lokal atas hasil publish: tap 07.29.00 Hadir, 07.30.01 Terlambat, 13/13 kasus batas sesuai, tanpa galat konsol.
- **Batas simulator:** tidak memuat database, notifikasi, akun/hak akses, laporan, atau reader RFID; hanya aturan tap dan finalisasi akhir hari.
