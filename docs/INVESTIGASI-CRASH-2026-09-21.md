# Investigasi Crash — Sesi 2026-09-21

Dokumen ini **menambah**, bukan menggantikan, `TODO-VERSI-BERIKUTNYA.md` dan `PANDUAN-TESTER.md`. Ditulis atas arahan eksplisit pemilik proyek: audit terukur dulu, tidak mengubah kode bisnis sebelum akar masalah cukup jelas. Setiap klaim di bawah ditandai tingkat keyakinannya — jangan diperlakukan sebagai fakta kalau ditandai DUGAAN.

Status database: `data/absensi.db` di `C:\AbsensiSekolah-Test` **sengaja dihapus manual oleh pemilik proyek** untuk menguji ulang alur instalasi pertama. **Ini bukan bug** — jangan disimpulkan sebagai kegagalan fitur Restore, jangan diperbaiki, jangan dipulihkan.

**Riwayat revisi:**
- 2026-09-21, revisi 1: temuan Event Log, audit pola kode, rancangan awal §4/§5.
- 2026-09-21, revisi 2: menambah daftar bug UAT (§7), memperketat rancangan exception handling sesuai koreksi teknis pertama, mengganti urutan kerja jadi Tahap A–G.
- 2026-09-21, revisi 3: koreksi retensi log, koreksi klaim "stack trace selalu aman" (DITARIK — lihat §4.7), perilaku deterministik 4 jenis exception diperjelas, dependency graph proyek + 2 opsi lokasi `PencatatGalat` ditambahkan (§4.1).
- 2026-09-21, revisi 4: **Tahap A DITUTUP** — pemilik proyek memilih Opsi 1 (§4.1) dan menyetujui retensi 30 berkas dengan syarat pola nama resmi. **Tahap B dan C SELESAI DIKERJAKAN** — implementasi `PencatatGalat`, 3 handler global, sanitasi, rotasi, dan pengalihan `Debug.WriteLine` sudah ditulis dan diuji (§6). Tidak ada isi §1/§2/§3/§7 revisi sebelumnya yang dihapus atau diubah substansinya; §4/§5 diberi catatan status selesai tanpa menghapus rancangan aslinya (rancangan tetap jadi acuan "kenapa kode ditulis begini").
- 2026-09-21, revisi 5 (dokumen ini — sesi hardening kedua): ditemukan dan ditutup 2 celah dari review Tahap B/C — sanitasi rekursif untuk `AggregateException.InnerExceptions` (bukan cuma `InnerException` tunggal) dan batas ukuran berkas cadangan `_gagal-tulis.log` (§4.7, §6.7). Item 2 §4.9/Skenario 10 **diverifikasi ulang dengan skenario yang BENAR** setelah skenario sebelumnya ("Impor Excel berkas terkunci") terbukti salah — tidak melewati `PencatatGalat` sama sekali (lihat §6.7). Item 6 (dialog tidak berulang) resmi ditandai **BELUM TERVERIFIKASI DINAMIS** sesuai arahan, bukan dipaksakan. Tidak ada isi §1-§3/§7 yang berubah; §4.7/§6 ditambah, tidak ditulis ulang.

**Status: Tahap A DITUTUP, Tahap B dan C SELESAI termasuk hardening tambahan (lihat §6 dan §6.7). BERHENTI DI SINI sesuai arahan pemilik proyek — Tahap D (reproduksi crash terkontrol) belum dimulai, root cause dua crash 15:15/15:25 BELUM diperbaiki, dan tidak ada perubahan database/Restore/Wizard/XAML/UI/backlog UI-UX yang dikerjakan.**

---

## 1. Fakta Terbukti

*(tidak berubah dari revisi sebelumnya)*

### 1.1 Dua crash nyata, terkonfirmasi lewat Windows Event Log (`Application`, provider `Application Error`)

| Waktu | Exception | Modul yang fault | Proses |
|---|---|---|---|
| 2026-09-21 15:15:37 | `0xc00000fd` (`STATUS_STACK_OVERFLOW`) | `ntdll.dll` | `C:\AbsensiSekolah-Test\Absensi.App.exe` |
| 2026-09-21 15:25:13 | `0xe0434352` (exception CLR/.NET terkelola, tak tertangani) | `KERNELBASE.dll` | PID `0x17d4`, sama dengan baris berikut |
| 2026-09-21 15:25:14 | `0xc000041d` (`STATUS_FATAL_USER_CALLBACK_EXCEPTION`) | `KERNELBASE.dll` | PID `0x17d4` — pasangan crash yang sama, exception managed lolos sampai batas callback native |

Event Log **tidak memuat call stack** — hanya modul yang fault dan kode exception. Tidak ada dump (`.dmp`) ditemukan. `createdump.exe` ada di paket runtime tapi tidak aktif tanpa `COMPlus_DbgEnableMiniDump` diset — tidak diset di instalasi ini.

### 1.2 Tidak ada jaring pengaman exception sama sekali

Grep menyeluruh `src/` untuk `DispatcherUnhandledException`, `AppDomain.UnhandledException`, `TaskScheduler.UnobservedTaskException`: **nol hasil**.

### 1.3 Mekanisme log yang ada sekarang tidak aktif di build Release

`Debug.WriteLine` (dipakai di `App.xaml.cs:121,221` dan disebut untuk Telegram/Impor di `TODO-VERSI-BERIKUTNYA.md` A.3) dihias `[Conditional("DEBUG")]` — pemanggilnya dihapus kompiler total di Release. Tidak ada `DefineConstants` yang menambah `DEBUG` di Release pada `csproj` manapun. **Build Release/self-contained tidak pernah mencatat apa pun dari titik-titik itu.**

### 1.4 Audit pola "InvalidOperationException diserap WndProc" — TIDAK ditemukan duplikatnya

Hanya `Wizard/WizardInstalasiWindow.xaml.cs:49` yang punya pola ini, dan sudah diperbaiki. `Gerbang/GerbangWindow.xaml.cs:34` (`OnClosing`) berbeda bentuk dan re-entrant-safe sejauh dibaca statis.

### 1.5 Tidak ditemukan pola loop umpan-balik klasik (PropertyChanged / CollectionChanged)

`ObservableObject.SetField` sudah pakai `EqualityComparer.Equals`. Nol langganan manual `.PropertyChanged +=` atau `CollectionChanged` lintas objek. Satu-satunya `SelectionChanged` (`KalenderView.xaml.cs:15`) tidak menyentuh balik properti sumbernya.

### 1.6 Satu pola berulang identik di 3 jendela — layak diawasi tapi belum terbukti jadi penyebab

`GerbangWindow`, `RegistrasiKartuWindow`, `TapKartuTunggalWindow` — ketiganya punya `Loaded`/`Activated`/`LostFocus` yang sama-sama memanggil `FokusKolomTap()`. `GerbangWindow` satu-satunya yang juga membuka dialog modal dari `OnClosing` selagi pola ini aktif.

---

## 2. Dugaan Sementara (belum terbukti)

### 2.1 Kandidat crash 15:25 (fatal callback exception) — KEYAKINAN SEDANG-TINGGI

Hipotesis: exception tak tertangani di dalam native callback, karena tidak ada try/catch di lapisan Command/event handler manapun, dan tidak ada `DispatcherUnhandledException`. Kandidat tertinggi (bukan satu-satunya): `Gerbang/GerbangWindow.xaml.cs:57-72` (`KolomTap_PreviewKeyDown`) → `GerbangViewModel.ProsesTap` mengakses SQLite sinkron di dalam event input native, tanpa try/catch. Tidak bisa dipastikan tanpa call stack.

### 2.2 Kandidat crash 15:15 (stack overflow) — KEYAKINAN RENDAH

Tidak ditemukan rekursi swadiri di `Absensi.Domain`/`Absensi.Data`/`Absensi.App` (semua logika rentang tanggal pakai `for`/`while`). Aktivitas persis pukul 15:15/15:25 **tidak dapat dikonfirmasi** oleh pemilik proyek — dugaan pustaka pihak ketiga (ClosedXML/QuestPDF) TETAP berstatus dugaan tidak terkonfirmasi, tidak dinaikkan jadi kandidat utama. **Penyebab 15:15 masih belum diketahui** — perlu reproduksi terkontrol dengan logging aktif (§5 Tahap D), bukan tebakan.

---

## 3. Informasi yang Masih Dibutuhkan

