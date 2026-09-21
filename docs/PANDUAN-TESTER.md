# Panduan Smoke-Test Manual — Alur Instalasi Pertama

Dokumen ini untuk developer/tester (bukan TU sekolah). Berisi langkah reproduksi manual untuk skenario yang **tidak bisa** diuji lewat xUnit — `System.Windows.Application` adalah singleton per proses, jadi pengujian yang butuh menjalankan siklus hidup `Application`/`Window` WPF sungguhan (bukan sekadar ViewModel) tidak bisa dijalankan sebagai automated test biasa tanpa merusak sisa test suite. Jalankan manual setiap kali menyentuh `App.xaml.cs`, `App.xaml`, atau jendela apa pun yang dibuka langsung dari `OnStartup`.

## Kenapa Ini Perlu Diuji Manual

RENCANA-TUGAS.md Tugas 14 menemukan bug nyata yang **lolos dari 318 unit test**: sebelum `App.xaml` diberi `ShutdownMode="OnExplicitShutdown"`, menyelesaikan wizard instalasi pertama membuat aplikasi **keluar diam-diam** alih-alih membuka layar Masuk — karena WPF bawaan (`ShutdownMode="OnLastWindowClose"`) mematikan `Application` begitu jendela wizard (jendela pertama dan saat itu satu-satunya yang terbuka) ditutup, sebelum baris kode berikutnya sempat membuka `LoginWindow`. Tidak ada unit test yang menjalankan `OnStartup` WPF sungguhan, jadi celah ini hanya bisa ketahuan lewat menjalankan `.exe` yang benar-benar sudah di-build.

## Persiapan

1. Publish build self-contained (lihat perintah di README/CLAUDE.md bagian build, atau):
   ```
   dotnet publish src/Absensi.App/Absensi.App.csproj -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -p:PublishReadyToRun=true -p:PublishTrimmed=false -o publish/AbsensiSekolah
   ```
2. Salin folder `publish/AbsensiSekolah` ke folder BERSIH di luar repo (mis. `C:\AbsensiSekolah-SmokeTest`) — pastikan folder itu **belum punya** subfolder `data/`.

## Skenario 1 — Instalasi Baru: Wizard Selesai → Layar Masuk Tampil

**Ini skenario yang gagal sebelum perbaikan Tugas 14 — paling penting untuk selalu diulang.**

1. Jalankan `Absensi.App.exe` dari folder bersih tadi.
2. **Harap:** layar "Instalasi Pertama" muncul (judul jendela persis itu).
3. Isi kunci lisensi apa saja → klik **"Instalasi Baru"**.
4. Isi nama sekolah → **Lanjut**.
5. Isi nama tahun ajaran + tanggal mulai/selesai (pakai kalender, jangan ketik manual — DatePicker WPF kadang tidak mem-parsing teks yang diketik langsung tanpa menekan Tab/Enter) → **Lanjut**.
6. Biarkan jadwal harian dengan nilai bawaan → **Lanjut**.
7. Isi username + sandi + konfirmasi sandi Kepala Sekolah → **Selesaikan Instalasi**.
8. **Harap:** layar "Instalasi selesai" muncul dengan nama akun yang baru dibuat.
9. Klik **"Buka Halaman Masuk"**.
10. **HARAP (ini yang dulu gagal):** layar **Masuk** muncul, aplikasi TIDAK tertutup/keluar.
11. Login dengan akun Kepala Sekolah yang baru dibuat di langkah 7.
12. **Harap:** jendela utama (menu Dashboard/Absensi/Siswa/dst.) muncul.

**Bila langkah 10 gagal** (aplikasi keluar/proses hilang, tidak ada jendela apa pun): regresi bug Tugas 14 — periksa `App.xaml` masih punya `ShutdownMode="OnExplicitShutdown"`, dan setiap titik yang seharusnya mengakhiri aplikasi memanggil `Shutdown()` secara eksplisit (lihat komentar di `App.xaml.cs`).

## Skenario 2 — Wizard Dibatalkan (Tutup Jendela) → Aplikasi Benar-Benar Keluar

1. Jalankan `.exe` dari folder bersih (belum ada `data/absensi.db`, atau hapus dulu bila skenario 1 baru dijalankan di folder yang sama).
2. Begitu layar "Instalasi Pertama" muncul, tutup jendelanya lewat tombol X.
3. **Harap:** proses `Absensi.App.exe` benar-benar berhenti (cek Task Manager) — bukan jadi proses "hantu" tanpa jendela.

## Skenario 3 — Login Dibatalkan → Aplikasi Benar-Benar Keluar

1. Jalankan `.exe` di folder yang sudah pernah menyelesaikan wizard (layar Masuk langsung muncul).
2. Tutup layar Masuk lewat tombol X tanpa login.
3. **Harap:** proses berhenti total, tidak ada proses hantu.

## Skenario 4 — Klik "Keluar" di Jendela Utama → Aplikasi Benar-Benar Keluar

1. Login sampai jendela utama tampil.
2. Klik tombol **"Keluar"** di pojok kanan atas header.
3. **Harap:** jendela tertutup DAN proses `Absensi.App.exe` berhenti total (cek Task Manager) — bukan hanya jendelanya hilang sementara prosesnya tetap jalan di background.
4. Ulangi, tapi tutup jendela utama lewat tombol X bawaan (bukan tombol Keluar) — hasil yang diharapkan sama: proses berhenti total.

