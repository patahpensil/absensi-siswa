# Tahap D — Baseline & Temuan Awal (sebelum matriks reproduksi)

Catatan ini disimpan DI LUAR repositori sesuai arahan. Tidak ada isi database
sungguhan, tidak ada nama/NISN/NIP asli — seluruh identitas di sini fiktif.

## 1. Lingkungan

- Waktu mulai Tahap D: 2026-09-21T17:52:04+08:00
- Komputer: DESKTOP-MR31HCI, Windows 10 Home Single Language 10.0.19045
- .NET SDK: 8.0.425, Microsoft.NETCore.App 8.0.31 (win-x64)
- Hash commit checkpoint: 2f65f75f5c6a93c6591f3773dede83f1c269b7bc
- Hash executable (SHA-256) yang diuji:
  1511681acdaaf82c52007d89534b0c3fc974d5c9f5011934c81429222c013cdc
  (dikonfirmasi identik antara publish/AbsensiSekolah dan salinan uji)
- Lokasi instalasi uji: C:\AbsensiSekolah-TahapD\instalasi\
- Lokasi dump (diusulkan, belum aktif): C:\AbsensiSekolah-TahapD\dumps\
- Tidak ada instance Absensi.App lain berjalan sebelum maupun sesudah baseline.

## 2. Baseline idle (PID 8552, sesaat setelah start, sebelum interaksi apa pun)

- Working Set: 131.44 MB
- Private Memory: 69.85 MB
- CPU (kumulatif sejak start): 2.08 detik
- Thread: 14
- Handle: 468
- Folder logs/: ada, kosong (tidak ada berkas .log)
- Proses ditutup bersih setelah baseline diambil (taskkill, tidak ada proses tersisa)

## 3. Baseline Event Viewer SEBELUM Tahap D (semua tanggal < 2026-09-21T17:52)

- Provider "Application Error" terkait Absensi: 9 event. Terbaru 2026-09-21 15:25:14
  (= Crash D-2), terlama tersimpan 2026-09-19 00:17:38.
- Provider "Windows Error Reporting" terkait Absensi: 15 event, rentang sama.
- Provider ".NET Runtime" (Id=1026) terkait Absensi: **7 event** — provider ini
  BELUM diperiksa di sesi investigasi Tahap A. Isinya memuat MANAGED STACK TRACE
  LENGKAP untuk exception tak tertangani (beda dari "Application Error" yang
  cuma modul+kode exception).

## 4. TEMUAN BESAR — ditemukan lewat korelasi log, SEBELUM satu skenario pun dijalankan

Event ".NET Runtime" (Id=1026) pukul **2026-09-21 15:25:12** (2 detik sebelum
"Application Error" mencatat Crash D-2 di 15:25:13-14 — korelasi waktu sangat
kuat, episode crash yang SAMA):

```
Application: Absensi.App.exe
CoreCLR Version: 8.0.3126.42015
Description: The process was terminated due to an unhandled exception.
Exception Info: System.InvalidOperationException: Cannot set Visibility to
Visible or call Show, ShowDialog, Close, or WindowInteropHelper.EnsureHandle
while a Window is closing.
   at System.Windows.Window.VerifyNotClosing()
   at System.Windows.Window.InternalClose(Boolean shutdown, Boolean ignoreCancel)
   at Absensi.App.Gerbang.GerbangWindow.OnClosing(CancelEventArgs e)
   at System.Windows.Window.WmClose()
   at System.Windows.Window.WindowFilterMessage(...)
   at System.Windows.Interop.HwndSource.PublicHooksFilterMessage(...)
   at MS.Win32.HwndWrapper.WndProc(...)
   at System.Windows.Threading.ExceptionWrapper.InternalRealCall(...)
   at System.Windows.Threading.ExceptionWrapper.TryCatchWhen(...)
   at System.Windows.Threading.Dispatcher.LegacyInvokeImpl(...)
   at MS.Win32.HwndSubclass.SubclassWndProc(...)
```

**Analisis kode** (`src/Absensi.App/Gerbang/GerbangWindow.xaml.cs`, commit 2f65f75):

```csharp
protected override void OnClosing(CancelEventArgs e)
{
    if (_diizinkanTutup)
    {
        _timer.Stop();
        base.OnClosing(e);
        return;
    }

    e.Cancel = true;

    var dialog = new KonfirmasiTutupWindow(new KonfirmasiTutupViewModel(_connection)) { Owner = this };
    if (dialog.ShowDialog() == true)
    {
        _diizinkanTutup = true;
        Close();   // <-- DIPANGGIL SINKRON DI DALAM OnClosing YANG SEDANG BERJALAN
    }
    else
    {
        FokusKolomTap();
    }
}
```

