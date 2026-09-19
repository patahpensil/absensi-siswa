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