## Skenario 5 — Restore Selesai → Aplikasi Menutup Diri (Perilaku yang Memang Diharapkan)

1. Login sebagai Kepala Sekolah/Admin → menu Pengaturan → bagian Backup.
2. Pulihkan dari salah satu berkas backup lokal, konfirmasi dialog.
3. **Harap:** pesan "Restore selesai" muncul, lalu aplikasi menutup diri sepenuhnya (ini SATU-SATUNYA skenario yang memang harus otomatis menutup aplikasi tanpa interaksi tambahan — sudah benar sejak Tugas 13, dipertahankan di Tugas 14).

## Skenario 6 — Manajemen Akun (Pengaturan → Akun)

1. Login sebagai Kepala Sekolah → menu Pengaturan → tab **Akun**.
2. **Harap:** tabel akun tampil (minimal berisi baris Kepala Sekolah) tanpa error/crash saat tab pertama kali dibuka.
3. Klik **"Tambah Akun"** → pilih peran **Admin** → isi username + sandi + konfirmasi → **Simpan**.
4. **Harap:** dialog tertutup, pesan "Akun ... berhasil dibuat." muncul, akun baru tampil di tabel.
5. Klik **"Keluar"**, buka lagi aplikasi, login dengan akun Admin yang baru dibuat.
6. Menu Pengaturan → tab **Akun** → **"Tambah Akun"** → periksa dropdown Peran.
7. **Harap:** dropdown Peran untuk Admin HANYA menampilkan "Wali Kelas" — tidak ada pilihan "Admin" (PRD §3.1: Admin tidak berwenang membuat Admin lain).
8. Login sebagai Wali Kelas mana pun → menu Pengaturan.
9. **Harap:** tab "Akun" sama sekali tidak muncul (bukan hanya tombol di dalamnya yang nonaktif).

## Skenario 7 — Gerbang: Tap Kartu (PALING KRITIS — jalankan setiap kali menyentuh `GerbangWindow.xaml`)

1. Login → menu **Absensi** → **"Buka layar gerbang"**.
2. Ketik UID apa saja (kartu tidak perlu terdaftar) ke kolom tap tersembunyi, lalu Enter — bisa disimulasikan dengan mengetik langsung karena layar ini memang didesain menerima input reader keyboard-emulation.
3. **Harap:** kartu "Kartu tidak dikenal · Ditolak · [jam]" tampil di kartu besar, aplikasi TIDAK crash.
4. Ulangi 3 kali lagi (total 4 tap beruntun), tunggu 8+ detik.
5. **Harap:** hingga 4 kartu tampil bersamaan, lalu turun ke daftar ringkas di bawah — tanpa crash di kedua tampilan.

## Skenario 8 — Impor Excel (Siswa/Guru)

1. Menu **Siswa** atau **Guru** → **"Impor Excel"**.
2. **Harap:** jendela wizard impor terbuka tanpa crash (sebelumnya crash instan begitu jendela ini pertama kali tampil — lihat tabel hasil).
3. Pilih berkas Excel yang valid, sampai ke langkah Preview (ringkasan "X baru · Y diperbarui · Z bermasalah" tampil).
4. **Harap:** tidak ada crash di langkah mana pun.

## Skenario 9 — Windowing Controls per Jendela

Bug UX pasca serah terima: sebelum perbaikan ini, `LoginWindow`/`WizardInstalasiWindow`
(`ResizeMode="NoResize"` tanpa `WindowStyle` eksplisit atau dengan `WindowStyle="SingleBorderWindow"`)
menyembunyikan tombol Minimize SEKALIGUS Maximize (perilaku bawaan WPF —
`NoResize` menyembunyikan keduanya, bukan cuma Maximize), dan seluruh
dialog modal memakai `WindowStyle="ToolWindow"` yang secara visual mirip
kotak dialog kecil tak lazim. TU melaporkan tidak bisa meminimalkan atau
menutup aplikasi lewat tombol standar Windows.

Jalankan checklist ini di SETIAP jendela terdaftar, di `.exe` hasil publish
(bukan `dotnet run`, supaya font/tema non-client area sama seperti yang
dilihat TU sungguhan). Untuk tiap jendela: lihat tombol yang tampil di
title bar, klik Minimize (kalau ada) → **harap** jendela meng-kecil ke
taskbar tanpa crash → klik ikon taskbar untuk kembalikan → klik Close (X)
→ **harap** perilaku sesuai kolom "Perilaku Close".