1. ~~Langkah reproduksi kedua crash~~ — dikonfirmasi tidak tersedia. Ditutup; digantikan reproduksi terkontrol di §5 Tahap D.
2. ~~Lokasi "laporan UAT"~~ — dijawab: `BUG-REPORT-UAT-2026-09-20.md`, belum ditemukan di repo (lihat §7). Bukan berarti bug-nya tidak nyata.
3. ~~Konfirmasi aktivitas ClosedXML/QuestPDF~~ — dijawab: tidak dapat dikonfirmasi.

**Item terbuka yang tersisa:** isi/path `BUG-REPORT-UAT-2026-09-20.md` (§7.4). **Item terbuka baru:** keputusan lokasi `PencatatGalat` (§4.1).

---

## 4. Rancangan Exception Handling & Logging

Prinsip yang mengikat (tidak berubah): global exception handler **bukan** mekanisme menelan-lalu-lanjut. Default adalah **catat lengkap, lalu tutup aman**. Whitelist "boleh dipulihkan" kosong pada rilis pertama.

### 4.1 Dependency Graph Proyek & Lokasi `PencatatGalat` — DIPUTUSKAN: OPSI 1

**Keputusan pemilik proyek (2026-09-21): Opsi 1.** `PencatatGalat` ditaruh di `Absensi.Data/Diagnostik/PencatatGalat.cs`. Alasan yang dicatat: jejak perubahan paling kecil, tidak menambah proyek/referensi baru, tidak ada circular dependency, `Absensi.App` dan `Absensi.Telegram` sudah memakai `Absensi.Data`, preseden I/O berkas lewat `LayananBackup` sudah ada, `Absensi.Domain` tetap bersih. Implementasi ada di §6.

**Graph referensi proyek saat ini** (dibaca langsung dari `<ProjectReference>` di setiap `.csproj`, bukan diasumsikan):

```
Absensi.Domain            (nol referensi keluar — CLAUDE.md §3 mewajibkan ini tetap begitu)
      ^
      |
Absensi.Data  ────────────────┐
      ^                       |
      |                       v
Absensi.Telegram        Absensi.Laporan
      ^                       ^
      |                       |
      └──────── Absensi.App ──┘
```

Detail per proyek (dari `.csproj`, bukan dugaan):
- `Absensi.Domain` → tidak mereferensikan proyek lain.
- `Absensi.Data` → mereferensikan `Absensi.Domain`.
- `Absensi.Laporan` → mereferensikan `Absensi.Domain`.
- `Absensi.Telegram` → mereferensikan `Absensi.Data` (dan karena itu transitif `Absensi.Domain`).
- `Absensi.App` → mereferensikan `Absensi.Data`, `Absensi.Telegram`, `Absensi.Laporan`.

**Temuan penting yang mengubah kesimpulan revisi sebelumnya:** `Absensi.Telegram` **sudah** mereferensikan `Absensi.Data` saat ini. Catatan revisi 2 yang menyebut "`Absensi.Telegram` tidak bergantung ke `Absensi.App`, butuh keputusan arsitektur" masih benar (memang tidak bergantung ke `Absensi.App`), tapi salah menyiratkan tidak ada lapisan bersama sama sekali — **`Absensi.Data` justru sudah menjadi leluhur bersama** `Absensi.App` dan `Absensi.Telegram` lewat graph di atas. Ini mengubah pilihan yang tersedia secara signifikan (lihat Opsi 1 di bawah).

Titik yang butuh memanggil `PencatatGalat` saat ini (dari audit §1.3): `App.xaml.cs` (dalam `Absensi.App`) dan `KlienBotTelegram.cs` (dalam `Absensi.Telegram`). `Absensi.Data` dan `Absensi.Domain` sendiri **tidak** punya titik `Debug.WriteLine` yang perlu diganti — keduanya melempar exception ke pemanggil, tidak menelan galat sendiri.

**Larangan yang mengikat (diulang dari arahan Anda):** `Absensi.Telegram` tidak boleh diberi referensi ke `Absensi.App`. Tidak boleh membuat proyek baru tanpa persetujuan.

#### Opsi 1 — Taruh `PencatatGalat` di `Absensi.Data` (mis. `Absensi.Data/Diagnostik/PencatatGalat.cs`)

Karena `Absensi.App` sudah mereferensikan `Absensi.Data`, dan `Absensi.Telegram` **sudah** mereferensikan `Absensi.Data` (temuan di atas), menaruh `PencatatGalat` di sana membuatnya langsung bisa dipakai keduanya **tanpa menambah satu referensi proyek pun, tanpa proyek baru, tanpa risiko circular dependency** (graph di atas sudah satu arah: `Domain ← Data ← {Telegram, Laporan} ← App`, menaruh kelas baru di `Data` tidak membalik arah panah manapun).

- **Konsekuensi:** `Absensi.Data` (dideskripsikan CLAUDE.md §3 sebagai "akses SQLite, migrasi, repositori") menerima satu tanggung jawab baru yang bukan akses SQL: menulis berkas log teks. Ini BUKAN pencemaran murni — `Absensi.Data/Backup/LayananBackup.cs` yang sudah ada di proyek yang sama juga melakukan I/O berkas non-SQL (`File.ReadAllBytes`, `Directory.CreateDirectory`, dll.) untuk kebutuhan backup, jadi preseden "Data boleh menyentuh file system, bukan cuma SQL" sudah ada di proyek ini.
- **Tidak mencemari `Absensi.Domain`** — `Domain` tetap nol referensi keluar seperti sekarang, tidak disentuh sama sekali.
- **Mudah diuji:** proyek tes yang ada (`tests/Absensi.Domain.Tests`) saat ini hanya mereferensikan `Domain`. Uji `PencatatGalat` butuh proyek tes yang mereferensikan `Data` — bisa proyek tes baru `Absensi.Data.Tests` (proyek TES baru dianggap wajar/rutin, beda dari "proyek produksi baru" yang dilarang tanpa persetujuan — tapi tetap akan dikonfirmasi dulu sebelum dibuat, bukan diasumsikan).

#### Opsi 2 — Proyek baru netral tanpa referensi keluar (mis. `Absensi.Diagnostik`), direferensikan oleh `App` dan `Telegram`

Proyek keenam, sejajar `Absensi.Domain` (nol referensi keluar), isinya murni `PencatatGalat` + util sanitasi (§4.7). `Absensi.App` dan `Absensi.Telegram` masing-masing menambah satu `<ProjectReference>` ke proyek ini.

- **Konsekuensi:** mengubah "Struktur Repositori" yang didokumentasikan eksplisit di CLAUDE.md §3 (lima proyek di `/src`) — perubahan struktural yang didefinisikan dokumen itu sendiri, bukan cuma penambahan berkas. **Ini persis "proyek baru" yang Anda minta tidak dibuat tanpa persetujuan** — disebut di sini sebagai opsi, bukan dikerjakan.
- **Pemisahan tanggung jawab paling bersih**: `Absensi.Data` tetap murni "akses SQLite, migrasi, repositori" sesuai definisi CLAUDE.md, tidak menerima tanggung jawab logging sama sekali.
- **Butuh entri baru di `Absensi.sln`** dan setup `.csproj` baru (target framework, dsb.) — pekerjaan tambahan dibanding Opsi 1, walau kecil.

#### Rekomendasi (bukan keputusan — menunggu Anda)

Opsi 1 lebih kecil jejaknya: nol proyek baru, nol perubahan referensi, memakai graph yang sudah ada, dan preseden I/O-non-SQL di `Absensi.Data` sudah ada lewat `LayananBackup`. Opsi 2 lebih murni secara pemisahan tanggung jawab tapi mengubah invarian struktural yang CLAUDE.md definisikan eksplisit. **Tahap B tidak akan dimulai sampai Anda memilih salah satu (atau menolak keduanya dan meminta alternatif lain).**

### 4.2 Berkas yang akan diubah (Tahap B — belum dikerjakan, menunggu §4.1)

| Berkas | Perubahan |
|---|---|
| `src/Absensi.App/App.xaml.cs` | Daftarkan 3 handler di `OnStartup` (paling awal, sebelum baris yang membuka koneksi database), lepaskan di `OnExit`. Ganti 2 `Debug.WriteLine` (baris 121, 221) jadi panggilan `PencatatGalat`. |
| `PencatatGalat.cs` **(baru, lokasi sesuai §4.1)** | Satu-satunya kelas yang tahu format baris log, lokasi berkas, dan menjalankan sanitasi (§4.7). Tidak bergantung simbol `DEBUG`. Tidak pernah melempar exception ke pemanggilnya (lihat §4.7 "kegagalan penulisan log"). |
| `LokasiData.cs` (atau padanannya di lokasi §4.1 kalau Opsi 2 dipilih) | Tambah `FolderLog`, `PastikanFolderLogAda()`, mengikuti pola `FolderData` yang sudah ada. |
| `src/Absensi.Telegram/KlienBotTelegram.cs` | 4 `catch` tanpa logging (baris 37, 41, 70, 74 — `TODO-VERSI-BERIKUTNYA.md` A.3.1) diarahkan ke `PencatatGalat`. |
| Proyek tes (baru atau yang ada, sesuai §4.1) | Unit test `PencatatGalat` terisolasi — lihat §4.8. |
| `docs/PANDUAN-TESTER.md` | Skenario manual baru pembuktian logging aktif di Release — lihat §4.9. |

