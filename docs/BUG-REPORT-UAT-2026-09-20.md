# Bug Report — UAT Aplikasi Absensi SMP N 1 Pancarijang

**Tanggal test:** 20 September 2026
**Build .exe:** `publish/AbsensiSekolah/Absensi.App.exe` (203 MB, 20 Sep 2026 01:24)
**Commit terakhir:** `e2ec306` di master
**Environment test:** Windows, folder bersih `C:\AbsensiSekolah-Test\`
**Ditemukan oleh:** UAT manual, bukan unit test (336 unit test hijau semua)
**Trigger UAT:** Persiapan test hardware reader RFID sebelum go-live

---

## Ringkasan Bug

| # | Bug | Severity | Layar | Blocker? |
|---|---|---|---|---|
| 1 | Form Tambah Siswa: tombol Simpan tidak reaktif | **KRITIS** | Siswa | ✅ Ya (blokir tambah manual) |
| 2 | Form Tambah Siswa: aplikasi hang setelah klik Simpan | **KRITIS** | Siswa | ✅ Ya |
| 3 | Dropdown Kelas di form Tambah Siswa tidak refresh saat master kelas berubah | **SEDANG** | Siswa | ❌ Workaround: restart aplikasi |
| 4 | Dropdown Kelas kosong tampilkan kotak putih besar menutupi field bawahnya | **SEDANG** | Siswa | ❌ Kosmetik |
| 5 | Impor Excel: tidak ada tombol Cancel/Batal di wizard 10 langkah | **SEDANG** | Siswa | ❌ UX |
| 6 | Logo sekolah masih placeholder lingkaran di header MainWindow | **RENDAH** | Header global | ❌ Kosmetik |
| 7 | Dashboard "belum dibangun" — empty state | **RENDAH** | Dashboard | ❌ Sudah tercatat di TODO |

---

## Detail Per Bug

### Bug #1 — Tombol Simpan form Tambah Siswa tidak reaktif ⚠️ KRITIS

**Layar:** Siswa → Tambah

**Reproduksi:**
1. Login sebagai kepsek
2. Menu Siswa → tombol Tambah
3. Isi semua field valid:
   - NISN: `0000000001` (10 digit)
   - Nama lengkap: `Edgar Azzikra`
   - Jenis kelamin: L
   - Kelas: 7A-Test (dari dropdown)
   - Tempat lahir: Rappang
   - Tanggal lahir: `2009-03-15` (format YYYY-MM-DD)
   - Alamat, nama ortu, no HP: terisi
4. Klik tombol Simpan

**Hasil aktual:**
- Tombol tidak merespons klik
- Tidak ada pesan error
- Tidak ada pesan validasi
- Data tidak tersimpan (tabel utama tetap kosong)

**Hasil diharapkan:**
- Siswa tersimpan ke database
- Muncul di tabel Siswa
- Form tertutup atau reset untuk input siswa berikutnya
- Notifikasi "Siswa tersimpan" muncul

**Dampak:** TU tidak bisa tambah siswa manual satu per satu. Semua penambahan harus via impor Excel — tidak praktis untuk mutasi siswa baru masuk di tengah tahun.

**Kemungkinan penyebab:**
- `SimpanCommand.CanExecute` return false tapi tidak visible di UI
- Command tidak ter-bind ke tombol
- Validasi tersembunyi ada di field yang tidak kelihatan

**Fix suggestion:** Cek `SiswaViewModel.SimpanCommand` — kemungkinan `CanExecute` cek field yang tidak ter-INotifyPropertyChanged saat user ketik.

---

### Bug #2 — Aplikasi hang setelah klik Simpan berulang ⚠️ KRITIS

**Layar:** Siswa → Tambah

**Reproduksi:**
1. Ikuti langkah Bug #1
2. Klik tombol Simpan beberapa kali (karena tidak merespons)
3. Coba interaksi lain (klik menu, scroll)

**Hasil aktual:**
- Aplikasi freeze / not responding
- Window Windows menjadi abu-abu
- Harus tutup paksa via Task Manager atau `Stop-Process`

**Hasil diharapkan:**
- Aplikasi tetap responsif meski tombol tidak berfungsi
- Kalau ada error, tampilkan pesan bukan hang

**Dampak:** Data yang sedang diketik hilang saat force-close. Bisa menyebabkan database lock kalau ada transaksi setengah jalan.

**Kemungkinan penyebab:**
- Command jalan tapi stuck di operasi database sinkron di UI thread (tidak `await`)
- Deadlock antara connection pool SQLite dan UI thread
- Loop infinite di `CanExecute` yang re-evaluate terus

**Fix suggestion:** Bungkus `LayananSiswa.Tambah()` dengan `Task.Run()` supaya tidak blok UI thread, dan `await` dengan `IAsyncRelayCommand` (CommunityToolkit.Mvvm).

---

### Bug #3 — Dropdown Kelas tidak refresh setelah master berubah 🟡 SEDANG

**Layar:** Siswa → Tambah (dropdown Kelas)

**Reproduksi:**
1. Buka form Tambah Siswa — dropdown Kelas kosong (belum ada kelas)
2. **Tanpa tutup form**, klik menu Kelas → tambah kelas `7A-Test` → simpan
3. Klik menu Siswa → klik Tambah lagi (tanpa restart aplikasi)
4. Cek dropdown Kelas

**Hasil aktual:**
- Dropdown Kelas tetap kosong
- Harus restart aplikasi untuk dropdown terisi

**Hasil diharapkan:**
- Dropdown otomatis refresh saat form dibuka
- Atau ada mekanisme MessageBroker yang notify perubahan master

**Dampak:** UX buruk untuk TU. Bingung kenapa dropdown kosong padahal kelas sudah dibuat.

**Fix suggestion:**
- Load ulang daftar kelas di `SiswaViewModel.BukaFormTambah()` setiap kali dipanggil
- Atau subscribe ke event global `MasterKelasBerubah` yang di-broadcast oleh `LayananKelas`

---

### Bug #4 — Dropdown kosong tampilkan kotak putih menutupi field 🟡 SEDANG (Kosmetik)

**Layar:** Siswa → Tambah

**Reproduksi:**
1. Buka form Tambah Siswa saat master kelas belum ada
2. Klik dropdown Kelas untuk expand
3. Perhatikan area di bawah dropdown

**Hasil aktual:**
- Kotak putih besar tampil (dropdown empty state)
- Menutupi label dan input "Tanggal lahir"
- Menyembunyikan bahwa masih ada field lain di bawah

**Hasil diharapkan:**
- Empty state dropdown pakai teks kecil: "Belum ada kelas. Buat di menu Kelas."
- Tidak menutupi field lain

**Fix suggestion:** ComboBox WPF butuh `ItemsPanelTemplate` custom untuk empty state, atau tampilkan pesan di popup dengan `MinHeight` kecil.

---

### Bug #5 — Impor Excel tidak ada tombol Cancel 🟡 SEDANG (UX)

**Layar:** Siswa → Impor Excel (wizard 10 langkah)

**Reproduksi:**
1. Menu Siswa → Impor Excel
2. Pilih file .xlsx
3. Masuk ke tahap Analisis / Validasi / Preview
4. Cari tombol untuk membatalkan

**Hasil aktual:**
- Tidak ada tombol Batal / Cancel di step manapun
- User terjebak sampai selesai atau tutup paksa via X window

**Hasil diharapkan:**
- Setiap step wizard punya tombol "Batal" di kiri bawah
- Tombol Esc juga cancel
- Konfirmasi "Batalkan impor?" sebelum tutup kalau sudah masuk step Preview

**Dampak:** Kalau salah pilih file, TU harus tutup seluruh dialog paksa — kesan aplikasi tidak ramah.

**Fix suggestion:** Tambah tombol Batal di footer `ImporSiswaWizardWindow.xaml` dan handle `KeyDown` untuk Escape key.

---

### Bug #6 — Logo sekolah placeholder di header MainWindow 🔵 RENDAH (Kosmetik)

**Layar:** MainWindow (header top-bar)

**Reproduksi:**
1. Setup wizard, di langkah 2 tidak upload logo (atau upload logo)
2. Login → header MainWindow

**Hasil aktual:**
- Sudut kiri atas hanya lingkaran biru muda kosong (placeholder)
- Meski logo sudah di-upload di wizard/Pengaturan, tidak ditampilkan

**Hasil diharapkan:**
- Kalau logo diupload: tampil di header
- Kalau kosong: tampilkan inisial nama sekolah (fallback yang sudah ada di LoginWindow)

**Catatan:** Fix logo di LoginWindow sudah dilakukan di commit `dce314e`, tapi header MainWindow belum. Sudah disebut di analisis Tugas 14 sebagai "belum diperbaiki, murni tampilan bukan bug".

**Fix suggestion:** Terapkan pattern yang sama di LoginWindow ke `MainWindow.xaml` header — binding ke `sekolah.logo_path` + fallback initials.

---

### Bug #7 — Dashboard belum dibangun 🔵 RENDAH (Scope Tercatat)

**Layar:** Dashboard (menu default setelah login)

**Reproduksi:**
1. Login → langsung buka Dashboard

**Hasil aktual:**
- Layar kosong dengan teks "Layar 'Dashboard' belum dibangun"

**Hasil diharapkan (PRD §14.2):**
- Ringkasan harian: Hadir · Terlambat · Izin/Sakit · Alpa
- Status rekap notifikasi hari itu
- Peringatan pairing (siswa belum tertaut Telegram)
- Daftar siswa belum tap
- Tombol cepat: Koreksi, Mode gerbang

**Catatan:** Ini sudah tercatat di `TODO-VERSI-BERIKUTNYA.md` sebagai fitur v2. Bukan bug regresi, memang tidak masuk 15 tugas versi 1.

---

## Yang Sudah Terverifikasi Bekerja (Regresi Test)

Berikut yang berjalan normal saat UAT — jangan sampai rusak di fix bug di atas:

✅ **Wizard 6 langkah** — dari Lisensi sampai Selesai berjalan mulus
✅ **Wizard → Login transition** — tidak crash, tidak keluar (fix `e2ec306`)
✅ **Login berhasil dengan akun kepsek** yang dibuat di wizard
✅ **MainWindow render** — sidebar 11 menu, header dengan nama sekolah, empty state
✅ **Windowing controls** — Minimize / Maximize / Close di title bar semua jendela
✅ **Title dinamis** — "Absensi - SMP Negeri 1 Pancarijang"
✅ **Master Kelas** — tambah kelas, simpan, muncul di daftar dengan nama + tingkat + wali
✅ **Reader RFID keyboard-emulation** — kirim UID + Enter otomatis ke Notepad (format: `0001329346`)
✅ **Filter kelas di layar Siswa** — dropdown atas tabel bekerja
✅ **Hitungan §14.4** — "Kelas 7A-Test · 0 siswa belum punya kartu · 0 belum berfoto" muncul dengan benar

---

## Belum Diverifikasi (Karena Blocker Bug #1-#2)

Belum bisa dilanjut UAT karena tidak ada siswa di database:

- ⏳ Impor Excel siswa (jalur produksi normal)
- ⏳ Impor foto siswa massal
- ⏳ Registrasi kartu RFID → siswa
- ⏳ Layar gerbang tap kartu end-to-end
- ⏳ Aturan tap: jeda 15 detik, kartu tidak terdaftar, tap ganda, tap di jam pulang
- ⏳ Koreksi status manual
- ⏳ Kalender: tanggal libur → tap ditolak
- ⏳ Notifikasi Telegram outbox
- ⏳ Laporan bulanan/mingguan/semester
- ⏳ Backup otomatis + restore
- ⏳ Manajemen akun (Admin + Wali Kelas)

**Semua item di atas menunggu bug #1 dan #2 diperbaiki, atau bypass via impor Excel dulu.**

---

## Rekomendasi Prioritas Fix

**Wajib fix sebelum serah terima sungguhan:**
- Bug #1 dan #2 (form Tambah Siswa) — kritis karena TU sekolah akan sering pakai untuk mutasi siswa baru di tengah tahun ajaran

**Boleh ditunda ke iterasi v1.1:**
- Bug #3, #4 (dropdown refresh + empty state)
- Bug #5 (Cancel button impor)
- Bug #6 (logo header MainWindow)

**Sudah scope versi berikutnya:**
- Bug #7 (Dashboard) — di TODO-VERSI-BERIKUTNYA.md

---

## Action Item Selanjutnya

### Prompt Fix untuk Agen Claude Code (Sesi Berikutnya)

```
Fix critical bugs di form Tambah Siswa (SiswaView.xaml + SiswaViewModel):

