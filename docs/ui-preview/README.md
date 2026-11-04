# Pratinjau UI absensi SMP 1

Buka [index.html](index.html) dengan browser. Seluruh CSS, ikon SVG, JavaScript dan data contoh berada di satu berkas; tidak memerlukan pemasangan dependency atau koneksi ke server sekolah.

Pratinjau ini memuat layar gerbang, daftar absensi, riwayat tap, panel siswa belum tap/Izin/Sakit, serta pemeriksaan aturan. Nama dan NISN di dalamnya adalah data contoh.

## Arah desain

Susunan panel mengikuti referensi Binance Advanced: pantauan utama, daftar pantauan di kanan, dan aktivitas bertab di bawah. Sentuhan terminal berupa bilah perintah, pintasan keyboard, angka monospace dan judul panel ringkas. Palet utama tetap biru seragam, putih dan abu muda; hijau/kuning/merah digunakan untuk status.

Mode gerbang memperbesar nama, foto/placeholder dan status untuk layar besar.

## Pengoperasian

- `Ctrl+K`: fokus bilah perintah.
- `GERBANG`: buka pantauan gerbang.
- `HARIAN` atau `SISWA`: tampilkan seluruh daftar siswa.
- `LOG` atau `RIWAYAT`: buka riwayat tap.
- `UJI`: buka pemeriksaan aturan, kemudian klik **Jalankan pengujian**.
- `TAP` atau `F2`: buka formulir simulasi kartu.
- `BANTUAN` atau `?`: tampilkan panduan perintah.
- Nama/NISN: cari siswa pada daftar.
- `Esc`: tutup simulasi/panduan atau kembali dari mode presentasi.

Tombol skenario cepat menyiapkan ulang kondisi siswa contoh yang dipilih. **Ulangi simulasi** pada riwayat mengembalikan seluruh data contoh ke kondisi awal. Memuat ulang halaman juga mengembalikan kondisi awal.

## Pemeriksaan

Pemeriksaan terakhir pada 4 November 2026:
- 14 dari 14 pemeriksaan aturan HTML lulus di browser.
- Perintah navigasi, pencarian, bantuan, `TAP`, `Ctrl+K` dan `F2` diperiksa melalui interaksi browser.
- Empat ukuran bingkai (390, 768, 1280 dan 1366 px) diperiksa tanpa luapan horizontal atau kartu terpotong.
- Sintaks dua blok JavaScript diperiksa sebelum commit.

Mesin keputusan siswa pada perubahan sentuhan terminal dipertahankan dengan SHA-256 `c5de303a75f0727628f20e0c27ff0b4a9b54b0824def268b804bce7c6fc4d522`.

## Batas pratinjau

Ini adalah prototipe HTML untuk peninjauan UI/UX. Data bekerja sementara di memori browser; tidak ada login sungguhan, akses database sekolah, input perangkat RFID, atau pengiriman notifikasi. Tampilan ini belum diterapkan ke aplikasi WPF. Pengujian HTML tidak membuktikan integrasi backend, keamanan ASVS, atau kelayakan produksi.

## Referensi desain

- [Binance — Customize the Futures Trading Interface](https://www.binance.com/en-KZ/support/faq/detail/a784518335b0492a9ebfa4a72e1ca092)
- [Bloomberg — Terminal Essentials: IB, Worksheets & Launchpad](https://www.bloomberg.com/professional/insights/technology/bloomberg-terminal-essentials-ib-worksheets-launchpad/)
- [Yale Library — Bloomberg Basics](https://guides.library.yale.edu/Bloomberg/Bloomberg_Basics)
