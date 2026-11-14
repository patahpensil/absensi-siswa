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
2. ~~**T6** Cek peran di lapisan layanan~~ — selesai, lihat §9.
3. ~~**T8** Libur ditandai setelah hari dibuka~~ — selesai, lihat §9.
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
| D-03 | P-19 menetapkan dua kanal: Telegram dan WhatsApp. P-20 menetapkan satu kanal per siswa pilihan orang tua dan WhatsApp memakai nomor HP orang tua langsung. P-16 tentang verifikasi penautan Telegram ditangguhkan. Penyedia/API WhatsApp, kanal untuk wali kelas/guru, isi pesan, batas data keluar, biaya per pesan serta aliran pihak ketiga masih harus diputuskan | Pilihan dua kanal tidak dibuka ulang; tidak mengimplementasikan WhatsApp atau usulan P-16 sebelum rincian diputuskan; tidak mengklaim semua salinan data siswa hanya di Indonesia sambil mengirim detail lewat pihak ketiga |
| D-04 | Model komersial/tarif/kontrak, masa aktif, akses saat menunggak/berakhir, ekspor dan bantuan | Tidak menetapkan harga atau menjalankan suspend/penghapusan otomatis |
| D-05 | Retensi per kelas data, audit/log, cache lokal, laporan sementara, backup dan pascakontrak; dasar dan prosedur pemusnahan | Tidak mengaktifkan penghapusan; konflik retensi "selamanya" belum ditutup |
| D-06 | Provider Indonesia, pembagian VM/cluster/instance, HSM, perlindungan memori, biaya dan pihak pengelola | Tidak membeli atau menyediakan infrastruktur; tidak mengklaim kelayakan L3 |
| D-07 | Target beban, latensi p95/p99, throughput, uptime, RPO/RTO dan batas anggaran pemilik layanan | Tidak menjanjikan kapasitas, SLA atau kecukupan dana pemilik tanpa bukti |
| D-08 | Teknologi frontend web dan model hosting/autentikasi, domain/subdomain serta daftar fitur pilot | Tidak mengganti bahasa backend/desktop atau menambah fitur web di luar kebutuhan yang ditinjau |
| D-09 | Strategi transisi versi 0.9, keberadaan data nyata, migrasi satu kali, pilot, cutover dan rollback | Tidak mengonversi database sekolah atau meninggalkan dua sumber kebenaran |
| D-10 | Pendaftaran hardware MFA, autentikator cadangan, pemulihan, posture perangkat, nilai umur sesi dan tata kelola bantuan | Tidak membuat login password saja sebagai pengecualian otomatis L3 |
| D-11 | Tanggung jawab penyedia/sekolah, kewenangan pihak sekolah, pengaturan privasi/aliran data, dan proses permintaan data | Tidak menganggap hosting Indonesia atau ASVS membuktikan seluruh kepatuhan hukum |
| D-12 | **Lingkungan uji sementara di GitHub** (Actions/runner di luar Indonesia) dipakai untuk menguji tooling dan kinerja aplikasi atas instruksi pemilik, 4 November 2026. Pemilik juga memutuskan seluruh kode tetap di GitHub, sedangkan uji tampilan dan fitur lewat browser memakai **Cloudflare Pages** (akun khusus uji, di luar Indonesia) — lihat §10. Kebijakan data dan keamanan data lingkungan uji, serta pemindahan ke lingkungan uji di Indonesia, **belum dibahas** | Hanya data sintetis yang jelas ditandai. Tidak ada data siswa/guru nyata, kredensial produksi, atau token bot sungguhan di repo, log CI, maupun artefak. Bukan bukti kelayakan lokasi data, ASVS, atau kinerja produksi |

Prioritas pembahasan berikutnya yang disarankan: kapasitas awal dan dana yang harus disiapkan pemilik layanan (D-06/D-07/PRD §23), lalu rincian offline/replay (D-01/D-02), backup/retensi dan pemulihan akun. Ini urutan usulan, bukan pemilihan provider, angka anggaran atau persetujuan pengadaan. Evaluasi biaya menghitung pengeluaran penyedia; tarif/tagihan sekolah belum ditentukan.

### 6.2 Keputusan pembahasan yang sudah dicatat

P-01–P-15, P-17 dan P-18 dicatat pada PRD §25.5: koreksi berwenang, kartu tetap lintas tahun, tiga alur registrasi siswa, prioritas Izin/Sakit atas tap, notifikasi Alpa di batas akhir, batas Hadir 07.30/rekap orang tua 08.10, riwayat kelas, antrean offline terenkripsi, waktu tap terverifikasi, penahanan notifikasi tidak pasti, kelengkapan sebelum kunci bulan, pemulihan antrean rusak, libur setelah tap, koreksi kalender, riwayat jadwal, pergantian wali kelas dan acuan kalender akademik dengan penyesuaian siswa masuk/keluar tengah periode.

**P-16 ditangguhkan:** verifikasi tambahan penautan Telegram belum disetujui. **P-19 (12 November 2026):** kanal notifikasi mendukung Telegram dan WhatsApp; WhatsApp belum diimplementasikan sampai rincian D-03 diputuskan. Aturan waktu/status notifikasi yang disepakati tetap tercatat. Jangan menganggap penangguhan sebagai persetujuan usulan tersebut.

### 6.3 Utang sinkronisasi dokumen dan bukti implementasi

- **DOC-01 — terbuka:** selaraskan `CLAUDE.md`, `README.md`, `SKEMA-DATABASE.md`, `RENCANA-TUGAS.md`, panduan pemasangan/tester dan dokumen turunan lain dengan PRD baru sebelum tugas implementasi terkait. Contoh konflik yang masih ada: standalone/SQLite sebagai penyimpanan resmi, migrasi setiap desktop, rekap 08.00, contoh tap 07.30 menjadi Terlambat, larangan notifikasi libur tanpa pengecualian koreksi kalender, serta akses wali yang hanya mengikuti penunjuk master saat ini. PRD tetap menjadi rujukan tertinggi; jangan mengubah aturan yang telah disepakati dari dokumen lama.
- **T3 dan bug/UAT lama tetap terbuka** sesuai §3. T6 dan T8 sudah dikerjakan pada checkpoint §9; tidak lagi dianggap belum diimplementasikan. UAT perangkat fisik tetap terpisah dari tes otomatis dan web uji.
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
2. ~~Selesaikan P-06: pisahkan daftar belum tap wali kelas dari rekap orang tua~~ — selesai pada §9 (migrasi 004–005).
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
| T6 | `AuthorizationGuard`: master siswa/guru/kelas, registrasi & ganti kartu, impor, pengaturan dan pairing hanya Kepala Sekolah/Admin, diperiksa di lapisan layanan sebelum transaksi. Unggah/impor foto memeriksa peran sebelum berkas disalin. Dialog tap kartu tunggal menampilkan penolakan sebagai pesan. **Perubahan perilaku:** wali kelas tidak lagi bisa mendaftarkan kartu siswa (tes lama mengizinkannya, bertentangan dengan PRD §3). | #13 |
| P-06 | Migrasi 004 menambah kolom daftar belum tap pada `jadwal_hari` (baris lama diisi dari `jam_rekap`, tidak mengubah perilaku instalasi lama); migrasi 005 menamainya `not_tapped_list_time`. Daftar belum tap wali kelas dan rekap orang tua dijalankan terpisah. Pengaturan/wizard punya kolom "Belum tap (wali)" dan "Rekap orang tua"; isian awal wizard 08.00/08.10. | #14 |
| T8 | Baris absensi pada tanggal yang ditetapkan libur setelah hari dibuka tetap tersimpan, tetapi tidak dihitung pada seluruh laporan (P-13). | #15 |
| CI | Workflow `CI`: build dan seluruh tes di Windows untuk setiap push/PR. Dependabot mingguan aktif. (Workflow build `.exe` sempat dibuat lalu dihapus, lihat §10.) | #1, #4, #16 |

**Bukti:** CI `master` commit `0b8151c` lulus — Domain 66, Data 266, Telegram 10, Laporan 32, App 266 (**640 tes**, 0 gagal), dijalankan di runner Windows GitHub.

**Belum dikerjakan / tetap terbuka:**
- T3 (finalisasi Bolos/guru saat PC mati), DOC-01 (sisa penyelarasan dokumen turunan, lihat §10), P-14 (pemberitahuan koreksi kalender), butir 4–7 §3, register UAT, serta D-01–D-12.
- UAT manual pada Windows dengan reader RFID sungguhan untuk perubahan T6 (pesan penolakan) dan P-06 (dua kolom jadwal) belum dilakukan.
- PR Dependabot #2–#12 belum ditinjau; sebagian versi mayor (mis. Microsoft.Data.Sqlite 10, xunit runner 4) perlu keputusan pemilik.

## 10. Penyelarasan setelah tinjauan PRD §24 dan rencana uji browser — 4 November 2026

- **Identifier berbahasa Inggris (§24.2):** identifier baru dari T6/P-06/T8 diganti ke bahasa Inggris. Migrasi 005 mengganti nama kolom `jam_daftar_belum_tap` menjadi `not_tapped_list_time`; migrasi 004 tidak diubah.
- **Atribusi (§24.5):** commit baru tanpa trailer atribusi. Deskripsi PR #1, #4, #13–#16 tidak bisa dibersihkan dari lingkungan kerja ini (proxy menambahkan kembali baris atribusi); perlu disunting manual oleh pemilik bila diinginkan. Aturan dicatat di CLAUDE.md §5–§6.
- **Build `.exe` uji dihapus** atas keputusan pemilik (workflow `Rilis build uji`).
- **Simulator aturan tap dihapus.** `Absensi.Simulator.Core`, `Absensi.Simulator.Web`, tesnya dan workflow `Simulator browser` dihapus karena hanya mencakup aturan tap, bukan seluruh fitur yang ingin diuji pemilik. Aturan batas tetap diuji oleh `Absensi.Domain.Tests`.
- **Rencana uji yang disepakati pemilik:** seluruh kode tetap di GitHub; GitHub Actions membangun aplikasi web uji dan menerbitkannya ke Cloudflare Pages (akun khusus uji) pada tautan tetap. Tahap 1 menjalankan kode asli Domain/Data di browser dengan data sintetis tersimpan di browser untuk menguji seluruh menu, alur dan UI/UX; login masih simulasi dan data tidak dibagi antar-perangkat. Tahap 2 (belum diputuskan) menjalankan backend C# ASP.NET Core + PostgreSQL di server staging. Teknologi frontend produksi tetap keputusan D-08.
- **DOC-01 (sebagian):** `CLAUDE.md` §5, §7, §8 dan `SKEMA-DATABASE.md` (`jadwal_hari`) diselaraskan dengan P-06/§20/D-03. Sisa: `README.md`, `RENCANA-TUGAS.md`, panduan pemasangan/tester dan `CLAUDE.md` §2 (tumpukan teknologi baseline) belum ditinjau.
- **Perlu dihapus manual oleh pemilik** (penghapusan ditolak dari lingkungan kerja ini): prarilis `uji-terbaru` dan tag-nya; cabang `simulator-build`; cabang yang sudah digabung `ci/github-actions-uji`, `ci/rilis-build-uji`, `feat/p06-pisah-jadwal-rekap`, `feat/simulator-browser`, `fix/simulator-tema`, `fix/t6-cek-peran-layanan`, `fix/t8-libur-setelah-hari-dibuka`, `refactor/identifier-inggris`, `chore/rapikan-sesuai-prd`; cabang usang `docs/revisi-online-prd-claude` (isinya digantikan PRD 1.0 di `master`).
- **`CLAUDE.md` dihapus** atas keputusan pemilik; `docs/PRD.md` menjadi satu-satunya acuan kerja (standar kode PRD §24). Rujukan "CLAUDE.md §…" pada komentar kode/tes diganti ke pasal PRD yang setara, tanpa perubahan perilaku. Aturan "backup dikejar bila terlewat" hanya tercatat di `CLAUDE.md` lama; kode baseline mempertahankannya dan PRD §21.9 meninjaunya untuk server (D-02). Rujukan wali kelas lewat `kelas.wali_guru_id` kini menunjuk PRD §3; riwayat penugasan P-17 (§3.3) belum diterapkan di kode.
- **Folder `master pembuatan aplikasi absensi Final/`** (PRD v0.9, skema, panduan UI dan rencana tugas lama) dipindah ke `arsip/master-pembuatan-aplikasi-final-v0.9/` agar tidak disangka acuan.