`Close()` dipanggil SINKRON, MASIH DI DALAM `OnClosing` yang sedang berjalan
(untuk permintaan tutup yang PERTAMA) — WPF `Window.VerifyNotClosing()` secara
eksplisit melarang memanggil `Show`/`ShowDialog`/`Close`/`WindowInteropHelper.EnsureHandle`
selagi window statusnya "closing". Ini BENTUK YANG SAMA PERSIS dengan bug Wizard
yang sudah diperbaiki di commit `e2ec306` (di sana: mengisi `DialogResult` di
dalam `Closing` — API berbeda, guard WPF yang sama: `VerifyNotClosing()`).
Perbaikan Wizard TIDAK ikut diterapkan ke `GerbangWindow` saat itu.

**Sifat bug:** BUKAN race condition/timing-dependent — ini deterministik.
Siapa pun yang menutup layar Gerbang (tombol X/Alt+F4) LALU menekan "Ya" pada
dialog konfirmasi PASTI memicu ini, karena `Close()` kedua selalu dipanggil
sementara `OnClosing` pertama masih di call stack. Exception managed lolos
sampai batas `WndProc` native → cocok PERSIS dengan pola
`0xe0434352` (CLR exception) → `0xc000041d` (`STATUS_FATAL_USER_CALLBACK_EXCEPTION`)
yang tercatat provider "Application Error" untuk Crash D-2.

**Kenapa belum pernah ketahuan lewat smoke-test manual sebelumnya:**
`PANDUAN-TESTER.md` Skenario 9 (audit windowing, 2026-09-19) eksplisit
menandai `GerbangWindow` "SENGAJA TIDAK DISENTUH" (fokusnya kiosk-mode
fullscreen, bukan alur tutup+konfirmasi). Skenario 7 (tap kartu di Gerbang)
juga tidak menutup jendelanya. Tidak ada skenario terdokumentasi manapun
yang benar-benar menutup GerbangWindow lewat X/Alt+F4 dan menekan "Ya".

**Untuk Crash D-1 (stack overflow 15:15:37):** TIDAK ADA event ".NET Runtime"
(Id=1026) pada rentang waktu itu. Ini KONSISTEN dengan perilaku CLR yang
terdokumentasi: `StackOverflowException` tidak melalui jalur pelaporan
"unhandled exception" normal (tidak ada ruang stack tersisa untuk menjalankan
mesin pelaporan exception secara andal) — providernya memang tidak akan
pernah mencatat event untuk kelas crash ini. Tidak ada temuan baru untuk D-1
dari korelasi log; masih perlu reproduksi + dump untuk diagnosis lebih lanjut.

## 5. Implikasi untuk rencana Tahap D (BUKAN keputusan — untuk dipertimbangkan)

- Crash D-2 kemungkinan besar SUDAH bisa diklasifikasikan **TERBUKTI** murni
  dari korelasi log historis + pembacaan kode, TANPA perlu dump maupun
  menjalankan matriks Kelompok 1 (D1.1-D1.10) secara penuh — cukup satu
  reproduksi terarah (tutup GerbangWindow → konfirmasi Ya) untuk konfirmasi
  langsung dengan timestamp baru, kalau Anda menghendaki.
- Crash D-1 (stack overflow) TIDAK terbantu oleh temuan ini — tetap perlu
  jalur Kelompok 2 (kandidat ClosedXML/QuestPDF) DAN/ATAU crash dump untuk
  py mendiagnosis, karena tidak ada jejak managed stack trace apa pun yang
  tersimpan untuk kelas crash ini di Windows Event Log.

Belum ada tindakan perbaikan apa pun yang dilakukan. Belum ada skenario
matriks Bagian 6 yang dijalankan. Menunggu keputusan Anda soal konfigurasi
dump (lihat laporan utama) dan apakah temuan ini mengubah prioritas matriks.

## Catatan tambahan — kesalahan skrip otomasi (BUKAN crash aplikasi)

Selama D2.16 (percobaan pertama), skrip PowerShell saya keliru mengklik
tombol "Close" pada TITLE BAR MainWindow (bukan pada dialog wizard impor
yang dimaksud, karena window sudah kembali ke MainWindow sebelum baris kode
itu jalan) — ini memicu `Application.Current.Shutdown()` yang SAH lewat
tombol X MainWindow (perilaku yang memang didesain begitu). Dikonfirmasi
BUKAN crash: tidak ada entri log baru di `logs/20260921.log` sesudah
kejadian ini (kalau ini exception tak tertangani, PencatatGalat pasti
mencatatnya). Aplikasi dijalankan ulang dari kondisi bersih untuk
melanjutkan D2.16.