| Jendela | Minimize | Maximize | Perilaku Close | Muncul di taskbar? |
|---|---|---|---|---|
| `LoginWindow` | Ada, aktif | Ada, **non-aktif** (abu-abu — WPF tidak punya mode "Minimize+Close tanpa tombol Maximize sama sekali", lihat catatan di `LoginWindow.xaml`) | Menutup aplikasi (jalur `Shutdown()` yang sama seperti tombol "Keluar" di layar ini, kalau ada, atau menutup window biasa) | Ya |
| `MainWindow` (Shell utama) | Ada, aktif | Ada, aktif — bisa restore ke ukuran normal dan kembali maximize | Menutup aplikasi (`MainWindow.xaml.cs` memanggil `Application.Current.Shutdown()` dari `Closed`) | Ya |
| `WizardInstalasiWindow` (langkah 1-5) | Ada, aktif | Ada, non-aktif (sama seperti Login) | Memicu dialog konfirmasi "Batalkan instalasi? Semua isian sejauh ini akan hilang." — pilih **Tidak** → wizard TETAP TERBUKA, isian tidak hilang; pilih **Ya** → wizard tertutup DAN aplikasi keluar (instalasi belum tertulis ke database) | Ya |
| `WizardInstalasiWindow` (langkah 6, "Selesai") | Ada, aktif | Ada, non-aktif | Menutup TANPA dialog konfirmasi (instalasi sudah tertulis ke database sejak langkah 5) — setara klik "Buka Halaman Masuk", lanjut ke `LoginWindow` | Ya |
| Semua dialog modal (`KonfirmasiTutupWindow`, `KonfirmasiKunciBulanWindow`, `AlasanTidakAktifWindow`, `ImporGuruWizardWindow`, `ImporSiswaWizardWindow`, `QrWindow`, `KonfirmasiRestoreWindow`, `ResetSandiWindow`, `TambahAkunWindow`, `RegistrasiKartuWindow`, `TapKartuTunggalWindow`) | **Tidak ada** (disengaja — pola dialog standar) | **Tidak ada** (disengaja) | Hanya tombol Close (X) di title bar, plus tombol Batal/Tutup di dalam dialog itu sendiri (perilaku masing-masing tidak berubah oleh perbaikan windowing ini) | **Tidak** — `ShowInTaskbar="False"` |
| `GerbangWindow` | **SENGAJA TIDAK DISENTUH** — tetap `WindowStyle="None"`, fullscreen, tanpa title bar/tombol sama sekali sesuai PRD §14.1 ("tidak bisa ditutup tanpa sandi") | — | — | — |

## Skenario 10 — Logging Galat Aktif di Release (Tahap C, docs/INVESTIGASI-CRASH-2026-09-21.md)

Ditambahkan setelah dua crash nyata (`STATUS_STACK_OVERFLOW` dan `STATUS_FATAL_USER_CALLBACK_EXCEPTION`, keduanya 2026-09-21) ditemukan lewat Windows Event Log TANPA jejak apa pun di aplikasi — mekanisme log lama (`Debug.WriteLine`) ternyata dihapus kompiler total di build Release (dihias `[Conditional("DEBUG")]`), dan tidak ada `DispatcherUnhandledException`/`AppDomain.UnhandledException`/`TaskScheduler.UnobservedTaskException` sama sekali. `PencatatGalat` (`Absensi.Data/Diagnostik/PencatatGalat.cs`) menggantikan mekanisme itu. Jalankan checklist ini di `.exe` hasil publish Release (bukan `dotnet run`) setiap kali menyentuh `PencatatGalat.cs`, ketiga handler global di `App.xaml.cs`, atau `LokasiData.FolderLog`.