Tidak ada perubahan skema database, XAML/UI, atau jalur Restore.

### 4.3 Tiga titik penangkap — perilaku deterministik

#### `Application.DispatcherUnhandledException`

Urutan langkah yang PASTI dijalankan, dalam urutan ini, tidak boleh dibalik:

1. **Catat exception lebih dulu**, tanpa syarat, sebelum langkah lain apa pun (`PencatatGalat.Catat`, dibungkus try/catch sendiri — lihat §4.7 "kegagalan penulisan log tidak boleh jadi exception baru").
2. Cek whitelist pemulihan (§4.4) — **kosong pada implementasi awal**, jadi cabang ini secara praktis tidak pernah dipakai sekarang. Kalau (di masa depan) jenisnya ada di whitelist: tampilkan pesan ramah, `e.Handled = true`, kembali — aplikasi lanjut jalan.
3. **Selain itu (jalur default, satu-satunya jalur aktif sekarang):**
   - Tampilkan SATU pesan ramah lewat `MessageBox.Show` **kalau masih aman** — dibungkus try/catch tersendiri; kalau `MessageBox.Show` sendiri gagal/melempar, itu ditelan diam-diam DI TITIK INI SAJA (bukan dilempar ulang) karena exception asli sudah tercatat di langkah 1, tujuannya cuma supaya pengguna tahu, bukan syarat mutlak.
   - **`e.Handled` TETAP `false`** untuk exception yang tidak dikenal (sesuai arahan eksplisit — tidak diubah jadi `true` untuk "menutup dengan rapi", karena itu sama saja berpura-pura tahu keadaan aman padahal tidak). Exception dibiarkan lanjut ke jalur default .NET (proses berhenti, Windows mencatat APPCRASH seperti sebelumnya) — bedanya SEKARANG sudah ada baris log kita sendiri dengan stack trace sebelum proses benar-benar berhenti.
4. **Pencegahan dialog berulang/loop exception baru:** satu flag (`_sedangMenanganiGalatFatal`, di-set `true` di awal langkah 1) diperiksa di awal handler — kalau sudah `true` (berarti handler ini terpanggil lagi selagi exception SEBELUMNYA masih diproses, mis. karena `MessageBox.Show` di langkah 3 sendiri memicu dispatch exception baru), langkah 3 (tampilkan dialog) **dilewati sepenuhnya**, hanya langkah 1 (catat) yang tetap jalan. Ini mencegah dialog bertumpuk atau rekursi lewat message pump.

#### `AppDomain.CurrentDomain.UnhandledException`

- **Best-effort logging SAJA.** Tidak ada `MessageBox`, tidak ada operasi database, tidak ada pemicuan backup, tidak ada UI apa pun — proses mungkin sudah dalam keadaan yang tidak aman untuk memompa message loop lagi (`IsTerminating` biasanya `true` di titik ini, dan sesuai arahan: jangan mencoba memulihkan atau menjalankan operasi kompleks saat terminasi).
- Baris log mencantumkan `e.IsTerminating` apa adanya.
- Tidak pernah menjanjikan pemulihan — API `UnhandledExceptionEventArgs` memang tidak punya cara membatalkan terminasi, jadi tidak ada langkah "coba lanjutkan" sama sekali di sini, beda dari `DispatcherUnhandledException` yang masih (secara teori, lewat whitelist masa depan) punya jalur lanjut.

#### `TaskScheduler.UnobservedTaskException`

- Catat lebih dulu (sama, tanpa syarat).
- `e.SetObserved()` dipanggil **setelah** logging berhasil (atau setidaknya dicoba), dengan komentar kode eksplisit (CLAUDE.md §5) yang menyebutkan: tujuannya HANYA supaya finalizer thread tidak ikut menghasilkan konsekuensi tambahan (mis. proses ikut mati lewat jalur finalizer) untuk exception yang SUDAH tercatat — **bukan perbaikan akar masalah**, dan bukan alasan untuk berhenti mencari kenapa `Task` itu sampai tidak pernah di-`await`/diperiksa.

#### `StackOverflowException` dan `OutOfMemoryException`

- Tidak ada kode di rancangan ini yang berpura-pura bisa menangani `StackOverflowException` — CLR tidak mengizinkannya ditangkap oleh mekanisme apa pun di atas, ini batasan platform sejak .NET Core, bukan pilihan desain. Diagnosis murni lewat Windows Event Log (sudah terbukti berhasil menemukan crash 15:15 dengan cara ini) atau crash dump (WER `LocalDumps`, perubahan registry — keputusan terpisah, di luar kode) atau debugger terpasang saat reproduksi.
- `OutOfMemoryException` diperlakukan sama secara KEBIJAKAN (tidak diandalkan untuk pulih) walau secara teknis kadang masih tertangkap `catch` biasa — kalau tertangkap salah satu dari 3 handler di atas, tetap masuk jalur default (§4.3 langkah 3), TIDAK PERNAH masuk whitelist. Catatan teknis: pencatatan log di bawah tekanan memori harus tetap ringan (hindari membangun string besar) supaya usaha mencatat tidak ikut gagal karena OOM yang sama.

### 4.4 Whitelist "boleh dipulihkan" — KOSONG pada rilis pertama

Tidak berubah dari revisi sebelumnya: kosong sampai ada bukti kasus per kasus, ditambah satu per satu dengan jenis exception spesifik + lokasi kode yang membuktikan tidak ada tulisan data setengah jalan + komentar yang menyebut alasan dan rujukan PRD/keputusan.

### 4.5 Risiko melanjutkan proses dalam keadaan tidak konsisten

Tidak berubah: command/`CanExecute` basi, cache di memori (`CacheTampilanGerbang`, `ObservableCollection` layar) tidak lagi cocok database yang sudah rollback, status transaksi SQLite pada koneksi tunggal yang tidak jelas kalau exception memutus jalur `using`/`finally` (BUTUH diverifikasi per lokasi transaksi sebelum satu pun exception masuk whitelist §4.4, bukan diasumsikan aman), dan operator yang terus memakai aplikasi di atas data yang sudah tidak konsisten tanpa tahu ada yang salah.

### 4.6 Pesan ke pengguna

Umum dan ramah, bahasa Indonesia, TANPA detail teknis (tanpa nama exception, tanpa stack trace, tanpa path berkas) — kontras dengan isi log (§4.7) yang justru harus selengkap mungkin untuk diagnosis developer. Redaksi pasti disusun saat Tahap B.

### 4.7 Logging — format, lokasi, retensi, dan SANITASI (koreksi menggantikan revisi sebelumnya)

**KOREKSI PENTING (menarik klaim revisi sebelumnya):** revisi sebelumnya menyatakan "stack trace dan nama method selalu aman dicatat". **Klaim ini SALAH dan ditarik.** Stack trace bisa memuat: path lokal (termasuk nama akun Windows kalau folder profil pengguna muncul di path build/symbol), lokasi source/build di mesin developer, nama assembly, dan detail lingkungan lain. Tidak ada bagian dari sebuah exception — termasuk stack trace — yang "otomatis aman" tanpa diperiksa. Kebijakan baru di bawah ini menggantikan paragraf lama.

#### 4.7.1 Yang TIDAK PERNAH dicatat, tanpa pengecualian

Sandi/hash sandi, token Bot Telegram, isi kredensial apa pun, NISN, NIP, nama siswa/guru, nomor telepon, isi record database mentah, parameter SQL, connection string.

#### 4.7.2 Yang boleh dicatat, HANYA setelah lewat sanitasi — tidak pernah `exception.ToString()` mentah

