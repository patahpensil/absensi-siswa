# Panduan Smoke-Test Manual — Alur Instalasi Pertama

Dokumen ini untuk developer/tester (bukan TU sekolah). Berisi langkah reproduksi manual untuk skenario yang **tidak bisa** diuji lewat xUnit — `System.Windows.Application` adalah singleton per proses, jadi pengujian yang butuh menjalankan siklus hidup `Application`/`Window` WPF sungguhan (bukan sekadar ViewModel) tidak bisa dijalankan sebagai automated test biasa tanpa merusak sisa test suite. Jalankan manual setiap kali menyentuh `App.xaml.cs`, `App.xaml`, atau jendela apa pun yang dibuka langsung dari `OnStartup`.

## Kenapa Ini Perlu Diuji Manual

RENCANA-TUGAS.md Tugas 14 menemukan bug nyata yang **lolos dari 318 unit test**: sebelum `App.xaml` diberi `ShutdownMode="OnExplicitShutdown"`, menyelesaikan wizard instalasi pertama membuat aplikasi **keluar diam-diam** alih-alih membuka layar Masuk — karena WPF bawaan (`ShutdownMode="OnLastWindowClose"`) mematikan `Application` begitu jendela wizard (jendela pertama dan saat itu satu-satunya yang terbuka) ditutup, sebelum baris kode berikutnya sempat membuka `LoginWindow`. Tidak ada unit test yang menjalankan `OnStartup` WPF sungguhan, jadi celah ini hanya bisa ketahuan lewat menjalankan `.exe` yang benar-benar sudah di-build.

## Persiapan

1. Publish build self-contained (lihat perintah di README bagian build, atau):
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
2. Pulihkan dari salah satu berkas backup lokal, konfirmasi dialog. Backup yang dibuat sejak 12 November 2026 terenkripsi kunci lisensi; backup dari instalasi dengan kunci lisensi lain harus ditolak dengan pesan, tanpa mengubah data.
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

## Skenario 11 — Jalur Keluar Layar Gerbang (UAT 2026-09-22)

Ditambahkan setelah UAT melaporkan dialog "Tutup layar gerbang?" nyaris tidak bisa dipakai: tombol utamanya terpotong di luar area jendela (`KonfirmasiTutupWindow` punya `Height="320"` tetap, lebih kecil dari konten sungguhan), dan `GerbangWindow` memakai `Topmost="True"` yang menutupi Task Manager/Alt+Tab/Start — kombinasi keduanya membuat operator nyaris terjebak di layar gerbang tanpa jalur keluar yang berfungsi. `KonfirmasiTutupViewModel` (logika autentikasi) TIDAK berubah — hanya `KonfirmasiTutupWindow.xaml`/`.xaml.cs` dan `GerbangWindow.xaml`/`.xaml.cs`. Sebagian besar langkah di bawah ini **tidak bisa diuji lewat xUnit** (alasan sama seperti seluruh dokumen ini: siklus hidup `Window`/`Application` WPF sungguhan) — jalankan manual di `.exe` publish setiap kali menyentuh salah satu dari keempat berkas itu.

