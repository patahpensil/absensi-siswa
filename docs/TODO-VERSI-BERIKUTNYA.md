# Ditunda ke Versi Berikutnya

Ditemukan lewat audit pengerasan Tugas 14 (RENCANA-TUGAS.md). Pemilik proyek memutuskan item-item ini **boleh ditunda** — bukan blocker serah terima, tapi harus diperbaiki di versi berikutnya. Jangan dikerjakan sebelum diminta.

## A.1 — Jam literal di pesan galat

**Berkas:** `src/Absensi.App/Wizard/WizardInstalasiViewModel.cs:322`, `src/Absensi.App/Pengaturan/PengaturanViewModel.cs:190`

Pesan validasi format jam memuat contoh literal `"...misalnya 07:30."`. Bukan nilai yang dipakai untuk keputusan apa pun (sekadar contoh format di hint), tapi secara harfiah melanggar CLAUDE.md §4.1 aturan 2 ("Tidak boleh ada angka 07:30 ... di dalam kode"). Perbaikan: ganti dengan contoh generik yang tidak menyerupai jam sekolah sungguhan (mis. "HH:mm").
   **STATUS: SELESAI — commit `4a291cfac36a0a3f6e95e24e1ca464d87bfa0943`.** Kedua pesan diganti jadi `"... tidak valid. Gunakan format 24 jam HH:mm."` (bukan literal `07:30` maupun jam contoh lain apa pun). Dikonfirmasi di `docs/MASTER-BUG-UIUX-REGISTER.md` A-10 (2026-09-22).

## A.2 — Timestamp migrasi tidak lewat IPenyediaWaktu

**Berkas:** `src/Absensi.Data/Migrations/MigrationRunner.cs:46`

`DateTime.UtcNow.ToString("o")` dipakai langsung untuk mengisi `skema_versi.diterapkan`, memotong `IPenyediaWaktu`. Tidak ada pengujian yang bergantung padanya saat ini. Perbaikan: `MigrationRunner.Run` menerima `IPenyediaWaktu` sebagai parameter.
   **STATUS: DITUTUP SEBAGAI PENYIMPANGAN SENGAJA (bukan coding).** `skema_versi.diterapkan` adalah metadata audit teknis, bukan waktu bisnis — tidak pernah dibaca balik oleh kode maupun test manapun. Komentar penjelas ditambahkan langsung di `MigrationRunner.cs`. Rincian dan preseden di `docs/MASTER-BUG-UIUX-REGISTER.md` A-11 (2026-09-22).

## A.3 — Logging dan catch luas

1. `src/Absensi.Telegram/KlienBotTelegram.cs` (baris 37, 41, 70, 74): `catch (HttpRequestException)`/`catch (TaskCanceledException)` tanpa logging sama sekali. Desain disengaja (PRD §9.4: "internet putus → antre, coba lagi"), tapi kegagalan yang **menetap** (mis. token salah permanen) tidak meninggalkan jejak apa pun. Perbaikan: tambah `System.Diagnostics.Debug.WriteLine` di tiap catch, pola sama seperti `App.xaml.cs`.
   **STATUS: SELESAI — commit `2f65f75`** (Tahap B/C, sebelum item ini sempat dicoret di sini). Keempat catch sudah diarahkan ke `PencatatGalat.Catat` (bukan `Debug.WriteLine` seperti usulan awal di atas — `PencatatGalat` belum ada saat poin ini pertama ditulis, dan sejak `2f65f75` menjadi satu-satunya jalur logging resmi di seluruh proyek). Diaudit ulang dan dikonfirmasi di `docs/MASTER-BUG-UIUX-REGISTER.md` A-9 (2026-09-22). Utang test terpisah (belum ada pengujian otomatis untuk jalur ini) dicatat di register A-19, bukan di sini.
2. `src/Absensi.App/MasterData/ImporGuruWizardViewModel.cs:102`, `ImporSiswaWizardViewModel.cs:111`: `catch (Exception ex)` terlalu luas dibanding pola yang benar di `LaporanViewModel.cs:297,454` (`catch (Exception ex) when (ex is InvalidOperationException or ArgumentException)`). Berisiko menampilkan pesan teknis mentah (mis. teks `IOException` bila berkas Excel sedang terbuka di aplikasi lain). Perbaikan: persempit jenis exception yang ditangkap.

## B.2 — Dua tombol utama di layar Pengaturan