Bug #1: Tombol Simpan tidak reaktif meski semua field valid
- Cek SimpanCommand binding dan CanExecute
- Test dengan data: NISN 10 digit, nama, JK, kelas dari dropdown,
  tanggal format YYYY-MM-DD, semua field lain terisi

Bug #2: Aplikasi hang setelah klik Simpan berulang
- Kemungkinan operasi database sinkron di UI thread
- Bungkus dengan Task.Run atau pakai IAsyncRelayCommand

Bug #3: Dropdown Kelas tidak refresh saat master kelas berubah
tanpa restart aplikasi
- Reload daftar kelas setiap form dibuka

Bug #4: Dropdown Kelas kosong tampil kotak putih besar menutupi
field "Tanggal lahir"

Bug #5: Impor Excel wizard 10 langkah tidak punya tombol Batal/Cancel
- Tambah tombol Batal di footer setiap step
- Handle Escape key

Bug #6: Logo sekolah di header MainWindow masih placeholder lingkaran
- Terapkan pattern yang sama seperti LoginWindow (commit dce314e)
- Binding sekolah.logo_path + fallback inisial nama sekolah

Verifikasi manual (bukan cuma unit test):
- Test wizard fresh install → tambah kelas → tambah siswa manual →
  siswa muncul di tabel