1. **Dialog muat di 1366×768, DPI 100/125/150%** → di layar gerbang (Login → Absensi → "Buka layar gerbang"), tekan Alt+F4 → **harap:** dialog "Tutup layar gerbang?" tampil UTUH — judul, deskripsi, kolom Username, kolom Sandi, DAN kedua tombol ("Batal" dan "Tutup layar gerbang") semua terlihat dan tidak terpotong, di ketiga skala DPI Windows (ubah lewat Pengaturan Tampilan Windows, restart `.exe` tiap ganti skala).
2. **Pesan galat tidak memotong tombol** → isi username/sandi salah → klik "Tutup layar gerbang" → **harap:** pesan "Username atau sandi salah." tampil, jendela bertambah tinggi otomatis (`SizeToContent="Height"`), kedua tombol tetap terlihat penuh di bawah pesan.
3. **Fokus awal di Username** → begitu dialog tampil (langkah 1), **harap:** kursor ketik sudah aktif di kolom Username tanpa perlu klik.
4. **Tab mengunjungi kedua tombol** → dari kolom Username, tekan Tab berulang → **harap:** urutan fokus Username → Sandi → Batal → Tutup layar gerbang, kedua tombol menunjukkan indikator fokus (garis biru, lihat `IsKeyboardFocused` trigger di `TombolDasarStyle`) dan bisa dijalankan dengan Enter/Space saat fokus.
5. **Enter menjalankan konfirmasi** → isi username/sandi kepala sekolah/admin yang benar di kolom manapun → tekan Enter (bukan klik) → **harap:** layar gerbang tertutup dengan bersih (sama seperti klik tombol "Tutup layar gerbang").
6. **Escape membatalkan, bukan menutup layar gerbang** → buka dialog lagi (Alt+F4) → ketik apa saja → tekan Escape → **harap:** HANYA dialog yang tertutup (`DialogResult=false`), layar gerbang tetap terbuka dan fokus kembali ke kolom tap (lihat `FokusKolomTap()` di cabang `else`).
7. **Autentikasi salah tetap di layar gerbang** → ulangi dengan username/sandi salah, atau akun Wali Kelas dengan sandi benar → **harap:** dialog TETAP terbuka, pesan galat sesuai (`"Username atau sandi salah."` atau `"Hanya kepala sekolah atau admin yang bisa menutup layar gerbang."`), layar gerbang tidak tertutup.
8. **Topmost dilepas — Alt+Tab/Task Manager/Start tidak terhalang** → dengan layar gerbang aktif (dialog TIDAK terbuka), tekan Alt+Tab → **harap:** switcher Windows tampil DI ATAS layar gerbang, bisa pindah ke aplikasi lain. Tekan Ctrl+Shift+Esc → **harap:** Task Manager tampil di atas, tidak tersembunyi di belakang. Tekan tombol Windows → **harap:** Start menu tampil normal. Layar gerbang harus tetap fullscreen (tanpa title bar) selama tidak ada aplikasi lain yang difokuskan — perilaku fullscreen TIDAK dihapus, hanya `Topmost` yang dilepas.
9. **X dan Alt+F4 sama-sama membuka autentikasi, bukan jalan pintas** → `GerbangWindow` tetap `WindowStyle="None"` (sengaja tanpa title bar/tombol X terlihat, PRD §14.1 "tanpa mouse") — satu-satunya jalur adalah Alt+F4, yang memanggil `Close()` lalu dicegat `OnClosing()` persis sama seperti X akan dicegat kalau title bar-nya ada. **Harap:** Alt+F4 SELALU memunculkan dialog autentikasi, layar gerbang tidak pernah tertutup langsung tanpa dialog.
10. **Tidak ada dialog dobel** → tekan Alt+F4 dua kali berturut-turut dengan sangat cepat → **harap:** hanya SATU dialog "Tutup layar gerbang?" muncul (dijaga flag `_dialogTutupSedangTerbuka` di `GerbangWindow.xaml.cs`, pola sama seperti `_sedangMenanganiGalatFatal`).
11. **Regresi Skenario 7** → setelah verifikasi di atas, ulangi Skenario 7 (tap kartu) sekali → **harap:** tidak ada regresi, perilaku tap masih identik (perubahan sesi ini tidak menyentuh `KolomTap_PreviewKeyDown`/`ProsesTap`).

**Yang DIUJI otomatis (bukan manual)** untuk sesi ini: `KonfirmasiTutupViewModelTests` — keadaan awal `KonfirmasiCommand.CanExecute` (nonaktif saat kosong, aktif saat Username+Sandi terisi) dan `PesanGalat` kosong di awal. Logika autentikasi (peran/sandi benar-salah) sudah dites sebelumnya dan **tidak berubah** di sesi ini.

## Skenario A — Registrasi Kartu membaca kelas (UAT Bug #11, 2026-09-22, koreksi status 2026-09-23)