## 11. Web uji tahap 1 — 4 November 2026 (malam)

**Checkpoint historis:** uraian "belum ada login/menu" dan belum terbuktinya IndexedDB di bawah sudah digantikan oleh implementasi pada §12.

- **Bukti teknis lulus (CI, Chromium):** `Absensi.Web` (Blazor WebAssembly) memakai `Absensi.Data` asli: SQLite terbuka dengan `journal_mode = wal` dan `foreign_keys` aktif, migrasi sampai versi 5 (19 tabel), tulis-baca satu baris, dan Excel (ClosedXML) dibuat di memori. SQLite native ditautkan saat publish dengan `-p:WasmBuildNative=true` dan workload `wasm-tools-net8` (runner memakai SDK 10; tanpa `global.json`).
- **Peringatan yang diketahui:** penautan SQLite memberi peringatan WASM0001 untuk fungsi varargs `sqlite3_config`/`sqlite3_db_config`; tidak dipanggil oleh pemeriksaan di atas, tetapi belum diperiksa untuk seluruh alur aplikasi.
- **Belum dibuktikan:** penyimpanan tetap setelah halaman ditutup (SQLite berada di memori browser; perlu salinan ke IndexedDB), PDF (QuestPDF) di browser, dan seluruh layar PRD. Hanya halaman pemeriksaan teknis yang ada; belum ada login atau menu.
- **Deploy:** workflow `Web uji - deploy Cloudflare Pages` mempublikasikan `master` ke proyek Pages `absensi-uji` (akun khusus uji, di luar Indonesia; data sintetis saja, D-12). Situs bersifat publik kecuali dikunci dengan Cloudflare Access. Kredensial hanya dari GitHub Secrets.
- **Cara kerja bersama (keputusan pemilik):** tidak memakai cabang kerja; perubahan langsung ke `master` dengan `pull --rebase` sebelum push dan commit kecil agar mudah saling ditinjau.

## 12. Ruang kerja web uji dan UI/UX — 4 November 2026

**Instruksi pemilik:** Cloudflare hanya lingkungan uji sementara selama pengembangan. Seluruh perubahan dan penambahan fitur diperbarui pada tautan tetap `https://absensi-uji.pages.dev/` untuk ditinjau melalui browser. Fondasi tetap PRD, termasuk arsitektur produksi, data Indonesia, C# dan target ASVS L3. Arah tampilan memakai susunan panel Binance dan terminal Bloomberg dengan warna biru/putih/abu-abu sekolah.

### 12.1 Implementasi

- Kerangka navigasi atas, identitas sekolah/tahun ajaran, bilah perintah, angka monospace, header panel, dan status lingkungan uji. Ctrl+K memfokuskan perintah; F2 membuka Absensi; F3 membuka Siswa. Nama/NISN pada bilah perintah membuka pencarian siswa.
- Login dengan pilihan akun contoh. Dashboard membaca data asli SQLite: ringkasan status, tap masuk terbaru, daftar belum tap/Izin/Sakit, jadwal sekolah, kartu/foto/pairing yang belum lengkap. Tidak mengisi angka atau aktivitas fiktif agar dashboard tampak ramai.
- Daftar siswa/guru dapat dicari; siswa dapat difilter per kelas. Nomor kartu aktif ditampilkan utuh. Kelas menampilkan wali dan jumlah siswa aktif.
- Registrasi kartu untuk siswa yang sudah tersimpan: pilih siswa → input UID untuk uji browser → Simpan → verifikasi ulang pasangan. Satu siswa/satu kartu melalui `LayananRegistrasiKartu.DaftarkanSatu`; UID kosong/duplikat ditolak, nol depan dipertahankan, UID sementara dibuang saat dialog ditutup. Dialog tetap terbuka sesudah berhasil. Akun wali kelas tidak memiliki tindakan registrasi.
- Layar Absensi memproses UID melalui `PemrosesTap` asli, menampilkan hasil/alasan serta jam masuk/pulang tersimpan. Tidak menulis ulang aturan status ke JavaScript. Akun wali kelas hanya melihat kelasnya; tidak mendapat input pemrosesan kartu.
- Pengaturan menyediakan waktu skenario yang berhenti untuk uji batas detik, serta penyimpanan ulang dan reset data sintetis dengan konfirmasi. Kegagalan IndexedDB dibedakan dari perubahan yang masih berada dalam sesi.
- Dukungan data globalisasi WebAssembly diaktifkan karena pemroses tap/notifikasi menggunakan kultur `id-ID`.
- Workflow deploy menjalankan uji browser sebelum memperbarui Cloudflare, kemudian menguji hasil deploy. Pekerjaan deploy yang sudah tertinggal dibatalkan bila revisi baru masuk.

### 12.2 Batas implementasi dan pekerjaan berikutnya

Web uji belum menyediakan modul Koreksi, Kalender, Laporan, Notifikasi, dan Audit; halaman tersebut menyatakan belum tersedia. CRUD master lengkap, input siswa baru, impor Excel termasuk `NO_KARTU`, unggah foto dan penggantian kartu juga masih perlu ditambahkan pada browser. Registrasi yang tersedia pada checkpoint ini untuk siswa contoh yang sudah tersimpan.

Reader fisik, gerbang desktop dua monitor, pengiriman pesan eksternal, backend ASP.NET Core/PostgreSQL, MFA produksi, antrean offline dan isolasi tenant produksi belum dibuktikan oleh pengujian web ini. D-01–D-12, celah otorisasi/transaksi koreksi hasil review, riwayat data dan utang keamanan/UAT tetap terbuka. PRD tidak diganti dengan arsitektur Cloudflare.

### 12.3 Pengujian

Pengujian browser diperluas menjadi 16 kelompok: diagnostik; login dan sandi salah; peran/menu; jumlah master; pencarian/filter; perintah/pintasan; registrasi dan pembatalan; batas 07.30.00/07.30.01; penolakan jeda/kartu asing; tap pulang; empat ukuran layar; halaman belum tersedia; logout; dan data/lingkup wali setelah reload. Registrasi juga memeriksa fokus keyboard tetap di dalam dialog sesudah Simpan.

**Bukti final, 4 November 2026 pukul 22.04–22.05 WITA, commit kode `f91cdd97bfd6413c8b5ea67ef25f60a854f06d5d`:**

