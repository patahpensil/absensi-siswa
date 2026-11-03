# Progres dan Utang — pembaruan dokumentasi 2026-11-04

Satu-satunya folder kerja proyek: `D:\Project Absensi SMP 1`. Berkas ini adalah
ringkasan status terkini; rincian per butir ada di `MASTER-BUG-UIUX-REGISTER.md`,
`TODO-VERSI-BERIKUTNYA.md`, dan `RENCANA-TUGAS.md`.

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