**STATUS (koreksi 2026-09-23): menunggu UAT LENGKAP — TIDAK ditutup.** UAT lanjutan mengonfirmasi dropdown Kelas TERBUKA dan DAPAT DIPILIH — akar masalah fokus ComboBox (putaran keempat di bawah) terbukti benar diperbaiki. **Ini baru membuktikan sebagian alur.** Belum dikonfirmasi: siswa kelas terpilih tampil benar, tap kartu RFID diproses, tombol Simpan berhasil menulis ke database, dan kartu yang didaftarkan benar-benar tercatat/terverifikasi. Bug #11 tetap terbuka sampai langkah 1-9 di bawah (seluruh alur pilih kelas → tap → Simpan → verifikasi) dikonfirmasi lulus UAT, bukan hanya langkah 1-9 sebagian. Lihat A-30 di `MASTER-BUG-UIUX-REGISTER.md`. Riwayat 4 putaran investigasi di bawah dipertahankan apa adanya.

**Putaran keempat (2026-09-23) — AKAR MASALAH SEBENARNYA ditemukan lewat logging produksi sungguhan (bukan harness):** `KolomTap_LostFocus` merebut fokus balik dari ComboBox Kelas SETIAP kali operator klik untuk membuka dropdown-nya — menutup paksa dropdown itu sebelum sempat memilih apa pun. Terbukti dari `logs/20260923.log` nyata: `ComboBox.DropDownOpened` tercatat puluhan kali berturut-turut dalam ~5 detik, `SelectedItem` selalu `(null)` — pola loop buka-tutup paksa, bukan interaksi normal. Diperbaiki: `KolomTap_LostFocus` tidak lagi merebut fokus selama `ComboBox.IsDropDownOpen`, dan `ComboBox.DropDownClosed` (baru) yang mengembalikan fokus ke `KolomTap` setelah operator selesai memilih. Jembatan `PresentationTraceSources.DataBindingSource` → `PencatatGalat` (`App.xaml.cs`) **dipertahankan permanen** — kegagalan binding WPF di layar mana pun sekarang tercatat di `logs/`. Lihat A-30 di `MASTER-BUG-UIUX-REGISTER.md` untuk detail lengkap.

**Putaran ketiga (2026-09-23) — regresi ditemukan dan diperbaiki:** pesan "Kelas ini belum punya siswa" (ditambahkan putaran kedua) ternyata tampil SEBELUM kelas dipilih (bug di `TidakAdaSiswaSekarang`, hanya cek `SiswaSekarang is null` yang TRUE sejak dialog dibuka) — diperbaiki, sekarang mensyaratkan kelas sudah benar-benar dipilih dulu. Verifikasi tambahan: ComboBox SUNGGUHAN disimulasikan dipilih (bukan cuma dibaca `ItemsSource`-nya) terhadap database UAT nyata (4 kelas) — `KelasTerpilih` ikut terisi benar, 0 binding error WPF tertangkap. Lihat A-29 di `MASTER-BUG-UIUX-REGISTER.md` untuk detail lengkap.

**Status: Implemented — menunggu UAT manual pengguna. TIDAK diklaim selesai.** Putaran pertama (2026-09-22) diperbaiki: `RegistrasiKartuViewModel.cs` (kelas/siswa dimuat dalam try/catch, properti `TahunAjaranTanpaKelas`/`GagalMuatKelas`/`PesanMuatKelasGagal` baru), `RegistrasiKartuWindow.xaml` (teks "(PRD §16.5)" dihapus dari UI, pesan empty-state/gagal-muat baru, `SizeToContent="Height"`), `App.xaml.cs`/`Shell/MainWindow.xaml.cs` (klik "Registrasi" tanpa tahun ajaran aktif dulu MELEMPAR exception tak tertangani sampai crash handler global dan **menutup seluruh aplikasi** — sekarang menampilkan pesan dan tidak membuka jendela).

**Putaran kedua (2026-09-23) — audit mendalam terhadap database UAT NYATA** (`publish/Bug11-12-UAT/data/absensi.db`, tempat `7A-UAT`/`7B-UAT`/`7C-UAT` dibuat): data diperiksa langsung (hanya 1 tahun ajaran, ketiga kelas `tahun_ajaran_id` cocok persis), query SQL asli dijalankan manual (mengembalikan 3 baris benar), `RegistrasiKartuViewModel`+`RegistrasiKartuWindow` SUNGGUHAN dijalankan lewat `Show()` + Dispatcher dipompa (meniru siklus hidup aplikasi asli) terhadap SALINAN database itu — ComboBox menampilkan 3 item dengan benar. **Skenario persis yang dilaporkan operator (dropdown kosong meski kelas ada, setelah restart) TIDAK berhasil direproduksi** dari kode maupun data yang diperiksa — dicatat jujur, BUKAN pernyataan bug tidak nyata. Perbaikan tambahan putaran ini (di luar dugaan akar masalah): pesan "Kelas ini belum punya siswa terdaftar di sistem." vs "Seluruh siswa di kelas ini sudah terdaftar." sekarang dibedakan eksplisit (sebelumnya satu teks statis dipakai untuk keduanya). Diverifikasi otomatis lewat `RegistrasiKartuViewModelTests` (14 test total, 7 baru sejak versi pertama). Langkah di bawah WAJIB dijalankan manual — terutama langkah 7-9 yang jadi inti laporan UAT.