1. **Folder `logs/` belum ada** → salin publish ke folder BERSIH (belum punya `data/`/`logs/`/`backup/`) → jalankan `.exe` → **harap:** folder `logs/` otomatis muncul begitu jendela pertama tampil (dipanggil `LokasiData.PastikanFolderLogAda()` di baris pertama `OnStartup`, sebelum database dibuka).
2. **Log benar-benar ditulis dari proses Release lewat aksi UI sungguhan** — **KOREKSI (2026-09-21): jangan pakai skenario "Impor Excel berkas terkunci"** — dicoba dan ternyata TIDAK melewati `PencatatGalat` sama sekali, exception-nya ditangkap lokal oleh `ImporGuruWizardViewModel.AnalisisBerkas`/`ImporSiswaWizardViewModel` yang langsung menaruh `ex.Message` ke `PesanGalat` tanpa mencatat (celah terpisah, sudah disebut `TODO-VERSI-BERIKUTNYA.md` A.3.2 soal `catch` terlalu luas — bukan untuk diperbaiki di sini). **Pakai skenario yang TERBUKTI melewati `PencatatGalat`:** di layar "Instalasi Pertama" langkah 1, ketik kunci lisensi APA SAJA (mis. `TESTPULIH001`) → klik **"Pulihkan dari PC Lama"** — TAPI sebelum itu, taruh berkas `terakhir.enc` (isi ACAK/rusak, bukan hasil `EnkripsiBackup.Enkripsi` sungguhan) dan `terakhir.txt` (isi tanggal ISO 8601 apa saja) di `backup/cloud-stub/<SHA256-HEX-KUNCI-LISENSI-huruf-besar>/` supaya `PenyimpanCloudBackupLokal.UnduhTerakhirAsync` menemukan berkasnya tapi `EnkripsiBackup.Dekripsi` gagal (tag AES-GCM tidak cocok) → **harap:** `logs/yyyyMMdd.log` (tanggal hari ini) berisi entri baru dengan sumber `WizardInstalasiViewModel.PulihkanDariPcLama`, jenis exception, stack trace, `HResult`/`Source`/`TargetSite`.
3. **Data sensitif tidak muncul di log** → periksa manual isi berkas dari langkah 2: tidak ada NISN/NIP/nama siswa-guru/token Bot Telegram/connection string/path akun Windows. Exception yang `Source`-nya `Absensi.Domain`/`Absensi.Data` harus tampil sebagai placeholder `[pesan disembunyikan — ...]`, bukan teks pesan aslinya. **Catatan dari verifikasi nyata:** skenario langkah 2 di atas menghasilkan stack trace yang mencantumkan path build `D:\Project Absensi SMP 1\src\...\EnkripsiBackup.cs:line 51` — ini BUKAN pelanggaran kebijakan (kebijakan menyasar path akun Windows `C:\Users\<nama>\...`, bukan seluruh path secara umum), tapi dicatat sebagai pengamatan: path proyek build ikut kebawa ke log kalau PDB tidak dihapus dari publish Release (`Absensi.Data.pdb` dkk. saat ini memang ikut dipublish, terpisah dari isu ini).
4. **Aplikasi tidak menampilkan dialog galat berulang** → pastikan hanya SATU kotak pesan "Aplikasi mengalami galat tak terduga..." muncul per episode crash (dijaga flag `_sedangMenanganiGalatFatal` di `App.xaml.cs`). **Status: BELUM TERVERIFIKASI DINAMIS** — lihat catatan di bawah tabel hasil, item ini sengaja tidak dipaksakan karena tidak ada exception global yang aman dan sudah bisa direproduksi lewat UI tanpa menambah hook/tombol pengujian permanen (dilarang eksplisit).
5. **Kegagalan menulis log tidak membuat aplikasi gagal dibuka atau memunculkan dialog kedua** → set folder `logs/` jadi read-only lewat Properti Windows sebelum memicu exception yang sama seperti langkah 2 → **harap:** aplikasi tetap tampil dan menampilkan SATU pesan galat yang sama seperti biasa (bukan pesan tambahan soal gagal menulis log, bukan crash tambahan). Diverifikasi lewat unit test (`Catat_TidakPernahMelemparWalauFolderLogTidakBisaDitulis`, `Catat_KegagalanMenulisUtamaDanCadangan_TetapTidakMelempar`), belum lewat klik manual folder read-only sungguhan.
6. **Rotasi menyisakan maksimum 30 berkas resmi, berkas asing tidak tersentuh** → taruh manual >30 berkas `yyyyMMdd.log` bertanggal mundur DITAMBAH satu berkas nama asing (mis. `catatan-manual.txt`) di `logs/` → jalankan `.exe` sampai menulis satu entri log baru → **harap:** berkas `yyyyMMdd.log` tersisa tepat 30 (yang tanggalnya terbaru, termasuk punya hari ini), `catatan-manual.txt` masih ada dengan isi tidak berubah.

**Verifikasi yang SUDAH dijalankan (update 2026-09-21, sesi hardening kedua):**
- **15 unit test `PencatatGalatTests`** (bertambah dari 11: +2 pengujian `AggregateException` — pesan gabungan bawaan .NET tidak bocor tapi InnerException non-sensitif tetap tampil, dan pemotongan saat InnerExceptions terlalu banyak; +2 pengujian berkas cadangan `_gagal-tulis.log` — batas ukuran 1 MB direset bukan tumbuh terus, dan kegagalan jalur utama MAUPUN cadangan sekaligus tetap tidak melempar) — **lulus semua di konfigurasi Debug MAUPUN Release** (`dotnet test -c Release`).
- **Langkah 1 dan 2 di atas SUDAH diverifikasi lewat `.exe` Release sungguhan** (bukan cuma unit test) — didorong lewat UI Automation asli (`System.Windows.Automation`, `InvokePattern`/`ValuePattern` pada tombol dan kolom sungguhan, bukan memanggil method ViewModel langsung): wizard instalasi baru dituntaskan penuh lewat klik tombol sungguhan (lisensi → identitas sekolah → tahun ajaran, termasuk `DatePicker` yang ternyata perlu format `dd/MM/yyyy` karena kultur aplikasi `id-ID`, bukan `M/d/yyyy` — dicatat di sini supaya tidak perlu ditemukan ulang → jadwal harian default → akun Kepala Sekolah → login sungguhan → `MainWindow` tampil), lalu instalasi TERPISAH yang sengaja dihentikan di langkah 1 dengan skenario "Pulihkan dari PC Lama" + berkas `cloud-stub` yang sengaja dirusak (lihat langkah 2 di atas) — exe yang dijalankan di-hash (`sha256sum`) dan dikonfirmasi identik dengan hasil `dotnet publish` terbaru sebelum dipakai. Hasil: `logs/20260921.log` benar-benar berisi entri nyata (contoh lengkap di `INVESTIGASI-CRASH-2026-09-21.md` §6), aplikasi tetap hidup dan responsif (`Get-Process` mengonfirmasi `Responding=True`) sesudahnya, pesan yang ditampilkan ke pengguna ramah dan tidak memuat stack trace.
- **Item 4 (dialog tidak berulang) ditandai BELUM TERVERIFIKASI DINAMIS** — sesuai arahan eksplisit, tidak dipaksakan dan tidak ditambah tombol/hook pengujian permanen untuk memicu exception global buatan. Diverifikasi lewat pembacaan kode saja (flag `_sedangMenanganiGalatFatal`). Verifikasi dinamisnya menunggu Tahap D (reproduksi crash terkontrol), bukan sesi ini.
- **Item 3 dan 5 diverifikasi lewat unit test**, belum lewat klik manual folder read-only sungguhan di `.exe` — dianggap cukup karena unit test menjalankan kode Release yang identik.