Baris log dibangun dari field-field berikut secara terpisah (bukan `ex.ToString()` yang menggabungkan semuanya tanpa saringan):
- `ex.GetType().FullName`, `ex.HResult`, `ex.TargetSite?.Name` — aman apa adanya, murni identitas teknis, tidak pernah berisi data pengguna.
- `ex.Source` dan `ex.StackTrace` — **wajib melewati fungsi sanitasi path** sebelum ditulis: cari-dan-ganti path yang mengandung folder profil pengguna Windows (`%USERPROFILE%`/`C:\Users\<nama akun>\...`) dengan placeholder generik (mis. `<profil-pengguna>`), supaya nama akun Windows developer/tester tidak ikut tersimpan di log yang berjalan di mesin sekolah.
- `ex.Message`, dan `Message` setiap `InnerException` (rekursif, dibatasi kedalaman maksimum — usul 5 level — supaya rantai `InnerException` tidak bisa dibuat tak berhingga oleh pustaka pihak ketiga) — **wajib melewati fungsi sanitasi pesan** sebelum ditulis:
  - Untuk exception dari namespace `Absensi.Domain`/`Absensi.Data` (lapisan yang menyentuh data sekolah, dan tempat paling mungkin pesan galatnya menyisipkan NISN/NIP/nama secara langsung dalam teks): `Message` **disembunyikan secara default**, diganti placeholder (`"[pesan disembunyikan — lihat exception type dan stack trace]"`), sampai ada audit eksplisit yang memverifikasi pesan dari kelas exception tertentu tidak pernah menyisipkan data personal — verifikasi itu PEKERJAAN TERPISAH sebelum kebijakan ini boleh dilonggarkan untuk kelas tertentu, bukan diasumsikan aman sekarang.
  - Untuk exception dari BCL/.NET atau pustaka pihak ketiga (`SqliteException`, `IOException`, `HttpRequestException`, dll.) — pesannya umumnya deskripsi kondisi teknis (mis. "database is locked"), bukan data sekolah, TAPI tetap dijalankan lewat saringan pola sebagai pertahanan berlapis: redaksi substring yang cocok pola connection string (`Data Source=`, `Password=`), pola token Bot Telegram (angka diikuti `:` diikuti deretan karakter alfanumerik panjang), dan deretan angka 10-18 digit berurutan (bentuk umum NISN/NIP) — kalau pola ini ditemukan di `Message` exception BCL sekalipun, bagian yang cocok diganti placeholder, bukan seluruh pesan dibuang.

#### 4.7.2.a Rekursi ke `AggregateException.InnerExceptions` (jamak) — DITAMBAHKAN sesi hardening kedua

Ditemukan lewat review: rancangan awal §4.7.2 hanya menyebut `InnerException` TUNGGAL (rantai lurus A→B→C). `AggregateException` punya `InnerExceptions` JAMAK (bisa banyak, sejajar, bukan rantai), dan **`AggregateException.Message` bawaan .NET MENYUSUN ULANG teks SELURUH `InnerExceptions` ke Message miliknya sendiri** — kalau `Message` miliknya dicatat apa adanya (walau `Source`-nya sendiri bukan `Absensi.Domain`/`Data`), data sensitif dari salah satu InnerException-nya bisa bocor lewat Message si pembungkus. Kebijakan yang diimplementasikan:
- **`AggregateException.Message` TIDAK PERNAH dicatat apa adanya** — diganti teks netral `"(AggregateException, N InnerException — lihat rincian di bawah, BUKAN Message gabungan bawaan .NET)"`, terlepas dari `Source` si pembungkus.
- Rekursi masuk ke SETIAP item `InnerExceptions` (bukan cuma `InnerException` tunggal — untuk `AggregateException`, `InnerException` bawaan hanya mengembalikan item PERTAMA), masing-masing dengan aturan sanitasi Source-nya SENDIRI (§4.7.2 tetap berlaku per-item).
- **Guard siklus referensi**: `HashSet<Exception>` berbasis identitas objek (`ReferenceEqualityComparer`) mencegah exception yang sama dicatat dua kali kalau ada siklus (defensif — .NET tidak menyediakan API publik untuk membuat siklus lewat `InnerException`, tapi dijaga untuk pembungkus non-standar di masa depan).
- **Batas jumlah TOTAL** (bukan cuma kedalaman): maksimum 20 exception per entri log, terpisah dari batas kedalaman 5 level — relevan khusus untuk `AggregateException` yang bisa fan-out LEBAR (banyak `InnerExceptions` sejajar), bukan cuma dalam. Melebihi batas → baris penanda `"... (dipotong — terlalu banyak InnerExceptions untuk satu entri)"`.

#### 4.7.3.a Batas ukuran berkas cadangan `_gagal-tulis.log` — DITAMBAHKAN sesi hardening kedua

Ditemukan lewat review: rancangan awal §4.7.3 tidak membatasi ukuran berkas cadangan — kalau kegagalan menulis log utama MENETAP (mis. disk penuh berkepanjangan), berkas ini bisa tumbuh tanpa batas. Kebijakan yang diimplementasikan: **batas 1 MB** — begitu berkas cadangan mencapai/melewati batas itu, penulisan BERIKUTNYA **menimpa (reset)** berkas itu sepenuhnya alih-alih menambah (`File.WriteAllText`, bukan `File.AppendAllText`), bukan rotasi berkas terpisah seperti log resmi (§4.7.4) — sengaja jauh lebih sederhana karena jalur ini hanya terpakai saat jalur utama SUDAH gagal. Kegagalan menulis berkas cadangan ITU SENDIRI tetap ditelan diam-diam sepenuhnya (§4.7.3, tidak berubah) — tidak pernah memanggil `PencatatGalat`/`TulisCadangan` lagi secara rekursif untuk melaporkannya.

#### 4.7.3 Kegagalan penulisan log tidak boleh jadi exception baru

`PencatatGalat.Catat` sendiri **tidak pernah melempar** ke pemanggilnya. Implementasinya membungkus operasi tulis berkas dalam try/catch miliknya sendiri; kalau penulisan gagal (folder tidak writable, disk penuh, dll.), kegagalan itu:
- Dicoba dicatat SATU KALI lagi ke jalur cadangan paling minimal (mis. baris tunggal ke berkas fallback tetap, tanpa membangun ulang seluruh konteks exception asli) — **bukan** memanggil `PencatatGalat.Catat` lagi secara rekursif untuk melaporkan kegagalan `PencatatGalat.Catat` itu sendiri (itu yang bisa jadi loop).
- Kalau percobaan cadangan itu JUGA gagal, ditelan diam-diam sebagai keputusan sadar (satu-satunya kasus "menelan diam-diam" yang disengaja di seluruh rancangan ini, dengan alasan: tidak ada tempat lain yang lebih aman untuk melaporkannya tanpa risiko rekursi, dan kegagalan menulis log tidak boleh menghalangi aplikasi selesai menangani exception ASLI-nya).

#### 4.7.4 Lokasi, format, retensi (30 berkas — disetujui, dengan syarat berikut)

- Lokasi: `logs/` sejajar `data/`/`backup/` (pola sudah ada di `LokasiData.cs`).
- Satu berkas per hari, nama **pola resmi tetap**: `yyyyMMdd.log` (8 digit tanggal, ekstensi `.log`, tanpa karakter lain) — pola ini menjadi SATU-SATUNYA kriteria untuk menentukan berkas mana yang "milik aplikasi" saat rotasi.
- **Retensi maksimum 30 berkas** yang cocok pola nama resmi di atas.
- **Rotasi HANYA menghapus berkas yang namanya cocok persis pola `yyyyMMdd.log`** (divalidasi lewat pencocokan pola, bukan sekadar ekstensi `.log`) — berkas lain apa pun di folder `logs/` (nama berbeda, ekstensi lain, atau ditaruh manual oleh siapa pun) **tidak pernah disentuh oleh rotasi**, sama sekali tidak masuk daftar kandidat hapus.
- **Kegagalan rotasi tidak boleh membuat aplikasi gagal dibuka.** Rotasi dijalankan dalam try/catch tersendiri, terpisah dari operasi tulis baris log itu sendiri — kegagalan menghapus berkas lama (mis. sedang dibuka aplikasi lain) tidak menghentikan `OnStartup`.
- **Kegagalan rotasi dicatat apabila pencatatan masih memungkinkan** — dengan `PencatatGalat.Catat` yang sama (bukan mekanisme terpisah), level `ERROR` bukan `FATAL` (aplikasi tetap jalan), tunduk pada aturan 4.7.3 kalau pencatatan itu sendiri yang gagal.

### 4.8 Pengujian unit (Tahap B)