1. Buka panel Kelas, pastikan kelas test (mis. `7A-Test`) tersedia dan tampil di tabel.
2. Buka menu Absensi → Registrasi Kartu, TANPA restart aplikasi.
3. **Harap:** dropdown Kelas terisi — kelas yang sama seperti di panel Kelas.
4. Pilih kelas.
5. **Harap:** hanya siswa kelas itu yang tampil sebagai "Siswa sekarang"/kandidat berikutnya.
6. Tutup dialog (tombol Batal), buka lagi — ulangi 5 kali berturut-turut.
7. **Harap:** dropdown Kelas terisi ulang dengan benar di SETIAP pembukaan, tidak pernah kosong selama kelas memang ada.
8. Buat kelas baru di panel Kelas (mis. `7Z-Baru`), TANPA restart, buka Registrasi Kartu lagi.
9. **Harap:** `7Z-Baru` langsung muncul di dropdown.
10. Baca teks di bawah judul "Registrasi Kartu".
11. **Harap:** berbunyi persis "Selama registrasi berlangsung, tap kartu tidak dicatat sebagai absensi." — teks "PRD §16.5" TIDAK terlihat di mana pun pada dialog ini.
12. Selagi dialog Registrasi terbuka, tap kartu apa pun (terdaftar atau tidak) → cek menu Absensi/tabel harian setelah dialog ditutup.
13. **Harap:** tidak ada baris absensi baru tertulis akibat tap saat mode Registrasi aktif (perilaku ini TIDAK diubah sesi ini — `StatusModeRegistrasi` sudah diuji lewat `GerbangViewModelTests.ProsesTap_ModeRegistrasiAktif_TidakMenulisAbsensi`, tidak disentuh).
14. **Kasus tambahan — tidak ada tahun ajaran aktif** (kalau bisa disimulasikan tanpa merusak data UAT, mis. di instalasi test terpisah): klik "Registrasi" → **harap:** muncul pesan "Tidak ada tahun ajaran aktif. Atur tahun ajaran terlebih dahulu..." dan aplikasi TETAP TERBUKA (tidak crash/tidak menutup diri) — ini regresi kritis yang diperbaiki sesi ini (dulu menutup seluruh aplikasi).
15. **Kasus tambahan — tahun ajaran aktif tanpa kelas sama sekali** (instalasi baru sebelum kelas dibuat): buka Registrasi Kartu → **harap:** pesan "Tahun ajaran aktif ini belum mempunyai kelas. Tambahkan kelas dulu di menu Kelas." tampil jelas di dalam dialog, bukan dropdown kosong tanpa penjelasan.
16. **Kasus tambahan (baru 2026-09-23) — kelas yang belum punya siswa sama sekali:** buat kelas baru di panel Kelas TANPA memasukkan siswa apa pun ke dalamnya (baik manual maupun impor) → buka Registrasi Kartu → pilih kelas itu → **harap:** kartu "Siswa sekarang" menampilkan teks "Kelas ini belum punya siswa terdaftar di sistem." (BUKAN "Seluruh siswa di kelas ini sudah terdaftar.") — dan kelas itu **tetap ada** di dropdown Kelas, tidak hilang.

## Skenario A3 — Registrasi Kartu Siswa: REDESAIN FUNDAMENTAL, pilih siswa eksplisit satu-per-satu (2026-09-23)

