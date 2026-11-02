# D-2 — TERBUKTI (direproduksi 2026-09-21 18:29-18:33)

## Skenario yang dijalankan (1× — berhenti sesuai instruksi begitu gejala cocok)

1. Wizard diselesaikan dengan data fiktif (sekolah "Sekolah Uji Tahap D", akun
   `admin_tahapd`), login berhasil.
2. Menu Absensi → "Buka layar gerbang" → GerbangWindow terbuka.
3. Alt+F4 dikirim ke GerbangWindow (18:29:59) → dialog "Tutup layar gerbang"
   muncul (konfirmasi + re-autentikasi, sesuai PRD §14.1 "tidak bisa ditutup
   tanpa sandi").
4. Isi username/sandi akun uji yang sama, klik "Tutup layar gerbang" (18:31:01).

## Hasil — cocok PERSIS dengan pola historis Crash D-2

`logs/20260921.log` mencatat DUA entri berurutan (rantai penanganan lengkap):

```
[2026-09-21T18:31:01+08:00] [FATAL] [DispatcherUnhandledException]
System.InvalidOperationException: Cannot set Visibility to Visible or call Show,
ShowDialog, Close, or WindowInteropHelper.EnsureHandle while a Window is closing.
   HResult=-2146233079, Source=PresentationFramework, TargetSite=VerifyNotClosing
   at System.Windows.Window.VerifyNotClosing()
   at System.Windows.Window.InternalClose(Boolean shutdown, Boolean ignoreCancel)
   at Absensi.App.Gerbang.GerbangWindow.OnClosing(CancelEventArgs e)
   at System.Windows.Window.WmClose()
   ...
--- akhir entri ---

[2026-09-21T18:33:28+08:00] [FATAL] [AppDomain.UnhandledException (IsTerminating=True)]
System.InvalidOperationException: Cannot set Visibility to Visible or call Show,
ShowDialog, Close, or WindowInteropHelper.EnsureHandle while a Window is closing.
   (stack sama persis)
--- akhir entri ---
```

Screenshot dialog tunggal yang tampil ke pengguna: `d2-layar-setelah-klik.png`
(di folder catatan yang sama) — hanya menampilkan pesan ramah "Aplikasi
mengalami galat tak terduga dan perlu ditutup. Rincian teknis sudah dicatat
di folder 'logs'. Buka kembali aplikasi untuk melanjutkan." TANPA stack
trace/path/nama exception teknis.

Setelah dialog ditutup (Enter, 18:33:28-30), proses BENAR-BENAR BERHENTI
(dikonfirmasi `Get-Process` mengembalikan null). Tidak ada dialog kedua.
Tidak ada proses hantu tersisa.

## Klasifikasi: TERBUKTI (akar masalah kode)

Exception, method, dan call stack **identik** dengan event ".NET Runtime"
historis pukul 15:25:12 (2026-09-21) yang ditemukan lewat korelasi log
sebelum reproduksi ini dijalankan (lihat `00-baseline-dan-temuan-awal.md`).

**Root cause: `src/Absensi.App/Gerbang/GerbangWindow.xaml.cs`, method
`OnClosing`** — memanggil `Close()` secara sinkron MASIH DI DALAM `OnClosing`
yang sedang berjalan (untuk permintaan tutup pertama), melanggar
`Window.VerifyNotClosing()`. Bentuk identik dengan bug Wizard yang sudah
diperbaiki di commit `e2ec306`; perbaikan itu tidak ikut diterapkan ke
`GerbangWindow`.

## Nuansa penting (dicatat jujur, bukan disembunyikan)

**Sinyal OS-level BERBEDA dari crash historis asli**, walau EXCEPTION dan
CALL STACK managed-nya identik:
- Historis (sebelum ada `DispatcherUnhandledException`): tercatat di
  provider "Application Error" (`0xe0434352` lalu `0xc000041d`
  `STATUS_FATAL_USER_CALLBACK_EXCEPTION`) DAN "Windows Error Reporting"
  DAN ".NET Runtime" — exception mentah lolos sampai batas WndProc native
  tanpa ada satu pun handler .NET yang menangkapnya lebih dulu.
- Reproduksi sesi ini (SESUDAH `DispatcherUnhandledException` ada):
  **HANYA** tercatat di provider ".NET Runtime" (event baru pukul 18:33:28)
  — **NOL event baru** di "Application Error" maupun "Windows Error
  Reporting" dibandingkan baseline sebelum Tahap D. **Tidak ada crash dump
  yang tercipta** di `C:\AbsensiSekolah-TahapD\dumps` walau WER LocalDumps
  sudah aktif dan diverifikasi (DumpType=2, DumpCount=3).

**Penjelasan teknis:** begitu `DispatcherUnhandledException` menangkap
exception lebih dulu (walau `e.Handled` tetap `false`), WPF/.NET meneruskannya
lewat jalur terminasi "unhandled exception" .NET YANG LEBIH TERKELOLA
(tercatat rapi lewat provider ".NET Runtime" Id=1026), BUKAN lewat jalur
mentah lolos ke WndProc native yang tadinya memicu
`STATUS_FATAL_USER_CALLBACK_EXCEPTION` dan mengaktifkan pipeline WER penuh
(Application Error + dump). Ini efek SAMPINGAN yang WAJAR dan DIHARAPKAN dari
menambah `DispatcherUnhandledException` (Tahap B) — bukan kegagalan
reproduksi, bukan bug baru. Sesuai kriteria klasifikasi Bagian 10 dokumen
instruksi: exception sama, method/call-stack sama, gejala aplikasi
(tetap crash/berhenti) sama secara substansi, HANYA kode OS-level
terminasinya "dapat dijelaskan sebagai rangkaian yang sama" — disebabkan
perubahan kode YANG SUDAH DISETUJUI (Tahap B), bukan oleh alat/metode
pengujian.

**Konsekuensi untuk usulan dump:** karena jalur ini tidak lagi memicu WER
penuh, dump kemungkinan TIDAK AKAN pernah tercipta untuk kelas crash D-2 ini
lagi (justru pertanda perbaikan Tahap B bekerja) — WER LocalDumps praktis
hanya akan relevan untuk D-1 (stack overflow) atau exception lain yang lolos
dari ketiga handler global.

## Verifikasi Bagian 9 (checklist instruksi) — SEMUA terpenuhi dinamis

1. Log tercipta — ✅ (2 entri, lihat di atas)
2. Hanya satu dialog — ✅ (screenshot, tidak ada dialog kedua terlihat)
3. Dialog tidak menampilkan stack trace/path/nama exception teknis/data sensitif — ✅
4. Aplikasi shutdown sesuai rancangan — ✅ (Handled tetap false, tidak ada usaha "pulih")
5. Proses benar-benar berhenti — ✅
6. Tidak ada dialog berulang — ✅
7. Logging tidak memicu exception kedua — ✅ (kedua entri log tertulis bersih)
8. Event Viewer — ✅ diperiksa (lihat nuansa di atas)
9. Crash dump — ✅ diperiksa, TIDAK ADA (dijelaskan kenapa, bukan kegagalan)
10. Tidak dianggap "pulih" hanya karena dialog tampil — ✅ ditegaskan proses tetap berhenti

**Item C dari Tahap B/C ("BELUM TERVERIFIKASI DINAMIS") kini
TERVERIFIKASI DINAMIS lewat reproduksi ini.**

Tidak ada kode yang diubah. Berhenti mengulang D-2 sesuai instruksi.