- Test impor Excel dengan file valid dan invalid
- Test Cancel di setiap step impor

Update PANDUAN-TESTER.md tambah Skenario 10: "Form CRUD manual"
dengan checklist lengkap.

Commit setelah semua bug hijau, jangan gabung dengan pekerjaan lain.
```

### Untuk User (Pemilik Proyek)

**Sebelum sesi fix agen:**
1. Simpan file ini di `D:\Project Absensi SMP 1\docs\BUG-REPORT-UAT-2026-09-20.md`
2. Backup database `C:\AbsensiSekolah-Test\data\hang-backup.db` (kalau masih ada) — untuk debug agen

**Setelah agen commit fix:**
1. Rebuild `.exe` sesuai panduan deploy
2. UAT ulang dengan fokus: tambah siswa manual, impor Excel dengan Cancel
3. Kalau semua hijau, lanjut UAT registrasi kartu + layar gerbang tap
4. Kalau semua tahap UAT hijau, `.exe` benar-benar siap serah terima

---

## Riwayat Sesi UAT

- **20 Sep 2026 01:20** — Build `.exe` self-contained sukses (203 MB, 258 DLL runtime)
- **20 Sep 2026 01:32** — Smoke-test bersih: wizard → login → MainWindow ✅
- **20 Sep 2026 01:40** — Test reader RFID di Notepad ✅ (UID 10 digit + Enter)
- **20 Sep 2026 01:43** — Buat 2 kelas test (1a test, 7A-Test) ✅
- **20 Sep 2026 01:48** — Bug #3 ditemukan (dropdown kosong)
- **20 Sep 2026 01:49** — Workaround: restart aplikasi ✅
- **20 Sep 2026 01:55** — Bug #1 ditemukan (Simpan tidak reaktif)
- **20 Sep 2026 01:56** — Bug #5 ditemukan (Cancel Impor tidak ada)
- **20 Sep 2026 02:00** — Bug #2 ditemukan (aplikasi hang), tutup paksa via Task Manager
- **20 Sep 2026 02:05** — Reset database untuk strategi baru: bypass form dengan Impor Excel

---

*Dokumen ini dibuat sebagai artefak UAT sebelum test hardware lanjutan.*
*Serah terima ke SMP N 1 Pancarijang menunggu fix Bug #1 dan #2.*