**STATUS: Implemented — menunggu UAT MANUAL PENGGUNA LENGKAP. Bug #11 TETAP "menunggu UAT", TIDAK ditutup oleh sesi ini.** Lihat A-32 di `MASTER-BUG-UIUX-REGISTER.md` untuk latar belakang keputusan. Skenario A di atas (dan riwayat 4 putarannya) **dipertahankan apa adanya sebagai riwayat** — desain antrean-otomatis yang diuji di sana sudah DIGANTIKAN alur baru ini; jangan dites lagi sebagai alur yang berlaku sekarang.

**Yang berubah:** dropdown "Nama siswa" BARU muncul setelah Kelas dipilih — admin memilih SATU siswa eksplisit (tidak ada lagi kursor yang lompat otomatis ke siswa berikutnya). Tap RFID hanya mengisi UID SEMENTARA (belum tersimpan) sampai Simpan ditekan. Kolom Kartu di panel Siswa sekarang menampilkan UID sebenarnya atau teks "Belum terdaftar", bukan True/False. Dialog TIDAK menutup diri sendiri setelah Simpan berhasil — operator lanjut ke siswa berikutnya di kelas yang sama tanpa membuka ulang dialog; hanya tombol Batal/X yang menutup dialog. **Cakupan dibatasi ke Registrasi Kartu SISWA saja** — alur Guru dan "Ganti kartu" Siswa (`TapKartuTunggalWindow`, dialog terpisah, tombol "Ganti kartu" di tabel Siswa/Guru) TIDAK berubah, tidak perlu diuji ulang di skenario ini.

1. **Dropdown Nama siswa muncul & berisi benar** — Login → menu Absensi → Registrasi Kartu → pilih kelas yang punya ≥2 siswa aktif. **Harap:** dropdown "Nama siswa" BARU tampil di bawah dropdown Kelas, berisi seluruh siswa aktif kelas itu (bukan cuma satu/kursor otomatis).
2. **Siswa baru tampil tanpa restart** — buka panel Siswa → "Tambah siswa" → isi data siswa baru di kelas yang sama seperti langkah 1 (JANGAN isi kartu — kolom kartu memang tidak ada di form tambah siswa) → Simpan → kembali ke Registrasi Kartu (dialog boleh ditutup-buka lagi, TANPA restart aplikasi) → pilih kelas yang sama. **Harap:** siswa yang baru ditambahkan langsung muncul di dropdown "Nama siswa".
3. **Siswa tanpa kartu menampilkan "Belum terdaftar"** — di panel Siswa, cari siswa yang belum pernah tap kartu. **Harap:** kolom Kartu menampilkan teks "Belum terdaftar" (BUKAN "False" atau kosong).
4. **Pilih siswa → panel "Siswa sekarang" tampil benar** — di Registrasi Kartu, pilih satu siswa dari dropdown "Nama siswa". **Harap:** panel "Siswa sekarang" menampilkan Nama, NISN, Kelas, dan status kartu siswa itu ("Belum terdaftar" kalau belum punya kartu).
5. **Tap UID tersedia → tampil sebagai "belum disimpan"** — dengan siswa terpilih di langkah 4 (yang belum punya kartu), tap kartu RFID yang belum pernah dipakai. **Harap:** muncul teks "Kartu terdeteksi: [UID] (belum disimpan)" — tombol Simpan AKTIF, TIDAK ada pesan "berhasil"/"terdaftar" pada tahap ini.
6. **Tap UID yang sudah dipakai siswa lain → ditolak** — pilih siswa lain (yang belum punya kartu), tap UID kartu yang SUDAH terdaftar ke siswa lain (mis. kartu dari langkah sebelumnya kalau sudah disimpan, atau kartu Guru yang sudah aktif). **Harap:** pesan penolakan tampil menyebutkan pemilik kartu itu, kolom UID sementara TETAP KOSONG, tombol Simpan TETAP NONAKTIF.
7. **Pilih siswa yang sudah punya kartu → tap kartu baru ditolak** — pilih siswa yang statusnya di panel "Siswa sekarang" sudah menunjukkan UID (bukan "Belum terdaftar"), tap kartu RFID baru yang belum dipakai siapa pun. **Harap:** ditolak dengan pesan bahwa siswa ini sudah punya kartu aktif (registrasi kartu kedua bukan alur ini — itu "Ganti kartu" di panel Siswa, terpisah).
8. **Simpan berhasil, panel Siswa ter-refresh tanpa restart** — ulangi langkah 4-5 dengan siswa & UID yang benar-benar valid → klik **Simpan**. **Harap:** pesan "Kartu RFID [UID] berhasil disimpan untuk [nama]." tampil, dialog TETAP TERBUKA, panel "Siswa sekarang" untuk siswa itu langsung menunjukkan UID barunya (bukan lagi "Belum terdaftar"). Tutup dialog (Batal) → buka panel Siswa → **harap:** kolom Kartu siswa itu menunjukkan UID yang sama, TANPA restart aplikasi.
9. **Data bertahan setelah aplikasi ditutup-buka ulang** — setelah langkah 8, tutup aplikasi sepenuhnya (tombol Keluar) → buka lagi → login → panel Siswa. **Harap:** kolom Kartu siswa dari langkah 8 tetap menunjukkan UID yang sama (bukti tersimpan sungguhan di database, bukan cuma di memori sesi).
10. **Simpan berturut-turut untuk 2 siswa dalam kelas yang sama, tanpa buka-tutup dialog** — buka Registrasi Kartu, pilih kelas → pilih siswa A → tap UID A → Simpan (dialog tetap terbuka) → pilih siswa B di dropdown yang sama → tap UID B → Simpan. **Harap:** KEDUA siswa berhasil tersimpan dengan UID masing-masing tanpa perlu menutup dan membuka ulang dialog di antaranya.
11. **Batal tidak menyimpan apa pun** — pilih siswa yang belum punya kartu, tap UID yang valid (tampil "belum disimpan"), JANGAN klik Simpan — klik **Batal** atau tutup lewat X. Buka lagi Registrasi Kartu, pilih siswa yang sama. **Harap:** status kartu siswa itu TETAP "Belum terdaftar" — UID yang ditap sebelum Batal tidak pernah tersimpan.
12. **Tap saat mode registrasi tidak tercatat sebagai absensi** — regresi dari Skenario A langkah 12-13: selagi dialog Registrasi Kartu terbuka, tap kartu apa pun → tutup dialog → cek menu Absensi/tabel harian. **Harap:** tidak ada baris absensi baru akibat tap selama mode Registrasi aktif (perilaku ini tidak disentuh sesi ini).
13. **Alur Guru dan "Ganti kartu" Siswa tidak berubah (regresi cakupan)** — di panel Guru, tap-daftarkan kartu untuk guru seperti biasa; di panel Siswa, gunakan tombol "Ganti kartu" pada siswa yang sudah punya kartu. **Harap:** kedua alur ini berjalan PERSIS seperti sebelumnya (dialog `TapKartuTunggalWindow`, satu langkah tap-dan-simpan, tidak ada dropdown "Nama siswa" tambahan) — sesi ini secara sengaja tidak menyentuhnya.

