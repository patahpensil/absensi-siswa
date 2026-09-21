# Backlog — Redesain UI/UX (Identitas Visual Baru)

**Status: KEPUTUSAN DICATAT, BELUM DIKERJAKAN.** Tidak ada kode/XAML yang diubah untuk item ini. Fase ini dimulai **setelah** stabilitas selesai — urutannya mengikuti `docs/INVESTIGASI-CRASH-2026-09-21.md` §5 Tahap A–G (logging aktif → exception handling → reproduksi → root cause → pengujian first-run → Bug #1/#2 lalu #8/#9/#10). Dokumen ini tidak mengganggu urutan itu dan tidak menyisipkan pekerjaan UI di tengah-tengahnya.

Dokumen ini **menambah** `PANDUAN-UI-UX.md` yang sudah ada — bukan menggantikannya. `PANDUAN-UI-UX.md` tetap berlaku sebagai acuan implementasi SAAT INI (palet §3.1/§3.2 di sana masih yang dipakai kode sampai redesain ini benar-benar dikerjakan dan dokumen itu direvisi secara sadar pada saatnya).

---

## 1. Latar Belakang Keputusan

Pemilik proyek memutuskan (2026-09-21) identitas visual aplikasi diarahkan ulang mengambil inspirasi dari seragam resmi sekolah pada foto referensi yang diberikan langsung ke asisten pada sesi ini (bukan berkas di repo — kalau foto itu perlu dirujuk lagi nanti, minta pemilik proyek melampirkannya ulang).

Prinsip arah visual:
- **Putih** sebagai warna permukaan utama (kartu, tabel, latar konten).
- **Biru navy** sebagai warna identitas dan aksi utama (header, tombol utama, menu aktif) — menggantikan "Biru seragam" `#1B3A6B` yang ada di `PANDUAN-UI-UX.md` §3.1 saat ini dengan navy yang lebih gelap.
- **Merah** dan **kuning/emas** HANYA sebagai aksen terbatas (bukan warna dominan, bukan warna status).
- Warna status semantik (sukses/peringatan/galat/informasi) **tetap harus berbeda** dari warna identitas/aksen — bukan dipakai bergantian. Ini SEJALAN dengan temuan yang sudah tercatat di `TODO-VERSI-BERIKUTNYA.md` ("Reuse warna status di berkas UI" — 7 layar sudah teridentifikasi memakai warna status kehadiran sebagai warna sukses/gagal generik). **Kedua pekerjaan ini akan digabung saat redesain dikerjakan**, bukan dikerjakan dua kali terpisah: redesain ini sekalian jadi kesempatan menegakkan pemisahan warna status vs warna aksi/identitas secara menyeluruh.

## 2. Token Warna — Arah Awal (BELUM final, belum divalidasi kontras)

| Token | Hex | Peran yang dimaksud |
|---|---|---|
| Primary Navy | `#172641` | Warna identitas & aksi utama (pengganti `#1B3A6B` lama) |
| Primary Hover | `#223654` | Status hover untuk elemen Primary Navy |
| Primary Pressed | `#101C30` | Status ditekan untuk elemen Primary Navy |
| Background | `#F5F7FA` | Latar halaman (pengganti "Kertas" `#F5F6F7` lama) |
| Surface | `#FFFFFF` | Latar kartu, tabel, dialog |
| Border | `#D8DEE8` | Pembatas, bingkai kolom (pengganti "Garis" `#DFE3E8` lama) |
| Text Primary | `#182230` | Teks utama (pengganti "Tinta" `#14181F` lama) |
| Text Secondary | `#667085` | Teks sekunder/label (pengganti "Abu tulis" `#5B6472` lama) |
| Accent Red | `#D92D3A` | Aksen terbatas SAJA — bukan warna status galat (lihat §3) |
| Accent Gold | `#D6A62E` | Aksen terbatas SAJA — bukan warna status peringatan (lihat §3) |

**Belum ditentukan saat audit nanti (bukan kelalaian, sengaja ditunda ke fase audit):**
- Varian hover/pressed untuk Accent Red dan Accent Gold.
- Apakah warna status di `PANDUAN-UI-UX.md` §3.2 (Hadir/Terlambat/Izin-Sakit/Alpa-Ditolak/Tidak lengkap) ikut disesuaikan nadanya supaya harmonis dengan navy baru, atau dipertahankan persis seperti sekarang untuk menghindari asosiasi keliru dengan Accent Red/Gold yang mirip.
- Rasio kontras WCAG untuk setiap pasangan teks/latar di atas — wajib dihitung saat audit (§4), bukan diasumsikan dari sekadar terlihat cukup gelap/terang.

## 3. Batasan yang Mengikat (bukan sekadar saran)

1. Warna status kehadiran (Hadir/Terlambat/Izin-Sakit/Alpa-Ditolak/Tidak lengkap — `PANDUAN-UI-UX.md` §3.2) tidak boleh dipakai sebagai warna sukses/gagal generik, dan sebaliknya Accent Red/Gold baru ini tidak boleh dipakai untuk mewarnai data kehadiran. Dua sistem warna yang terpisah dengan tegas.
2. Accent Red dan Accent Gold **terbatas** — dipakai untuk aksen kecil (ikon, garis penekanan, badge non-status), bukan latar tombol/blok besar. Navy dan putih tetap mendominasi.
3. Perubahan ini **tidak mengubah struktur data atau logika absensi** — murni token visual (warna, mungkin tipografi/spasi ikut diaudit di §4), tidak menyentuh `Absensi.Domain`/`Absensi.Data`.

## 4. Ruang Lingkup Audit Saat Fase Ini Dimulai

Dilakukan menyeluruh di seluruh layar (`docs/PANDUAN-UI-UX.md` §9 "Daftar Layar" jadi checklist dasarnya), mencakup:

- Konsistensi warna dan token (termasuk menegakkan §3 di atas di seluruh layar, sekali jalan dengan menyelesaikan item "Reuse warna status" di `TODO-VERSI-BERIKUTNYA.md`).
- Tipografi dan hierarki visual.
- Sidebar serta navigasi aktif.
- Tombol utama, sekunder, dan berbahaya (destructive) — termasuk menyelesaikan item "Dua tombol utama di layar Pengaturan" (`TODO-VERSI-BERIKUTNYA.md` B.2) sebagai bagian dari audit ini, bukan terpisah.
- Tabel, formulir, dialog, notifikasi, empty state, loading, dan validasi.
- Jarak, alignment, ukuran area klik, dan respons layar (termasuk batas 1280 px yang disebut CLAUDE.md §2).
- Aksesibilitas keyboard, focus state, kontras, serta pembaca layar — termasuk menyelesaikan item aksesibilitas ComboBox "Peran" (`TODO-VERSI-BERIKUTNYA.md`) sebagai bagian dari audit ini.
- Konsistensi istilah untuk pengguna sekolah (Bahasa Indonesia yang dipakai TU/guru, bukan istilah teknis — CLAUDE.md §5 "Penamaan").

## 5. Kriteria Berhasil

Hasil akhir harus: elegan, profesional, tenang, mudah dipakai petugas sekolah (bukan pengguna teknis), **memiliki identitas SMP** (bukan template generik), dan **tidak terlihat seperti UI generik buatan AI**. Kriteria ini kualitatif dan dinilai lewat review visual langsung saat fase dikerjakan — bukan checklist otomatis.

## 6. Hubungan dengan Pekerjaan Lain yang Sudah Tercatat

- `TODO-VERSI-BERIKUTNYA.md` — 3 item (warna status generik, dua tombol utama Pengaturan, aksesibilitas ComboBox Peran) akan **digabung** ke audit ini saat dikerjakan, ditandai di §4 di atas. Item lain di dokumen itu (jam literal, timestamp migrasi, catch luas, template impor) TIDAK terkait UI — tetap berdiri sendiri, tidak terpengaruh keputusan ini.
- `docs/INVESTIGASI-CRASH-2026-09-21.md` — urutan Tahap A–G di sana **mendahului** fase ini. Tidak ada tumpang tindih pekerjaan; redesain ini secara eksplisit dijadwalkan sesudahnya.
- `PANDUAN-UI-UX.md` — akan direvisi sebagai bagian dari pengerjaan fase ini (bukan sekarang), setelah token final di §2 ditentukan lewat audit kontras dan review visual.