**Hasil smoke-test build sebelumnya (masih berlaku, tidak diulang di sesi ini):**

**Hasil smoke-test build ini:** build berhasil (`dotnet build`, 0 error),
seluruh 336 unit test hijau (termasuk 3 test baru `MainWindowViewModelTests`
untuk `Logo`/`TitleJendela`), dan tabel di atas sudah diverifikasi lewat
`.exe` hasil publish sungguhan (Windows UI Automation `WindowPattern` +
pesan `WM_SYSCOMMAND`/`SC_CLOSE` asli — simulasi paling setia dari klik
tombol X sungguhan, bukan sekadar memanggil `Window.Close()` dari kode):

- `LoginWindow`: `CanMinimize=True`, `CanMaximize=False` terkonfirmasi;
  Minimize → taskbar → restore berjalan normal, proses tetap hidup.
- `MainWindow`: `CanMinimize=True`, `CanMaximize=True` terkonfirmasi;
  terbuka dalam kondisi `Maximized` secara default; siklus penuh
  Maximize → Normal → Maximize → Minimize → Maximize berjalan tanpa
  masalah; judul terkonfirmasi tampil sebagai "Absensi - SMP Final Test"
  (mengikuti nama sekolah sungguhan, bukan teks statis "Absensi"); tutup
  lewat X mematikan seluruh aplikasi seperti diharapkan.
- `WizardInstalasiWindow`: `CanMinimize=True`, `CanMaximize=False`
  terkonfirmasi di langkah 1 maupun langkah 6. Tutup lewat X di langkah 1
  memicu dialog "Batalkan instalasi?" — ditekan **Tidak** (lewat ESC,
  yang secara baku dipetakan ke tombol No pada `MessageBoxButton.YesNo`)
  → wizard TETAP TERBUKA, proses tetap hidup, judul tidak berubah; ditekan
  **Ya** → wizard tertutup DAN aplikasi keluar sepenuhnya. Tutup lewat X
  di langkah 6 ("Selesai") TIDAK memicu dialog apa pun — langsung lanjut
  ke `LoginWindow` (judul berubah jadi "Masuk — Absensi"), persis seperti
  klik "Buka Halaman Masuk", proses tetap hidup. **Bug ditemukan &
  diperbaiki dalam sesi yang sama** selagi memverifikasi ini: mengisi
  `DialogResult` langsung di dalam event `Closing` melempar
  `InvalidOperationException` yang diserap diam-diam oleh WPF (X jadi
  tidak bereaksi sama sekali, tanpa crash terlihat) — diperbaiki dengan
  membatalkan penutupan yang sedang berjalan (`e.Cancel = true`) lalu
  menjadwalkan `DialogResult = true; Close();` lewat
  `Dispatcher.BeginInvoke`, persis seperti alur tombol "Buka Halaman
  Masuk". Dikonfirmasi lewat log diagnostik sementara (dihapus lagi
  setelah terverifikasi) bahwa urutan Closing → e.Cancel → BeginInvoke →
  Closing ulang (DialogResult sudah true) → Close berjalan tepat seperti
  dirancang.
- Dialog modal: perubahan `WindowStyle`/`ResizeMode`/`ShowInTaskbar` di
  seluruh 11 jendela bersifat deklaratif murni (atribut XAML, sama persis
  dengan pola `ResizeMode="NoResize"` yang sudah terverifikasi
  menghasilkan `CanMinimize=False`/`CanMaximize=False` di jendela lain
  pada sesi yang sama) — tidak diuji klik manual satu per satu karena
  sesi interaktif ini sempat terganggu aktivitas latar tak terduga di
  desktop bersama (jendela "Save As" muncul sendiri di tengah pengujian,
  bukan hasil skrip pengujian — lihat catatan di riwayat sesi). Tester
  disarankan tetap mengecek sekali secara visual sebelum serah terima,
  tapi risikonya rendah karena tidak ada logika kode kustom di baliknya.

---

## Hasil Smoke-Test Terakhir

