# Sistem Informasi Berbasis Digital UPT SMP Negeri 1 Pancarijang

## STATUS TERKINI (untuk melanjutkan)

Ditulis sebagai penanda posisi sesi - fakta di bawah diverifikasi langsung dari
repo saat penanda ini ditulis, bukan dari ingatan sesi sebelumnya. Perbarui
blok ini setiap kali sesi baru menutup pekerjaan, jangan biarkan basi.

**Commit master terkini:** `e1e63c1` - "fix(dashboard): latar grafik
pyqtgraph terang, bukan gelap bawaan", di atas `92f6ec6` - "feat(dashboard):
grafik kedisiplinan + status operasional (PRD §14.1)". `feat/dashboard`
sudah fast-forward ke master atas instruksi eksplisit pengguna (tidak ada
merge commit). Working tree bersih. Full suite 717 lulus sebelum merge.
Grafik Dashboard diverifikasi VISUAL oleh pengguna sendiri lewat tangkapan
layar (server uji terpisah, `absensi.db` tidak tersentuh) sebelum dikunci.

**Slice yang sudah selesai** (menu §18, diverifikasi dari `app/ui/` dan
`app/server/routers/` saat ini):

| Menu §18 | Status |
|---|---|
| DASHBOARD | Sebagian - `app/ui/dashboard/`: LayarDashboard (ringkasan + tren keterlambatan per kelas + alpa terbanyak) + LayarRingkasKepsek. Siswa saja; status operasional membaca sinyal nyata (jalur/klien/antrean lobi), pencadangan jujur "belum tersedia" |
| ABSENSI - layar scan | Sebagian - `app/ui/lobi/` SIMULATOR saja, reader fisik belum tersambung |
| ABSENSI - riwayat | Selesai - `app/ui/riwayat_scan/` |
| ABSENSI - koreksi satuan (siswa) | Selesai - `app/ui/koreksi_satuan/` |
| ABSENSI - koreksi satuan (guru) | Selesai - `app/ui/koreksi_guru/` (bukan nama menu §18 tersendiri, tapi fungsinya di area ABSENSI/§6) |
| ABSENSI - koreksi massal | Selesai - `app/ui/koreksi_massal/` |
| DATA MASTER | Selesai - `app/ui/data_master/` |
| KALENDER | Selesai - `app/ui/kalender/` |
| NOTIFIKASI | Sebagian - `app/ui/notifikasi/`: empat template terpisah (editable), antrean baca-saja, mode uji. Pemicu nyata (enqueue dari absensi) BELUM disambungkan - lihat utang #6 di bawah |
| LAPORAN | Sebagian - `app/ui/laporan/`: rekap/detail siswa, ekspor Excel/PDF berkop, arsip PDF otomatis, Koreksi Setelah Laporan Terbit. **Siswa saja** - laporan guru di luar cakupan (lihat catatan slice di bawah) |
| PERANGKAT | Sebagian - `app/ui/perangkat/`: Uji Baca Kartu, Izin Darurat Alat, Wizard Ganti Jalur Alat. **Belum ada**: layar daftar/kelola unit fisik (tidak ada registry perangkat, lihat utang di bawah), Pemantauan Kesehatan Alat (SKILL.md) |
| PENGGUNA | Selesai - `app/ui/pengguna/` |
| PENGATURAN | Selesai - `app/ui/pengaturan/` |
| SISTEM (pencadangan, pemulihan, log audit) | Belum ada sama sekali |