**Diverifikasi otomatis** lewat 17 test `RegistrasiKartuViewModelTests` (mencakup seluruh 10 poin TEST WAJIB yang diminta: dua siswa tampil di dropdown, siswa baru tampil tanpa restart, siswa tanpa kartu → "Belum terdaftar", UID tersedia bisa disimpan, UID duplikat ditolak, siswa yang sudah punya kartu tidak menerima kartu kedua, data bertahan setelah reopen, panel Siswa menunjukkan UID setelah refresh, kegagalan database tidak menampilkan pesan berhasil — disimulasikan lewat penutupan koneksi sebelum Simpan, tap saat registrasi tidak membuat baris absensi — dicakup test `GerbangViewModelTests` yang sudah ada dan tidak disentuh). Seluruh solusi: 0 error/0 warning, seluruh test hijau di Debug maupun Release. **Langkah 1-13 di atas TETAP WAJIB dijalankan manual** — perubahan sebesar ini (redesain alur, bukan sekadar bugfix) tidak bisa dianggap selesai hanya dari unit test.

## Skenario A2 — Panel Siswa membaca kelas baru tanpa restart (UAT Bug #3, 2026-09-23, ditutup 2026-09-23)

**STATUS AKHIR: ✅ SELESAI — LULUS UAT (2026-09-23).** Bug #3 ditutup — lihat A-27 di `MASTER-BUG-UIUX-REGISTER.md`.