`PencatatGalat` diuji terisolasi (xUnit), mencakup MINIMAL:
- Menulis satu baris exception ke folder temporer → berkas `yyyyMMdd.log` ada, format sesuai §4.7.2.
- `logs/` belum ada → dibuat sendiri, tidak melempar.
- Sanitasi path: stack trace buatan yang mengandung path profil pengguna palsu → placeholder muncul di output, path asli tidak ada di berkas.
- Sanitasi pesan: exception buatan dari namespace `Absensi.Domain`/`Absensi.Data` → `Message` di berkas adalah placeholder, bukan teks asli, walau teks aslinya sengaja dibuat tidak mengandung data personal (kebijakannya "sembunyikan secara default", bukan "sembunyikan kalau terdeteksi berbahaya").
- Sanitasi pola: `Message` yang sengaja disisipi pola mirip token Telegram / connection string / deretan 12 digit → bagian yang cocok diganti placeholder, sisanya tetap ada.
- **Simulasi kegagalan penulisan (poin baru):** folder log dibuat read-only / path tidak valid → `PencatatGalat.Catat` dipanggil berulang kali dalam satu pengujian, dipastikan TIDAK melempar exception ke pemanggil dan TIDAK menyebabkan stack overflow/rekursi tak berhenti (menguji langsung aturan §4.7.3).
- Rotasi: buat >30 berkas dengan pola nama resmi + beberapa berkas dengan nama TIDAK cocok pola → jalankan rotasi → pastikan HANYA berkas pola resmi tersisa maksimum 30, dan berkas nama asing sama sekali tidak terhapus.

### 4.9 Pengujian bahwa logging BENAR-BENAR aktif di `.exe` Release/self-contained (Tahap C)

Unit test (§4.8) tidak membuktikan ini — berjalan di konfigurasi Debug, bukan lewat `.exe` yang dipublish. Skenario manual baru, ditambahkan ke `PANDUAN-TESTER.md` saat Tahap C, MINIMAL mencakup:

1. **Folder `logs/` belum ada** di instalasi bersih → jalankan `.exe` → picu satu exception yang diketahui aman dan bisa diulang (kandidat: berkas Excel yang sedang dikunci proses lain saat Impor — sudah pernah menyebabkan crash sungguhan di riwayat `PANDUAN-TESTER.md` percobaan 3) → **harap:** folder `logs/` otomatis dibuat.
2. **Berkas log benar-benar ditulis** → `logs/yyyyMMdd.log` (tanggal hari ini) muncul, berisi entri exception dari langkah 1, format sesuai §4.7.2, TANPA data sensitif (periksa manual: tidak ada NISN/NIP/nama/token/path akun Windows di isinya).
3. **Simulasi kegagalan penulisan log tidak menimbulkan exception baru yang terlihat pengguna** — set folder `logs/` jadi read-only lewat properti Windows sebelum memicu exception yang sama → **harap:** aplikasi tetap menampilkan pesan ramah yang sama seperti langkah 2 (bukan crash tambahan, bukan pesan galat KEDUA soal gagal menulis log).
4. **Rotasi menyisakan maksimum 30 log resmi** → taruh manual >30 berkas `yyyyMMdd.log` palsu (tanggal mundur) DITAMBAH 1 berkas nama asing (mis. `catatan-manual.txt`) di `logs/` sebelum menjalankan `.exe` → jalankan → **harap:** berkas `yyyyMMdd.log` tersisa tepat 30 (yang tanggalnya terbaru), `catatan-manual.txt` MASIH ADA, tidak ikut terhapus.
5. **Aplikasi tidak menampilkan dialog exception berulang** → picu exception yang sama 2 kali berturut-turut dalam sesi yang sama (kalau exception jenis itu memang bisa diulang tanpa menutup aplikasi, mis. lewat jalur yang whitelist-nya nanti terisi) ATAU verifikasi lewat kode (§4.3 langkah 4, flag `_sedangMenanganiGalatFatal`) bahwa struktur mencegah dialog bertumpuk — dicatat sebagai pemeriksaan kode + satu percobaan manual, bukan hanya pemeriksaan kode saja.
6. **Pemeriksaan build** (bukan runtime): pastikan berkas `PencatatGalat.cs` tidak memanggil `System.Diagnostics.Debug.*` di jalur manapun, supaya tidak mengulang §1.3.

### 4.10 Yang TIDAK dilakukan

- Tidak membuat `catch (Exception) { }` kosong atau generik di titik mana pun sebagai "solusi cepat".
- Tidak menambah pustaka logging pihak ketiga tanpa persetujuan eksplisit lebih dulu.
- Tidak mengubah `TerapkanRestore`/`SelesaikanRestoreDanTutup`.
- Tidak mengisi whitelist §4.4 dengan asumsi — hanya dengan bukti per kasus.
- Tidak membuat proyek baru (Opsi 2 di §4.1) tanpa persetujuan eksplisit.
- Tidak memanggil `exception.ToString()` mentah di jalur logging manapun (§4.7.2).

---

## 5. Urutan Kerja (Tahap A–G)

- **Tahap A — review rancangan logging dan exception handling.** ✅ **DITUTUP** — pemilik proyek memilih Opsi 1 (§4.1) dan menyetujui retensi 30 berkas (§4.7.4).
- **Tahap B — implementasi logging Release paling kecil dan aman.** ✅ **SELESAI** — lingkup §4.2 dan §4.8 dikerjakan persis sesuai rancangan. Rincian lengkap (berkas, ringkasan perubahan, hasil test) di §6.
- **Tahap C — build Release/self-contained dan buktikan log benar-benar tercipta.** ✅ **SELESAI SEBAGIAN** — lihat §6 untuk apa yang sudah dibuktikan lewat `.exe` Release sungguhan vs apa yang masih perlu klik manual sebagai tindak lanjut.
- **Tahap D — reproduksi crash secara terkontrol.** ⛔ **BELUM DIMULAI** — sesuai arahan eksplisit, berhenti setelah Tahap C.
- **Tahap E — identifikasi serta perbaiki root cause.** ⛔ Belum dimulai.
- **Tahap F — pengujian first-run.** ⛔ Belum dimulai.
- **Tahap G — perbaiki Bug #1, #2, #8/#9/#10.** ⛔ Belum dimulai.

**Tidak termasuk di tahap manapun** (arahan yang masih berlaku): refactor besar, perubahan UI massal, perubahan skema database, perbaikan jalur Restore. Bug #3–#6 dan #7 ditunda ke versi 1.1/berikutnya.

**Status saat ini: BERHENTI setelah Tahap B/C sesuai arahan eksplisit pemilik proyek — menunggu hasil diperiksa sebelum Tahap D dimulai.**

---

## 6. Perbaikan Terverifikasi Sesi Ini (Tahap B & C)

### 6.1 Berkas yang berubah

| Berkas | Jenis | Ringkasan |
|---|---|---|
| `src/Absensi.Data/Diagnostik/PencatatGalat.cs` | **Baru** | Kelas statis tunggal: `Catat(exception, sumber, level, folderLog)`. Menulis `logs/yyyyMMdd.log`, sanitasi path akun Windows + pesan domain/data + pola token/connection-string/angka panjang, rotasi 30 berkas resmi, tidak pernah melempar (jalur cadangan `_gagal-tulis.log` untuk kegagalan ganda). |
| `src/Absensi.App/LokasiData.cs` | Diubah | Tambah `FolderLog` (alias ke `PencatatGalat.FolderLogBawaan`) dan `PastikanFolderLogAda()`, mengikuti pola `FolderData`/`PastikanFolderDataAda` yang sudah ada. |
| `src/Absensi.App/App.xaml.cs` | Diubah | Daftar/lepas 3 handler global (`DispatcherUnhandledException`, `AppDomain.UnhandledException`, `TaskScheduler.UnobservedTaskException`) di `OnStartup`/`OnExit`; flag `_sedangMenanganiGalatFatal` mencegah dialog bertumpuk; 2 `Debug.WriteLine` (Penjadwal, backup pembukaan) diarahkan ke `PencatatGalat.Catat` level `ERROR`. |
| `src/Absensi.App/Wizard/WizardInstalasiViewModel.cs` | Diubah | 1 `Debug.WriteLine` (pengambilan backup cloud gagal di `PulihkanDariPcLama`) diarahkan ke `PencatatGalat.Catat` level `ERROR` — ditemukan lewat grep ulang, tidak disebut eksplisit di revisi sebelumnya, sekarang tercakup. |
| `src/Absensi.Telegram/KlienBotTelegram.cs` | Diubah | 4 `catch` yang sebelumnya sama sekali tanpa logging (`KirimPesanAsync` ×2, `AmbilPembaruanAsync` ×2) diarahkan ke `PencatatGalat.Catat` level `ERROR`, lewat referensi `Absensi.Data` yang SUDAH ADA — **tidak ada referensi baru ke `Absensi.App` yang ditambahkan**, perilaku kirim/terima (nilai kembalian `false`/list kosong saat gagal) tidak berubah. |
| `tests/Absensi.Data.Tests/PencatatGalatTests.cs` | **Baru** | 11 unit test (§4.8). |
| `docs/PANDUAN-TESTER.md` | Diubah | Skenario 10 baru (6 langkah) + 3 baris tabel "Hasil Smoke-Test Terakhir" (2026-09-21). |
| `docs/INVESTIGASI-CRASH-2026-09-21.md` | Diubah | Dokumen ini. |