**Slice berikutnya:** **Slice 14 - Modul Pencadangan (Sistem, PRD §15)** -
nomor ini dikonfirmasi pengguna sendiri di sesi sebelumnya ("modul backup baru
ada di Slice 14"), dan masih satu-satunya nomor slice mendatang yang punya
konfirmasi eksplisit. Slice ini menutup DUA utang aktif sekaligus (lihat di
bawah): snapshot koreksi massal yang masih menolak tegas, dan lampiran koreksi
guru yang belum terenkripsi. CATATAN JUJUR: nomor slice untuk pemicu nyata
Notifikasi/utang #6, temuan code-review minor/utang #7, laporan guru/utang #8,
registry perangkat/utang #9 masih belum dikonfirmasi pengguna di sesi mana
pun - JANGAN menebak urutannya, tanya pengguna slice mana yang sebenarnya
mereka mau duluan. Slice **Perangkat** (PcscReader, Wizard Ganti Jalur, Izin
Darurat, Uji Baca Kartu) dan Slice **Dashboard** (grafik kedisiplinan +
status operasional, PRD §14.1) sudah SELESAI dan digabung ke master - lihat
entri §18 di atas dan utang #9-#10 di bawah.

**Utang aktif tercatat** (verbatim inti, lihat "Yang belum dikerjakan" dan
"Utang semantik" di bawah untuk detail lengkap):

1. **Ambang koreksi massal hardcode** - `Pengaturan.ambang_snapshot_sebelum_massal`/
   `.ambang_konfirmasi_ketik` (`app/config/pengaturan.py`) hardcode `5000`,
   TIDAK punya `KunciPengaturan` maupun method `atur_...()` - beda dari
   `ambang_tahan_konversi_pulang` yang sudah tersambung penuh ke `settings`.
   Layar Pengaturan juga tidak mengekspos keduanya.
2. **Enkripsi lampiran at-rest** - berkas lampiran koreksi guru tersimpan
   polos di filesystem server, sejajar modul pencadangan (§15) yang juga
   belum dienkripsi karena belum dibangun sama sekali (lihat Slice 14 di atas).
3. ~~Kolom `jalur` masih hilang di `attendance_events`~~ **DITUTUP** - lihat
   entri "Jalur pada attendance_events" di bawah untuk detail lengkap.
   PRD §17 kini terpenuhi: `AttendanceEvent.jalur` tersimpan sejak
   `POST /scan`, dengan pola plumbing identik slice `uid_mentah`
   (parameter opsional diteruskan apa adanya, nol perubahan klasifikasi).
4. **Layar absensi lobi baru menangani 5 dari 8 label `HasilScan`** (PRD
   §7.4) - `KARTU_TIDAK_DIKENAL`/`KARTU_NONAKTIF` melebur jadi `UNKNOWN_CARD`,
   `HARI_LIBUR` melebur jadi `DI_LUAR_PERIODE` bersama `DI_LUAR_JENDELA`,
   `MANUAL_ENTRY` belum ada jalurnya sama sekali di `POST /scan`.
5. **UTANG SEMANTIK ALPA (WAJIB diputuskan sebelum Fase 2/WhatsApp aktif)** -
   scan yang dibuffer lalu tiba setelah cutoff bertemu ALPA-otomatis yang
   sudah dibuat scheduler; aturan lama "ALPA tak berubah otomatis" tidak
   dirancang untuk siswa tepat-waktu yang paketnya telat. Ini GERBANG WAJIB
   sebelum Fase 2 - ALPA palsu untuk siswa tepat-waktu berarti notifikasi
   keliru ke orang tua (bahaya yang §8 cegah). Kasus serupa juga ada di
   cutoff pulang (`jam_pulang` terisi benar lewat `catat_pulang()`, tapi
   `status_final` tidak ikut terkoreksi).
6. **PEMICU NYATA NOTIFIKASI BELUM DISAMBUNGKAN** (baru, slice Notifikasi) -
   LANGKAH 0 slice Notifikasi menemukan `NotificationRepository.antrekan()`
   TIDAK PERNAH dipanggil kode produksi mana pun (hanya test) - tidak ada
   satu pun titik di `app/core/engine.py`, jalur scan, atau `app/scheduler/`
   yang benar-benar mengantrekan notifikasi TERLAMBAT/ALPA_PAGI/ALPA_PULANG/
   REKAP. Dikonfirmasi pengguna: DISENGAJA di luar cakupan slice Notifikasi
   (yang fokus struktur: template/antrean/mode uji saja) karena menuntut
   menyentuh `app/core/engine.py` (Aturan Global #1). Detail per template:
   - TERLAMBAT ("setelah absensi tercatat") - perlu dipanggil dari jalur scan
     siswa TERLAMBAT, DAN perlu job scheduler proses-segera baru (job
     notifikasi yang ada sekarang cuma jalan di cron 09:15/cutoff pulang,
     bukan segera)
   - ALPA_PAGI - perlu dipanggil dari `MesinAbsensi.tetapkan_alpa()` atau
     `scheduler._jalankan_cutoff_alpa()`
   - ALPA_PULANG - perlu dipanggil dari `MesinAbsensi.jalankan_cutoff_pulang()`
     atau `scheduler._jalankan_cutoff_pulang()`
   - REKAP ("sesuai pengaturan") - belum ada `KunciPengaturan` untuk
     frekuensinya sama sekali, PRD tidak merinci nilainya - perlu keputusan
     pengguna dulu sebelum dibangun
   - **Pesan gabungan** (§13.3, kakak-beradik alpa bersamaan → satu pesan)
     terikat pada keputusan pemicu di atas, ikut ditunda
   - Struktur yang SUDAH SIAP menyambut pemicu ini (tidak perlu dibangun
     ulang): `JenisNotifikasi` sudah 4 nilai, `QueueProcessor` sudah
     cek-ulang status ALPA_PAGI/ALPA_PULANG dan mengalihkan tujuan saat mode
     uji aktif, `MessageTemplateRepository`/layar Template sudah siap diisi
7. **Temuan code-review slice Notifikasi (minor, tidak mendesak)** - dua
   temuan lain sudah diperbaiki (lihat entri "Notifikasi selesai sisi
   struktur" di bawah: validasi placeholder rusak, payload mode uji tidak
   kirim ulang nomor tanpa perubahan). Sisanya SENGAJA dicatat sebagai
   utang, bukan dikerjakan:
   - **(a) `mode_uji_whatsapp_aktif()`/`nomor_admin_mode_uji()` dibaca
     ulang tiap iterasi loop** di `QueueProcessor.proses_satu_putaran()`
     (`app/notify/processor.py`) - harusnya di-hoist sekali sebelum loop
     seperti `batas_percobaan_notifikasi()` sudah dilakukan. Query
     berlebih, bukan bug fungsional - baru terasa kalau antrean berisi
     ratusan baris sekaligus
   - **(b) `NotifikasiService.daftar_template()` 4 query terpisah**
     (satu `cari_aktif()` per `JenisNotifikasi`) alih-alih satu query
     gabungan - konstan 4, bukan N+1 yang membesar seiring data,
     dampaknya kecil
   - **(c) Baris `jenis` lama (nilai sebelum migrasi `1cdfb1a07236`)
     akan melempar galat saat dibaca ulang** karena tidak lagi cocok
     dengan `JenisNotifikasi` Python yang baru - diverifikasi lewat
     migrasi percobaan (db scratch, baris `jenis='ALPA'` disisipkan
     sebelum upgrade): migrasi SENDIRI tetap sukses tanpa CHECK
     constraint apa pun (kolomnya cuma `VARCHAR`, SQLite tidak
     menegakkan panjang/nilai), tapi baris itu lalu gagal dibaca balik
     lewat `MessageTemplateRepository`/`NotificationRepository`. Nihil
     risiko untuk `absensi.db` saat ini (dikonfirmasi kosong di kedua
     tabel sebelum migrasi ditulis) - relevan hanya kalau migrasi ini
     nanti dijalankan ke basis data yang SUDAH punya baris lama
8. **Laporan guru di luar cakupan slice Laporan** (baru) - PRD §14.2 hanya
   menyebut "per kelas"/"per siswa", konsep yang tidak berlaku untuk guru
   (sama seperti Koreksi Massal). `app/server/laporan_service.py` murni
   siswa; belum ada padanan untuk absensi guru. Catatan desain lain dari
   slice ini (tidak dianggap utang, lihat entri lengkap di bawah):
   `Laporan harian/mingguan/bulanan/per kelas/per siswa` PRD §14.2 SENGAJA
   diimplementasikan sebagai SATU bentuk keluaran komposit (rentang
   tanggal + saring kelas/siswa opsional), bukan lima layar terpisah -
   kelima nama tetap terpenuhi sebagai kombinasi parameter yang valid.
   Ekspor Excel TIDAK diarsipkan (hanya PDF, sesuai bacaan literal PRD
   §9.3 "arsip PDF" - Excel murni kenyamanan unduh).

9. **Tidak ada registry perangkat terpusat** (baru, slice Perangkat) -
   `devices`/`ModePerangkat` (`app/data/models/system.py`) adalah KODE
   MATI sejak sebelum slice ini: tidak dipakai repository/router/layar
   mana pun, dan enumnya sendiri sudah tidak konsisten dengan
   `app.rfid.base.JalurAlat` (cuma `SERIAL`/`HID`, tidak ada
   `PCSC`/`SIMULATOR`). Wizard Ganti Jalur Alat SENGAJA dibangun sebagai
   prosedur terpandu langsung (uji baca live + verifikasi kartu live per
   PC) tanpa registry ini - "seluruh unit terpasang" jadi pernyataan
   admin (checkbox), bukan sesuatu yang diverifikasi otomatis. Keputusan
   menghapus/menulis-ulang `devices` masih menunggu pemilik proyek.
   `id_perangkat` (FK attendance_events -> devices) karena itu TETAP
   tidak pernah terisi.
10. **Status koneksi klien Dashboard adalah proksi, bukan heartbeat**
    (baru, slice Dashboard, PRD §14.1) - `StatusOperasional.klien` dibaca
    dari `SesiKlien.terakhir_dipakai` (waktu request terautentikasi
    terakhir), BUKAN koneksi jaringan real-time - tidak ada mekanisme
    heartbeat terpisah yang dibangun, dan sengaja tidak diklaim seolah-olah
    ada. Telemetri antrean PC lobi (`telemetri_lobi`, `POST /perangkat/telemetri`,
    dilapor `KlienScan.laporkan_telemetri` tiap 15 detik) adalah mekanisme
    BARU yang tidak ada sebelum slice ini - kalau satu proses lobi kelak
    melayani lebih dari satu reader/station sekaligus (bootstrap saat ini
    hanya pernah satu), angka `hitung_pending()` yang SAMA dilaporkan untuk
    tiap station, dicatat di docstring `LayarAbsensiLobi._laporkan_telemetri`.

Utang lain yang tercatat tapi lebih kecil (lihat README di bawah untuk
detail): flakiness
`AuditLogRepository.daftar_terbaru()` kurang tiebreaker `id DESC`;
`app/ui/lobi/main.py` masih hardcode `MockReader` dari env var, belum
membaca `Pengaturan.jalur_alat_aktif` untuk memilih driver sungguhan
(PcscReader/SerialReader/HidReader) secara otomatis.

**Hal pertama yang harus dilakukan saat sesi berikutnya dibuka:** baca
SKILL.md lalu blok ini dan "Utang semantik" secara utuh, lalu tanyakan ke
pengguna slice mana yang akan dikerjakan berikutnya (Slice 14/Pencadangan?
pemicu nyata Notifikasi/utang #6? temuan code-review minor/utang #7?
laporan guru/utang #8? registry perangkat/utang #9? atau slice lain yang
belum tercatat di sini) sebelum menulis kode apa pun.

---

Aplikasi absensi RFID untuk siswa dan guru. Dirancang sebagai `.exe` client-server
di jaringan lokal sekolah: satu server (basis data SQLite mode WAL) di ruang TU,
klien di ruang kepala sekolah dan TU, satu PC di lobi untuk titik scan. Notifikasi
WhatsApp ke orang tua/wali dibangun penuh, koneksinya belum diaktifkan.

Aturan bisnis lengkap dan keputusan yang sudah dikunci ada di
`.claude/skills/absensi-smpn1-pancarijang/SKILL.md` - baca itu dulu sebelum
mengubah apa pun di sini. Berkas ini hanya potret status kode saat ini.

## Menjalankan pengujian

Test suite utama (pytest, butuh dependency proyek):

```bash
pytest
```

Aturan absensi inti juga diuji lewat skrip mandiri tanpa dependency apa pun
selain Python 3.11+ - tidak butuh basis data, jendela, maupun alat RFID:

```bash
python3 -m tests.test_mesin_absensi
```

Ini disengaja: aturan status harus bisa dibuktikan benar sebelum satu reader
pun dibeli, dan tetap bisa dijalankan di PC sekolah yang belum dipasangi apa-apa.

## Menjalankan server

```bash
alembic upgrade head
python scripts/buat_admin_awal.py sqlite:///absensi.db --username admin --password <sandi-awal>
python scripts/seed_jadwal.py sqlite:///absensi.db --id-pengguna 1
python scripts/seed_kalender.py sqlite:///absensi.db --id-pengguna 1
python -m app.server.main sqlite:///absensi.db
```

Server hanya untuk jaringan lokal sekolah - jangan buka port ini ke internet
(SKILL.md "Stack teknis"). `--host 0.0.0.0` (bawaan) supaya PC lain di LAN bisa
mengakses; batasi lewat firewall sekolah, bukan lewat kode ini. Login pertama
lewat `POST /auth/login` akan menandai `wajib_ganti_password=true` - klien
wajib memanggil `POST /auth/ganti-password` sebelum melanjutkan.

## Struktur proyek

```
app/
├── config/         pengaturan.py (parameter waktu/ambang/saklar modul,
│                   DB-backed lewat SettingRepository, dengan fallback nilai
│                   awal yang sudah dikunci SKILL.md untuk dipakai tanpa DB),
│                   keys.py, seed.py
├── core/
│   ├── status.py                  kosakata status, jenis hari, peran - murni
│   ├── mesin_absensi.py           aturan penentuan status - murni, diuji lewat
│   │                              tests/test_mesin_absensi.py
│   ├── kalender.py                kalender akademik dua jalur (siswa/guru) - murni
│   ├── koreksi_massal.py          aturan koreksi massal: penyaring, ambang
│   │                              snapshot/konfirmasi, tindakan per baris - murni
│   ├── koreksi_satuan.py          koreksi status_final SATU baris siswa - murni
│   ├── koreksi_guru.py            koreksi status_final SATU baris guru (DINAS_LUAR/
│   │                              IZIN/SAKIT/CUTI saja - HADIR/TERLAMBAT ditolak
│   │                              sebagai target manual) - murni, modul TERPISAH
│   │                              dari koreksi_satuan.py (kewenangan & target
│   │                              status beda, lihat PERAN_KOREKSI_GURU di status.py)
│   ├── engine.py                  MesinAbsensi - sambungan DB untuk mesin_absensi.py
│   │                              DAN kalender.py (jalur siswa - jenis hari lewat
│   │                              app.core.kalender_service.muat_kalender)
│   ├── kalender_service.py        KalenderService - sambungan DB untuk kalender.py:
│   │                              jenis_hari_siswa/guru, pratinjau dampak, tambah
│   │                              (dengan audit log), batalkan (undo)
│   ├── koreksi_massal_service.py  KoreksiMassalService - sambungan DB untuk
│   │                              koreksi_massal.py
│   ├── notifikasi.py              kewenangan template/antrean/mode uji, nomor
│   │                              tujuan efektif saat mode uji aktif - murni,
│   │                              TIDAK menghitung status atau pemicu kirim
│   ├── laporan.py                 kewenangan lihat/ekspor laporan, tally
│   │                              status per siswa (RekapSiswa), bentuk
│   │                              HasilLaporan/LaporanDetailSiswa - murni,
│   │                              TIDAK menghitung status kehadiran
│   ├── perangkat.py               izin darurat alat (durasi maks, kewenangan
│   │                              beri/perpanjang) + verifikasi wizard ganti
│   │                              jalur (3 kartu terdaftar) - murni
│   └── dashboard.py                bucketing grafik harian/mingguan/bulanan,
│                                  tren, perbandingan kelas, status jalur/
│                                  klien/antrean diam-atau-tidak - murni,
│                                  kewenangan DIPAKAI ULANG dari laporan.py
│                                  (satu baris akses "Dashboard dan laporan")
├── data/
│   ├── models/         model SQLAlchemy (attendance, core, schedule, kalender,
│   │                   koreksi_massal, notification, system, enums)
│   └── repositories/   repository pattern untuk seluruh akses data
├── notify/         antrean WhatsApp: NullProvider/LogProvider/MetaCloudProvider,
│                   processor (cek-ulang ALPA_PAGI/ALPA_PULANG, mode uji),
│                   template
├── rfid/           base.py (ScanEvent, JalurAlat, normalisasi UID, kunci jalur
│                   alat), pengemudi PcscReader/HidReader/SerialReader/MockReader
│                   (keempat pengemudi PRD §12.3 lengkap)
├── scheduler/      pemicu waktu: cutoff ALPA pagi, notifikasi ALPA pagi, cutoff
│                   pulang, notifikasi ALPA pulang (per hari, dari school_schedules)
├── server/         layanan FastAPI - lihat "Menjalankan server" di atas
│   ├── app.py              buat_app() - pasang router, health check
│   ├── main.py             entrypoint uvicorn
│   ├── auth_service.py     login/logout/verifikasi token/ganti password
│   ├── security.py         hash password (argon2)
│   ├── dependencies.py     Session per-request, current user, wajib_peran()
│   ├── schemas.py          skema request/response Pydantic
│   ├── data_master_service.py  SiswaService/GuruService/KelasService/
│   │                       KartuService - orkestrasi repo + audit log untuk
│   │                       data master (PRD §11, §12), TIDAK menyentuh
│   │                       app.core.engine/mesin_absensi sama sekali
│   ├── pengguna_service.py     PenggunaService - buat akun, reset sandi,
│   │                       atur kelas diampu (PRD §10), TIDAK menyentuh
│   │                       app.core.engine/mesin_absensi sama sekali
│   ├── riwayat_scan_service.py RiwayatScanService - baca-saja peristiwa
│   │                       scan dengan lingkup per peran (PRD §7.4, §10),
│   │                       TIDAK menyentuh app.core.engine/mesin_absensi
│   ├── koreksi_satuan_service.py KoreksiSatuanService - koreksi
│   │                       status_final satu siswa + input manual IZIN/
│   │                       SAKIT/DISPENSASI (PRD §9.1, §10), TIDAK
│   │                       menyentuh app.core.engine/mesin_absensi
│   ├── koreksi_guru_service.py  KoreksiGuruService - koreksi status_final
│   │                       satu guru (PRD §6), hanya kepsek/admin (TIDAK
│   │                       ada pengecualian wali kelas), TIDAK menyentuh
│   │                       app.core.engine/mesin_absensi
│   ├── lampiran_storage.py  penyimpanan lokal lampiran foto surat tugas/
│   │                       keterangan guru - path relatif disimpan di DB,
│   │                       bukan isi berkas (PRD §6)
│   ├── notifikasi_service.py  NotifikasiService - template (versi baru +
│   │                       validasi placeholder), antrean baca-saja, mode
│   │                       uji (PRD §13); KONEKSI WHATSAPP NYATA TETAP MATI,
│   │                       TIDAK mengantrekan notifikasi dari absensi
│   ├── laporan_service.py  LaporanService - rekap/detail, ekspor, arsip
│   │                       otomatis, Koreksi Setelah Laporan Terbit (PRD
│   │                       §14.2, §9.3), siswa saja, TIDAK menyentuh
│   │                       app.core.engine/mesin_absensi
│   ├── laporan_export.py   render PDF (fpdf2) + Excel (openpyxl) berkop
│   │                       sekolah dari HasilLaporan - PDF dibangkitkan di
│   │                       server (bukan QtPrintSupport), tiap halaman PDF
│   │                       otomatis dapat kop + cap waktu cetak
│   ├── laporan_storage.py  penyimpanan lokal arsip PDF laporan - path
│   │                       relatif disimpan di DB (pola sama
│   │                       lampiran_storage.py)
│   ├── perangkat_service.py  IzinDaruratService (beri/perpanjang/cabut,
│   │                       audit tiap aksi) + GantiJalurService (wizard,
│   │                       verifikasi 3 kartu -> Pengaturan.atur_jalur_alat_aktif)
│   │                       - TIDAK menyentuh app.core.engine/mesin_absensi
│   ├── dashboard_service.py  DashboardService - ringkasan/grafik/
│   │                       perbandingan kelas/tren/alpa terbanyak/status
│   │                       operasional (PRD §14.1), siswa saja, wali kelas
│   │                       auto-scope ke kelasnya (pola sama laporan_service.py)
│   └── routers/            auth, scan, koreksi_massal, koreksi_satuan,
│                           koreksi_guru, siswa, guru, kelas, kartu, kalender,
│                           pengaturan, pengguna, riwayat_scan, notifikasi,
│                           laporan, perangkat, dashboard
└── ui/
    ├── lobi/           layar absensi lobi (SIMULATOR, online-only)
    │                   [SEBAGIAN ADA]
    ├── data_master/    layar Data Siswa/Guru/Kelas - tabel isian Tab/Enter,
    │                   autosave per baris, pendaftaran kartu (SimulatorReader
    │                   di dev) [SUDAH ADA]
    ├── kalender/       layar Kalender Akademik dua jalur - pratinjau dampak
    │                   wajib sebelum simpan, batalkan (undo), salin dari
    │                   tahun sebelumnya [SUDAH ADA]
    ├── pengaturan/     layar Pengaturan - jadwal per hari (versi berlaku per
    │                   tanggal), ambang, saklar modul, identitas sekolah
    │                   [SUDAH ADA]
    ├── pengguna/       layar Manajemen Akun & Peran - buat akun, reset sandi,
    │                   atur kelas diampu wali kelas [SUDAH ADA]
    ├── riwayat_scan/   layar Riwayat Scan - tabel baca-saja, lingkup per
    │                   peran [SUDAH ADA]
    ├── koreksi_satuan/ layar Koreksi Satuan - alasan wajib (tombol Simpan
    │                   mati sampai terisi), lingkup per peran, undo
    │                   [SUDAH ADA]
    ├── koreksi_massal/ layar Jalankan Tindakan Massal + Riwayat Tindakan
    │                   Massal - penyaring tiga lapis, pratinjau wajib
    │                   (menolak dengan pesan anggun bila perlu snapshot),
    │                   konfirmasi ketik, batalkan [SUDAH ADA]
    ├── koreksi_guru/   layar Koreksi Guru - status manual DINAS_LUAR/IZIN/
    │                   SAKIT/CUTI + lampiran foto surat (opsional, dipilih
    │                   lewat dialog berkas lokal), riwayat + lihat lampiran
    │                   + batalkan [SUDAH ADA]
    ├── notifikasi/     layar Notifikasi - editor empat template (variabel
    │                   nama_siswa/kelas/tanggal/jam/status), antrean
    │                   baca-saja, mode uji [SUDAH ADA]. Koneksi WhatsApp
    │                   nyata tetap mati; antrean akan tampak kosong
    │                   sampai pemicu nyata disambungkan (utang #6)
    ├── laporan/        layar Laporan - filter periode (Harian/Mingguan/
    │                   Bulanan mengisi ulang tanggal)/lingkup (Sekolah/
    │                   Kelas/Siswa), rekap atau detail harian, ekspor
    │                   Excel/PDF, tab Arsip Laporan + Koreksi Setelah
    │                   Laporan Terbit [SUDAH ADA]. Siswa saja (utang #8)
    ├── perangkat/      layar Uji Baca Kartu, Izin Darurat Alat, Wizard
    │                   Ganti Jalur Alat (QWizard 4 langkah, live)
    │                   [SUDAH ADA - sebagian, lihat utang #9]
    └── dashboard/      LayarDashboard (ringkasan ringkas + SATU grafik
                        tren keterlambatan per kelas + daftar alpa
                        terbanyak, mengikuti ui-guidelines.md bagian
                        "Dashboard" secara sengaja - BUKAN lima widget
                        terpisah untuk lima sub-fitur PRD §14.1, walau
                        seluruhnya ADA dan teruji di app/server/routers/
                        dashboard.py) + LayarRingkasKepsek (PRD §14.1
                        "satu layar ringkas") [SUDAH ADA]. Siswa saja.
                        "Status koneksi klien" adalah proksi aktivitas
                        terakhir (SesiKlien.terakhir_dipakai), BUKAN
                        heartbeat real-time - lihat app.core.dashboard

alembic/            migrasi skema
scripts/            buat_admin_awal.py - akun Admin pertama; seed_jadwal.py -
                    isi jadwal & parameter waktu awal ke DB; seed_kalender.py -
                    isi kalender akademik semester ganjil 2026/2027
tests/              pytest lengkap (models, repositories, core, notify, rfid,
                    scheduler, server) + tests/test_mesin_absensi.py (skrip mandiri)
docs/               PRD
```

## Yang belum dikerjakan

- `app/ui/` - layar absensi lobi (`app/ui/lobi/`) sudah ada, digerakkan
  `MockReader`/`SimulatorReader` (`python -m app.ui.lobi.main`), dengan
  antrean lokal store-and-forward di baliknya (`app/ui/lobi/antrean_lokal.py`
  + `forwarder.py`) - SATU seam `KlienScan.submit_scan()`
  (`app/ui/lobi/klien_scan.py`) yang SELALU menyimpan ke SQLite lokal lebih
  dulu (SKILL.md §21), baru mencoba kirim; `ForwarderAntrean` mengurasnya
  di latar begitu server terjangkau lagi, single-flight ketat (satu lock
  bersama antara percobaan langsung saat tap dan pengurasan latar - lihat
  docstring `klien_scan.py`). Autentikasi lewat akun Operator kiosk
  (kredensial + seluruh parameter timeout/antrean/backoff di variabel
  lingkungan, lihat `app/ui/lobi/konfigurasi.py`). Belum ada: layar koreksi
  satuan/massal dan dashboard; jalur `PcscReader`/`SerialReader`/
  `HidReader` sungguhan belum disambungkan ke layar lobi (masih SIMULATOR
  saja); `ScanResponse` diperluas dengan `id_pemilik`/`jenis_pemilik`/`nama`
  supaya layar bisa menampilkan nama (PRD §12.5) tanpa klien membaca
  `app/data` langsung. `ScanRequest`/`attendance_events` kini juga
  membawa `uid_mentah` (UID apa adanya dari alat, terpisah dari `uid_kartu`
  baku untuk lookup) sesuai PRD §17 - server tidak pernah menormalisasi
  ulang; antrean lokal ikut menyimpannya supaya scan offline tak
  kehilangan nilai mentahnya
- **`app/server/` mencakup**: login/token/ganti password, submit scan RFID,
  koreksi massal (pratinjau/jalankan/batalkan/riwayat), data master
  siswa/guru/kelas/kartu RFID (buat/ubah/daftar, arsipkan kelas,
  terbitkan/nonaktifkan/cetak-ulang kartu - kepsek/admin saja, PRD §11-§12),
  kalender akademik dua jalur (daftar, jenis-hari-per-tanggal, pratinjau
  dampak, tambah, batalkan, salin-tahun - lihat bagian "Kalender Akademik"
  di bawah), pengaturan (jadwal per hari, ambang, saklar modul, identitas
  sekolah - lihat bagian "Pengaturan" di bawah), manajemen akun & peran
  (buat akun, reset sandi, atur kelas diampu - lihat bagian "Manajemen Akun
  & Peran" di bawah), riwayat scan baca-saja dengan lingkup per peran (lihat
  bagian "Riwayat Scan" di bawah), dan koreksi satuan status absensi siswa
  dengan undo (lihat bagian "Koreksi Satuan" di bawah). Belum ada endpoint
  untuk dashboard/laporan maupun backup
- **Antrean lokal sisi klien saat offline** (SKILL.md: "PC lobi menyimpan
  scan ke antrean lokalnya lebih dulu, lalu mengirim ke server") belum
  dibangun - ini tanggung jawab kode yang jalan di PC klien (bagian dari
  app/ui/ atau proses klien tersendiri), bukan app/server/. Titik
  penyisipannya sudah disiapkan: `KlienScan.submit_scan()`
  (`app/ui/lobi/klien_scan.py`) adalah SATU seam tempat scan dikirim -
  menggantinya jadi antrean lokal tidak perlu mengubah `app/ui/lobi/layar.py`
- **Layar absensi lobi baru menangani 5 dari 8 label `HasilScan`** yang
  didefinisikan `app/core/status.py` (sesuai PRD §7.4) - `KARTU_TIDAK_DIKENAL`,
  `KARTU_NONAKTIF`, dan `MANUAL_ENTRY` tidak pernah dihasilkan
  `app.core.mesin_absensi.klasifikasi_scan()` saat ini, dan tidak ada di
  `JenisEventScan` (yang benar-benar keluar dari `POST /scan`). Dua utang
  backend tercatat dari verifikasi ini, perlu dikerjakan sebelum §20:
  (i) kartu **nonaktif** (HILANG/DIGANTI/NONAKTIF) melebur jadi
  `UNKNOWN_CARD` - tidak bisa dibedakan dari kartu yang benar-benar tak
  terdaftar; (ii) `HARI_LIBUR` melebur jadi `DI_LUAR_PERIODE` bersama
  `DI_LUAR_JENDELA` - dua label §7.4 yang berbeda jadi satu nilai API.
  Mode input manual darurat (`MANUAL_ENTRY`) juga belum ada jalurnya sama
  sekali di `POST /scan`
- ~~Kolom `jalur` masih hilang di `attendance_events`~~ **DITUTUP** - lihat
  entri "Jalur pada attendance_events" di bawah. `id_perangkat` (FK ke
  `devices`) TETAP belum pernah diisi - `devices`/`ModePerangkat`
  (`app/data/models/system.py`) adalah KODE MATI: tidak dipakai
  repository/router/layar mana pun, dan enum-nya sendiri sudah tidak
  konsisten dengan `app.rfid.base.JalurAlat` (hanya `SERIAL`/`HID`, tidak
  ada `PCSC`/`SIMULATOR`). Slice Perangkat (di bawah) SENGAJA tidak
  menghidupkannya - wizard ganti jalur dibangun sebagai prosedur
  terpandu tanpa registry perangkat terpusat, bukan lewat tabel ini.
  Keputusan menghapus/menulis-ulang `devices` masih menunggu pemilik
  proyek, belum dilakukan siapa pun.
- ~~Jalur reader **PC/SC** (`PcscReader`)~~ **DITUTUP slice Perangkat** -
  `app/rfid/pcsc_reader.py` (pyscard, polling APDU `FF CA 00 00 00` +
  debounce kartu-menempel lokal ke driver, lihat docstringnya). Empat
  pengemudi (PC/SC, serial, keyboard-1-unit, simulator) kini lengkap;
  `app/rfid/factory.py` membangun keempatnya. **BELUM** disambungkan ke
  layar lobi sungguhan (`app/ui/lobi/main.py` masih hardcode `MockReader`
  dari env var, tidak membaca `Pengaturan.jalur_alat_aktif`) - beda gap
  dari "PcscReader belum dibangun", lihat entri `app/ui/` di atas.
- **Menu PERANGKAT (§18) - sebagian, slice Perangkat**: Uji Baca Kartu
  (`app/ui/perangkat/tab_uji_baca_kartu.py` + `GET /kartu/cek/{uid_kartu}`,
  kepsek/admin) dan Izin Darurat Alat (`app/ui/perangkat/tab_izin_darurat.py`
  + `app/server/routers/perangkat.py`, tabel baru `izin_darurat_alat`,
  maks 1x24 jam via `Pengaturan.maks_jam_izin_darurat_alat()`, beri =
  kepsek/admin/wali kelas, perpanjang/cabut = kepsek/admin saja) sudah
  ada, plus Wizard Ganti Jalur Alat (`app/ui/perangkat/wizard_ganti_jalur.py`,
  QWizard 4 langkah: jalur+unit terpasang -> uji baca live -> verifikasi
  3 kartu live -> konfirmasi beralasan, commit lewat
  `POST /perangkat/ganti-jalur`). Banner MODE DARURAT lobi
  (`LayarAbsensiLobi.set_mode_darurat`) kini disambungkan nyata lewat
  polling `KlienScan.cek_mode_darurat()` tiap 15 detik. **Cakupan yang
  SENGAJA ditinggalkan**: layar daftar/kelola perangkat fisik per unit
  (tidak ada registry, lihat catatan `devices`/`ModePerangkat` di atas).
  ~~Pemantauan Kesehatan Alat (SKILL.md, status tiap jalur + waktu baca
  terakhir) belum dibangun sama sekali~~ **DITUTUP slice Dashboard** -
  lihat entri Dashboard di bawah, `StatusOperasional.jalur`.
- **Dashboard (§14.1) - `app/ui/dashboard/`**: LayarDashboard (ringkasan
  hari berjalan ringkas + SATU grafik tren keterlambatan per kelas +
  daftar siswa alpa terbanyak, sengaja mengikuti ui-guidelines.md bagian
  "Dashboard" - bukan lima widget terpisah untuk lima sub-fitur PRD,
  meski seluruhnya ADA dan teruji di server: `GET /dashboard/ringkasan`,
  `/grafik` (granularitas HARIAN/MINGGUAN/BULANAN - `app.core.dashboard.
  grafik_dari_seri`, satu query dibagi tiga granularitas, bukan tiga
  query terpisah), `/perbandingan-kelas`, `/tren-keterlambatan`,
  `/alpa-terbanyak`, `/status-operasional`) + LayarRingkasKepsek (PRD
  §14.1 "satu layar ringkas"). Siswa saja (pola sama Laporan/Koreksi
  Massal - "per kelas"/ALPA tidak berlaku guru). Kewenangan DIPAKAI
  ULANG dari `app.core.laporan.pastikan_berwenang`/`PERAN_LAPORAN` -
  SKILL.md punya satu baris tabel akses "Dashboard dan laporan" yang
  menaungi keduanya, bukan kebetulan sama nilai.
  - **"Status koneksi klien" adalah proksi aktivitas terakhir**
    (`SesiKlien.terakhir_dipakai`, diperbarui tiap request terautentikasi),
    BUKAN heartbeat jaringan real-time - tidak ada mekanisme heartbeat
    terpisah yang dibangun, ini batas jujur dari data yang tersedia.
  - **Telemetri antrean PC lobi (`telemetri_lobi`, `POST /perangkat/telemetri`)
    adalah mekanisme BARU** yang tidak ada sebelum slice ini - `KlienScan.
    hitung_pending()` sebelumnya murni lokal (SQLite di PC lobi), server
    sama sekali tidak punya visibilitasnya. `LayarAbsensiLobi._laporkan_telemetri`
    melapor tiap 15 detik; kalau satu proses lobi kelak melayani LEBIH
    DARI SATU reader/station sekaligus (bootstrap saat ini hanya pernah
    satu), angka `hitung_pending()` yang SAMA dilaporkan untuk tiap
    station - simplifikasi yang belum pernah salah pada praktiknya,
    dicatat di docstring `_laporkan_telemetri`.
- **Snapshot basis data otomatis** sebelum tindakan massal >5.000 baris -
  modul koreksi massal (backend DAN layar, lihat slice Koreksi Massal di
  bawah) menolak tegas lewat `SnapshotDiperlukan` (HTTP 412) alih-alih
  benar-benar mengambil snapshot - modul backup (§15, Slice Sistem) belum
  dibangun
- ~~Empat template notifikasi terpisah (Terlambat/ALPA pagi/ALPA pulang/Rekap)~~
  **DITUTUP slice Notifikasi** (lihat entri "Notifikasi selesai sisi
  struktur" di bawah) - `JenisNotifikasi` sekarang TERLAMBAT/
  ALPA_PAGI/ALPA_PULANG/REKAP, isinya (termasuk kalimat berbeda untuk ALPA
  pulang) diedit bebas admin lewat layar Template, bukan lagi digabung satu
  jenis ALPA generik
- **Data master siswa/guru/kelas + siklus kartu RFID selesai sisi
  endpoint dan layar** (`app/server/routers/{siswa,guru,kelas,kartu}.py`,
  `app/ui/data_master/`, `python -m app.ui.data_master.main`). Catatan dari
  slice ini:
  - Skema `students`/`teachers`/`classes` diselaraskan ke PRD §11 lewat
    migrasi `ad26fb5488c8`: kolom `nis` (peninggalan draf `data-model.md`
    pra-PRD v2.0, tak dipakai modul lain manapun) dihapus, `nisn` jadi
    kunci utama wajib+unik sesuai PRD (bukan tebakan - SKILL.md "Struktur
    data inti" sudah menulis `nisn*` sejak awal, kodenya saja yang belum
    menyusul). `Teacher.status_kepegawaian` (topik lain, TODO PRD Bagian
    34 draf lama) SENGAJA tidak disentuh - `pangkat_jabatan` kolom baru
    terpisah
  - `Kelas.wali_kelas_id` menunjuk `teachers.id` (fakta data master untuk
    laporan/cetak) - TERPISAH dari `User.id_kelas_diampu` (akses login
    wali kelas, sudah ada sebelum slice ini). Mengisi wali kelas di Data
    Master TIDAK membuat/menghubungkan akun login; itu tetap langkah
    terpisah di menu Pengguna (belum ada)
  - `StatusKartu` TIDAK ditambah nilai `RUSAK` terpisah dari
    `HILANG`/`NONAKTIF` walau PRD §12.6 menulis "aktif, hilang, rusak,
    cetak ulang" - aksi "Nonaktifkan" di layar memakai `NONAKTIF` generik,
    alasan (mis. "rusak"/"hilang") tercatat di teks alasan/log audit,
    bukan di enum. Perlu diputuskan kalau laporan nanti butuh membedakan
    alasan nonaktivasi secara terstruktur
  - Data Master mengelola SATU `Guardian` utama per siswa (field "Nama
    orang tua"/"No HP-WA" di tabel PRD §11.1) - tabel `guardians` sendiri
    sudah mendukung banyak wali per siswa (utk kakak-beradik satu kontak,
    §13.3), tapi layar belum punya UI menambah wali kedua
  - `GET /siswa` dan `GET /kelas` melakukan N+1 lookup (nama kelas per
    siswa, nama wali per kelas) - wajar di skala ~1.000 siswa, catat kalau
    nanti terasa lambat
  - Layar Data Master online-only (tidak ada antrean lokal) - mengedit
    data master mensyaratkan jaringan lokal, beda dari absensi masuk/pulang
    yang harus tetap jalan offline (§21)
- **Kalender Akademik selesai sisi endpoint dan layar** (`app/server/
  routers/kalender.py`, `app/ui/kalender/`, `python -m app.ui.kalender.main`).
  Tidak menyentuh `app/core/kalender.py` (mesin klasifikasi kalender) maupun
  `app/core/engine.py` - keduanya sudah lengkap dan sudah tersambung sebelum
  slice ini (`MesinAbsensi._jenis_hari_siswa()` sudah memanggil
  `muat_kalender()`, diverifikasi di LANGKAH 0). Catatan dari slice ini:
  - `KalenderService.salin_dari_tahun_sebelumnya()` dan `.daftar()` (untuk
    layar, termasuk entri yang sudah dibatalkan) adalah METHOD BARU yang
    ditambahkan - murni orkestrasi repository (salin data internal, bukan
    impor berkas, sesuai SKILL.md), tidak menghitung ulang dampak apa pun.
    Satu entri log audit induk untuk seluruh operasi salin, bukan satu per
    baris disalin
  - Pratinjau dampak WAJIB dijalankan sebelum tombol Simpan aktif di layar
    (§8.3) - ditegakkan di UI (`DialogTambahEntri`: mengubah tanggal/jenis
    setelah pratinjau membatalkan pratinjau lama). Server TIDAK mengikat
    `POST /kalender/pratinjau` dengan `POST /kalender` lewat token/state apa
    pun - klien yang taat aturan tetap bisa langsung memanggil tambah tanpa
    pratinjau, sama seperti koreksi massal yang sudah ada (`pratinjau`/
    `jalankan` juga tidak terikat secara teknis). Konsisten dengan pola yang
    sudah ada, bukan celah baru
  - `jumlah_orang` untuk pratinjau dampak dihitung sebagai (siswa aktif +
    guru aktif) digabung di router (`_jumlah_orang_terdampak`) - mesin murni
    `Kalender.dampak_perubahan()` sendiri tidak membedakan populasi per
    jalur siswa/guru (satu angka untuk seluruh entri), ini simplifikasi yang
    sudah ada di `app/core/kalender.py`, bukan sesuatu yang ditambahkan slice
    ini
  - Semester genap 2026/2027 dan status 23-25 Desember 2026 (§23) TIDAK
    diberi nilai bawaan/tebakan di UI manapun - kolom tahun ajaran dan
    tanggal tetap murni input bebas, menunggu keputusan sekolah
  - Layar tidak menyediakan opsi "jadwal khusus ujian/MPLS/Porseni" - sesuai
    instruksi, penyesuaian hari itu tetap lewat koreksi massal, bukan kalender
  - Tabel di layar hanya menampilkan status Aktif/Dibatalkan, belum
    menampilkan siapa yang membuat/membatalkan (`id_pengguna`/
    `dibatalkan_oleh` ada di model, belum diekspos di `EntriKalenderResponse`)
- **Pengaturan selesai sisi endpoint dan layar** (`app/server/
  routers/pengaturan.py`, `app/ui/pengaturan/`, `python -m app.ui.pengaturan.main`).
  Tidak mengubah cara `app/core/engine.py`/`app/scheduler/scheduler.py`
  membaca konfigurasi - murni UI di atas mekanisme yang sudah ada. Catatan
  LANGKAH 0 dan keputusan dari slice ini:
  - **Ada DUA mekanisme jadwal terpisah, dikonfirmasi bukan digabung**:
    `ScheduleRepository`/`school_schedules` (per hari, versi berlaku per
    tanggal - `berlaku_mulai`) adalah SATU-SATUNYA sumber klasifikasi
    kehadiran nyata (dibaca `MesinAbsensi` lewat `jadwal_dari_baris()`).
    `Pengaturan.cutoff_alpa()`/`.waktu_kirim_notif_alpa()` (tabel
    `settings`, TIDAK bersejarah/tidak per-tanggal) hanya menentukan jam
    berapa CRON JOB scheduler menyala (`SchedulerAbsensi._baca_jam_dari_config`)
    - bukan ambang klasifikasinya sendiri. Layar Pengaturan mengekspos
    keduanya secara terpisah dan jujur (tab "Jadwal" untuk yang pertama,
    field waktu di tab "Ambang & Saklar" untuk yang kedua) alih-alih
    menyamarkan seolah satu hal
  - `Pengaturan.jam_masuk()`/`.batas_terlambat()`/`.jam_pulang()` (Setting,
    bukan per-hari) SENGAJA TIDAK diekspos di layar manapun - method ini
    tidak dibaca kode produksi mana pun (dikonfirmasi lewat pencarian
    kode), hanya nilai bawaan usang untuk mengisi form yang sudah lebih
    baik digantikan versi terbaru di tabel `school_schedules` itu sendiri.
    Mengeksposnya akan menciptakan dua "jam masuk" yang bisa saling
    berbeda dan membingungkan admin
  - Menambah jadwal SELALU insert baris baru, tidak pernah mengubah
    `berlaku_sampai` baris lama - `ScheduleRepository.jadwal_berlaku()`
    sudah memilih baris ber-`berlaku_mulai` terbaru yang berlaku pada
    suatu tanggal (order by `berlaku_mulai desc limit 1`), jadi historis
    otomatis tidak bergeser tanpa perlu logika tambahan
  - **Wiring baru (bukan perubahan mekanisme)**: `app.state.scheduler`
    diisi `app/server/main.py` setelah `buat_app()` dipanggil, dipakai
    router Pengaturan memanggil `SchedulerAbsensi.reload_jadwal()` (method
    yang SUDAH ADA sebelumnya, didokumentasikan untuk kebutuhan persis
    ini) setelah jadwal siswa atau `cutoff_alpa`/`waktu_kirim_notif_alpa`
    berubah - supaya scheduler yang sedang berjalan tidak perlu di-restart
    manual. Tidak fatal bila `app.state.scheduler` tidak ada (mis. test)
    atau jam belum lengkap (`KonfigurasiBelumLengkap` ditangkap diam-diam)
  - Identitas sekolah (§14.2) disimpan lewat kunci `settings` baru
    (`NAMA_SEKOLAH`, `NPSN_SEKOLAH`, dst.) - **BELUM "terpakai di kop"
    secara nyata** karena modul laporan/cetak sendiri belum dibangun
    (lihat gap dashboard/laporan di atas). Data tersimpan dan bisa dibaca
    balik lewat endpoint, siap dipakai begitu modul laporan ada
  - Logo sekolah disimpan sebagai data URI base64 di kolom `settings.nilai`
    (Text, tidak dibatasi ukuran secara eksplisit) - dipilih lewat berkas
    PNG/JPG di layar, tidak divalidasi ukuran/dimensi
  - `jalur_alat_aktif` (kunci jalur reader) SENGAJA TIDAK dimasukkan ke
    layar ini - PRD §18 menaruhnya di menu PERANGKAT, bukan PENGATURAN
  - `provider_whatsapp`, `batas_percobaan_notifikasi`, dan
    `jeda_kirim_alpa_pulang_menit` SENGAJA TIDAK dimasukkan - ketiganya
    detail modul NOTIFIKASI (PRD §18 menu terpisah), bukan bagian
    "ambang"/"saklar" yang diminta slice ini. Saklar `modul_whatsapp_aktif`
    tetap diekspos (nyala/mati generik). MASIH BEGITU setelah slice
    Notifikasi (lihat entri "Notifikasi selesai sisi struktur" di bawah) -
    slice itu menambah layar template/antrean/mode uji tapi SENGAJA tidak
    menyentuh ketiga pengaturan ini juga (di luar cakupan yang diminta),
    tetap tercatat sebagai gap terbuka
- **Manajemen Akun & Peran selesai sisi endpoint dan layar**
  (`app/server/pengguna_service.py`, `app/server/routers/pengguna.py`,
  `app/ui/pengguna/`, `python -m app.ui.pengguna.main`). Catatan LANGKAH 0
  dan keputusan dari slice ini:
  - **Hierarki wewenang bukan pembagian eksklusif** - dikoreksi dari
    paraphrase instruksi ke PRD (tabel "Peran dan Hak Akses") yang
    ditandai eksplisit ke pengguna: kepala sekolah berwenang membuat/
    mereset akun KEEMPAT peran (termasuk admin dan kepala sekolah lain -
    PRD: "Membuat akun admin dan kepala sekolah: Kepsek Ya, Admin —"),
    admin HANYA wali kelas & operator ("Buat akun wali kelas dan
    operator: Kepsek Ya, Admin Ya"). Lihat `HIRARKI_WEWENANG_AKUN` di
    `pengguna_service.py`, dipakai sama untuk buat akun maupun reset sandi
  - **Pembatasan kelas wali kelas ditegakkan lewat primitif baru**
    `app.core.akses_kelas.pastikan_akses_kelas()` (murni, tanpa DB) -
    LANGKAH 0 menemukan `PERAN_KOREKSI_SATUAN` (app/core/status.py) sudah
    ada tapi TIDAK PERNAH dipanggil kode mana pun, karena endpoint koreksi
    satuan sendiri belum dibangun. Primitif ini disiapkan sebagai
    SATU-SATUNYA jalur pemeriksaan (sama seperti
    `koreksi_massal.pastikan_berwenang()`) untuk dipakai endpoint koreksi
    satuan kelak - diuji langsung di lapisan data
    (`tests/core/test_akses_kelas.py`), TIDAK ada endpoint yang
    memanggilnya hari ini karena belum ada endpoint yang butuh (bukan
    kelalaian, dicatat sebagai utang di bawah)
  - Reset sandi (`POST /pengguna/{id}/reset-password`) BEDA dari
    `POST /auth/ganti-password` yang sudah ada (self-service, wajib sandi
    lama) - endpoint baru ini tidak perlu tahu sandi lama, memaksa
    `wajib_ganti_password=True`, dan mencabut seluruh sesi target (sama
    seperti perilaku ganti-password sendiri)
  - `User.id_kelas_diampu` bisa diisi saat pembuatan akun ATAU diubah
    belakangan lewat `PUT /pengguna/{id}/kelas-diampu` (menyambung ke
    `KelasRepository` dari slice Data Master) - hanya berlaku untuk akun
    berperan WALI_KELAS, ditolak 422 untuk peran lain
  - TIDAK ada endpoint nonaktifkan/hapus akun - hanya "buat akun" dan
    "reset sandi" yang diminta slice ini; PRD menyebut penghapusan data
    massal/reset sebagai wewenang kepala sekolah saja tapi itu bukan
    "hapus akun pengguna" - dicatat sebagai kemungkinan gap terpisah,
    bukan diasumsikan termasuk di sini
- **Riwayat Scan selesai sisi endpoint dan layar** (`GET /riwayat-scan`,
  `app/server/riwayat_scan_service.py`, `app/ui/riwayat_scan/`,
  `python -m app.ui.riwayat_scan.main`). Catatan LANGKAH 0 dan keputusan
  dari slice ini:
  - Kolom `jalur` dikonfirmasi TIDAK ADA di `attendance_events` (PRD §17,
    utang lama - lihat catatan slice Data Master) - respons
    `RiwayatScanResponse.jalur` selalu `None` sekarang, field tetap
    disediakan di skema supaya UI tidak perlu berubah begitu kolomnya ada
  - `JenisEventScan` dikonfirmasi hanya 5 nilai nyata (bukan 8 label PRD
    §7.4) - gap yang sama persis dengan yang sudah tercatat di catatan
    slice Data Master (`app/ui/lobi/tampilan.py`). `LABEL_JENIS_EVENT` di
    `riwayat_scan_service.py` memetakan 5 nilai itu ke istilah §7.4 apa
    adanya (mis. "Kartu Tidak Dikenal / Nonaktif" menggabungkan dua label
    PRD yang sudah melebur jadi satu di `UNKNOWN_CARD`) - TIDAK berpura-
    pura granular, tidak menyentuh `app.core.mesin_absensi.klasifikasi_scan()`
  - **`app.core.akses_kelas.pastikan_akses_kelas()` sekarang punya
    PEMANGGIL PERTAMA** (di `RiwayatScanService.cari()`) - mengurangi
    "UTANG KOREKSI SATUAN" yang dicatat di slice Manajemen Akun & Peran,
    tapi tidak menghapusnya: primitif itu masih menunggu endpoint koreksi
    satuan (tulis) yang sebenarnya, belum dibangun
  - Filter "kelas" dan "orang" di layar berupa isian ID mentah (`QLineEdit`
    angka), BUKAN dropdown nama - `GET /kelas`, `GET /siswa`, `GET /guru`
    (Data Master) dibatasi kepsek/admin saja, sedangkan Riwayat Scan
    terbuka untuk keempat peran (operator/wali kelas tidak bisa memanggil
    endpoint itu untuk mengisi dropdown). Nama orang tetap tampil di kolom
    hasil (dikirim server, bukan dicari klien) - keterbatasannya hanya
    pada UI PENYARING, bukan pada tampilan hasilnya
  - Wali kelas yang memaksakan filter kelas selain kelasnya sendiri
    mendapat 403 (`AksesKelasDitolak`) - BUKAN diam-diam diabaikan/dipaksa
    ke kelasnya sendiri, supaya kesalahan penyaringan terlihat jelas,
    bukan tersembunyi sebagai hasil yang tampak benar
- **Koreksi Satuan selesai sisi endpoint dan layar** (`app/core/koreksi_satuan.py`,
  `app/server/koreksi_satuan_service.py`, `app/server/routers/koreksi_satuan.py`,
  `app/ui/koreksi_satuan/`, `python -m app.ui.koreksi_satuan.main`). **Siswa
  saja** - koreksi satuan guru di luar cakupan (PRD §10 "koreksi satuan
  kelas sendiri" hanya bermakna untuk siswa). Catatan LANGKAH 0 dan
  keputusan dari slice ini:
  - **"UTANG KOREKSI SATUAN" DITUTUP** - `app.core.akses_kelas.pastikan_akses_kelas()`
    (dibuat di slice Manajemen Akun & Peran, dipakai baca-saja di
    `RiwayatScanService` sejak slice Riwayat Scan) sekarang punya pemanggil
    di jalur TULIS sungguhan: `KoreksiSatuanService` memanggilnya untuk
    setiap koreksi/undo yang dilakukan wali kelas, sesuai instruksi "jangan
    buat penjaga baru". Tidak ada logika pembatasan kelas baru ditulis di
    slice ini
  - `AttendanceRepository.koreksi()` SUDAH ADA sebelum slice ini (dibangun
    entah kapan sebelumnya, dipakai koreksi massal) - LANGKAH 0 menemukan
    ini alih-alih membangun ulang. Slice ini hanya menambah lapisan di
    atasnya: `app/core/koreksi_satuan.py` (fungsi murni, mirror
    `app.core.koreksi_massal.terapkan_tindakan` PERSIS untuk kolom yang
    disentuh - `status_final` + `alasan_final`, BUKAN `status_masuk`) dan
    tabel baru `koreksi_satuan` (migrasi `78beaa10fbb1`) untuk mendukung
    UNDO per-koreksi, setara `tindakan_massal` tapi untuk satu baris -
    `audit_logs` sendiri TIDAK punya kolom `id_absensi` sehingga tidak bisa
    dipakai melacak "koreksi mana untuk baris apa" secara andal
  - `AlasanFinal.KOREKSI_MANUAL` sudah disediakan sejak awal (app/core/status.py)
    persis untuk kasus ini, belum pernah dipakai sebelum slice ini
  - Input manual IZIN/SAKIT/DISPENSASI pada siswa yang BELUM PERNAH scan
    (tidak ada baris `attendance` sama sekali) membuat baris baru dengan
    `status_masuk=None` (bukan dikarang) lalu segera dikoreksi ke
    `status_final` yang diminta - `AttendanceRepository.tambah()` diubah
    tipe `status_masuk` dari `str` jadi `str | None` (sudah begitu di
    model, hanya type hint yang menyusul) supaya jalur ini tidak perlu
    berbohong soal tipe
  - Layar memakai isian ID Siswa mentah (bukan dropdown nama) - alasan
    sama persis dengan Riwayat Scan: `GET /siswa` dibatasi kepsek/admin,
    sedangkan Koreksi Satuan juga dipakai wali kelas. Endpoint
    `GET /koreksi-satuan/status` dipanggil dulu untuk menampilkan
    nama+kelas+status saat ini sebagai konfirmasi sebelum "Simpan" bisa
    diklik, sekaligus memvalidasi lingkup kelas lebih awal
  - Undo memanggil ULANG `AttendanceRepository.koreksi()` (bukan mutasi
    diam-diam) - setiap pembatalan tetap tercatat sebagai baris
    `audit_logs` "koreksi_absensi" tersendiri, DITAMBAH satu entri
    "batalkan_koreksi_satuan" - jejaknya utuh dua arah, bukan cuma
    menghapus/membalik nilai tanpa bekas
- **Koreksi Massal selesai sisi layar** (`app/ui/koreksi_massal/`,
  `python -m app.ui.koreksi_massal.main`) di atas backend yang SUDAH ADA
  sebelum slice ini (`app/core/koreksi_massal.py` + `koreksi_massal_service.py`,
  `app/server/routers/koreksi_massal.py`) - slice ini TIDAK mengubah aturan
  penerapan tindakan, hanya menambah layar + dua field passthrough di
  response. Catatan LANGKAH 0 dan keputusan dari slice ini:
  - **Snapshot pra-tindakan (undo) DIKONFIRMASI berbeda dari modul
    pencadangan §15**: mekanisme undo (`TindakanMassalBaris.nilai_sebelum`,
    ditulis setiap `jalankan()` tanpa syarat) sudah lengkap sejak backend
    dibangun dan tidak disentuh slice ini. `SnapshotDiperlukan` (dilempar
    `KoreksiMassalService.jalankan()` saat baris terdampak > ambang) adalah
    hal yang SAMA SEKALI BEDA: penolakan tegas karena modul pencadangan
    basis data otomatis (§15, Slice Sistem) belum ada - bukan kegagalan
    mekanisme undo
  - Layar menangani `SnapshotDiperlukan` (HTTP 412) dengan ANGGUN dan
    PROAKTIF: `POST /koreksi-massal/pratinjau` sudah mengembalikan
    `perlu_snapshot`, jadi `DialogJalankanTindakan` menampilkan pesan
    "Pencadangan otomatis diperlukan ... menyusul di slice Sistem" TEPAT
    SAAT pratinjau dihitung - sebelum kolom alasan bahkan bisa diisi -
    alih-alih menunggu tombol Jalankan ditekan dan menampilkan galat
    mentah. Galat 412 dari endpoint `/jalankan` sendiri tetap ditangani
    sebagai jaring pengaman kalau data berubah di antara pratinjau dan
    eksekusi (mis. tindakan lain berjalan bersamaan) - lihat
    `_tangani_penolakan_jalankan()` di `dialog_tindakan.py`
  - **Dua field ditambahkan ke `TindakanMassalResponse`**
    (`app/server/schemas.py`) untuk memenuhi "pelaku, cakupan" di layar
    Riwayat: `id_pengguna` (mengikuti persis pola `KoreksiSatuanResponse.id_pengguna`
    - ID mentah, BUKAN username yang di-resolve) dan `penyaring` (JSON
    filter yang sudah tersimpan apa adanya di kolom `tindakan_massal.penyaring`,
    diformat jadi teks oleh `app.ui.koreksi_massal.format_tampilan.format_cakupan()`
    murni di sisi klien). `waktu_dibatalkan`/`dibatalkan_oleh` ikut
    ditambahkan sekalian karena kolomnya sudah ada di model, konsisten
    dengan `dibatalkan`. Ini PASSTHROUGH field yang sudah ada di
    `TindakanMassal`, BUKAN mengubah `app/core/koreksi_massal.py`/
    `koreksi_massal_service.py` (aturan penerapan tindakan tidak disentuh)
  - **"Pelaku" ditampilkan sebagai ID Pengguna mentah, bukan username** -
    keputusan konsistensi dengan `KoreksiSatuanResponse` (juga hanya
    `id_pengguna`) dan Riwayat Scan (kolom kelas/orang di penyaring juga ID
    mentah), bukan keterbatasan teknis: `GET /pengguna` sebenarnya bisa
    dipanggil klien ini (gerbang perannya sama, kepsek/admin) untuk
    resolve username, sengaja tidak dilakukan supaya slice ini tidak
    menambah dependensi ke modul Pengguna hanya demi kosmetik tampilan.
    Bisa ditingkatkan nanti kalau operator sekolah memang butuh nama,
    bukan ID
  - `app/ui/koreksi_massal/validasi_form.py` dan `format_tampilan.py`
    murni tanpa Qt (pola sama `app.ui.kalender.validasi_form`), diuji
    langsung tanpa Qt maupun server di `tests/ui/koreksi_massal/`
  - **Ditemukan, TIDAK dikerjakan (dicatat sebagai utang di bawah)**:
    `Pengaturan.ambang_snapshot_sebelum_massal`/`.ambang_konfirmasi_ketik`
    (`app/config/pengaturan.py`) hardcode `5000`, TIDAK punya
    `KunciPengaturan` maupun method `atur_...()` - beda dari
    `ambang_tahan_konversi_pulang` yang sudah tersambung penuh ke
    `settings`. Layar Pengaturan (`app/ui/pengaturan/`) juga tidak
    mengekspos keduanya. Tidak disentuh di slice ini karena itu perubahan
    di modul Pengaturan, bukan Koreksi Massal
  - **Ditemukan, TIDAK dikerjakan**: `tests/server/test_koreksi_satuan.py::test_koreksi_tercatat_di_audit_log`
    sempat gagal SEKALI saat `pytest` penuh dijalankan (lulus lagi begitu
    diulang, dan lulus konsisten sendirian) - `AuditLogRepository.daftar_terbaru()`
    (`app/data/repositories/audit_log.py`) mengurutkan `ORDER BY waktu DESC`
    tanpa penambat kedua (mis. `id DESC`) untuk memutus seri saat dua baris
    kebetulan punya `waktu` yang sama persis. Pre-existing, tidak
    disebabkan slice ini (berkas yang gagal maupun `audit_log.py` tidak
    disentuh) - dicatat sebagai utang, BUKAN diperbaiki di sini (rule
    "satu slice satu tesis")
- **Koreksi Guru selesai sisi endpoint dan layar** (`app/core/koreksi_guru.py`,
  `app/server/koreksi_guru_service.py`, `app/server/routers/koreksi_guru.py`,
  `app/server/lampiran_storage.py`, `app/ui/koreksi_guru/`,
  `python -m app.ui.koreksi_guru.main`). Catatan LANGKAH 0 dan keputusan
  dari slice ini:
  - **StatusGuru dan toleransi 09:30 DIKONFIRMASI sesuai PRD §6, TIDAK
    disentuh** - `tentukan_status_masuk_guru()` (app/core/mesin_absensi.py)
    dan `_proses_scan_guru()` (app/core/engine.py) sudah lengkap sebelum
    slice ini. Slice ini murni menambah koreksi MANUAL di atasnya, sama
    sekali tidak menghitung status dari scan
  - **Modul TERPISAH dari `app.core.koreksi_satuan` (siswa)**, bukan
    generalisasi satu fungsi untuk dua kosakata status - pola yang sama
    dengan `koreksi_satuan.py` sendiri dibuat terpisah dari
    `koreksi_massal.py`. `app.core.koreksi_satuan` sebelumnya eksplisit
    menulis "koreksi satuan guru di luar cakupan" di docstring-nya -
    catatan itu masih akurat (modul itu tetap siswa-saja), koreksi guru
    sekarang hidup di modul barunya sendiri
  - **Peran DIBATASI kepsek/admin saja** (`PERAN_KOREKSI_GURU`,
    app/core/status.py) - TIDAK ada pengecualian wali kelas seperti
    `PERAN_KOREKSI_SATUAN` (siswa), karena wali kelas mengampu kelas
    siswa, bukan guru (PRD §6 tidak menyebut wali kelas sama sekali)
  - **`status_baru` DIBATASI ke 4 nilai manual** (DINAS_LUAR/IZIN/SAKIT/
    CUTI) lewat `pastikan_status_manual()` - mengoreksi ke HADIR/
    TERLAMBAT ditolak 422 (`StatusBukanManual`). Beda dari koreksi satuan
    siswa yang bebas ke status apa pun - PRD §6 memberi admin kewenangan
    menginput EMPAT status itu, bukan kewenangan bebas mengoreksi guru ke
    status apa pun
  - **Lampiran (PRD §6 "sebaiknya disertai lampiran") disimpan sebagai
    BERKAS LOKAL + PATH**, bukan base64 di kolom teks - beda sengaja dari
    `logo` sekolah (`IdentitasSekolah.logo`, base64 di `settings.nilai`)
    karena lampiran bertambah terus seiring waktu, logo satu-satunya dan
    kecil. Direktori dasar dikonfigurasi lewat `--lampiran-dir` di
    `app/server/main.py` (mirip `database_url`) - BUKAN env var (tidak
    ada preseden env var sisi server sebelum ini, hanya sisi klien) dan
    BUKAN `Pengaturan`/`settings` (itu untuk aturan bisnis, bukan path
    filesystem). `app.state.lampiran_dir` disuntikkan lewat parameter
    `buat_app()` yang OPSIONAL supaya seluruh pemanggil `buat_app(session_factory)`
    yang sudah ada (test lama) tidak perlu berubah
  - Nama berkas lampiran DI-GENERATE (UUID4 + ekstensi) - nama asli dari
    pengguna TIDAK PERNAH dipakai sebagai nama berkas, mencegah tabrakan
    nama DAN celah path traversal (dikonfirmasi lewat
    `tests/server/test_lampiran_storage.py`). Ekstensi dibatasi ke
    jpg/jpeg/png/pdf, ukuran dibatasi 10 MB - keduanya batas teknis
    infrastruktur (melindungi disk dari unggahan tak sengaja), bukan
    ambang bisnis yang perlu lewat `Pengaturan` seperti SKILL.md §4
  - **`POST /koreksi-guru` memakai multipart/form-data**, bukan JSON -
    satu-satunya endpoint di app/server/ yang begitu, karena lampiran
    opsional perlu ikut dalam satu permintaan yang sama dengan status +
    alasan (bukan alur unggah-dulu-rujuk-belakangan). Butuh dependency
    baru `python-multipart` (ditambahkan ke `pyproject.toml`) - FastAPI
    tidak bisa mem-parsing form/file tanpanya
  - **Dua jalur pengaman ganda dipertahankan** (pola yang sama dengan
    koreksi massal): `wajib_peran()` di router MENOLAK peran lain
    sebelum service pernah dipanggil, DAN `pastikan_berwenang()` di
    dalam `KoreksiGuruService` tetap ada sebagai lapis kedua - keduanya
    kebetulan memakai himpunan peran yang identik di slice ini, jadi
    baris kedua secara praktis tidak pernah tersentuh lewat HTTP, tapi
    dipertahankan supaya pemanggil non-HTTP (skrip, test) tetap aman
  - **Smoke test manual dijalankan dua kali** terhadap server FastAPI
    sungguhan (bukan TestClient) + `absensi.db` sungguhan: (1) lewat
    layar PySide6 nyata (diverifikasi via tangkapan layar + Windows UI
    Automation) untuk alur Cek Status -> Simpan tanpa lampiran -> `201
    Created` -> `attendance.status_final` berubah, `status_masuk` tetap
    `None` untuk guru yang belum pernah scan; (2) lewat `KlienKoreksiGuru`
    produksi langsung (tanpa Qt, UI Automation pada Qt terbukti tidak
    stabil di lingkungan ini) untuk alur lampiran: unggah berkas
    sungguhan -> path relatif tersimpan -> unduh -> byte identik dengan
    berkas asli -> batalkan -> `status_final` kembali ke nilai sebelum,
    audit log `batalkan_koreksi_guru` tercatat
  - **Ditemukan, TIDAK dikerjakan** (dicatat sebagai utang di bawah):
    enkripsi lampiran at-rest - berkas tersimpan polos di filesystem
    server, sejajar dengan modul pencadangan (§15) yang juga belum
    dienkripsi karena belum dibangun sama sekali. Di luar cakupan slice
    ini sesuai instruksi eksplisit
  - Di luar cakupan (sesuai instruksi eksplisit, bukan lupa): sumber foto
    peserta didik (§23) - topik berbeda dari lampiran surat guru di sini
- **Notifikasi selesai sisi struktur (template/antrean/mode uji)**
  (dikerjakan di branch `feat/notifikasi-template-antrean`, sudah
  fast-forward ke master) - `app/core/notifikasi.py`,
  `app/server/notifikasi_service.py`, `app/server/routers/notifikasi.py`,
  `app/ui/notifikasi/`, `python -m app.ui.notifikasi.main`. **Koneksi WhatsApp
  nyata tetap mati (Fase 2)** - slice ini murni struktur, sesuai instruksi
  eksplisit pengguna. Catatan LANGKAH 0 dan keputusan dari slice ini:
  - **`JenisNotifikasi` dipecah dari MASUK/PULANG/ALPA generik jadi EMPAT**
    (PRD §13.1): `TERLAMBAT`, `ALPA_PAGI`, `ALPA_PULANG`, `REKAP` - menutup
    utang yang sudah tercatat sejak awal. Migrasi `1cdfb1a07236` mengubah
    kolom `jenis` di `message_templates` dan `notification_queue` lewat
    `batch_alter_table` (pola sama migrasi `41cd37afef4c`, peran
    KEPALA_SEKOLAH) - aman dijalankan karena kedua tabel masih kosong di
    `absensi.db` (dikonfirmasi lewat query langsung sebelum menulis migrasi)
  - **LANGKAH 0 menemukan `NotificationRepository.antrekan()` TIDAK PERNAH
    dipanggil kode produksi** - dilaporkan ke pengguna sebelum menulis kode
    apa pun (Aturan Global #1: menuntut menyentuh `app/core/engine.py`),
    pengguna mengonfirmasi slice ini fokus struktur saja. Lihat "STATUS
    TERKINI", utang #6, untuk rincian lengkap pemicu yang masih tertunda
  - **Cek-ulang status sebelum kirim digeneralisasi** - `app/notify/processor.py`
    sebelumnya cuma memeriksa `item.jenis == JenisNotifikasi.ALPA` (nilai
    lama, tunggal); sekarang memeriksa `item.jenis in (ALPA_PAGI, ALPA_PULANG)`
    lewat `masih_perlu_dikirim()` yang SAMA (app/core/mesin_absensi.py,
    TIDAK diubah) - kedua jenis sama-sama berarti "status_final ALPA saat
    ini", satu pemeriksaan yang sama berlaku untuk keduanya tanpa perlu
    membedakan pagi/pulang
  - **Mode uji (§13.3) diterapkan di `QueueProcessor`**, bukan di provider
    individual - `app.core.notifikasi.tujuan_efektif()` (murni) menghitung
    nomor tujuan SAAT KIRIM; `nomor_tujuan` yang tersimpan di baris antrean
    TIDAK PERNAH ditimpa, supaya riwayat tetap menunjukkan penerima
    sesungguhnya begitu mode uji dimatikan. Mode uji aktif tanpa nomor admin
    ditandai `FAILED` (bukan diam-diam terkirim ke nomor asli, dan bukan
    dibiarkan `PENDING` selamanya) - dua lapis pengaman: `NotifikasiService.ubah_mode_uji()`
    menolak SIMPAN pengaturan yang tidak konsisten (aktif tanpa nomor
    tersimpan), `QueueProcessor` menolak KIRIM kalau kondisi itu tetap
    lolos lewat jalur lain
  - **Bawaan `mode_uji_whatsapp_aktif` adalah NONAKTIF**, bukan aktif -
    pertimbangan eksplisit: gerbang keamanan sesungguhnya adalah
    `modul_whatsapp_aktif` (bawaan `False`) dan `provider_whatsapp` (bawaan
    `"NULL"`), keduanya sudah menahan SELURUH pengiriman sampai admin sadar
    mengubahnya. Membuat mode uji bawaan AKTIF tanpa nomor admin justru akan
    menahan pengujian `LogProvider` yang sengaja dipakai admin untuk
    memeriksa jalur notifikasi sebelum WhatsApp sungguhan tersedia
  - **Validasi placeholder template sebelum tersimpan** - `NotifikasiService._pastikan_placeholder_dikenal()`
    merakit template ke objek `MessageTemplate` SEMENTARA (tidak pernah
    masuk Session) lewat `app.notify.template.rakit_pesan()` yang SAMA,
    dengan nilai contoh (`nama_siswa`, `kelas`, `tanggal`, `jam`, `status`) -
    template yang menyebut placeholder tak dikenal ditolak 422 SEBELUM
    tersimpan sama sekali, bukan tersimpan lalu gagal dirakit belakangan
    untuk siswa sungguhan
  - **Nomor admin mode uji dirapikan lewat `app.core.validasi_data_master.normalisasi_nomor_whatsapp()`**
    yang SAMA dipakai Data Master (PRD §13.3: "nomor disimpan dalam format
    baku") - bukan penjaga/normalizer baru
  - `PERAN_NOTIFIKASI` (app/core/status.py) kebetulan sama isinya dengan
    `PERAN_KOREKSI_MASSAL` (Kepsek+Admin) tapi dibuat TERPISAH - pola yang
    sama dengan `PERAN_KOREKSI_GURU` dibuat terpisah dari `PERAN_KOREKSI_MASSAL`
    walau himpunan perannya identik, supaya tiap area tetap punya nama
    kewenangannya sendiri yang bisa berubah independen nanti
  - **Antrean (`GET /notifikasi/antrean`) baca-saja**, tidak ada aksi
    kirim-ulang/batalkan manual dari layar ini - `NotificationRepository.daftar()`
    (baru) murni untuk ditampilkan, terpisah dari `daftar_untuk_diproses()`
    yang dipakai `QueueProcessor` (tidak disentuh). Tombol kirim-ulang FAILED
    manual tidak diminta slice ini, dicatat sebagai kemungkinan gap terpisah
    kalau nanti dibutuhkan
  - **Smoke test manual dijalankan terhadap server FastAPI sungguhan** (uvicorn,
    bukan TestClient) + `KlienNotifikasi` produksi langsung: login -> daftar
    template kosong (4 jenis) -> simpan versi 1 -> simpan versi 2 (versi
    naik) -> placeholder tak dikenal ditolak 422 -> antrean kosong -> mode
    uji aktif tanpa nomor ditolak 422 -> isi nomor (dirapikan ke format
    baku) -> aktifkan -> nonaktifkan lagi. Seluruhnya lulus. Automasi Qt
    offscreen (`QT_QPA_PLATFORM=offscreen`) terbukti HANG tanpa batas di
    lingkungan ini (konsisten dengan catatan slice Koreksi Guru: "UI
    Automation pada Qt terbukti tidak stabil di lingkungan ini") - tidak
    dipaksakan, layar `app/ui/notifikasi/layar.py` divalidasi lewat
    kombinasi tinjauan kode (mengikuti pola `app/ui/pengaturan/layar.py`
    persis) dan smoke test produksi non-Qt di atas
  - **Ditemukan, TIDAK dikerjakan** (dicatat sebagai utang #6 di STATUS
    TERKINI): pemicu nyata `antrekan()` dari absensi (empat template), job
    scheduler proses-segera untuk TERLAMBAT, kunci pengaturan+job untuk
    REKAP, dan pesan gabungan kakak-beradik (§13.3) - semuanya di luar
    cakupan sesuai instruksi eksplisit pengguna
  - **Ditemukan, TIDAK dikerjakan** (di luar cakupan, tidak diminta):
    kirim-ulang manual baris `FAILED` dari layar antrean; ekspos
    `batas_percobaan_notifikasi`/`jeda_kirim_alpa_pulang_menit`/`provider_whatsapp`
    di layar (sudah dicatat sebagai gap terpisah sejak slice Pengaturan,
    tidak disentuh di sini karena bukan bagian "template/antrean/mode uji"
    yang diminta)
  - **Code-review (`/code-review`) menemukan 5 hal, DUA diperbaiki di commit
    susulan slice ini:** (1) `rakit_pesan()` cuma menangkap `KeyError` -
    template rusak seperti `"{}"` (kurung kosong) melempar `IndexError`
    mentah, bocor jadi HTTP 500 alih-alih 422. Sekarang juga menangkap
    `IndexError`/`ValueError`/`AttributeError` (seluruh galat realistis
    dari `str.format` yang rusak), diuji lewat `tests/notify/test_template.py`
    dan regresi HTTP di `tests/server/test_notifikasi.py`. (2) Tab Mode Uji
    selalu mengirim ulang `nomor_admin` walau cuma toggle aktif/nonaktif,
    menulis entri audit log kosong (`nilai_sebelum == nilai_sesudah`) tiap
    kali - logika "kirim nomor HANYA kalau berubah" diekstrak jadi fungsi
    murni `app.ui.notifikasi.format_payload.payload_mode_uji()` (pola sama
    `app.ui.koreksi_massal.validasi_form`), diuji tanpa Qt. Sisa TIGA
    temuan (efisiensi query di `processor.py`/`daftar_template()`, risiko
    baca-balik baris `jenis` lama) dicatat sebagai utang #7 di STATUS
    TERKINI, bukan dikerjakan - salah satu klaim review (migrasi gagal di
    tengah jalan) diverifikasi TIDAK akurat sebelum dicatat (lihat utang
    #7c)
- **Laporan selesai sisi endpoint dan layar** (dikerjakan di branch
  `feat/laporan-absensi`, sudah fast-forward ke master) - `app/core/laporan.py`, `app/data/models/laporan.py`
  (tabel `laporan_arsip`, migrasi `d78c3682db53`), `app/data/repositories/laporan.py`,
  `app/server/laporan_service.py`, `app/server/laporan_export.py`,
  `app/server/laporan_storage.py`, `app/server/routers/laporan.py`,
  `app/ui/laporan/`, `python -m app.ui.laporan.main`. Catatan LANGKAH 0 dan
  keputusan dari slice ini:
  - **Tidak ada pustaka Excel/PDF di repo sebelumnya** - ditambahkan
    `openpyxl==3.1.5` (Excel) dan `fpdf2==2.8.8` (PDF) ke `pyproject.toml`,
    keduanya pure-Python tanpa dependensi native (aman PyInstaller).
    Ditanyakan eksplisit ke pengguna sebelum menulis kode (bukan diam-diam
    dipilih), dikonfirmasi
  - **PDF dibangkitkan di SERVER, bukan klien PySide6** - sengaja
    menghindari `QtPrintSupport` walau PySide6 sudah ada sebagai
    dependensi: percobaan Qt offscreen di slice Notifikasi terbukti hang
    tanpa batas di lingkungan ini, dan §3.3 sudah menjadikan server
    satu-satunya pemegang berkas (pola sama `lampiran_storage.py`) -
    menaruh generator PDF di server konsisten dengan itu, bukan
    pengecualian baru
  - **Lima jenis laporan PRD §14.2 (harian/mingguan/bulanan/per kelas/per
    siswa) diimplementasikan sebagai DUA bentuk keluaran komposit**, bukan
    lima layar terpisah - lihat docstring `app/core/laporan.py`: REKAP
    (satu baris per siswa, tally status dalam rentang tanggal + saring
    kelas opsional - harian/mingguan/bulanan/per-kelas/seluruh-sekolah
    SATU bentuk yang sama, dibedakan cuma rentang tanggal + saring) dan
    DETAIL PER SISWA (log harian satu siswa apa adanya, bukan tally -
    laporan per siswa secara konvensi menunjukkan HARI MANA yang bolong).
    Tombol Harian/Mingguan/Bulanan di layar cuma mengisi ulang tanggal
    mulai/selesai, bukan mode terpisah
  - **Siswa SAJA** - PRD §14.2 hanya menyebut "per kelas"/"per siswa",
    konsep yang tidak berlaku untuk guru (sama seperti Koreksi Massal).
    Laporan guru dicatat sebagai utang #8 di STATUS TERKINI, di luar
    cakupan slice ini
  - **NIK TIDAK PERNAH dipilih dari `Student` ke struktur laporan sama
    sekali** (Aturan Global #6) - bukan disamarkan lalu disembunyikan di
    lapisan tampilan, `RekapSiswa`/`LaporanDetailSiswa`/`BarisDetailHarian`
    (app/core/laporan.py) memang tidak punya field NIK untuk dibawa.
    Diuji di tiga lapis: `tests/server/test_laporan.py` (NIK contoh tidak
    muncul di respons JSON), `tests/server/test_laporan_export.py` (NIK
    contoh tidak muncul di BYTE berkas PDF/Excel jadi)
  - **Ekspor PDF SELALU mengarsipkan, tidak ada jalur pintas** (PRD §9.3:
    "arsip PDF otomatis setiap laporan dicetak") - `LaporanService.ekspor_pdf()`
    satu-satunya cara menghasilkan PDF, dan selalu memanggil
    `laporan_storage.simpan_arsip_laporan()` + `LaporanRepository.catat_arsip()`
    dalam pemanggilan yang sama, tidak bisa "generate PDF tanpa arsip".
    Ekspor Excel TIDAK diarsipkan (bacaan literal §9.3 "arsip PDF" -
    Excel murni kenyamanan unduh, bukan medium keabsahan laporan)
  - **Cap waktu cetak muncul di SETIAP halaman otomatis** lewat hook
    `header()`/`footer()` fpdf2 (`_PdfLaporan` di `laporan_export.py`) -
    bukan logika yang ditulis ulang per halaman, jadi tidak mungkin lupa
    di halaman kedua dst. Sengaja TIDAK memakai placeholder jumlah
    halaman total (`{nb}`) fpdf2 - footer cuma "Halaman N", bukan "N/M",
    untuk mengurangi permukaan kegagalan yang belum diverifikasi stabil
  - **Koreksi Setelah Laporan Terbit (PRD §9.3) mengorelasikan DUA sumber
    koreksi** - `koreksi_satuan` (baru: `KoreksiSatuanRepository.daftar_setelah()`)
    dan `tindakan_massal_baris` (baru: `MassCorrectionRepository.daftar_baris_setelah()`,
    dipasangkan dengan induk `TindakanMassal`-nya karena pelaku/alasan
    cuma ada di induk) - keduanya menyaring `waktu > waktu_cetak` DAN
    `id_absensi` dalam cakupan arsip, mengecualikan koreksi yang sudah
    dibatalkan (nilai sudah kembali seperti semula). `koreksi_guru` TIDAK
    diikutsertakan - konsisten dengan laporan yang siswa-saja
  - **`LaporanArsip.id_kelas` dipakai ulang sebagai SATU kolom keputusan
    akses** untuk rekap-per-kelas MAUPUN detail-per-siswa (diturunkan dari
    kelas siswa itu saat dicetak) - `None` HANYA berarti arsip
    seluruh-sekolah. Ini yang membuat `daftar_arsip()`/`koreksi_setelah_terbit()`
    cukup satu pemeriksaan `pastikan_akses_kelas()` (primitif yang sama
    Riwayat Scan/Koreksi Satuan, bukan penjaga baru) untuk membatasi wali
    kelas ke seluruh bentuk laporan, tanpa cabang akses terpisah per bentuk
  - **Operator TIDAK BERWENANG SAMA SEKALI** ke `/laporan/*` - beda
    sengaja dari Riwayat Scan yang terbuka untuknya. PRD §10 baris
    "Dashboard dan laporan": sel operator cuma berisi "riwayat scan", bukan
    akses laporan. `PERAN_LAPORAN` (app/core/status.py) TIDAK memasukkan
    `Peran.OPERATOR`
  - **Smoke test manual dijalankan terhadap server FastAPI sungguhan**
    (uvicorn, bukan TestClient) + `KlienLaporan` produksi langsung: rekap
    kelas -> detail per siswa -> ekspor Excel (NIK tidak muncul) -> ekspor
    PDF (NIK tidak muncul, diarsipkan otomatis) -> daftar arsip -> unduh
    arsip (byte identik dengan yang diekspor) -> koreksi lewat
    `KoreksiSatuanRepository` langsung -> koreksi-setelah-terbit
    mendeteksinya. Seluruhnya lulus. Otomasi Qt tidak dicoba lagi untuk
    layar ini - sudah terbukti hang di slice Notifikasi, divalidasi lewat
    tinjauan kode (mengikuti pola `app/ui/pengaturan/layar.py`) + smoke
    test produksi non-Qt di atas
  - **Ditemukan, TIDAK dikerjakan** (di luar cakupan, tidak diminta):
    laporan guru (utang #8); kirim-ulang/tandai-sudah-ditinjau untuk
    laporan yang sudah basi di layar Koreksi Setelah Terbit - layar ini
    murni MENAMPILKAN, admin tetap mencetak ulang manual lewat tab
    Laporan; dropdown nama kelas/siswa di form (isian ID mentah, alasan
    sama persis dengan Riwayat Scan/Koreksi Satuan: `GET /siswa`/`GET /kelas`
    dibatasi kepsek/admin, Laporan juga dipakai wali kelas)
  - **Bug ditemukan lewat pemeriksaan visual manual (bukan test otomatis),
    diperbaiki di commit susulan:** logo sekolah di kop Excel tampil
    ukuran ASLI penuh (mis. 200x200px, menimpa beberapa baris tabel di
    bawahnya), bukan dikecilkan seperti dimaksud - `openpyxl.drawing.image.Image.width`/`.height`
    ternyata TIDAK bertahan setelah `save()`/dibaca ulang (diverifikasi
    lewat percobaan langsung), padahal PDF (fpdf2) tidak kena masalah
    yang sama. Ukuran tampil nyata Excel ditentukan `anchor.ext`, bukan
    atribut `.width`/`.height` - `_tulis_kop_excel()` sekarang membuat
    `OneCellAnchor`+`XDRPositiveSize2D` eksplisit (60x60px). Regresi
    diuji di `tests/server/test_laporan_export.py::test_excel_logo_ukuran_besar_tetap_dikecilkan_konsisten`
    (test lama dengan `logo=None` tidak pernah menyentuh jalur ini,
    itu sebabnya lolos sebelum bug ini ditemukan)
- **Jalur pada attendance_events selesai** (dikerjakan di branch
  `feat/jalur-attendance-events`, sudah fast-forward ke master) - menutup
  utang §17 yang tercatat sejak slice Data Master. Pola plumbing IDENTIK
  slice `uid_mentah`
  (`0670ad4`/`f6b69a2af59c`) - parameter opsional (bawaan `None`)
  diteruskan apa adanya, nol perubahan klasifikasi. Catatan LANGKAH 0 dan
  keputusan dari slice ini:
  - **LANGKAH 0 menghitung TUJUH titik `attendance_event_repo.buat(...)`**
    di `app/core/engine.py` - jumlah persis sama dengan slice `uid_mentah`
    (semua titik itu memang satu-satunya tempat baris `attendance_events`
    pernah dibuat). Diverifikasi lewat grep sebelum menulis kode, bukan
    ditebak
  - **Rantai klien SUDAH membawa jalur hampir sampai ke server sebelum
    slice ini** - `app.rfid.base.ScanEvent.jalur` sudah ada sejak awal,
    `app/ui/lobi/layar.py` sudah memanggil `KlienScan.submit_scan(jalur=...)`,
    dan `AntreanLokal`/tabel lokal `antrean_scan` sudah punya kolom
    `jalur TEXT NOT NULL` sejak slice antrean lokal. Satu-satunya mata
    rantai yang putus: `KlienScan._kirim_item()`/`._kirim()` tidak
    meneruskan `jalur` ke body JSON `POST /scan` - dua baris kode,
    diperbaiki di slice ini, BUKAN pekerjaan besar seperti dugaan awal
  - **Cabang klasifikasi yang tergoda berubah: NOL, diverifikasi lewat
    grep** sebelum menulis kode - `app.core.mesin_absensi` tidak pernah
    membaca `station_id`/`jalur` untuk keputusan apa pun
    (`ScanMasuk.station_id` cuma dideklarasikan, tak pernah dicabangkan).
    `tests/core/test_engine.py` tidak disentuh sama sekali dan tetap
    23/23 hijau; `git diff` membuktikan `app/core/mesin_absensi.py` nol
    perubahan
  - **`AttendanceEvent.jalur`** tipe `SAEnum(JalurAlat, native_enum=False)`
    (bukan `String` biasa seperti `uid_mentah`) - konsisten dengan kolom
    `jenis_event`/`jenis_pemilik` di tabel yang sama, dan `JalurAlat`
    memang sudah kosakata baku (`app.rfid.base`), bukan teks bebas.
    Migrasi `0df2d01ad591` nullable, baris lama dibiarkan `None` - tidak
    ada backfill karangan, pola sama migrasi `uid_mentah`
  - **`ScanRequest.jalur` WAJIB di kontrak HTTP** (bukan opsional seperti
    `station_id`) - setiap klien nyata selalu punya nilai ini dari
    `ScanEvent.jalur`, sama seperti `uid_mentah` sebelumnya. Router
    mengonversi `JalurAlat(payload.jalur)`, melempar 422 dengan pesan
    jelas untuk nilai yang tidak dikenal - bukan 500 mentah
  - **Ditemukan, DIKERJAKAN karena satu gap yang sama (bukan pelebaran
    cakupan):** `RiwayatScanResponse.jalur` sudah ada sejak slice Riwayat
    Scan, sengaja selalu `None` dengan komentar eksplisit "kolom jalur
    belum ada di attendance_events, lihat README". Begitu kolomnya ada,
    membiarkan field itu tetap hardcode `None` akan membuat komentarnya
    berbohong dan datanya tetap tak terlihat di satu-satunya layar yang
    sudah menyediakan tempat untuknya (PRD §7.4) - satu baris di
    `app/server/routers/riwayat_scan.py` (`jalur=b.event.jalur.value if ...`),
    bukan fitur baru
  - **Test baru mengikuti pola persis `test_uid_mentah_fidelitas.py`** -
    `tests/core/test_jalur_fidelitas.py` (7 titik + 1 test "opsional,
    klasifikasi tak berubah"), plus regresi di `tests/data/repositories/test_attendance_event.py`,
    `tests/server/test_app.py` (tersimpan + 422 jalur tak dikenal),
    `tests/server/test_riwayat_scan.py` (tampil di layar), dan
    `tests/ui/lobi/test_klien_scan.py` (byte-persis di wire offline->dikuras,
    sengaja pakai `JalurAlat.PCSC` bukan `SIMULATOR` supaya tidak lolos
    kebetulan)
  - **Smoke test manual dijalankan terhadap server FastAPI sungguhan**
    (uvicorn, bukan TestClient) lewat `KlienScan` produksi: scan online
    (PCSC) tersimpan dan tampil di `GET /riwayat-scan`, DAN scan offline
    (SERIAL, server sengaja tak terjangkau) diantrekan lalu dikuras -
    jalur tetap `SERIAL` di attendance_events setelah dikuras, bukan
    berubah/hilang. Seluruhnya lulus

## Utang semantik

**UTANG SEMANTIK (wajib diputuskan sebelum Fase 2 / WhatsApp aktif):**
Scan yang dibuffer lalu tiba setelah cutoff bertemu ALPA-otomatis yang sudah
dibuat scheduler. Aturan lama "ALPA tak berubah otomatis" ditulis untuk
siswa yang benar-benar telat, bukan untuk siswa tepat-waktu yang paketnya
telat. Aturan yang DISARANKAN untuk diputuskan pemilik proyek: scan
terbuffer dengan waktu_scan di dalam jendela tepat-waktu berhak mengoreksi
ALPA-OTOMATIS (hanya status yang dihasilkan sistem; tidak pernah status
yang diinput manusia). Ini slice rekonsiliasi tersendiri yang menyentuh
klasifikasi. Sebelum Fase 2 ini WAJIB tuntas karena ALPA palsu untuk siswa
tepat-waktu = notifikasi keliru ke orang tua (bahaya yang §8 cegah).

Slice antrean ini SUDAH menyimpan bukti untuk rekonsiliasi itu:
`AttendanceEvent` mentah dengan `waktu_scan` asli.

(Ditemukan dan dikonfirmasi pemilik proyek saat verifikasi LANGKAH 0 slice
antrean lokal sisi klien - lihat juga kasus serupa di cutoff pulang: scan
pulang terbuffer yang tiba setelah `jalankan_cutoff_pulang` sudah
mengonversi `status_final=ALPA` akan mengisi `jam_pulang` yang benar lewat
`AttendanceRepository.catat_pulang()`, tapi `status_final`-nya tidak ikut
terkoreksi - kasus yang sama, keputusan yang sama ditunggu.)

**UTANG MODUL LAPORAN - DITUTUP (slice Laporan):** Identitas sekolah (PRD
§14.2: nama, NPSN, alamat, logo, nama+NIP kepala sekolah), sudah tersimpan
sejak slice Pengaturan lewat `GET /pengaturan/identitas-sekolah`, sekarang
BENAR-BENAR terpakai di kop - `app/server/laporan_export.py` memanggil
`Pengaturan.identitas_sekolah()` yang SAMA (bukan field kop terpisah) untuk
mengisi header PDF (`_PdfLaporan.header()`, tiap halaman) dan baris kop
Excel (`_tulis_kop_excel()`). Kriteria selesai slice Pengaturan ("terisi &
terpakai di kop") sekarang genap terpenuhi.

**UTANG KOREKSI SATUAN - DITUTUP (slice Koreksi Satuan):**
`app.core.akses_kelas.pastikan_akses_kelas()` (primitif pembatasan kelas
wali kelas, dibuat di slice Manajemen Akun & Peran, diuji murni di
`tests/core/test_akses_kelas.py`) sempat hanya punya pemanggil di jalur
BACA (`RiwayatScanService.cari()`, slice Riwayat Scan). Slice Koreksi
Satuan menyambungkannya ke jalur TULIS sungguhan: `KoreksiSatuanService`
memanggil primitif yang SAMA (bukan penjaga baru) di `koreksi()`,
`batalkan()`, `status_saat_ini()`, dan `daftar_riwayat()` sebelum
mengizinkan wali kelas menyentuh data attendance siswa. Aturan "kelas
sendiri saja" tetap satu jalur pemeriksaan tunggal, sesuai instruksi
aslinya (SKILL.md "Peran dan hak akses": "Pembatasan kelas wali kelas
ditegakkan di lapisan data"). Koreksi satuan GURU tetap di luar cakupan -
kalau nanti dibangun, gunakan primitif yang sama untuk siswa; guru tidak
relevan dengan `pastikan_akses_kelas` sama sekali (wali kelas tidak
mengampu guru).

## Aturan yang sudah dikodekan (ringkas - lihat SKILL.md untuk lengkapnya)

| Aturan | Nilai |
|---|---|
| Hadir | scan sampai 07:20:59 |
| Terlambat | scan mulai 07:21:00 |
| Alpa pagi | tidak scan sampai 07:30 |
| Jendela pulang | 13:20-15:00; Jumat 11:00-14:00 |
| Tidak scan pulang | status_final ALPA, status_masuk tidak berubah |
| Ambang tahan konversi pulang | 30% - melindungi dari alat mati |
| Toleransi guru | 09:30, guru tidak mengenal ALPA |
| Anti-duplikat | 10 detik, global per UID lintas jalur (bukan per alat) |
| Kunci jalur alat | alat beda jalur ditolak, jalur lain tetap jalan |
| Koreksi massal | hanya Kepsek/Admin; snapshot & konfirmasi ketik di atas 5.000 baris; status manual (IZIN/SAKIT/DISPENSASI) dikecualikan bawaan |
| Kalender akademik | dua jalur (siswa/guru berbeda), tanggal tak-diatur bawaan EFEKTIF, Minggu selalu libur |

Tidak ada satu pun angka di atas yang ditulis tetap di logika status - seluruhnya
dibaca dari `Pengaturan` (`app/config/pengaturan.py`) atau `school_schedules`.

## Prinsip yang tidak boleh dilanggar

1. `app/core/` tidak pernah mengimpor dari `app/ui/`. SKILL.md juga menulis
   "maupun `app/data/`" - dalam praktiknya `engine.py`, `kalender_service.py`,
   dan `koreksi_massal_service.py` adalah pengecualian yang disengaja (lapisan
   integrasi tipis yang menyambungkan aturan murni ke basis data, dengan
   nama yang menandakannya jelas); modul lain di `app/core/` (`status.py`,
   `mesin_absensi.py`, `kalender.py`, `koreksi_massal.py`) tetap 100% murni.
2. Peristiwa scan mentah (`attendance_events`) disimpan permanen dan tidak
   pernah diubah.
3. Koreksi menyimpan nilai lama, pelaku, waktu, dan alasan lewat audit log.
4. Absensi tercatat lebih dulu; notifikasi menyusul dalam transaksi terpisah.
5. Seluruh perbandingan waktu memakai waktu lokal WITA, tidak pernah UTC.