| Pemeriksaan | Hasil |
|---|---|
| Build Release dan pengujian Windows | Domain 66, Data 266, Telegram 10, Laporan 32, App 266: **640 lulus**. [CI](https://github.com/patahpensil/absensi-smp1/actions/runs/41843369567) |
| Build native SQLite WebAssembly dan Chromium | **16 kelompok lulus**. [Bukti browser](https://github.com/patahpensil/absensi-smp1/actions/runs/41843369676) |
| Pengujian sebelum deploy | **16 kelompok lulus**, termasuk layout 1440×900, 1366×768, 768×1024, dan 390×844 |
| Pengujian setelah deploy | **16 kelompok lulus** pada hasil unggahan Cloudflare. [Workflow deploy](https://github.com/patahpensil/absensi-smp1/actions/runs/41843369569) |
| Pemeriksaan langsung di tautan tetap | Login contoh, daftar 24 siswa, registrasi UID dengan nol depan, waktu skenario berbahasa Indonesia, tap 07.30 → Hadir dan pembaruan dashboard terverifikasi |

Tautan tetap tetap `https://absensi-uji.pages.dev/`. Bukti ini tidak menyatakan lulus ASVS L3, kesiapan produksi, uji kapasitas, ataupun reader RFID fisik. Build lokal tidak dinyatakan lulus karena runtime lingkungan lokal gagal membaca informasi proses; bukti build dan pengujian berasal dari runner GitHub di atas. Peringatan native SQLite WASM0001 yang telah dicatat pada §11 tetap perlu ditriase untuk alur lain yang belum diuji.

## 13. Format jam tidak bergantung bahasa perangkat — 5 November 2026

- **Masalah:** jadwal sekolah (`jadwal_hari`) ditulis dengan `ToString("HH:mm")` tanpa kultur. Pada format kustom .NET, `:` berarti pemisah jam milik kultur aktif; untuk `id-ID` pemisahnya titik, sehingga PC/browser berbahasa Indonesia menyimpan `07.30`, sedangkan perangkat lain `07:30`. Pembacaan `TimeOnly.Parse` juga mengikuti kultur. Uji Chromium CI berbahasa Inggris sehingga tidak menangkapnya. Commit `475e0d4` (format detik jam absensi) belum dicatat di berkas ini; perbaikannya kini disatukan di sini.
- **Perubahan:** `StoredTime` (Absensi.Data) menulis jam selalu `HH:mm`/`HH:mm:ss` dengan `InvariantCulture` dan membaca format tersebut serta format lama bertitik (`07.30`, `07.30.05`) yang mungkin sudah tersimpan. Dipakai `RepositoriJadwalHari` dan `RepositoriAbsensi`. Teks pesan notifikasi tidak diubah.
- **Tes:** `StoredTimeCultureTests` — simpan jadwal di kultur `id-ID` lalu baca di `en-US`, isi kolom mentah bertitik dua, baca data lama bertitik di `en-US`/`id-ID`/invariant.
- **Belum dibuktikan:** data nyata di instalasi desktop lama belum diperiksa. Parse tanggal/waktu lain (`DateTime.Parse` atas teks ISO/`"o"`) belum diaudit untuk kultur; risikonya lebih rendah karena format ISO tidak memakai pemisah kultur, tetapi tetap utang.

## 14. Panel detail siswa/guru di web uji — 5 November 2026

**Laporan pemilik:** nama siswa/guru diklik tidak menampilkan riwayat absensi. **Penyebab:** panel detail (PRD §14.6, PANDUAN-UI-UX §9 "Panel detail siswa") belum dibangun; nama hanya teks. Bukan masalah penautan kartu RFID.

- **Perubahan:** nama pada menu Absensi (sesuai PRD §14.6), daftar Siswa dan daftar Guru kini membuka panel samping tanpa berpindah halaman: identitas, status hari ini, kartu RFID, nama orang tua (siswa), status Telegram, rekap bulan, riwayat per tanggal (status, jam masuk/pulang, keterangan, penanda **koreksi** dari `absensi_*.sumber`, tanggal yang ditetapkan libur ditandai tidak dihitung) dan navigasi bulan. Esc/× menutup panel.
- **Layanan asli:** rekap memakai `LayananLaporan.PerOrang` (aturan hari efektif dan libur P-13 tidak ditulis ulang). Riwayat memakai `RepositoriAbsensi.DaftarSiswaUntukRentang/DaftarGuruUntukRentang`, yang kini juga mengembalikan `Sumber`.
- **Hak akses:** wali kelas hanya membuka siswa kelasnya dan melihat riwayatnya; rekap tidak ditampilkan karena `LayananLaporan` menolak wali kelas (PRD §3, laporan hanya kepala sekolah/admin). Panel guru hanya untuk kepala sekolah/admin, mengikuti menu Guru.
- **Uji Chromium:** langkah baru membuka detail dari Absensi, Siswa dan Guru, memeriksa jam 07:30:00/07:30:01/13:10:00, rekap, kartu, pindah bulan, lebar 390 px, dan pembatasan rekap wali setelah reload.

**Butuh keputusan Edgar:**
- PRD §14.6 hanya mengatur panel **siswa** di menu Absensi. Panel guru dan pembukaan dari daftar Siswa/Guru ditambahkan atas laporan pemilik; isinya belum diatur PRD.
- Apakah wali kelas boleh melihat rekap bulan siswanya sendiri di panel? Saat ini tidak, karena PRD §3 menyatakan laporan hanya untuk kepala sekolah/admin.

**Batas:** tombol Cetak laporan dan Koreksi status di panel nonaktif sampai modulnya dibangun. Foto belum tampil (unggah foto belum ada di web). Data contoh tidak memiliki riwayat sebelum tanggal uji, sehingga bulan lampau tampil kosong sementara hari efektif tetap dihitung dari kalender; ketentuan P-18 (siswa masuk/keluar tengah periode) belum diterapkan pada perhitungan laporan.

**Bukti §13–§14, 5 November 2026 pukul 15.33–15.45 WITA, commit `75e2126`:** CI Windows Domain 66, Data 274 (8 tes baru `StoredTimeCultureTests`), Telegram 10, Laporan 32, App 266 — **648 lulus**, 0 gagal. Tes `IndonesianCulture_SeparatorIsDot` lulus di runner Windows, artinya risiko pemisah titik pada `id-ID` memang terjadi di sana. Uji Chromium **17 kelompok lulus** sebelum deploy dan 17 lulus pada hasil deploy Cloudflare (termasuk langkah panel detail). Belum ada pemeriksaan visual oleh pemilik dan belum ada UAT desktop/RFID fisik.

## 15. Modul Koreksi di web uji — 5 November 2026

**Keputusan pemilik (5 Nov):** isi panel detail siswa/guru disetujui. Wali kelas boleh melihat riwayat siswanya **dan mengedit** (mengoreksi) riwayat siswa kelasnya, karena wali kelas paling dekat dengan siswa. Ini sejalan dengan PRD §3/§3.2/§10. Data bulan lampau yang kosong pada tahap uji diterima pemilik.

- **Menu Koreksi:** pilih kelas dan tanggal, daftar dari `RepositoriAbsensi.DaftarUntukKoreksi`, tombol Ubah status per siswa. Wali kelas hanya melihat kelasnya sendiri.
- **Dari panel detail:** setiap baris riwayat siswa punya tombol Ubah bila akun berwenang atas kelas pada tanggal itu.
- **Formulir:** pilihan Hadir/Terlambat/Izin/Sakit/Alpa (PRD §16.10 langkah 4), alasan wajib, kolom "atas permintaan" (wali kelas pemohon) hanya untuk admin/kepala sekolah. Bulan terkunci menolak koreksi. Hasil berhasil baru tampil setelah status tersimpan dibaca ulang. Penyimpanan, otorisasi kelas, kunci bulan, audit dan antrean notifikasi tetap melalui `LayananKoreksi` asli; jam tap asli tidak diubah.
- **Perubahan layanan:** `LayananKoreksi.KoreksiStatusSiswa` kini menolak alasan kosong (PRD §10: alasan wajib, diperiksa di layanan). Tes lama yang mengoreksi tanpa alasan diperbarui; tes baru memastikan penolakan dan status tidak berubah. WPF menampilkan penolakan ini sebagai pesan biasa.
- **Uji Chromium:** admin mengoreksi dengan atas permintaan; alasan kosong tidak dapat disimpan; penanda koreksi tampil di panel; wali kelas mengoreksi dari panel tanpa kolom atas permintaan dan hanya melihat satu kelas.

**Utang:** alasan koreksi disimpan di kolom `keterangan` absensi; jejak audit mencatat status lama/baru dan pemohon, tetapi belum memuat alasan sebagai kolom audit tersendiri (PRD §10 menyebut audit mencatat alasan). Kunci bulan belum tersedia di web. Pesan notifikasi koreksi hanya masuk antrean outbox; pengiriman tetap nonaktif.

## 16. Unggah foto siswa dan guru di web uji — 5 November 2026

- **Perubahan:** panel detail siswa/guru punya tombol Unggah/Ganti foto untuk kepala sekolah/admin (PRD §16.4 langkah 6: foto susulan satu per satu). Wali kelas tidak mendapat tombol ini (PRD §3: master data hanya kewenangan penuh).
- **Pengolahan:** di browser foto dikecilkan seperti `PengolahFoto` desktop: muat ke 200×200 tanpa dipotong, ditengahkan, PNG. Berkas yang bukan gambar ditolak dengan pesan. Nama berkas mengikuti desktop (`<NISN>.png` / `<NIP>.png`) dan dicatat lewat `LayananSiswa/LayananGuru.SimpanFoto` asli (audit lewat `JalurPerubahanData`). Pesan berhasil baru tampil setelah foto tersimpan dan perubahan database berhasil.
- **Penyimpanan uji:** isi foto disimpan di IndexedDB (`foto/<nama>`), dimuat ke memori sekali saat aplikasi dibuka (PRD §5.5), dan disalin ke `/tmp/foto` untuk kode laporan berbasis berkas. Reset data contoh ikut menghapus foto. Tanpa foto tetap tampil lingkaran inisial.
- **Uji Chromium:** gambar 300×400 menjadi 200×200, teks ditolak, foto tampil di daftar siswa, foto guru, foto bertahan setelah reload, wali kelas tidak mendapat tombol unggah.

**Batas:** impor foto massal per folder (PRD §16.4 langkah 1–5) tetap tanpa entry point, sesuai revisi 2026-09-23. Bila pencatatan database gagal setelah foto tersimpan, berkas foto dengan nama yang sama sudah tertimpa (sama dengan perilaku desktop). Foto di situs uji publik harus foto contoh, bukan foto siswa asli (D-12).

**Bukti §15–§16, 5 November 2026 ±16.40 WITA, commit `fd3cf03`:** CI Windows 650 lulus (Data 276), uji Chromium 19 kelompok lulus sebelum dan sesudah deploy, termasuk koreksi dan unggah foto.

## 17. Modul Laporan di web uji — 5 November 2026

- **Menu Laporan** (kepala sekolah/admin): Harian, Mingguan (Senin–Sabtu), Bulanan (kolom libur diarsir, alasan libur dicatat), Per orang (satu orang atau satu kelas/seluruh guru) dan Semester, untuk siswa atau guru, dengan filter kelas. Pratinjau tampil di layar; Unduh Excel dan Unduh PDF memakai `PenulisExcelLaporan`/`PenulisPdfLaporan` asli, data dari `LayananLaporan`. Judul dan rentang mengikuti `LaporanViewModel` desktop. Tombol Cetak memakai dialog cetak browser untuk pratinjau.
- **Panel detail:** tombol Laporan Excel/PDF membuat laporan per siswa/guru (PRD §11.3/§17.8) untuk bulan yang sedang ditampilkan; foto ikut bila ada.
- **Wali kelas:** menu Laporan menampilkan pemberitahuan; `LayananLaporan` tetap menolak wali kelas (PRD §3).
- **PDF di browser:** QuestPDF memakai pustaka native. Uji Chromium mencatat hasil PDF sebagai catatan (bukan lulus/gagal) sampai terbukti; bila gagal, aplikasi menampilkan alasan dan menyarankan Excel atau Cetak.

**Butuh keputusan Edgar:** PRD §11.1 menyebut laporan mingguan "dipakai wali kelas", tetapi PRD §3 dan `LayananLaporan` membatasi laporan untuk kepala sekolah/admin. Keputusan 5 Nov mengizinkan wali kelas melihat dan mengoreksi riwayat siswanya, belum menyebut laporan/rekap. Saat ini wali kelas tidak mendapat rekap di panel dan tidak bisa membuka menu Laporan.

**Batas:** logo sekolah belum ada di web (unggah logo belum dibuat). Siswa masuk/keluar tengah periode (P-18) belum diterapkan pada perhitungan laporan.

**Bukti §17, 5 November 2026 ±17.15 WITA, commit `ab702c7`:** CI Windows lulus; uji Chromium **21 kelompok lulus** sebelum deploy dan 21 lulus pada hasil deploy Cloudflare. Excel dari menu Laporan dan dari panel per siswa terunduh sebagai berkas `.xlsx` valid. **PDF terbukti tidak dapat dibuat di browser:** `PenulisPdfLaporan` gagal dengan `TypeInitializationException` karena QuestPDF memerlukan pustaka native yang tidak tersedia di WebAssembly. Aplikasi menampilkan alasan tersebut dan menyarankan Excel atau Cetak → Simpan sebagai PDF dari browser (tata letak pratinjau, bukan tata letak `PenulisPdfLaporan`). Di arsitektur PRD, PDF dibuat backend ASP.NET Core (tahap 2), tempat QuestPDF berjalan normal; ini keterbatasan lingkungan uji browser, bukan bukti PDF produksi.

## 18. Penautan RFID guru di web uji — 7 November 2026

- **Laporan pemilik:** halaman Guru menampilkan UID/status kartu tetapi belum menyediakan aksi penautan. Ini kekurangan implementasi web terhadap PRD §16.15 dan §17.9–§17.10.
- **Perubahan:** guru aktif tanpa kartu mendapat tombol Daftarkan kartu. Dialog menampilkan nama/NIP, menerima pembacaan reader pada kolom RFID, mempertahankan nol depan, dan hanya menautkan setelah Simpan kartu ditekan. Enter dari reader tidak otomatis menyimpan. Tutup/Escape membuang UID sementara. UID aktif tampil pada baris guru dan panel detail yang sudah ada.
- **Integritas:** layanan C# memeriksa kewenangan admin/kepala sekolah, guru masih aktif/ada, keunikan UID lintas siswa/guru dan satu kartu aktif per guru. Pemeriksaan pemilik dan penulisan kartu/audit dilakukan dalam satu transaksi. Pesan berhasil memerlukan pembacaan ulang pasangan guru–UID. Penyimpanan browser tetap lewat IndexedDB yang sudah ada, dengan pesan kegagalan penyimpanan tetap ditampilkan.
- **Absensi:** reader bersama memakai PemrosesTap yang sudah ada untuk membedakan kartu guru dan siswa. Petunjuk Absensi kini menyebut kedua menu registrasi. Penautan pada dialog Guru tidak memanggil pemroses absensi.
- **Verifikasi yang disiapkan:** tes layanan untuk dua peran berwenang, duplikat lintas master, kartu kedua, guru tidak aktif/tidak ditemukan, penolakan wali kelas dan panjang UID. Uji Chromium menambah registrasi guru, konfirmasi manual, duplikat dua arah, batal, tap guru Masuk→Hadir, riwayat serta pemulihan UID/jam setelah reload pada tanggal yang sama. Menu Guru ikut pemeriksaan lebar desktop/HP. Hasil eksekusi dicatat setelah workflow selesai, belum dinyatakan lulus pada catatan ini.
- **Batas:** penggantian kartu guru yang sudah aktif, tambah/ubah/impor master guru dan tabel absensi guru harian masih perlu dilengkapi di web. Kartu terdaftar tidak diganti diam-diam. Riwayat absensi guru dapat dilihat dari klik nama guru. Pengujian reader asli untuk guru masih menunggu pemilik. Arsitektur produksi dan PRD tidak diubah.

**Bukti eksekusi, 7 November 2026 pukul 12.04–12.06 WITA, commit `9579a41`:** [CI Windows](https://github.com/patahpensil/absensi-smp1/actions/runs/42336534859) lulus **660 tes** (Domain 66, Data 286, Telegram 10, Laporan 32, App 266), termasuk 10 kasus layanan baru. [Workflow deploy](https://github.com/patahpensil/absensi-smp1/actions/runs/42336534931) lulus **23 kelompok browser sebelum deploy dan 23 sesudah deploy**. [Bukti browser terpisah](https://github.com/patahpensil/absensi-smp1/actions/runs/42336534926) juga lulus. Unggahan Cloudflare selesai pukul **12.04.52 WITA**, versi `https://a887e8e4.absensi-uji.pages.dev`, tautan tetap `https://absensi-uji.pages.dev/`. Pemeriksaan langsung pada tautan tetap menunjukkan tombol Daftarkan kartu per guru dan dialog nama/NIP dengan kolom RFID otomatis mendapat fokus. PDF browser tetap memiliki keterbatasan native QuestPDF pada §17, bukan dinyatakan berhasil oleh jumlah kelompok browser ini. Reader guru asli belum diuji.


## 19. Pengendali TV gerbang terpisah — 7 November 2026

- **Keputusan pemilik:** jalankan rancangan TV fullscreen terpisah dari dashboard. Pengendali gerbang hanya untuk kepala sekolah dan admin/TU; PRD §3 dan §14.1.1 diselaraskan.
- **Web uji:** menu Layar gerbang dan tombol pada dashboard membuka pengendali. Buka layar TV membuka jendela presentasi terpisah, dipindahkan manual ke TV pada Windows Extend, kemudian Fullscreen diaktifkan lewat tombol di jendela TV. Status pengendali membedakan terbuka/belum fullscreen/fullscreen. Pop-up yang diblokir diberi pesan. Esc tetap mengikuti browser; tidak memberi hak pengelolaan.
- **Pemisahan:** TV tidak membuka Blazor/SQLite kedua. Snapshot foto/nama/NISN atau NIP/hasil/jam/ringkasan dikirim dari satu pengendali. Semua keputusan tap tetap memakai PemrosesTap C# asli. Maksimal empat kartu besar selama delapan detik dan empat pembacaan terakhir, dengan foto dari cache atau placeholder jelas. Bunyi berhasil/ditolak memakai Web Audio, bergantung izin/interaksi browser.
- **Kontrol:** Buka TV memulai penerimaan. Hentikan penerimaan tap dan Tutup tampilan TV merupakan aksi berbeda, dikonfirmasi dengan sandi akun sendiri dan dicatat melalui jalur audit. Penutupan TV tetap memungkinkan input pada pengendali. Logout/penutupan pengendali mengakhiri sesi; TV menolak input dan membersihkan data bila heartbeat terputus. URL gate.html langsung tidak membuka sesi/data. Pesan dibatasi origin, referensi jendela pembuka/TV dan token sesi dalam memori, tanpa token dalam URL.
- **Otorisasi:** GateAccess memeriksa akun sesi sekaligus peran/keaktifan terbaru dari database pada setiap aksi dan pembacaan. Menu/perintah wali tidak memberikan pengendali. Pembukaan gerbang WPF juga diberi guard layanan di factory, dan command menolak wali walaupun dipanggil langsung. Penutupan WPF tetap memakai verifikasi kepala sekolah/admin yang sudah ada.
- **Pengujian disiapkan:** tes layanan untuk admin/kepala sekolah, wali, peran palsu, akun dicabut, tanpa sesi dan sandi salah. Tes command WPF untuk peran berwenang/ditolak. Chromium menguji jendela terpisah/fullscreen, kartu siswa/guru dan foto, penutupan vs penghentian, autentikasi konfirmasi, logout, URL langsung/pesan palsu dan larangan wali. Hasil dicatat setelah workflow selesai.
- **Batas terbuka:** TV fisik/HDMI, perizinan fullscreen pada perangkat pemilik, audio fisik, empat reader, konflik ketikan HID dan dua monitor belum dibuktikan. Input pada formulir tidak dicuri; fokus TV/kolom reader perlu dipulihkan saat mengisi formulir. Produksi perlu penanganan input perangkat dan sesi backend sesuai PRD, bukan asumsi browser selalu menangkap keyboard. Pemilihan monitor otomatis tidak diterapkan. Pemisahan kontrol stop/close dan snapshot TV baru diterapkan di web uji; WPF lama masih memerlukan penyelarasan alur stop/close dan pemilihan layar selain guard akses yang ditambahkan di perubahan ini. Belum lulus ASVS L3 dan tidak ada deployment produksi.

**Bukti akhir §19, 7 November 2026 pukul 12.44–12.46 WITA, commit `1d9e25e`:** [CI Windows](https://github.com/patahpensil/absensi-smp1/actions/runs/42339517269) lulus **671 tes** (Domain 66, Data 294, Telegram 10, Laporan 32, App 269). [Deploy Cloudflare](https://github.com/patahpensil/absensi-smp1/actions/runs/42339517221) lulus **25 kelompok browser sebelum dan 25 sesudah deploy**; [bukti browser terpisah](https://github.com/patahpensil/absensi-smp1/actions/runs/42339517281) juga lulus. Pengujian meliputi fullscreen nyata melalui Fullscreen API di Chromium, dua jendela, foto siswa/guru, status kartu, konfirmasi sandi salah/benar, stop tanpa menutup TV, close TV tanpa stop, reader Absensi terkunci saat stop, penolakan URL TV langsung/pesan palsu, logout menutup TV serta larangan menu/perintah wali. Percobaan awal menolak deploy karena perintah gerbang terlarang diperlakukan sebagai pencarian; sudah diperbaiki menjadi pesan penolakan eksplisit, tanpa membuka akses wali. Data persistensi dan jam diuji pada tanggal yang sama.

Deploy selesai pukul **12.45.05 WITA** pada `https://c1e4bd7b.absensi-uji.pages.dev`, tautan tetap `https://absensi-uji.pages.dev/`. Pemeriksaan langsung tautan tetap membuka pengendali melalui akun contoh Admin, jendela TV terpisah dan tombol Fullscreen; pengendali menampilkan **TV fullscreen**, sedangkan TV tidak memuat menu admin. Dashboard diperbarui setelah tap dari TV. PRD §14.1.1 memuat keputusan yang disetujui. Hasil ini tidak membuktikan TV/HDMI/reader fisik, empat reader bersamaan, audio fisik atau ASVS L3. Keterbatasan PDF browser §17 tetap berlaku; kelompok browser yang mencatat kegagalan native PDF tidak menyatakan ekspor PDF berhasil.

## 20. Keputusan kanal notifikasi P-19 — 12 November 2026

- **Keputusan pemilik:** kanal notifikasi mendukung dua alat, Telegram dan WhatsApp. Dicatat pada PRD §1, §9, §20, §21.6, D-03 dan §25.5.
- **Belum diputuskan (D-03):** penyedia/API WhatsApp, cara orang tua/wali memilih dan menautkan kanal, isi pesan minimal, biaya per pesan, serta aliran data ke pihak ketiga di luar Indonesia. P-16 tetap ditangguhkan.
- **Belum dikerjakan:** kode pengiriman WhatsApp, perubahan skema (`pairing`/`outbox` saat ini khusus Telegram `chat_id`) dan pengujiannya. Perubahan ini hanya dokumen; tidak ada perubahan kode atau deploy.
- **Utang catatan:** commit `1805176`, `49ada45` dan `18c8120` (11–12 November) belum dicatat di berkas ini oleh pembuatnya.

## 21. Kanal notifikasi orang tua Telegram/WhatsApp di web uji — 12 November 2026

**Acuan:** PRD §9, P-19, P-20, D-03.

- **Data:** migrasi 007 menambah `siswa.notification_channel` dan `outbox.channel` (`telegram`/`whatsapp`, bawaan `telegram` sehingga data lama tidak berubah perilaku). `ParentRecipientResolver` menjadi satu-satunya penentu tujuan pesan orang tua: rekap, Alpa di batas akhir, Terlambat real-time dan koreksi/ralat. WhatsApp memakai `no_hp_ortu` langsung, dinormalisasi ke `62…` oleh `ParentPhoneNumber` (Domain); nomor kosong/tidak valid berarti pesan tidak dibuat. Kolom `outbox.chat_id` menyimpan alamat kanal (chat id Telegram atau nomor WhatsApp).
- **Kewenangan dan audit:** `ParentChannelService.SetChannel` hanya untuk Kepala Sekolah/Admin, menolak WhatsApp bila nomor belum valid, dan mencatat `notification_channel` lama→baru di audit (`ubah_kanal_notifikasi`).
- **Pengirim:** `PengirimOutbox` (Telegram) hanya mengambil baris kanal Telegram (`ListQueuedForChannel`); baris WhatsApp tetap `antre` karena pengirim WhatsApp belum ada.
- **Web uji:** panel detail siswa menampilkan kanal; Kepala Sekolah/Admin dapat memilih Telegram atau WhatsApp. Nomor HP hanya terlihat oleh akun pengelola. Halaman Notifikasi menampilkan kolom dan filter Kanal. Tidak ada pesan yang dikirim (P-20).
- **Tes disiapkan:** Domain `ParentPhoneNumberTests`; Data `ParentNotificationChannelTests` (bawaan Telegram, rekap/koreksi/tap real-time ke WhatsApp, nomor tidak valid, penolakan wali kelas, audit, pemisahan antrean); Telegram `KirimAntreanAsync_WhatsAppRow_NotSentByTelegramAndStaysQueued`; Chromium langkah "Kanal notifikasi orang tua".

- **Perbaikan selama uji:** setelah kanal tersimpan, tombol Simpan menjadi nonaktif dan fokus keyboard keluar dari panel sehingga Esc tidak menutupnya; fokus kini dikembalikan ke panel (`5bf8e46`).

**Bukti, 12 November 2026 ±05.45 WITA, commit `50e7f05`:** [CI Windows](https://github.com/patahpensil/absensi-smp1/actions/runs/43105025248) lulus (build dan seluruh tes, termasuk tes baru Domain/Data/Telegram). [Bukti browser](https://github.com/patahpensil/absensi-smp1/actions/runs/43105025370) lulus. [Deploy Cloudflare](https://github.com/patahpensil/absensi-smp1/actions/runs/43105025910) lulus **34 langkah browser sebelum dan 34 sesudah deploy**, termasuk penolakan WhatsApp tanpa nomor HP, normalisasi `080000000001` → `6280000000001`, tap Terlambat 08.20 masuk antrean kanal WhatsApp (bukan Telegram) dan audit `ubah_kanal_notifikasi`. Versi `https://cd17d8a5.absensi-uji.pages.dev`, tautan tetap `https://absensi-uji.pages.dev/`. Belum ada pemeriksaan visual oleh pemilik.

**Belum dikerjakan / butuh keputusan Edgar (D-03):** penyedia/API WhatsApp dan pengirimnya; kanal untuk wali kelas dan guru (masih Telegram); kolom kanal pada impor Excel siswa (siswa hasil impor memakai Telegram); tampilan kanal di aplikasi desktop WPF; aliran data ke pihak ketiga luar negeri dan biaya per pesan.

## 22. Sisa master data dan operasional sekolah di web uji — 12 November 2026

**Acuan:** PRD §3.1, §5.8, §7 P-15, §16.17; PANDUAN-UI-UX §9.

**Master data (commit `09dcf6f`, `4a92095`):**
- **Mutasi siswa:** `LayananSiswa.TandaiTidakAktif` kini mewajibkan alasan (pindah/keluar + catatan) dan menonaktifkan kartu aktif siswa dalam transaksi yang sama (PRD §5.8). Audit mencatat `status` dan `kartu_aktif`. Riwayat absensi tetap.
- **Nonaktif guru:** alasan pensiun/pindah tugas/berhenti lewat dialog yang sama (`MutationDialog`).
- **Manajemen akun (Pengaturan):** buat akun, atur ulang sandi, nonaktifkan, mengikuti hierarki §3.1 (Kepala Sekolah → Admin, Admin → Wali Kelas). `LayananAkun` kini memvalidasi username (`a–z 0–9 . _`, 3–32), sandi minimal 8, dan menolak username ganda.
- Bukti: [CI](https://github.com/patahpensil/absensi-smp1/actions/runs/43134963849), [bukti browser](https://github.com/patahpensil/absensi-smp1/actions/runs/43134964029), [deploy](https://github.com/patahpensil/absensi-smp1/actions/runs/43134963836) lulus.

**Operasional (commit `f920766`…`f561d21`):**
- **Identitas sekolah (Pengaturan):** `SchoolIdentityService` — nama wajib (≤120), NPSN 8 angka bila diisi, alamat ≤250, Kepala Sekolah/Admin saja, audit `ubah_identitas_sekolah`. Judul aplikasi langsung memakai nama baru.
- **Jadwal harian berlaku mulai tanggal (P-15):** migrasi 008 tabel `schedule_change`; `ScheduleTimeline` memilih versi terakhir yang berlaku pada tanggal itu, kalau tidak ada memakai `jadwal_hari`. `LayananJadwalHari.ScheduleFrom` menolak tanggal hari ini/lampau dan di luar periode, menolak urutan jam yang salah (pesan menyebut harinya), audit `jadwal_berlaku_mulai` (lama→baru per hari). Pemroses tap, pembukaan hari, kalender (hari sekolah/libur), Notifikasi dan panel jadwal dasbor memakai jadwal pada tanggal yang bersangkutan. Pengaturan: lihat jadwal untuk tanggal tertentu, daftar versi, dan isian 7 hari "berlaku mulai" (bawaan besok).
- **Kehadiran guru (§16.17, menu Guru):** Kepala Sekolah memilih tanggal; tanggal mendatang hanya Izin/Dinas luar/Cuti, tanggal hari ini/lampau Hadir/Izin/Sakit/Dinas luar/Cuti/Alpa; keterangan wajib. Memakai `LayananStatusGuru` (kunci bulan dan audit `tetapkan_status_guru`). Admin hanya melihat.
- **Tes disiapkan:** Data `ScheduleAndSchoolSettingsTests` (versi lama/baru per tanggal, tap Senin lama Hadir vs Senin baru Terlambat, Sabtu jadi libur hanya sejak tanggal berlaku, penolakan tanggal/peran/urutan jam, audit, identitas sekolah). Chromium 4 langkah baru.
- **Perbaikan selama uji:** `@bind` pada `input type=time` dengan properti teks gagal dikompilasi (`c8028c5`); langkah audit dipindah ke akun Admin karena menu Audit memang hanya untuk Admin (`f561d21`).

**Bukti, 12 November 2026 ±10.20 WITA, commit `f561d21`:** [CI Windows](https://github.com/patahpensil/absensi-smp1/actions/runs/43140007982) lulus (build dan seluruh tes). [Bukti browser](https://github.com/patahpensil/absensi-smp1/actions/runs/43140007865) lulus. [Deploy Cloudflare](https://github.com/patahpensil/absensi-smp1/actions/runs/43140007983) lulus **40 langkah browser sebelum dan 40 sesudah deploy**. Versi `https://2a5aee99.absensi-uji.pages.dev`. Belum ada pemeriksaan visual oleh pemilik.

**Belum dikerjakan / utang:**
- **P-18 tanggal mulai/berakhir siswa:** ditunda (belum ada kolom dan aturan laporannya). Perlu dibahas dengan Edgar.
- **Impor foto massal:** tidak dibuat, sesuai instruksi Edgar menghapusnya.
- **Desktop WPF masih memakai `LayananJadwalHari.Perbarui`** yang mengubah jadwal secara retroaktif — bertentangan dengan P-15. Perlu dipindah ke `ScheduleFrom`.
- **Periode akademik baru menyalin jadwal dasar saja**, bukan versi jadwal terjadwal di periode aktif.
- **Kehadiran guru di muka:** belum ada tes yang membuktikan status Dinas luar tetap benar ketika hari itu tiba dan guru tidak tap.
- Backup dan token bot tidak berlaku di web uji (browser lokal); tetap fitur desktop.

## 23. Peta prioritas sisa pekerjaan dan P0 — 12 November 2026

**Keputusan pemilik (12 November 2026):** pekerjaan versi online — backend ASP.NET Core/PostgreSQL, isolasi sekolah, MFA, antrean tap offline dan waktu terverifikasi (P-08…P-12), pengirim WhatsApp, VALIDASI-01 — **ditangguhkan**. D-01…D-12 tetap tercatat, tidak dibahas sampai pemilik membukanya kembali.

**Urutan kerja yang dipakai:**
- **P0 — data salah / menyimpang dari keputusan PRD:** jadwal desktop retroaktif (P-15), periode baru tidak menyalin versi jadwal, alasan dan tanggal koreksi belum masuk audit (§16.10 langkah 7), keterangan status guru belum wajib di layanan (§16.17 langkah 4), `HashSandi` dengan hash rusak.
- **P1 — web uji sesuai PANDUAN-UI-UX §9:** PDF di browser, saringan audit per akun/tanggal dan 90 hari, unggah logo, penautan Telegram dan status bot (simulasi), kolom kanal pada impor Excel, status lisensi di login, wizard instalasi, pemeriksaan visual pemilik.
- **P2 — menunggu keputusan pemilik:** P-18, T3, laporan untuk wali kelas (PRD §11.1 vs §3), P-14, PR Dependabot versi mayor.
- **P3 — desktop, UAT fisik, kebersihan:** penanganan galat ViewModel, backup lokal tak terenkripsi, rotasi cadangan pra-restore, audit kultur parse tanggal, WASM0001, register A-xx, DOC-01.

**P0 yang dikerjakan:**
- **Jadwal desktop (P-15):** `LayananJadwalHari.Save` menjadi satu pintu simpan layar Pengaturan desktop. Periode yang belum punya jadwal sama sekali mendapat jadwal dasar (pengisian awal); bila jadwal sudah ada, perubahan disimpan lewat `ScheduleFrom` dengan tanggal "Berlaku mulai" (bawaan besok, DatePicker baru). Jadwal tanggal lampau tidak lagi ditimpa. `Perbarui` tetap ada untuk pengisian awal dan dipakai tes lama.
- **Periode akademik baru:** menyalin jadwal yang berlaku pada tanggal mulai periode baru, termasuk versi terjadwal.
- **Audit koreksi:** `koreksi_status_siswa` kini mencatat `tanggal=…; status=…` lama dan `…; alasan=…` baru; `tetapkan_status_guru` mencatat tanggal dan keterangan. Keterangan status guru wajib diisi di layanan.
- **`HashSandi.Verifikasi`:** iterasi bukan angka/≤0/terlalu besar, base64 rusak atau kosong → `false`, bukan exception yang menghentikan login.
- **Status guru di muka saat harinya tiba:** sudah dibuktikan tes lama `TetapkanStatus_DiMuka_TidakTertimpaSaatPembukaanHariBerjalan`; finalisasi akhir hari hanya menyentuh baris `masuk` (`DaftarGuruMasukTanpaPulang`), jadi Izin/Dinas luar/Cuti tidak diubah. Utang ini ditutup tanpa kode baru.
- **Tes disiapkan:** App `SimpanJadwalCommand_JadwalSudahAda_BerlakuMulaiBesokTanpaMenimpaJadwalLama`, `SimpanJadwalCommand_TanggalBerlakuHariIni_DitolakTanpaPerubahan`; Data `NewAcademicPeriod_CopiesScheduleInForceAtItsStart`, `TetapkanStatus_KeteranganKosong_DitolakTanpaPerubahan`, `TetapkanStatus_DiMuka_AuditMemuatTanggalDanKeterangan`, `Verifikasi_HashRusak_MengembalikanFalseTanpaException`; tes audit koreksi diperbarui ke format baru.

**Belum terbukti:** tampilan DatePicker "Berlaku mulai" di WPF belum dilihat di Windows (hanya build dan tes App di CI).

**Bukti P0, 12 November 2026, commit `3af77e2`:** [CI Windows](https://github.com/patahpensil/absensi-smp1/actions/runs/43144316837) lulus (build dan seluruh tes Domain/Data/Telegram/Laporan/App). Percobaan pertama (`e05c663`) gagal di `PengaturanScrollTests` karena baris "Berlaku mulai" menambah tinggi kartu Jadwal Harian sehingga celah antar kartu keluar dari tampilan; DatePicker dipindah sebaris dengan tombol Simpan (`3af77e2`).

**P1 yang dikerjakan (commit `…2bf5829`):**
- **Audit:** saringan per akun (`#audit-account`). Saringan tanggal dan 90 hari bawaan ternyata sudah ada sejak sebelumnya (catatan daftar sisa pekerjaan sebelumnya keliru menyebutnya belum ada).
- **Logo sekolah:** `SchoolIdentityService.SetLogo` (nama berkas dibatasi `*.png` tanpa folder, Kepala Sekolah/Admin, audit `ubah_logo_sekolah`); unggah di Pengaturan → Identitas sekolah, dikecilkan seperti foto, tampil di judul aplikasi, layar masuk, dan kepala laporan.
- **Layar masuk:** status lisensi dengan teks yang sama seperti desktop (`RepositoriLisensi.BacaStatus`).
- **Bukti:** [CI](https://github.com/patahpensil/absensi-smp1/actions/runs/43144888493) lulus; [deploy Cloudflare](https://github.com/patahpensil/absensi-smp1/actions/runs/43144888533) lulus 40 langkah sebelum dan 40 sesudah deploy, versi `https://4f44741d.absensi-uji.pages.dev`.

**P1 yang belum dikerjakan:**
- **PDF di browser:** dugaan kuat pustaka QuestPDF memerlukan pustaka native yang tidak tersedia untuk WebAssembly; belum diselidiki tuntas. Jalur yang berfungsi: Cetak → Simpan sebagai PDF, dan Excel.
- **Penautan Telegram dan status bot (simulasi):** **ditangguhkan atas keputusan pemilik (12 November 2026, 11.16 WITA):** simulasi kanal Telegram dan WhatsApp dikerjakan bersamaan dengan versi online (poin E).
- **Wizard instalasi** di web uji belum dibuat.
- **Kolom kanal pada impor Excel** dipindah ke P2: format kolom impor (PRD §5.4) belum memuat kanal dan termasuk rincian D-03.

## 24. Penyelesaian sisa prioritas selain versi online — 12 November 2026

**Keputusan pemilik (12 November 2026, 11.37 WITA, lewat pilihan di chat):** T3 finalisasi saat aplikasi dibuka; P-18 otomatis dari data; backup lokal dienkripsi kunci lisensi; wizard web dipilih dari "Mulai ulang pengujian". Dicatat di PRD §5.3 dan §25.5.

**Selesai:**
- **T3** (`ffedbbf`): `LocalAttendanceClosure.FinalizeMissedDays` dipanggil dari `PembukaanHari` — hari sekolah lampau yang dibuka tetapi belum ditutup diberi aturan akhir hari (Bolos siswa kecuali hasil koreksi, Tidak lengkap guru), tanpa notifikasi, tanpa menutup hari; bulan terkunci dan tanggal libur dilewati. Tes `MissedDayFinalizationTests`.
- **P-18** (`2512cfc`): migrasi 009 `siswa_kelas.start_date/end_date`. Tanggal mulai otomatis bila siswa ditambahkan/diimpor ke kelas setelah periode berjalan; hari terakhir otomatis saat mutasi (dicatat di audit `hari_terakhir`). Laporan Bulanan, Per orang, Per kelas dan Semester tidak menghitung hari di luar rentang (bukan Alpa, bukan penyebut). Tes `StudentEnrollmentCoverageTests`.
- **Backup lokal terenkripsi** (`ed922c3`): `backup/*.db` ditulis AES-GCM dengan penanda `ABSENSI-BACKUP-1` bila lisensi aktif; restore mendekripsi dengan kunci lisensi aplikasi; kunci lain/berkas rusak → restore batal tanpa audit dan tanpa menimpa; backup lama SQLite polos tetap bisa dipulihkan.
- **Wizard instalasi web** (`4c1bbfa`): enam langkah memakai `LayananWizard`; aturan isian (NPSN, tanggal, urutan jam, username, sandi) kini diperiksa di `LayananWizard` sehingga desktop dan web menolak hal yang sama; wizard desktop menampilkan penolakan sebagai pesan.
- **PDF di browser** (`7acec31`): QuestPDF tidak lagi mendukung WebAssembly sejak 2024.3 ([dokumentasi QuestPDF](https://www.questpdf.com/going-production/platform-specific-dependencies.html)); tombol PDF di web diganti "Cetak / Simpan PDF". Desktop tetap membuat PDF.
- **A-39** ternyata sudah selesai (Tahap 1 dan 2 ada di kode); status register diperbarui.
- **DOC-01 (sebagian):** `SKEMA-DATABASE.md` memuat migrasi 006–009, finalisasi hari terlewat dan backup terenkripsi.

**Bukti:** commit `4c1bbfa` — [CI](https://github.com/patahpensil/absensi-smp1/actions/runs/43150852265), [bukti browser](https://github.com/patahpensil/absensi-smp1/actions/runs/43150852201) dan [deploy](https://github.com/patahpensil/absensi-smp1/actions/runs/43150852214) lulus, termasuk langkah Chromium wizard instalasi.

**Belum / utang:**
- Pengubahan manual tanggal mulai/hari terakhir siswa (P-18) oleh admin belum ada; laporan Harian/Mingguan (daftar kejadian) masih menampilkan baris di luar cakupan.
- Arsip audit pra-restore (`backup/arsip-audit/`) belum dienkripsi.
- Layar masuk web setelah wizard masih menampilkan daftar akun contoh yang tidak berlaku.
- Pemulihan dari backup tidak tersedia di wizard web (browser tidak punya folder backup).
- DOC-01 sisa: `PANDUAN-PEMASANGAN.md`, `PANDUAN-TESTER.md`, `RENCANA-TUGAS.md`.
- UAT perangkat fisik (Windows, reader RFID, TV) tetap harus dilakukan pemilik.

**Bukti tambahan:** `7acec31` (PDF web) diverifikasi oleh [bukti browser](https://github.com/patahpensil/absensi-smp1/actions/runs/43151481359) dan [deploy](https://github.com/patahpensil/absensi-smp1/actions/runs/43151481273) pada `9c94f04` (semua langkah lulus, versi `https://1618e1b3.absensi-uji.pages.dev`). Perubahan setelah `4c1bbfa` hanya lapisan web dan dokumen.

**Kendala (12 November 2026 ±11.57 WITA):** workflow CI Windows tidak dijalankan GitHub — anotasi: *"The job was not started because recent account payments have failed or your spending limit needs to be increased."* Pemilik perlu memeriksa Settings → Billing & plans di GitHub. Selama itu build/tes Windows (Domain/Data/Telegram/Laporan/App) tidak berjalan untuk commit baru; workflow web (Ubuntu) masih berjalan.

**Penyelesaian kendala (12 November 2026, ±14.26 WITA):** pemilik menjadikan repo **public** sehingga GitHub Actions tidak lagi memakai jatah menit akun gratis. Sebelumnya isi repo (178 commit, semua cabang/tag) diperiksa: tidak ada token/kunci asli, token Cloudflare hanya di GitHub Secrets, data di `arsip/` sintetis. [CI Windows](https://github.com/patahpensil/absensi-smp1/actions/runs/43168213412) dijalankan ulang untuk HEAD `f9a5eb9` dan **lulus** (build dan seluruh tes).


## 25. Pembersihan tampilan terhadap PANDUAN-UI-UX — 12 November 2026

**Permintaan pemilik:** pastikan tampilan tidak memuat ciri desain asal jadi.

**Dikerjakan:**
- **Teks:** rujukan "(PRD §…)" dihapus dari semua pesan yang tampil ke pengguna (layanan Data, desktop, web). Kode hiasan di kepala panel ("MASTER / 01", "02 / WATCHLIST", "RFID / UJI BROWSER", "§16.17", dll.), jejak navigasi kapital ("OPERASIONAL / LAPORAN"), "TERMINAL 01", "WEB UJI / 01", slogan layar masuk dan dasbor, serta jam statis di bilah status (tidak mengikuti jadwal sebenarnya) dihapus. Label kapital menjadi sentence case; layar gerbang memakai "Guru"/"Siswa" (§4.3).
- **Gaya web (`app.css`):** token palet §3.1/§3.2; tiga radius 6/10/999 (§5.2); dua bayangan (§5.3); gradasi, blur dan lingkaran hiasan dihapus; jarak huruf dihapus; seluruh padding/margin/gap di kisi 4 px (§5.1); skala huruf 12/14/18/22/28 (§4.2, sebelumnya ada 7–11 px); tinggi tombol dan kolom isian 36 px; fokus kolom isian garis biru 2 px (§6.2).
- **Huruf:** IBM Plex Sans (OFL) dibundel di web (`wwwroot/fonts`) dan desktop (`Resources/Fonts`, resource WPF); angka tabular tanpa huruf monospace terpisah (§4.1).
- **Layar gerbang (`gate.css`):** kartu berlatar warna status penuh (§8.2); nama 64 px, status/jam 40 px, NISN/NIP 28 px, label 24 px, foto 3:4 (§8.1, menyesuaikan layar lebih kecil).
- **Satu tombol utama per layar (§6.1):** tombol simpan di Pengaturan menjadi sekunder; "Kunci bulan" memakai gaya bahaya.
- **Bukti otomatis:** langkah Chromium baru memeriksa gaya terhitung di 11 menu (±2.300 elemen terlihat): IBM Plex termuat, radius hanya 0/6/10/999/50%, bayangan hanya dua tingkat, tanpa gradasi, tanpa jarak huruf, tanpa kapital paksa atau label kapital. [CI](https://github.com/patahpensil/absensi-smp1/actions), bukti browser dan deploy lulus.

**Belum terbukti / menunggu keputusan:**
- Belum dilihat langsung oleh pemilik; lingkungan agen tidak dapat membuka situs uji untuk tangkapan layar.
- Font di desktop WPF baru terbukti terkompilasi dan tes App lulus; tampilannya di Windows belum dilihat.
- **Warna tombol Bahaya:** §3 tidak punya merah umum dan §3.3 melarang warna status untuk tombol. Web memakai merah Alpa `#9B2C2C`, desktop memakai `#B3261E` (sementara). Perlu keputusan pemilik: satu warna resmi ditambahkan ke §3.1.
- Layar gerbang (jendela TV) tidak ikut diperiksa langkah otomatis di atas.

## 26. Audit hasil kerja agen lain (PR #20) dan perbaikannya — 12 November 2026

**Yang diaudit:** commit `3ad73a7`, `62183ae`, `9bd55f4`, `6cb1555` (masuk lewat PR #20). Hasil yang terbukti benar oleh [CI](https://github.com/patahpensil/absensi-smp1/actions/runs/43175522547) dan [deploy](https://github.com/patahpensil/absensi-smp1/actions/runs/43175522591): validasi backup sebelum restore (integrity_check, tabel wajib, foreign_key_check), laporan kejadian mengikuti rentang `siswa_kelas`, penanganan konflik dua tab web, gerbang empat kartu bernama panjang.

**Temuan:**
1. Arsip audit pra-restore dienkripsi dengan kunci DPAPI Windows (`data/audit-archive-key.dpapi`). Arsip tidak terbaca lagi setelah PC diganti atau Windows dipasang ulang. `CryptographicException` dari DPAPI tidak ditangani `PenangananGalat`. Bila berkas kunci hilang, restore terblokir permanen. Cara ini juga menyimpang dari keputusan backup lokal memakai kunci lisensi.
2. Fitur di luar PRD tanpa pembaruan PRD: mode reader serial/COM, .NET 10, dan DPAPI.
3. Pelanggaran cara kerja: memakai cabang + PR, satu commit besar `3ad73a7` dengan format `fix:` tanpa ruang, commit merah `62183ae` di master, dan PROGRES tidak diperbarui.
4. Tes arsip audit dipangkas dari 9 menjadi 2; tidak ada tes kunci salah/hilang atau kegagalan tulis.
5. Kecil: nama panjang di TV diperkecil sampai 36 px tanpa persetujuan (§8.1: 64 px); status reader tanpa "x dari y" (PRD §2); paket UAT tanpa tanda tangan dan kedaluwarsa 30 hari; PR Dependabot lama (#10, #21, #23, #28, #32) masih terbuka.

**Keputusan pemilik:** jangan pakai DPAPI. Dicatat di PRD §25.5 beserta tiga hal yang menunggu keputusan (reader serial, .NET 10, ukuran 36 px).

**Perbaikan temuan 1 dan 4:** `LocalAuditArchiveKey` dan paket `System.Security.Cryptography.ProtectedData` dihapus. `LayananArsipAudit` menulis penanda `ABSENSI-AUDIT-2` (AES-GCM, kunci lisensi) atau `ABSENSI-AUDIT-P` (polos, lisensi belum aktif). Layar Audit membuka arsip dengan kunci lisensi database aktif. Semua kegagalan baca (kunci lain, lisensi belum aktif, berkas rusak, arsip DPAPI lama) menjadi pesan di daftar arsip bermasalah, bukan galat tak tertangani. Tidak ada lagi berkas kunci terpisah yang bisa hilang. Tes arsip menjadi 9 (`LayananArsipAuditTests`) ditambah 2 tes restore di `LayananBackupTests`: arsip terenkripsi saat lisensi aktif dan tidak ikut rotasi 7 berkas; arsip gagal ditulis → restore batal tanpa baris `restore_backup`. `PANDUAN-PEMASANGAN.md` diperbarui.

**Perbaikan temuan 5 (status reader):** pada mode serial, status gerbang kini menampilkan "x dari y reader terhubung" dan memperbaruinya setiap detik dan saat port melapor gangguan. Mode keyboard tetap tanpa hitungan karena perangkat tidak dapat dibedakan (§4). Belum dicoba dengan reader serial fisik.

**Dokumen:** `RENCANA-TUGAS.md` tidak lagi menyebut .NET 8 sebagai versi aktif.

**Yang harus diputuskan atau dilakukan pemilik:** reader serial (masuk §4 atau dihapus), .NET 10, ukuran nama 36 px di TV, warna tombol Bahaya (§25 di atas). Tutup sendiri PR Dependabot lama #10, #21, #23, #28, #32 (sudah tidak relevan setelah .NET 10). Temuan 3 (cara kerja) tidak bisa diperbaiki mundur tanpa menulis ulang riwayat master; dicatat saja.

**Keputusan pemilik (12 November 2026, ±17.00 WITA):** reader serial/COM menjadi mode opsional (PRD §4); warna tombol Bahaya `#9B2C2C` (PANDUAN-UI-UX §3.1), desktop diubah dari `#B3261E` agar sama dengan web; register A-16 selesai. .NET 10 dan ukuran nama 36 px masih menunggu.

**Keputusan pemilik (12 November 2026, ±17.20 WITA):** .NET 10 disetujui (PRD §25.5). Nama di TV 64 px dipilih, tetapi belum diterapkan: pengukuran di Chrome (1920×1080, empat kartu) menunjukkan nama 37 huruf menjadi 5 baris, nama 82 huruf 9 baris, dan status/jam keluar layar. Tangkapan layar dikirim ke pemilik untuk dikonfirmasi.

**Kerentanan dependensi:** daftar peringatan Dependabot tidak bisa dibaca agen (403). Dicocokkan dengan PR keamanan terbuka dan data OSV: playwright 1.48.0 (CVE-2025-59288, hanya skrip unduh browser macOS; CI memakai Ubuntu) sempat dinaikkan ke 1.55.1 (`7931782`), tetapi [bukti browser](https://github.com/patahpensil/absensi-smp1/actions/runs/43191244107) gagal 21 langkah (data hilang setelah muat ulang, klik timeout). Kenaikan dibatalkan; penyebabnya belum diketahui dan menjadi utang.
- `arsip/proyek-python-lama`: python-multipart 0.0.20 → 0.0.31 dan pytest 8.3.4 → 9.0.3 agar peringatan hilang. Proyek ini arsip dan tidak dijalankan, jadi perubahan versi tidak diuji. Kerentanan paket NuGet aplikasi utama belum bisa diperiksa agen; pemilik dapat membuka tab Security → Dependabot di GitHub.

**Keputusan pemilik (12 November 2026, ±18.00 WITA):** ukuran nama TV tetap seperti sekarang (nama pendek 64 px, nama panjang mengecil). Dicatat di PRD §25.5 dan PANDUAN-UI-UX §8.1. Tidak ada lagi keputusan yang menunggu dari audit PR #20.

## 27. Mode nyata web dan cadangan web — 12 November 2026

**Keputusan pemilik:** uji coba data asli lewat web mode nyata di repo `Absensi-Uji-Real` (PRD §25.5). Kode dikembangkan di repo ini; repo baru hanya salinan dengan `"Mode": "nyata"`.

**Selesai (bukti: langkah Chromium "Mode nyata …" dan "Cadangan web …"):** `ModeAplikasi` dari `appsettings.json`; mode nyata memulai wizard pada database kosong, tanpa akun contoh, tanpa jam skenario, label "Uji coba data asli", reset hanya ke wizard dan hanya untuk Admin/Kepala Sekolah. Cadangan web `.zip` (database + foto) diunduh dan dipulihkan dari Pengaturan; database divalidasi dengan `LayananBackup.TerapkanRestore`.

**Belum/utang:** pemulihan web tidak menulis audit restore; berkas cadangan tidak terenkripsi (AES-GCM tidak tersedia di WebAssembly — belum diuji alternatifnya); notifikasi orang tua tetap tidak dikirim dari web.

**Repo Absensi-Uji-Real:** diisi salinan bersih dari `77b5f0f` (commit `f7a3083`, cabang `main`, tanpa `arsip/`, tanpa riwayat). Workflow deploy menguji mode uji dan nyata sebelum deploy, lalu memeriksa hasil deploy dengan `HANYA_MODE_NYATA=1`. Deploy pertama [gagal karena secret Cloudflare belum diisi](https://github.com/patahpensil/Absensi-Uji-Real/actions/runs/43226435744); menunggu pemilik mengisi `CLOUDFLARE_API_TOKEN` dan `CLOUDFLARE_ACCOUNT_ID`. Penyalinan perubahan berikutnya dilakukan manual atas permintaan pemilik.
- Pembaruan: secret diisi pemilik; [deploy](https://github.com/patahpensil/Absensi-Uji-Real/actions/runs/43231432663) membuat proyek Pages `absensi-uji-real` di akun Cloudflare Patahpensil01 dan lulus pemeriksaan Chromium mode nyata pada `https://b0351c62.absensi-uji-real.pages.dev` (percobaan pertama gagal SSL karena sertifikat proyek baru belum siap). Alamat produksi `https://absensi-uji-real.pages.dev` belum dicek langsung oleh agen (proxy memblokir).
- Perbaikan teks mode nyata: catatan penyimpanan tidak lagi menyuruh menjalankan wizard setelah instalasi selesai; panel "Cara menguji" di Absensi diganti "Cara memakai" tanpa menyebut waktu uji.

## 28. PRD versi 2.0 mengikuti aplikasi yang ada — 13 November 2026

**Instruksi pemilik:** susun ulang PRD mengikuti aplikasi yang ada. Pilihan pemilik: nomor bab tetap, cakupan web + desktop (web utama), bagian belum dibangun ditandai Rencana.

**Selesai:** kepala dokumen versi 2.0 dengan cara membaca dan label status; §0 baru (bentuk aplikasi, tabel fitur web/desktop, batas uji coba data asli); kotak "Status aplikasi" di setiap bab 1–25 dan subbab yang menyimpang (14.x, 16.x); koreksi teks foto massal, menu, wizard. Klaim status diperiksa terhadap kode (`Absensi.Web`, `Absensi.App`, `Absensi.Data`) dan tes Chromium; keputusan P-01–P-20 dan D-01–D-11 tidak diubah.

**Belum/perlu diperhatikan:** isi rinci bab rancangan online (§13, §18, §21–§23) dibiarkan sebagai Rencana tanpa ditulis ulang. Klaim pengiriman Telegram desktop berasal dari kode, belum diuji pemilik dengan bot nyata.

## 29. Keputusan hak akses P-21/P-22 — 13 November 2026

**Instruksi pemilik:** catat keputusan yang sudah disetujui di PRD, lalu bahas bentuk akses sekolah secara terpisah.

**Selesai (dokumen):** PRD §3 (P-21, P-22), §3.1, §11, §17.10, §20 (barcode orang tua sebagai saran pengembangan) dan §25.5 (riwayat, termasuk penegasan ulang P-08).

**Belum terbukti / utang kode (kode belum diubah):**
- Kepala Sekolah belum bisa membuka menu Audit di web dan desktop.
- Admin belum bisa menetapkan status kehadiran guru (masih hanya lihat).
- Admin belum bisa mereset sandi Kepala Sekolah; wajib ganti sandi setelah direset belum ada; ganti sandi sendiri belum ada.
- Wali kelas belum bisa membuka atau mengunduh laporan kelasnya.
- Pembuatan akun Kepala Sekolah pengganti lewat verifikasi penyedia menunggu versi online dan D-10.

**Temuan pembacaan repo `Absensi-Uji-Real` (13 November 2026), belum diperbaiki:**
- Web tidak membuka hari baru bila halaman dibiarkan terbuka melewati tengah malam: tap tampil "Hadir" di TV tetapi tidak tersimpan di `absensi_siswa` (`RepositoriAbsensi.SimpanKeputusanSiswa` memakai `UPDATE`); setelah muat ulang siswa tercatat Alpa. Desktop tidak terdampak (penjadwal 00.01).
- Wizard mengisi akhir periode bawaan hari ini + 5 bulan dan tanggal periode tidak dapat diubah; periode semester berikutnya bisa tertolak karena tumpang tindih.
- Web tidak meminta penyimpanan tetap (`navigator.storage.persist()`); tidak ada keluar otomatis; teks sisa mode uji masih tampil di mode nyata (`MasterImportDialog.razor:252`, BelumDibangun, SchoolAudit, Dasbor).

**Keputusan yang menunggu Edgar:** apakah sekolah hanya memakai aplikasi desktop atau juga portal browser/HP (bertentangan dengan rancangan portal sekolah §20–§21), dan apakah satu sekolah boleh memasang aplikasi di banyak PC.

## 30. Keputusan server P-23 — 14 November 2026

**Instruksi pemilik:** tetapkan server memakai AWS Paid Plan.

**Selesai (dokumen):** PRD §0 (tabel bentuk aplikasi), §21.6 (P-23), §25.1 (D-06 dan B-01 dipersempit), §25.5 (riwayat).

**Belum terbukti / utang:**
- Akun AWS belum dibuat; belum ada infrastruktur atau kode server.
- Harga di P-23 berasal dari situs pembanding pihak ketiga (region Jakarta), belum dicek di AWS Pricing Calculator; penyimpanan RDS, IP publik dan pajak belum dihitung pasti.
- Bukti lokasi AWS (dukungan teknis, telemetry, log, tagihan, subprosesor) dan KMS/HSM di Jakarta belum diperiksa (B-01).

**Keputusan yang menunggu Edgar:** bentuk akses sekolah (desktop saja / desktop + portal web terkunci perangkat / desktop + aplikasi Android wali kelas), jumlah PC per sekolah, iPhone atau Android saja, dan cara distribusi aplikasi HP.

## 31. Keputusan bentuk akses P-24 — 14 November 2026

**Instruksi pemilik:** kunci bentuk akses sekolah: desktop saja.

**Selesai (dokumen):** PRD §1 (frontend web), §20 (P-24 poin 1–8), catatan status §21, §25.1 (D-08), §25.5.

**Belum terbukti / utang:**
- Tidak ada kode untuk sertifikat perangkat, pendaftaran/pencabutan PC, jendela gerbang yang terpisah dari sesi login, pembacaan reader langsung dari perangkat, atau pembaruan bertanda tangan.
- Desktop sekarang: perlu dicek apakah Layar gerbang ikut tertutup saat pengguna keluar dan apakah reader model keyboard bisa menelan tap ke jendela kerja.
- Web uji (`absensi-uji`, `absensi-uji-real`) tetap alat uji, bukan produk.

**P-24 poin 8 ditetapkan (14 November 2026):** 5 PC per sekolah, keluar otomatis 10 menit (jendela gerbang tetap berjalan), sertifikat perangkat 1 tahun dengan perpanjangan otomatis selama berlangganan. Belum ada kode untuk ketiganya.

## 32. Koreksi isi data uji coba mode nyata — 14 November 2026

**Instruksi pemilik:** "data asli" di `Absensi-Uji-Real` berarti data pribadi pemilik, bukan data siswa Dapodik.

**Selesai (dokumen):** PRD §0, status §20 dan §25.5 diluruskan: uji coba memakai data pribadi pemilik sendiri atau nama karangan; data siswa asli tetap dilarang di kedua situs uji sampai layanan online siap (P-23, B-01). README `Absensi-Uji-Real` ikut diluruskan.

**Utang:** bug tengah malam dan wizard yang mengunci semester (PROGRES §29) tetap memengaruhi hasil uji coba mode nyata.

## 33. Keputusan komersial dan hukum P-25–P-28 — 14 November 2026

**Selesai (dokumen):** PRD §21.10 (P-25 harga, P-26 menunggak/berhenti, P-27 retensi dan arsip, P-28 pengendali/prosesor), §25.1 (B-04, D-04, D-05, D-11), §25.5.

**Ditetapkan kemudian (14 November 2026):** tenggang 14 hari, hanya-lihat 30 hari, beku sampai kontrak berakhir/dibayar, ekspor 30 hari setelah kontrak berakhir.

Kartu guru (baru dan pengganti) gratis.

**Belum ditetapkan / menunggu Edgar:** angka retensi per kelas data; template kontrak (perlu ahli hukum).

**Belum terbukti:** aturan retensi daftar hadir SMP yang berlaku (Permendikbud 45/2016 telah dicabut Permendikbudriset 20/2022; isi pengganti belum dibaca). Belum ada kode untuk penagihan, status langganan, atau penolakan PC sekolah yang menunggak.

## 34. Rincian antrean offline P-29 (D-01) — 14 November 2026

**Selesai (dokumen):** PRD §21.8 P-29 butir 1–9, §25.1 D-01, §25.5.

**Belum terbukti / utang:** belum ada kode untuk penghitung monoton, rantai catatan, buku wajah terenkripsi, sinkronisasi antrean, label Perlu pemeriksaan (ringan) maupun sahkan massal. Desktop dan web saat ini tidak memiliki antrean offline. Metode kriptografi dan penyimpanan kunci konkret ditetapkan saat perancangan teknis.

## 35. Rincian penahanan notifikasi P-30 (D-02) — 14 November 2026

**Selesai (dokumen):** PRD §21.9 P-30 butir 1–7, §25.1 D-02, §25.5. Batas "12.00" diterapkan sebagai batas akhir tap masuk sesuai jadwal sekolah (Jumat 10.00 di sekolah rujukan).

**Belum terbukti / utang:** belum ada server, sinyal hidup PC, penundaan rekap, penjadwalan ulang tugas, maupun pesan ralat. Isi pesan ralat belum dirumuskan.

## 36. Aturan tambahan penulisan kode P-31 — 14 November 2026

**Selesai (dokumen):** PRD §24.2 butir 1 disesuaikan, §24.7 (P-31) baru, §25.5.

**Belum terbukti / utang:**
- CI belum menjalankan pemeriksa ejaan otomatis dan belum menjadikan semua peringatan analyzer galat build; perlu dicek dan ditambahkan.
- Belum ada tes dua permintaan bersamaan maupun pemantauan memori untuk Layar gerbang/worker.
- Referensi tampilan dari GitHub (skill) belum ditambahkan pemilik.

## 37. Ganti nama repo — 14 November 2026

Pemilik mengganti nama repo `patahpensil/absensi-smp1` menjadi `patahpensil/absensi-siswa`. Rujukan aktif di PRD (kepala dokumen, §25.5) diperbarui. Tautan bukti historis di dokumen ini sengaja tidak diubah karena GitHub mengalihkan alamat lama.

**Yang harus dilakukan Edgar:** perbarui instruksi proyek Claude (bagian ACUAN dan TUJUAN) yang masih menyebut `absensi-smp1`; jangan membuat repo baru bernama `absensi-smp1`. Deskripsi "About" repo masih menyebut satu sekolah dan SQLite; dapat diperbarui lewat ikon pengaturan di samping "About".

## 38. Satu repo, dua situs uji (pilihan B) — 14 November 2026

Pemilik memilih B: `absensi-uji` dan `absensi-uji-real` dibangun dari satu hasil publish di repo ini. `web-deploy.yml` kini mendeploy salinan `"Mode": "nyata"` ke proyek `absensi-uji-real` (cabang produksi `main`) setelah deploy mode uji, lalu memeriksanya di Chromium dengan `HANYA_MODE_NYATA=1`.

- Proyek mode nyata sengaja **tidak dibuat otomatis**, karena nama subdomain bisa berubah bila proyek dibuat di akun lain.
- **Terbukti:** [run 43526181025](https://github.com/patahpensil/absensi-siswa/actions/runs/43526181025) memperbarui `absensi-uji` dan lulus Chromium, tetapi proyek `absensi-uji-real` **tidak ada** di akun Cloudflare secret repo ini (HTTP 404). Proyek itu berada di akun Patahpensil01 (dibuat dari secret repo `Absensi-Uji-Real`).
- Perbaikan: langkah mode nyata kini memakai pasangan secret tersendiri `CLOUDFLARE_API_TOKEN_REAL` dan `CLOUDFLARE_ACCOUNT_ID_REAL`. Sampai secret itu diisi, run deploy berakhir merah di langkah mode nyata; situs `absensi-uji` tetap diperbarui lebih dulu.
- **Belum terbukti:** deploy mode nyata dari repo ini.
- Repo `Absensi-Uji-Real` tidak lagi dipakai. Workflow-nya masih aktif dan akan menimpa situs dengan kode lama bila ada push ke `main` di sana.

**Yang harus dilakukan Edgar:**
1. Di Cloudflare akun Patahpensil01, buat API token baru berizin Cloudflare Pages: Edit, dan catat Account ID akun itu.
2. Di repo `patahpensil/absensi-siswa`: Settings → Secrets and variables → Actions → New repository secret. Isi `CLOUDFLARE_API_TOKEN_REAL` dan `CLOUDFLARE_ACCOUNT_ID_REAL`. Jangan kirim nilainya ke chat.
3. Jalankan ulang workflow "Web uji - deploy Cloudflare Pages" (tab Actions → Run workflow), atau beri tahu agen.
4. Setelah deploy mode nyata hijau, arsipkan repo `Absensi-Uji-Real`: Settings → Danger Zone → Archive this repository. Arsip juga mematikan workflow-nya.

## 39. Langkah 1a — bug tengah malam (web) — 14 November 2026

Memperbaiki temuan §29: halaman web yang dibiarkan terbuka melewati tengah malam tidak membuka hari baru, sehingga tap pertama hari itu tampil berhasil tetapi tidak tersimpan.

- Kelas baru `Absensi.Data.CurrentDayGuard` mencatat hari terakhir yang dibuka dan menjalankan `PembukaanHari` hanya bila tanggal jam berbeda. Web memanggilnya sebelum setiap tap (Layar gerbang dan meja Absensi) dan setiap detik lewat jam `GateHost`, yang terpasang selama sesi login. Daftar absensi dimuat ulang saat hari baru dibuka.
- Tes: `CurrentDayGuardTests` (5 kasus, termasuk bukti bahwa tap tanpa pembukaan hari tidak tersimpan) dan langkah Chromium "Tap setelah tengah malam tanpa muat ulang tersimpan di hari baru". Langkah Chromium memakai jam browser Playwright (`clock.setFixedTime`), bukan jam skenario aplikasi, karena jam skenario selalu membuka hari secara eksplisit dan menutupi bug ini.
- Pemeriksaan: tidak ada `await` di dalam `CurrentDayGuard`; `OpenedDay` diisi sebelum `SimpanAsync` ditunggu, jadi tap dan detak jam yang berjalan bergantian tidak membuka hari dua kali (dan `PembukaanHari` tetap idempoten). Galat pembukaan hari di detak jam ditampilkan di Layar gerbang, tidak mematikan detak.

**Utang:**
- Desktop masih punya celah sampai 30 detik setelah 00.00 (penjadwal berdetak tiap 30 detik); tap pada celah itu hilang dengan cara yang sama. Usulan: desktop memakai `CurrentDayGuard` sebelum `PemrosesTap`, dikerjakan bersama perubahan desktop berikutnya.
- Akar masalah tetap ada: `SimpanKeputusanSiswa/Guru` hanya `UPDATE`, jadi tap untuk siswa tanpa baris absensi hari itu tidak tersimpan tanpa galat. Usulan: tolak tap bila tidak ada baris yang berubah. Belum dikerjakan karena mengubah perilaku layanan bersama; perlu dibahas.

## 40. Langkah 1b — rentang periode akademik dapat diperbaiki — 14 November 2026

Memperbaiki temuan §29: wizard mengisi akhir periode "hari ini + 5 bulan" dan rentang periode tidak bisa diubah sesudahnya, sehingga semester berikutnya bisa tertolak karena tumpang tindih.

- Wizard web kini mengisi semester dan rentang awal mengikuti bulan: Juli–Desember → ganjil, akhir 31 Desember, nama tahun berjalan/berikutnya; Januari–Juni → genap, akhir 30 Juni, nama tahun sebelumnya/berjalan. Isian tetap dapat diubah dan diberi petunjuk agar disesuaikan dengan kalender pendidikan.
- Layanan baru `AcademicPeriodService.ChangeDates` (Kepala Sekolah/Admin, tercatat di audit `ubah_rentang_tahun_ajaran` dengan nilai lama dan baru). Aturan, diperiksa di dalam satu transaksi:
  - tanggal berakhir tidak boleh sebelum tanggal mulai, dan rentang harus berubah;
  - tidak boleh tumpang tindih dengan periode lain;
  - periode aktif harus tetap mencakup hari ini;
  - hari yang sudah punya catatan absensi (hari dibuka atau baris absensi siswa periode itu) harus tetap di dalam rentang.
- Web: Kelas → Tahun ajaran → tindakan "Ubah rentang periode".
- Tes: `AcademicPeriodDateChangeTests` (7 kasus) dan langkah Chromium "Rentang periode dapat diperbaiki; tanggal yang sudah tercatat tetap terlindungi".

**Keputusan yang saya ambil dan perlu Edgar ketahui (bukan D-xx, tetapi aturan baru):** periode aktif tidak boleh dipersingkat sampai tidak mencakup hari ini, dan hari yang sudah tercatat tidak boleh dikeluarkan dari periode. Keduanya mencegah absensi yang sudah ada kehilangan periodenya. Bila sekolah perlu mengakhiri semester lebih awal, caranya mengaktifkan periode berikutnya.

**Utang:**
- Desktop belum punya layar untuk mengubah rentang periode; layanannya sudah ada di lapisan Data.
- Baris absensi guru tidak terikat periode, jadi status guru yang ditetapkan di muka pada tanggal yang dikeluarkan dari rentang tetap tersimpan tanpa periode.

## 41. Langkah 1c — penyimpanan browser tetap dan teks sisa mode uji — 14 November 2026

- Web meminta penyimpanan tetap (`navigator.storage.persist()`) setelah data dimuat dan setelah wizard selesai. Permintaan tidak ditunggu, karena Firefox bisa bertanya dulu ke pengguna dan layar login tidak boleh tertahan. Hasilnya tampil di Pengaturan → Penyimpanan data (`#storage-persistence`). Browser yang menolak tetap dipakai seperti biasa, dengan anjuran mengunduh cadangan.
- Teks yang menyebut "web uji", "browser uji", "waktu uji", atau "data contoh" di halaman yang juga tampil pada mode nyata diganti dengan teks netral: Belum tersedia, Audit, Detail siswa (kanal pesan), Notifikasi, Kalender, Impor master, dan Dasbor. Run bukti browser pertama (commit `00575f1`) menangkap teks "Web uji" yang tertinggal di Notifikasi.
- Tes Chromium mode nyata kini membuka setiap menu yang tersedia dan gagal bila salah satu teks itu muncul, serta memastikan status penyimpanan tetap tampil di Pengaturan.

**Belum terbukti:** apakah Chrome di perangkat sekolah memberi penyimpanan tetap. Chrome memutuskan sendiri, antara lain dari seberapa sering situs dipakai. Hasilnya baru terlihat di Pengaturan pada perangkat itu.

**Utang:** keluar otomatis saat tidak dipakai (temuan §29) belum dibuat untuk web; desktop memakai aturan P-24 (10 menit).