Tidak disentuh (sesuai batasan): skema database, `TerapkanRestore`/`SelesaikanRestoreDanTutup`, `WizardInstalasiWindow.xaml`/logika langkah wizard, XAML/UI layar mana pun, `TODO-VERSI-BERIKUTNYA.md`, `BACKLOG-REDESAIN-UIUX.md`, `PANDUAN-UI-UX.md`.

### 6.2 Hasil unit test

- `dotnet test Absensi.sln` (Debug): **347/347 lulus** — 54 (`Absensi.Domain.Tests`) + 168 (`Absensi.Data.Tests`, termasuk 11 `PencatatGalatTests` baru) + 13 (`Absensi.Laporan.Tests`) + 8 (`Absensi.Telegram.Tests`) + 104 (`Absensi.App.Tests`). Sebelum sesi ini: 336. Tidak ada test lama yang berubah/gagal — murni penambahan.
- `dotnet test tests/Absensi.Data.Tests -c Release --filter PencatatGalatTests`: **11/11 lulus**, menjalankan logika yang SAMA PERSIS (IL) dengan yang dibundel ke `Absensi.App.exe` hasil publish (publish hanya membundel DLL Release yang sudah dikompilasi, tidak mengompilasi ulang).
- `dotnet build Absensi.sln` (Debug): 0 error, 0 warning.

### 6.3 Hasil 6 pengujian Release yang disepakati (§4.9) — status per item, jujur soal yang belum

| # | Item | Status | Bukti |
|---|---|---|---|
| 1 | Folder `logs/` belum ada → dibuat otomatis | ✅ **Diverifikasi lewat `.exe` Release sungguhan** | Publish baru (`dotnet publish ... -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -p:PublishReadyToRun=true -p:PublishTrimmed=false -o publish/AbsensiSekolah`) disalin ke folder bersih `C:\AbsensiSekolah-VerifikasiLog` (tanpa `data/`/`logs/`/`backup/`) → dijalankan → proses hidup (`Get-Process` mengonfirmasi judul jendela "Instalasi Pertama — Absensi") → `logs/` terkonfirmasi ada. Folder verifikasi dihapus lagi setelah selesai. |
| 2 | Log benar-benar ditulis dari executable Release | ⚠️ **Diverifikasi TIDAK LANGSUNG** — lewat `dotnet test -c Release` terhadap `Absensi.Data.dll` Release yang persis sama dibundel ke `.exe`, BUKAN lewat exception yang benar-benar dipicu dari dalam jendela `.exe` yang sedang berjalan. Format baris, isi, dan lokasi berkas terbukti benar untuk kode Release; yang belum dibuktikan adalah pemicunya lewat UI sungguhan. |
| 3 | Data sensitif tidak muncul di log | ✅ **Diverifikasi lewat unit test** (Debug dan Release) — path akun Windows, pesan `Source=Absensi.Data`/`Absensi.Domain`, pola token/connection-string/angka panjang semua diverifikasi diredaksi/disembunyikan. |
| 4 | Rotasi menyisakan maksimum 30 log resmi | ✅ **Diverifikasi lewat unit test** — 35 berkas resmi + 2 berkas asing → tersisa tepat 30 resmi, 2 asing tidak tersentuh (isi dicek sama persis). |
| 5 | Kegagalan menulis log tidak menyebabkan exception rekursif | ✅ **Diverifikasi lewat unit test** — `folderLog` dibuat menunjuk ke sebuah BERKAS (bukan folder) supaya penulisan pasti gagal, dipanggil 5× berturut-turut, tidak ada exception yang lolos ke pemanggil. |
| 6 | Aplikasi tidak menampilkan dialog exception berulang | ⚠️ **Diverifikasi lewat pembacaan kode, BELUM lewat percobaan klik manual** — flag `_sedangMenanganiGalatFatal` di `App.xaml.cs` secara struktural mencegah `MessageBox.Show` kedua terpanggil selagi satu episode fatal masih diproses, tapi belum ada percobaan yang benar-benar memicu 2 exception fatal berturut-turut di jendela sungguhan untuk membuktikannya end-to-end. |

**Yang MASIH BELUM dituntaskan (jujur, bukan diklaim selesai):** item 2 dan 6 di atas butuh exception yang dipicu dari dalam UI `.exe` yang sedang berjalan (bukan pemanggilan langsung ke `PencatatGalat.Catat` dari unit test) — ini butuh navigasi UI berlapis (login → menu → Impor Excel → dialog Open Windows dengan berkas terkunci) yang tidak dikerjakan sesi ini karena keterbatasan waktu. **Direkomendasikan sebagai langkah manual berikutnya sebelum Tahap D dimulai** — dicatat juga di `PANDUAN-TESTER.md` Skenario 10 baris tabel hasil.

### 6.4 Contoh log yang sudah disanitasi (diambil dari output unit test, disalin manual ke sini sebagai contoh)

```
[2026-09-21T17:03:11+07:00] [ERROR] [UjiSembunyi]
System.InvalidOperationException: [pesan disembunyikan — berasal dari lapisan data sekolah, lihat jenis exception dan stack trace]
   HResult=-2146233079, Source=Absensi.Data, TargetSite=-
   (tidak ada stack trace)
--- akhir entri ---
```
(Exception asli punya `Message` = "Siswa Budi Santoso dengan NISN 1234567890 tidak ditemukan." — tidak muncul sama sekali di atas, sesuai §4.7.2.)

```
[2026-09-21T17:03:11+07:00] [ERROR] [UjiJejak]
Absensi.Data.Tests.PencatatGalatTests+PengecualianJejakPalsu: 
   HResult=-2146233088, Source=<profil-pengguna>, TargetSite=-
   at Absensi.App.Contoh() in <profil-pengguna>\repo\Contoh.cs:line 42
--- akhir entri ---
```
(Path asli "C:\Users\budi123\repo\Contoh.cs" — nama akun `budi123` sudah tergantikan `<profil-pengguna>` di jalur `StackTrace` MAUPUN `Source` karena keduanya melewati fungsi sanitasi yang sama.)

### 6.5 Kegagalan atau keterbatasan yang masih ada (dicatat jujur, bukan disembunyikan)

1. **Item 2 dan 6 di §6.3** belum dibuktikan lewat UI sungguhan (lihat di atas).
2. **`AggregateException` dan pembungkus serupa** yang secara sengaja menyusun ulang teks `InnerException` ke `Message` miliknya sendiri BISA melewati sanitasi berbasis `Source` (`Source` milik si pembungkus sendiri, bukan `Absensi.Domain`/`Data`, jadi lolos ke jalur pola-scrub yang lebih longgar). Digrep: **nol pemakaian `AggregateException` di `src/` saat ini** — risiko ini teoretis, bukan celah aktif, tapi dicatat supaya tidak dianggap tidak ada kalau suatu saat ada kode baru yang memakainya.
3. **Berkas cadangan `_gagal-tulis.log`** (dipakai saat penulisan log utama gagal) tidak punya rotasi/batas ukuran sendiri — kalau kegagalan menulis menetap lama (mis. disk penuh berkepanjangan), berkas ini bisa tumbuh tanpa batas. Diterima sebagai keterbatasan jalur degenerate (§4.7.3: prioritasnya "tidak pernah melempar", bukan "sempurna dalam skenario kegagalan berkepanjangan").
4. **Timestamp log memakai `DateTimeOffset.Now` langsung, bukan `IPenyediaWaktu`** — penyimpangan yang disengaja dan didokumentasikan di komentar `PencatatGalat.cs` (timestamp diagnostik, bukan aturan bisnis yang pernah diuji nilainya; `Absensi.Telegram` yang jadi salah satu pemanggil juga tidak memegang `IPenyediaWaktu` sama sekali). Dicatat di sini juga supaya terlihat sebagai keputusan sadar, bukan kelalaian, kalau ada yang bertanya nanti.
5. **Pola redaksi (`PolaAkunWindows`, `PolaTokenTelegram`, `PolaConnectionString`, `PolaAngkaPanjang`) adalah heuristik**, bukan jaminan matematis menangkap SEMUA bentuk data sensitif — mis. NISN 9 digit atau nomor telepon berformat `+62...` dengan tanda hubung tidak akan cocok pola 10-18 digit murni. Ini kompromi yang sama seperti disebutkan §4.7.2 sendiri (pertahanan berlapis, bukan filter sempurna).
6. **Ketiga handler global belum pernah benar-benar menangkap exception SUNGGUHAN** dari aplikasi berjalan (hanya diverifikasi lewat pembacaan kode + unit test `PencatatGalat` yang dipanggil langsung) — ini persis pekerjaan Tahap D yang sengaja BELUM dimulai sesuai arahan.