| Tanggal | Skenario | Hasil | Catatan |
|---|---|---|---|
| 2026-09-19 (percobaan 1, build sebelum perbaikan XAML) | 1 (Instalasi Baru → Layar Masuk) | ✅ Lulus | Bug shutdown Tugas 14 terkonfirmasi teratasi — `MainWindowTitle` berpindah dari wizard ke `'Masuk — Absensi'` tanpa proses keluar. |
| 2026-09-19 (percobaan 1) | 6 (Manajemen Akun) | ❌ Gagal — **ditemukan & diperbaiki dalam sesi yang sama** | Aplikasi **crash total** (APPCRASH, dikonfirmasi lewat Windows Event Log) begitu tab "Akun" pertama kali dirender: `InvalidOperationException` — `DataGridCheckBoxColumn` kolom "Aktif" memakai binding TwoWay bawaan ke properti `AkunDaftarTampilan.Aktif` yang tidak punya setter. Diperbaiki dengan `Mode=OneWay` di `AkunView.xaml`. Tidak tertangkap oleh 333 unit test (tidak ada yang benar-benar merender XAML ini). |
| 2026-09-19 (percobaan 2, setelah perbaikan) | 1, 4 (Keluar), 6 | ✅ Semua lulus | Wizard → Masuk → tab Akun render tanpa crash → buat akun `admin1` (peran Admin) berhasil, tampil di tabel → tombol Keluar mematikan proses sepenuhnya → relaunch langsung ke layar Masuk (wizard tidak muncul lagi) → login sebagai `admin1` → dropdown Peran di dialog Tambah Akun HANYA menampilkan "Wali Kelas", tidak ada "Admin" (PRD §3.1 terkonfirmasi). |
| 2026-09-19 (percobaan 2) | 6, langkah buat Wali Kelas dengan guru sungguhan | ⚠️ Tidak dituntaskan manual | Instalasi baru belum punya data guru (butuh impor Excel dulu) — jalur ini divalidasi lewat pengujian otomatis lapisan layanan (`LayananAkunTests.BuatAkun_AdminBuatWaliKelas_Berhasil`, dkk.), bukan lewat klik manual, supaya smoke-test tidak melebar ke skenario impor Excel. |
| 2026-09-19 | 2, 3, 5 | ⚠️ Belum diulang manual di percobaan 2 | Sudah diverifikasi jalur `Shutdown()`-nya identik dengan Skenario 1/4 (semua lewat pemanggilan eksplisit yang sama di `App.xaml.cs`) — tidak diulang manual demi waktu, tapi tidak ada perubahan kode di titik-titik itu sejak percobaan 1. |
| 2026-09-19 (percobaan 3) | 8 (Impor Excel Guru) | ❌ Gagal — **ditemukan & diperbaiki dalam sesi yang sama, TIDAK terkait Tugas 13/14** | Aplikasi **crash total** (APPCRASH lain, dikonfirmasi lewat Windows Event Log) begitu jendela "Impor Excel Guru" pertama kali dirender: `InvalidOperationException` — properti read-only sama sekali TIDAK bisa diikat ke `Run.Text` karena `Run.Text` TwoWay/OneWayToSource secara BAWAAN di WPF (beda dari `TextBlock.Text` yang OneWay bawaan). Pola `<Run Text="{Binding JumlahBaru}">` di `ImporGuruWizardWindow.xaml`/`ImporSiswaWizardWindow.xaml` sudah ada sejak **Tugas 7** — bukan regresi Tugas 13/14, hanya baru ketahuan sekarang karena baru sekarang ada smoke-test .exe sungguhan atas layar ini. |
| 2026-09-19 (percobaan 3) | — (audit menyeluruh setelah temuan di atas) | 🔴 **4 lokasi lain dengan pola identik ditemukan by inspection, DIPERBAIKI SEKALIGUS sebelum sempat menyebabkan crash lagi** | Disisir seluruh `<Run Text="{Binding ...}">` di `src/Absensi.App` (grep menyeluruh): `GerbangWindow.xaml` (2 lokasi, **kartu tap kartu besar DAN daftar ringkas** — lihat di bawah, ini yang paling kritis), `Pengaturan/KonfirmasiRestoreWindow.xaml` (`HariDataHilang`, ditulis sendiri di Tugas 13/14), `Laporan/LaporanView.xaml` (rekap kehadiran per siswa/guru — secara teknis aman karena `record` positional pakai `{ get; init; }`, tetap diberi `Mode=OneWay` demi konsistensi dan menghilangkan keraguan). Setelah perbaikan, dipastikan **nol** `<Run Text="{Binding` tersisa di seluruh `src/Absensi.App` tanpa `Mode=OneWay`. |
| 2026-09-19 (percobaan 3) | 7 (Gerbang: Tap Kartu) — **PALING KRITIS** | ✅ Lulus setelah perbaikan | **Sebelum diperbaiki, ini akan crash pada TAP PERTAMA di sekolah manapun yang memasang aplikasi** — layar gerbang adalah layar yang berjalan sepanjang hari, setiap hari. Setelah `Mode=OneWay` ditambahkan ke `TeksStatus`/`TeksJam`/`Nama` (dua lokasi: kartu besar dan daftar ringkas), diuji dengan 4 tap kartu tak dikenal beruntun: kartu "Kartu tidak dikenal · Ditolak · [jam]" tampil benar di kartu besar, lalu turun ke daftar ringkas setelah 8 detik — proses tetap hidup dan responsif di kedua kondisi. |
| 2026-09-19 (percobaan 3) | 8 (Impor Excel Guru, setelah perbaikan) | ✅ Jendela wizard terbuka tanpa crash | Klik "Impor Excel" di layar Guru → jendela "Impor Excel Guru" tampil normal (langkah 1 "Analisis"). Pemilihan berkas lewat dialog Open bawaan Windows tidak dituntaskan (otomasi dialog native OS di luar kendali kode aplikasi, bukan risiko bug aplikasi) — jalur `AnalisisBerkas`/impor sendiri sudah tercakup pengujian otomatis (`KoreksiTugas7Tests`, dkk.), fokus smoke-test ini adalah memastikan JENDELA-nya sendiri tidak crash saat dibuka, dan itu sudah terkonfirmasi. |
| 2026-09-19 (percobaan 3) | Regresi penuh | ✅ 333/333 unit test tetap hijau | Tidak ada satu pun dari 5 lokasi yang diperbaiki tertangkap unit test manapun — mengonfirmasi celah nyata dalam cakupan pengujian otomatis untuk BINDING XAML (baru bisa diketahui lewat smoke-test `.exe` sungguhan), bukan berarti perbaikannya merusak sesuatu yang sebelumnya teruji. |
| 2026-09-19 (bug windowing pasca serah terima) | 9 (Windowing Controls) | ❌ Gagal — **ditemukan & diperbaiki dalam sesi yang sama** | TU melaporkan tidak bisa Minimize/Close lewat tombol standar. Akar masalah: `ResizeMode="NoResize"` di `LoginWindow`/`WizardInstalasiWindow` menyembunyikan tombol Minimize SEKALIGUS Maximize (perilaku bawaan WPF), dan semua dialog memakai `WindowStyle="ToolWindow"` (title bar mini tak lazim). Diperbaiki: Login/Wizard → `ResizeMode="CanMinimize"` (Minimize aktif, Maximize tampil non-aktif — WPF tidak punya mode "Minimize tanpa Maximize sama sekali"); MainWindow → `ResizeMode="CanResize"` + `WindowState="Maximized"` + `Icon`/`Title` dinamis (logo & nama sekolah, sebelumnya tak ada sama sekali); 11 dialog → `WindowStyle="SingleBorderWindow"` + `ShowInTaskbar="False"` (dari `ToolWindow`). |
| 2026-09-19 (bug windowing) | 9, langkah tutup Wizard lewat X di langkah 6 | ❌ Gagal — **ditemukan & diperbaiki dalam sesi yang sama, TIDAK terkait bug di atas** | Wizard tidak punya tombol Batal terpisah — semula X di langkah manapun langsung menutup tanpa konfirmasi. Ditambahkan `Closing` + `MessageBox` "Batalkan instalasi?" (langkah 1-5); langkah 6 (setelah "Selesaikan Instalasi", data sudah tertulis) sengaja dilewatkan. Percobaan pertama: mengisi `DialogResult` langsung di dalam `Closing` melempar `InvalidOperationException` yang diserap DIAM-DIAM oleh WPF di batas WndProc — X jadi tidak bereaksi sama sekali TANPA crash terlihat sama sekali (baru ketahuan lewat log diagnostik sementara, event log Windows kosong). Diperbaiki dengan `e.Cancel = true` + `Dispatcher.BeginInvoke(() => { DialogResult = true; Close(); })` — dikonfirmasi lewat log bahwa urutan Closing→cancel→BeginInvoke→Closing ulang (DialogResult sudah true)→Close berjalan tepat seperti dirancang, lanjut ke `LoginWindow`, proses tetap hidup. |
| 2026-09-19 (bug windowing, setelah perbaikan) | 9 (lengkap) | ✅ Lulus | Diverifikasi lewat `.exe` publish sungguhan dengan `WindowPattern` UI Automation + `WM_SYSCOMMAND`/`SC_CLOSE` asli (simulasi paling setia klik X sungguhan — `WindowPattern.Close()` COM terbukti TIDAK selalu memicu `Closing` secara andal di lingkungan ini, jangan dipakai lagi untuk uji serupa di masa depan): Login (`CanMinimize=True`/`CanMaximize=False`, Minimize/Restore OK), MainWindow (`CanMinimize=True`/`CanMaximize=True`, terbuka Maximized, siklus Minimize/Maximize/Restore penuh OK, judul "Absensi - SMP Final Test" mengikuti sekolah sungguhan, tutup X mematikan aplikasi), Wizard langkah 1 (konfirmasi Tidak→tetap terbuka, Ya→keluar total) dan langkah 6 (langsung ke Login tanpa konfirmasi). Dialog modal tidak diuji klik manual satu-satu (deklaratif murni, pola sama seperti yang sudah terverifikasi) karena sesi sempat terganggu aktivitas latar tak terduga di desktop bersama. 336/336 unit test tetap hijau (+3 baru untuk `Logo`/`TitleJendela`). |
| 2026-09-21 (Tahap B/C docs/INVESTIGASI-CRASH-2026-09-21.md) | 10, langkah 1 (folder `logs/` belum ada) | ✅ Lulus | Publish Release baru (`dotnet publish` persis perintah di atas) disalin ke folder bersih `C:\AbsensiSekolah-VerifikasiLog` (tanpa `data/`/`logs/`/`backup/`) → dijalankan → proses hidup dengan judul jendela "Instalasi Pertama — Absensi" (terkonfirmasi lewat `Get-Process`) → folder `logs/` terkonfirmasi ADA (kosong, karena belum ada exception yang terjadi selama percobaan ini) begitu proses berjalan. Folder verifikasi dihapus lagi setelah selesai (bukan folder instalasi sungguhan). |
| 2026-09-21 (Tahap B/C) | 10, langkah 2-6 (exception nyata lewat UI, dialog tunggal, folder read-only, rotasi lewat proses berjalan) | ⚠️ **Belum dituntaskan manual sesi ini** | Butuh navigasi UI berlapis (login → menu → Impor Excel → dialog Open bawaan Windows dengan berkas terkunci) yang tidak dikerjakan sesi ini karena batasan waktu — DIGANTIKAN untuk sesi ini oleh 11 unit test `PencatatGalatTests` yang lulus di konfigurasi Debug DAN Release (`dotnet test -c Release`, terhadap `Absensi.Data.dll` Release yang PERSIS sama dibundel ke `Absensi.App.exe` hasil publish), yang membuktikan format baris, sanitasi (path akun Windows, pesan Absensi.Domain/Data disembunyikan, pola token/connection-string/angka panjang diredaksi), tidak pernah melempar walau dipanggil berulang saat penulisan gagal, dan rotasi 30-berkas-resmi-tanpa-menyentuh-berkas-asing — SEMUA di luar jendela UI mana pun. **Direkomendasikan sebagai tindak lanjut manual sebelum Tahap D (reproduksi crash) dimulai**, supaya perilaku 3 handler global (`DispatcherUnhandledException`/`AppDomain.UnhandledException`/`TaskScheduler.UnobservedTaskException`) ikut terverifikasi lewat jendela sungguhan, bukan cuma lewat pemanggilan `PencatatGalat.Catat` langsung di unit test. |
| 2026-09-21 (Tahap B/C) | Regresi penuh | ✅ 347/347 unit test hijau (336 lama + 11 baru `PencatatGalatTests`) | `dotnet test Absensi.sln` konfigurasi Debug: semua proyek (`Absensi.Domain.Tests` 54, `Absensi.Data.Tests` 168, `Absensi.Laporan.Tests` 13, `Absensi.Telegram.Tests` 8, `Absensi.App.Tests` 104) hijau. Tidak ada perubahan pada test yang sudah ada — murni penambahan. `dotnet build Absensi.sln` (Debug) dan `dotnet publish ... -c Release` (per perintah Persiapan di atas) sama-sama 0 error. |
| 2026-09-21 (sesi hardening kedua — A.1/A.2 docs/INVESTIGASI-CRASH-2026-09-21.md) | 10, langkah 2 — **DIULANG dengan skenario yang benar** | ✅ Lulus, lewat UI Automation sungguhan (bukan pemanggilan method langsung) | Skenario "Impor Excel berkas terkunci" yang direkomendasikan baris tabel di atas **TERNYATA SALAH** — dicoba dibaca kodenya lebih dulu sebelum eksekusi, ketahuan `ImporGuruWizardViewModel.AnalisisBerkas` menangkap exception LOKAL tanpa memanggil `PencatatGalat` sama sekali (celah terpisah, sudah ada di `TODO-VERSI-BERIKUTNYA.md` A.3.2). Diganti skenario "Pulihkan dari PC Lama" + berkas `backup/cloud-stub/<hash>/terakhir.enc` yang sengaja dirusak (lihat Skenario 10 langkah 2 di atas untuk detail persis) — DITUNTASKAN penuh lewat `System.Windows.Automation` (`InvokePattern`/`ValuePattern` pada kontrol sungguhan, `.exe` yang dijalankan di-hash dan dikonfirmasi = hasil publish terbaru): wizard dituntaskan penuh sekali (lisensi→login→MainWindow, memverifikasi juga bahwa `DatePicker` butuh format `dd/MM/yyyy` bukan `M/d/yyyy` — kultur aplikasi `id-ID`), lalu percobaan terpisah dihentikan sengaja di langkah 1 dengan backup cloud rusak → `logs/20260921.log` terkonfirmasi berisi entri nyata (`WizardInstalasiViewModel.PulihkanDariPcLama`, `AuthenticationTagMismatchException`, stack trace lengkap), aplikasi tetap `Responding=True` sesudahnya, pesan ke pengguna ramah tanpa stack trace. |
| 2026-09-21 (sesi hardening kedua) | 10, langkah 4 (dialog tidak berulang) | ⛔ **BELUM TERVERIFIKASI DINAMIS** (status resmi, bukan kegagalan) | Sesuai arahan eksplisit: tidak dipaksakan, tidak ditambah tombol/hook/env var pengujian permanen untuk memicu exception global buatan. Tidak ada exception global (`DispatcherUnhandledException`) yang aman DAN sudah bisa direproduksi lewat UI ditemukan sesi ini. Menunggu Tahap D. |
| 2026-09-21 (sesi hardening kedua) | Regresi penuh | ✅ 351/351 unit test hijau (Debug DAN Release), 4 test baru (`PencatatGalatTests`: 2 `AggregateException`, 2 batas ukuran/kegagalan ganda berkas cadangan) | `dotnet test Absensi.sln -c Debug` DAN `-c Release` masing-masing 351/351 (54+172+13+8+104). `dotnet publish` Release/self-contained ulang, 0 error, exe di-hash sebelum dipakai untuk verifikasi UI. |