**Berkas:** `src/Absensi.App/Pengaturan/PengaturanView.xaml` (tab "Umum")

"Simpan Bot Telegram" dan "Simpan Jadwal Harian" sama-sama `TombolUtamaStyle`, tampil bersamaan di satu tab — melanggar Panduan UI/UX §12 ("Satu tombol utama per layar"). Perbaikan: turunkan salah satu ke `TombolSekunderStyle`, atau pisah jadi tab terpisah (sudah ada preseden tab di layar ini sejak Tugas 14 — tab "Akun").

## Reuse warna status di berkas UI

Palet warna status (`HadirTeksBrush`/`AlpaDitolakTeksBrush`, didefinisikan Panduan UI/UX §3.2 khusus 5 kategori status kehadiran) dipakai ulang sebagai warna sukses/gagal generik untuk pesan yang **bukan** data kehadiran. Melanggar Panduan UI/UX §3.3 ("Jangan memakai warna status untuk ... hiasan"). Ditemukan di:

- `src/Absensi.App/Kalender/KalenderView.xaml` (pesan status)
- `src/Absensi.App/Koreksi/KoreksiView.xaml`
- `src/Absensi.App/Koreksi/StatusGuruView.xaml`
- `src/Absensi.App/Laporan/LaporanView.xaml`
- `src/Absensi.App/Notifikasi/NotifikasiView.xaml` (status bot "Terkonfigurasi"/"Belum diatur")
- `src/Absensi.App/Pengaturan/PengaturanView.xaml` (×2: Bot Telegram, Jadwal Harian) dan `src/Absensi.App/Pengaturan/AkunView.xaml` (ditambahkan Tugas 14, sengaja mengikuti pola yang sudah ada di layar yang sama — lihat catatan di bawah)
- `src/Absensi.App/RegistrasiKartu/RegistrasiKartuWindow.xaml`

Catatan: `KalenderView.xaml` juga memakai `AlpaDitolakLatarBrush`/`AlpaDitolakTeksBrush`/`AlpaDitolakGarisBrush` untuk mewarnai sel tanggal libur di kalender — ini bukan data kehadiran juga (data kalender: sekolah/libur), jadi termasuk temuan yang sama.

**Belum termasuk file baru Tugas 14** (`AkunView.xaml`) yang sengaja MENGIKUTI pola lama demi konsistensi visual satu layar (lihat komentar di berkas itu) — akan ikut diperbaiki bersamaan saat item ini dikerjakan, bukan pelanggaran baru yang berdiri sendiri.

Perbaikan: buat brush "sukses"/"gagal" generik terpisah dari palet status kehadiran, ganti seluruh pemakaian di atas.

## Fitur yang PRD sebut tapi belum pernah dibangun (bukan regresi Tugas 14)

- **`template_import_siswa.xlsx`** (PRD §16.2 langkah 3, folder `templates/`) — tidak pernah ada fitur unduh-template di aplikasi manapun sejak Tugas 0-14. `docs/PANDUAN-PEMASANGAN.md` sudah ditulis berdasarkan kolom yang benar-benar divalidasi kode (`ImporSiswaExcel.KolomBaku`/`ImporGuruExcel.KolomBaku`), bukan berdasar berkas template yang tidak ada. Pertimbangkan menambah tombol "Unduh Template" di layar Siswa/Guru pada versi berikutnya.

## Nama aksesibilitas ComboBox "Peran" (Pengaturan → Akun → Tambah Akun)

Ditemukan saat smoke-test manual Tugas 14 (bukan blocker — tampilan visual untuk pengguna biasa sudah benar). `TambahAkunWindow.xaml`'s ComboBox Peran memakai `DisplayMemberPath="Teks"` untuk teks yang TERLIHAT ("Admin"/"Wali Kelas") — sudah benar untuk pengguna biasa. Tapi nama yang dibaca lewat UI Automation/pembaca layar untuk tiap pilihan jatuh balik ke `PeranPilihan.ToString()` (mis. "PeranPilihan { Peran = Admin, Teks = Admin }") karena `PeranPilihan` tidak meng-override `ToString()` dan tidak ada `AutomationProperties.Name` eksplisit di `ComboBoxItem`. Dampak: pengguna pembaca layar akan mendengar teks yang membingungkan saat memilih peran. Perbaikan: override `ToString()` pada `PeranPilihan` agar mengembalikan `Teks`, atau set `AutomationProperties.Name` lewat `ItemContainerStyle`.