### 6.6 Risiko regresi

- **Rendah** untuk perubahan `App.xaml.cs`/`LokasiData.cs`/`WizardInstalasiViewModel.cs`: seluruhnya aditif (pendaftaran handler baru, penggantian pemanggilan logging yang sebelumnya adalah no-op di Release), tidak ada baris logika bisnis yang dihapus/diubah. 347/347 test lama tetap hijau tanpa satu pun dimodifikasi.
- **Rendah-sedang** untuk `KlienBotTelegram.cs`: menambah pemanggilan `PencatatGalat.Catat` di jalur catch yang SEBELUMNYA silent — kalau `PencatatGalat.Catat` sendiri punya bug (tidak terlihat di 11 unit test), risikonya terbatas karena kelas itu dirancang tidak pernah melempar (§4.7.3), dan nilai kembalian `KirimPesanAsync`/`AmbilPembaruanAsync` (`false`/list kosong) tidak berubah sama sekali secara logika.
- **Tidak ada risiko** terhadap skema database, jalur Restore, atau UI — tidak ada satu berkas pun di area itu yang tersentuh (dikonfirmasi lewat `git status`/`git diff --stat`, lihat §6.1).
- **Belum diuji:** dampak nyata dari 3 handler global saat exception SUNGGUHAN terjadi di aplikasi berjalan (§6.5 poin 6) — teoretis risiko terendah karena `e.Handled` sengaja tidak pernah diubah jadi `true` untuk kasus tak dikenal (perilaku default .NET tetap berjalan seperti sebelumnya, hanya ditambah logging), tapi "belum diuji" tetap harus dicatat sebagai risiko, bukan diasumsikan aman.

---

## 6.7 Sesi Hardening Kedua (2026-09-21) — Tutup 2 Celah + Verifikasi UI Nyata

### 6.7.1 A.1 — Rekursi `AggregateException.InnerExceptions` + guard siklus + batas total

Diimplementasikan persis sesuai §4.7.2.a. Perubahan HANYA di `src/Absensi.Data/Diagnostik/PencatatGalat.cs` (tidak ada berkas lain tersentuh untuk item ini). Diuji lewat 2 unit test baru:
- `Catat_AggregateExceptionDenganInnerSensitif_MessageGabunganTidakBocorTapiInnerNonSensitifTetapTampil` — `AggregateException` dengan 2 `InnerExceptions` (satu `Source=Absensi.Data` berisi nama fiktif "Siti Aminah", satu `IOException` biasa) → dipastikan "Siti Aminah" TIDAK ADA di log, tapi pesan `IOException` yang non-sensitif TETAP tampil, dan penanda `"2 InnerException"` muncul menggantikan Message gabungan bawaan .NET.
- `Catat_AggregateExceptionMelebihiBatasTotal_DipotongDenganPenanda` — 25 `InnerExceptions` → dipastikan muncul penanda `"dipotong"`.

**Catatan jujur soal batas pengujian:** siklus referensi (`InnerException` yang menunjuk balik ke leluhurnya) TIDAK diuji dengan siklus sungguhan — `Exception.InnerException` tidak punya setter publik di .NET, jadi tidak ada cara membuat siklus lewat API publik untuk diuji tanpa reflection pada field privat BCL (dianggap terlalu rapuh untuk dijadikan test permanen). Guard `HashSet` berbasis identitas tetap ada di kode sebagai pengaman defensif, tapi keberadaannya divalidasi lewat pembacaan kode, BUKAN lewat test yang benar-benar memicu siklus.

### 6.7.2 A.2 — Batas ukuran `_gagal-tulis.log`

Diimplementasikan persis sesuai §4.7.3.a, HANYA di `PencatatGalat.cs`. Diuji lewat 2 unit test baru:
- `TulisCadangan_MelewatiBatasUkuran_DitimpaBukanTerusTumbuh` — berkas cadangan dipra-isi >1 MB, dipastikan ukurannya MENGECIL (ditimpa) setelah `Catat` dipanggil lagi, bukan terus bertambah.
- `Catat_KegagalanMenulisUtamaDanCadangan_TetapTidakMelempar` — target berkas log utama DAN berkas cadangan sama-sama dijadikan direktori (bukan berkas) sekaligus, dipastikan TIDAK melempar walau KEDUA jalur penulisan gagal bersamaan (skenario terpisah dari uji "folderLog sebagai berkas" yang sudah ada, supaya kegagalan jalur fallback teruji eksplisit sendiri).

### 6.7.3 Regresi penuh setelah A.1/A.2

`dotnet test Absensi.sln` — **351/351 lulus di konfigurasi Debug MAUPUN Release** (naik dari 347; 4 test baru: 2 `AggregateException` + 2 berkas cadangan). `dotnet publish` Release/self-contained diulang, 0 error. Exe hasil publish di-*hash* (`sha256sum`) sebelum dipakai untuk verifikasi §6.7.4, dikonfirmasi identik dengan hasil `dotnet publish` — memastikan yang diuji benar-benar build terbaru, bukan sisa lama.

### 6.7.4 B — Verifikasi item 2 lewat `.exe` Release NYATA, didorong UI Automation sungguhan

**Koreksi penting terhadap rekomendasi sesi sebelumnya:** Skenario 10 langkah 2 sebelumnya merekomendasikan "Impor Excel dengan berkas terkunci". **Sebelum dieksekusi, kodenya dibaca dulu** (`ImporGuruWizardViewModel.AnalisisBerkas`) — ternyata exception dari berkas terkunci ditangkap LOKAL oleh `catch (Exception ex) { PesanGalat = ex.Message; }` yang **tidak pernah memanggil `PencatatGalat`** (celah pre-existing yang sudah tercatat terpisah di `TODO-VERSI-BERIKUTNYA.md` A.3.2, bukan untuk diperbaiki di sini). Memakai skenario itu akan menghasilkan laporan PALSU ("terverifikasi") padahal tidak menguji jalur yang dimaksud sama sekali. **Diganti skenario yang benar-benar melewati `PencatatGalat`, tetap aman dan tidak menyentuh database:**

1. Salin `publish/AbsensiSekolah` (hasil `dotnet publish` terbaru, di-hash SHA-256 dan dikonfirmasi cocok) ke folder bersih.
2. Buat folder `backup/cloud-stub/<SHA256-HEX-KUNCI-LISENSI>/` berisi `terakhir.enc` (byte ACAK, bukan hasil enkripsi sungguhan) dan `terakhir.txt` (tanggal ISO 8601 valid) — meniru backup cloud yang RUSAK untuk kunci lisensi tertentu.
3. Jalankan `.exe`, **didorong lewat `System.Windows.Automation`** (bukan memanggil method ViewModel langsung dari kode): `ValuePattern.SetValue` pada kolom kunci lisensi sungguhan, `InvokePattern.Invoke` pada tombol **"Pulihkan dari PC Lama"** sungguhan.
4. Kode (`WizardInstalasiViewModel.PulihkanDariPcLama`) menemukan berkas (ADA), lalu `EnkripsiBackup.Dekripsi` gagal (tag AES-GCM tidak cocok, sesuai harapan karena datanya memang byte acak) → exception nyata `AuthenticationTagMismatchException` → tertangkap catch yang SUDAH diarahkan ke `PencatatGalat` sejak Tahap B.

**Bukti nyata (isi `logs/20260921.log`, disalin apa adanya):**
```
[2026-09-21T17:14:51+08:00] [ERROR] [WizardInstalasiViewModel.PulihkanDariPcLama]
System.Security.Cryptography.AuthenticationTagMismatchException: The computed authentication tag did not match the input authentication tag.
   HResult=-2146233087, Source=System.Security.Cryptography, TargetSite=Decrypt
   at System.Security.Cryptography.AeadCommon.Decrypt(SafeKeyHandle keyHandle, ReadOnlySpan`1 nonce, ReadOnlySpan`1 associatedData, ReadOnlySpan`1 ciphertext, ReadOnlySpan`1 tag, Span`1 plaintext, Boolean clearPlaintextOnFailure)
   at System.Security.Cryptography.AesGcm.Decrypt(Byte[] nonce, Byte[] ciphertext, Byte[] tag, Byte[] plaintext, Byte[] associatedData)
   at Absensi.Data.Backup.EnkripsiBackup.Dekripsi(Byte[] paket, String kunciLisensi) in D:\Project Absensi SMP 1\src\Absensi.Data\Backup\EnkripsiBackup.cs:line 51
   at Absensi.App.Wizard.WizardInstalasiViewModel.PulihkanDariPcLama()
