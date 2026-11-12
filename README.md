# Absensi Siswa dan Guru RFID

Proyek layanan absensi RFID untuk sekolah. Rancangan produksi mengikuti SaaS multi sekolah pada PRD: desktop C#/WPF, backend C#/ASP.NET Core terpisah, dan penyimpanan resmi di Indonesia. Implementasi desktop yang ada masih memakai SQLite; backend online dan kelulusan ASVS L3 belum tersedia.

**Web uji sementara:** https://absensi-uji.pages.dev/ — dipakai pemilik selama pengembangan untuk meninjau tampilan dan mencoba fitur. Setiap pembaruan mempertahankan tautan yang sama. Hanya data sintetis; bukan lingkungan produksi. Data web uji tersimpan di browser masing-masing melalui IndexedDB, tidak dibagikan antarperangkat.

Acuan kerja satu-satunya: `docs/PRD.md` (termasuk standar penulisan kode §24). Baca bagian yang relevan sebelum mengerjakan tugas apa pun.

## Struktur

```
/src
  /Absensi.Domain    - logika penentuan status kehadiran, tanpa UI dan tanpa database
  /Absensi.Data      - akses SQLite, migrasi, repositori
  /Absensi.Telegram  - pemanggilan Bot API, outbox
  /Absensi.Laporan   - keluaran Excel dan PDF
  /Absensi.App       - antarmuka WPF
  /Absensi.Web       - aplikasi web uji Blazor; memakai Domain/Data yang sama
/tests
  /Absensi.Domain.Tests
  /Absensi.Data.Tests
  /Absensi.Telegram.Tests
  /Absensi.Laporan.Tests
  /Absensi.App.Tests
  /web-browser       - pengujian alur web di Chromium
/docs                - PRD, skema database, panduan UI/UX
```

## Tumpukan Teknologi

- .NET 10 LTS (C#)
- WPF untuk antarmuka
- SQLite lewat Microsoft.Data.Sqlite
- xUnit untuk pengujian

## Build dan pengujian

Seluruh solusi, termasuk pengujian WPF, dijalankan pada Windows:

```
dotnet build Absensi.sln -c Release
dotnet test Absensi.sln -c Release
```

Workflow GitHub membangun web dengan native SQLite WebAssembly, menjalankan `tests/web-browser/check.mjs`, lalu memperbarui Cloudflare Pages setelah pengujian browser lulus. Hasil deploy juga diperiksa kembali di Chromium.

## Mencoba web uji

1. Pilih akun contoh pada layar login, lalu tekan Masuk.
2. Periksa Dashboard, Siswa, Guru, dan Kelas. Gunakan pencarian/filter atau bilah perintah; Ctrl+K memfokuskan perintah, F2 membuka Absensi, F3 membuka Siswa.
3. Pada Siswa, pilih **Daftarkan kartu** untuk satu siswa. Masukkan UID contoh, lalu Simpan. Nomor nol di depan tetap dipertahankan. Akun wali kelas tidak memiliki tindakan ini.
4. Pada Pengaturan, tetapkan tanggal/waktu uji dalam periode aktif. Waktu skenario berhenti agar batas detik dapat diuji.
5. Pada Absensi, proses UID terdaftar. Keputusan dibuat oleh layanan C# yang sama dengan desktop. Tap 07.30.00 Hadir, setelahnya Terlambat sesuai jadwal; jeda 15 detik tetap berlaku.

Tampilan mengadaptasi susunan panel Binance dan bilah perintah terminal Bloomberg, dengan palet biru/putih/abu-abu sekolah. Modul web yang belum tersedia ditandai jelas. Pengiriman notifikasi eksternal, backend produksi, sinkronisasi offline, dan pengujian reader fisik belum diaktifkan oleh web uji ini.