Akar masalah dikonfirmasi lewat pembacaan kode: `SiswaViewModel` di-cache oleh `MainWindowViewModel` (`_siswaViewModel ??= new SiswaViewModel(...)`) — constructor-nya (termasuk pemuatan daftar kelas) hanya jalan sekali per sesi. Diperbaiki: `SiswaViewModel.MuatUlangDaftarKelas()` (method baru) dipanggil `MainWindowViewModel` setiap kali menu "Siswa" dipilih, bukan hanya sekali. Diverifikasi otomatis lewat 3 unit test baru (`MasterDataViewModelTests`) — TAPI perilaku WPF `SelectedItem`/navigasi menu sungguhan tetap perlu diverifikasi manual.

1. Buka panel Siswa sekali (supaya `SiswaViewModel` ter-cache), catat daftar kelas yang tersedia di dropdown filter (atas tabel) dan dropdown form Tambah Siswa.
2. TANPA restart, buka panel Kelas → tambah kelas baru (mis. `9X-Test`) → simpan.
3. Kembali ke panel Siswa (klik menu "Siswa").
4. **Harap:** `9X-Test` langsung muncul di KEDUA dropdown (filter atas tabel, dan form Tambah Siswa) — tanpa restart aplikasi.
5. Pilih `9X-Test` di dropdown filter atas tabel.
6. Pindah ke panel lain (mis. Guru), lalu kembali ke Siswa.
7. **Harap:** filter kelas yang dipilih di langkah 5 TETAP `9X-Test` (tidak ter-reset ke "Semua kelas") — seleksi tidak boleh hilang gara-gara refresh daftar kelas di background.
8. Ulangi langkah 2-4 sekali lagi dengan kelas KEDUA (mis. `9Y-Test`).
9. **Harap:** dropdown menampilkan KEDUA kelas baru (`9X-Test` dan `9Y-Test`), TIDAK ADA duplikat entri kelas lama.

## Skenario B — Stabilitas tabel Siswa/Guru setelah navigasi (UAT Bug #12, 2026-09-22, ditutup 2026-09-23)

**STATUS AKHIR: ✅ SELESAI — LULUS UAT (2026-09-23).** Bug #12 ditutup — lihat A-26 di `MASTER-BUG-UIUX-REGISTER.md`.

Diperbaiki: `MasterData/SiswaView.xaml` dan `MasterData/GuruView.xaml` — `MinWidth` ditambahkan ke SETIAP kolom `DataGrid` (nilai sama dengan `Width` yang sudah ada, tidak ada perubahan visual/angka lain), mencegah kolom (termasuk kolom tindakan Ubah/Unggah foto/Ganti kartu/Mutasi) menyempit di bawah ukuran itu berapa pun hasil pengukuran ulang WPF saat View dibuat ulang oleh navigasi menu. `Resources/GayaAplikasi.xaml` (`TabelStyle`, dipakai 12 layar lain) **TIDAK disentuh** — audit mengonfirmasi bug ini murni soal definisi kolom per-layar, bukan gaya bersama. Diverifikasi otomatis lewat `SiswaGuruTabelKolomTests` (6 test baru, jalan di thread STA manual) yang membuktikan `InitializeComponent()` menghasilkan jumlah/urutan kolom identik dan `MinWidth` benar-benar terpasang setiap View dibuat ulang — TAPI tes ini TIDAK bisa mereproduksi pengukuran layout WPF sungguhan (`Measure`/`Arrange` di jendela nyata dengan ukuran berubah bertahap), jadi verifikasi visual di bawah tetap wajib.