--- akhir entri ---
```

**Pemeriksaan 7 syarat §B:**
1. ✅ Executable Release/self-contained terbaru — dikonfirmasi lewat hash SHA-256, sama dengan hasil publish terbaru.
2. ✅ Aksi lewat UI aplikasi — `InvokePattern`/`ValuePattern` UI Automation pada kontrol sungguhan (bukan pemanggilan method internal).
3. ✅ `logs/20260921.log` tercipta.
4. ✅ Waktu, level (`ERROR`), konteks (`WizardInstalasiViewModel.PulihkanDariPcLama`), tipe exception, `HResult`/`Source`/`TargetSite`, stack trace lengkap — semua tercantum.
5. ✅ Tidak ada nama siswa/guru/NISN/NIP/token/connection string/path akun Windows — dikonfirmasi lewat pembacaan isi berkas di atas. **Satu observasi jujur (bukan pelanggaran kebijakan):** stack trace mencantumkan path build `D:\Project Absensi SMP 1\src\...\EnkripsiBackup.cs` — path proyek, BUKAN path akun Windows (`C:\Users\<nama>\...`), jadi di luar cakupan yang diminta disamarkan (§4.7.1/§4.7.2 secara eksplisit menyasar path AKUN, bukan seluruh path). Dicatat sebagai pengamatan untuk pertimbangan Anda, bukan sesuatu yang saya perbaiki sepihak.
6. ✅ Pesan ramah ke pengguna — layar menampilkan "Tidak ditemukan backup cloud untuk kunci lisensi ini. Periksa kembali kunci lisensi, atau pilih 'Instalasi Baru'..." (pesan bawaan Wizard, bukan pesan generik §4.6 milik `DispatcherUnhandledException` — exception ini tertangkap LOKAL, bukan lolos ke handler global; item C di bawah membahas handler global secara terpisah). **Catatan:** pesan ini secara teknis mengaburkan beda "tidak ada backup" vs "backup rusak" — pre-existing, di luar cakupan pekerjaan ini.
7. ✅ Aplikasi tetap konsisten — `Get-Process` mengonfirmasi `Responding=True`, judul jendela tetap sama, kolom kunci lisensi masih bisa dipakai lagi (mis. mencoba "Instalasi Baru" setelahnya).

**Bonus yang terverifikasi sekalian (tidak diminta eksplisit, tapi relevan sebagai bukti tambahan):** seluruh alur Wizard 6 langkah dituntaskan penuh lewat UI Automation sungguhan sampai `MainWindow` tampil dan login berhasil, membuktikan `PastikanFolderLogAda()` membuat folder `logs/` dari `.exe` Release sungguhan (item 1 §4.9) TANPA satu pun exception muncul selama alur normal (log tetap kosong sampai skenario korup di atas dijalankan — mengonfirmasi logger tidak "berisik" untuk kondisi normal). Ditemukan sekalian (di luar tujuan awal): kultur aplikasi WPF `DatePicker` mengikuti `id-ID` (`dd/MM/yyyy`), bukan default `en-US` (`M/d/yyyy`) — dicatat di `PANDUAN-TESTER.md` supaya tidak perlu ditemukan ulang oleh siapa pun yang mengotomasi wizard di masa depan.

### 6.7.5 C — Item 6 (dialog tidak berulang saat handler global terpanggil)

**Status resmi: BELUM TERVERIFIKASI DINAMIS.** Sesuai arahan eksplisit, TIDAK ditambahkan tombol/menu/argumen command-line/environment variable/test hook permanen untuk memaksa `DispatcherUnhandledException` terpicu. Tidak ditemukan jalur UI yang aman DAN sudah terdokumentasi bisa memicu exception GLOBAL (lolos dari semua try/catch lokal) tanpa mengarang skenario buatan. Verifikasi lewat code review saja (§4.3, flag `_sedangMenanganiGalatFatal`): logikanya konsisten (dicek ulang di sesi ini, tidak berubah dari Tahap B), TAPI belum pernah benar-benar dipicu oleh exception sungguhan di aplikasi berjalan. **Verifikasi dinamis eksplisit ditunda ke Tahap D**, sesuai arahan — ini bukan kegagalan, ini status yang diminta dicatat apa adanya.

### 6.7.6 File yang berubah sesi hardening kedua

| Berkas | Perubahan |
|---|---|
| `src/Absensi.Data/Diagnostik/PencatatGalat.cs` | +§A.1 (rekursi `AggregateException.InnerExceptions`, guard siklus, batas total 20) dan +§A.2 (batas ukuran 1 MB berkas cadangan) |
| `tests/Absensi.Data.Tests/PencatatGalatTests.cs` | +4 unit test (§6.7.1, §6.7.2) |
| `docs/PANDUAN-TESTER.md` | Skenario 10 langkah 2 dikoreksi (skenario lama SALAH, diganti yang terbukti benar), langkah 4 ditandai belum terverifikasi dinamis, baris tabel hasil baru |
| `docs/INVESTIGASI-CRASH-2026-09-21.md` | Dokumen ini — §4.7.2.a, §4.7.3.a, §6.7 baru |

Tidak tersentuh (sesuai batasan eksplisit sesi ini): database, `TerapkanRestore`/`SelesaikanRestoreDanTutup`, Wizard (kode — hanya DIPAKAI lewat UI untuk pengujian, tidak diubah), XAML/UI mana pun, `TODO-VERSI-BERIKUTNYA.md`, `BACKLOG-REDESAIN-UIUX.md`, `PANDUAN-UI-UX.md`. Tidak ada commit dibuat.

---

## 7. Daftar Bug — sumber `BUG-REPORT-UAT-2026-09-20.md`

**Status berkas sumber:** belum ditemukan di repositori ini (dicari ulang di seluruh `D:\Project Absensi SMP 1`). Ini TIDAK dianggap bukti bug-bug berikut tidak nyata. Nomor bug tidak diubah/dihapus. Detail yang tidak tersedia ditandai eksplisit.

### 7.1 KRITIS / BLOCKER

**Bug #1 — Tombol Simpan tidak reaktif**
- Layar/langkah reproduksi persis: INFORMASI BELUM TERSEDIA.
- Dugaan arsitektural umum (BELUM diverifikasi): banyak `RelayCommand` dengan `_bisaEksekusi` di berbagai ViewModel form; kalau ada jalur validasi yang lupa memanggil `RaiseCanExecuteChanged()` pada command Simpan terkait, tombol bisa tampak macet nonaktif. Pola yang mungkin terjadi di lebih dari satu layar, bukan satu titik.

**Bug #2 — Aplikasi hang setelah penyimpanan dilakukan berulang**
- Layar/langkah reproduksi persis, jumlah pengulangan sebelum hang: INFORMASI BELUM TERSEDIA.
- Dugaan arsitektural umum (BELUM diverifikasi): koneksi SQLite tunggal dibagi semua layar + kemungkinan transaksi tidak selalu tertutup bersih pada jalur galat (sejalan dengan kekhawatiran §4.5) bisa menyebabkan penguncian setelah beberapa siklus simpan — SPEKULASI, level keyakinan sama seperti §2.2.

### 7.2 Prioritas Berikutnya

**Bug #8** — "perlu direproduksi dan diperbaiki." INFORMASI BELUM TERSEDIA lebih lanjut.

**Bug #9** — "aksi keluar harus terlihat dan dapat ditemukan pengguna." Kemungkinan terkait temuan windowing `PANDUAN-TESTER.md` Skenario 9 (tombol "Keluar" di header `MainWindow`) — DUGAAN penghubung, bukan konfirmasi.

**Bug #10** — "area tindakan harus tetap mudah dijangkau dan pesan validasi harus tetap terlihat." INFORMASI BELUM TERSEDIA soal layar mana persisnya.

### 7.3 Ditunda

- Bug #3–#6: ditunda ke versi 1.1.
- Bug #7: ruang lingkup versi berikutnya.

### 7.4 Yang dibutuhkan untuk melanjutkan §7

Isi lengkap atau path `BUG-REPORT-UAT-2026-09-20.md` — khususnya Bug #1, #2, #8, #9, #10.