1. Buka menu Siswa, periksa seluruh kolom (Nama, NISN, JK, Kelas, Kartu, TG, Alasan, tombol tindakan) — catat lebar/urutan sebagai baseline.
2. Pindah Siswa → Guru → Siswa sebanyak 10 siklus klik menu.
3. **Harap:** setiap kembali ke Siswa, lebar/urutan kolom, header, isi sel, dan tombol tindakan identik dengan baseline langkah 1 — tidak ada kolom menyempit, berhimpitan, atau tombol terpotong.
4. Pindah Guru → Kelas → Guru sebanyak 10 siklus.
5. **Harap:** kolom tabel Guru (Nama, NIP, Kartu, TG, Status, tombol tindakan) tetap stabil, sama seperti langkah 3.
6. Uji dengan data pendek (nama 1 kata, NISN/NIP normal) dan data panjang (nama sangat panjang, alasan nonaktif panjang) — pastikan teks terpotong dengan wajar (ellipsis/clip di dalam sel), bukan meluber menabrak kolom sebelah.
7. Resize jendela aplikasi (perkecil/perbesar) sambil tabel Siswa/Guru terbuka.
8. **Harap:** kolom menyesuaikan wajar, kolom Nama (star) yang menyerap/melepas ruang, kolom lain tetap minimal seukuran `MinWidth` yang sudah diberikan — horizontal scroll muncul hanya kalau lebar jendela memang tidak cukup, bukan pengganti layout.
9. Ulangi langkah 1-3 pada skala Windows 100%, 125%, dan 150% (Pengaturan Tampilan Windows) bila memungkinkan di mesin tester.
10. **Harap:** tidak ada teks bertumpuk, kolom hilang, atau tombol (Ubah/Unggah foto/Ganti kartu/Mutasi untuk Siswa; Ubah/Unggah foto/Kartu/Nonaktifkan untuk Guru) yang terpotong di skala DPI manapun.

## Skenario C — Migrasi 002 (A-38): Verifikasi pada Salinan Database UAT

Dijalankan sebelum publish berikutnya yang membawa migrasi 002 (`trg_audit_cegah_update`/`trg_audit_cegah_delete`, `MASTER-BUG-UIUX-REGISTER.md` A-38, commit `60b0c58`). **Hanya terhadap salinan database di folder sementara DI LUAR repo** — jangan buat folder publish UAT baru, jangan jalankan EXE terhadap database UAT asli.

1. Tutup semua proses `Absensi.App.exe` yang memegang `publish/Bug11-12-UAT/data/absensi.db`.
2. Salin `absensi.db` (dan `-wal`/`-shm` bila ada) ke folder sementara di luar repo, mis. `%TEMP%\absensi-migrasi-a38\`.
3. Tambahkan test sekali-pakai di `tests/Absensi.Data.Tests/` yang membuka salinan itu lewat `SqliteConnectionFactory.Open`, mencatat versi `skema_versi` serta jumlah dan isi baris `audit` SEBELUM, menjalankan `new MigrationRunner().Run(connection)`, lalu mencatat ulang SESUDAH — jalankan lewat `dotnet test ... --filter`, hapus lagi berkas test itu setelah lulus (bergantung path absolut di luar repo, bukan bagian suite permanen).

**Harap:**
- Versi skema naik dari `1` ke `2`.
- Jumlah **dan** isi baris `audit` identik sebelum-sesudah (migrasi ini DDL murni, tidak menyentuh data).
- `INSERT` baris audit baru tetap berhasil.
- `UPDATE`/`DELETE` terhadap baris `audit` mana pun — termasuk baris yang sudah ada sejak sebelum migrasi — ditolak dengan pesan "Baris audit tidak dapat diubah/dihapus (PRD §12)."

Sudah dijalankan sekali (2026-09-24) terhadap salinan database UAT nyata (24 baris audit) — seluruh hasil di atas terkonfirmasi.

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


## Skenario tambahan 12 November 2026 (perlu dicoba di Windows)

1. **Jadwal berlaku mulai (P-15):** Pengaturan → Jadwal Harian → ubah jam masuk Senin, pilih "Berlaku mulai" minggu depan → Simpan. **Harap:** pesan menyebut tanggal berlaku; tap hari ini tetap memakai jam lama. Pilih tanggal hari ini → **harap:** ditolak.
2. **Finalisasi hari terlewat (T3):** siswa tap masuk, jangan tap pulang, tutup aplikasi sebelum jam finalisasi Bolos; buka lagi keesokan harinya. **Harap:** status siswa kemarin menjadi Bolos tanpa pesan Telegram baru.
3. **Siswa pindahan (P-18):** tambah siswa baru di tengah bulan → laporan bulanan siswa itu hanya menghitung hari sejak ditambahkan.
4. **Backup terenkripsi:** buka folder `backup` → berkas `.db` terbaru tidak bisa dibuka dengan DB Browser (bukan SQLite polos).
