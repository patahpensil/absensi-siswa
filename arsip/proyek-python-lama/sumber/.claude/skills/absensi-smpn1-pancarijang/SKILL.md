---
name: absensi-smpn1-pancarijang
description: Konteks dan aturan baku proyek "Sistem Informasi Berbasis Digital UPT SMP Negeri 1 Pancarijang" — aplikasi desktop Python absensi RFID siswa dan guru dengan notifikasi WhatsApp. WAJIB dibaca sebelum menulis kode, merancang basis data, membuat layar antarmuka, menghitung status kehadiran, menyusun dokumen, atau menjawab pertanyaan apa pun yang menyangkut aplikasi ini. Gunakan skill ini setiap kali pengguna menyebut absensi RFID, SMPN 1 Pancarijang, kartu siswa, notifikasi WhatsApp ke orang tua, absensi guru, atau melanjutkan pekerjaan pada aplikasi absensi sekolahnya — termasuk saat pengguna hanya berkata "lanjutkan proyek absensi" atau "buatkan modul X" tanpa menyebut nama sekolahnya.
---

# Sistem Informasi Berbasis Digital UPT SMP Negeri 1 Pancarijang

Aplikasi desktop `.exe` untuk jaringan lokal sekolah. Tahap awal hanya menangani
absensi siswa dan guru berbasis kartu RFID, disimpan di server sekolah, dengan
notifikasi WhatsApp ke orang tua/wali.

Sekolah: UPT SMP Negeri 1 Pancarijang, Kabupaten Sidenreng Rappang, Sulawesi Selatan.
Jenjang **SMP**, bukan SLTA.

Rujukan resmi PRD: `docs/PRD_Absensi_RFID_SMPN1_Pancarijang.docx` (versi 2.0,
format Word untuk pengesahan sekolah). Dokumen PRD sebelumnya (draf markdown
berjudul SLTA, dan draf markdown v2.1) sudah tidak berlaku dan dipindahkan ke
arsip — jangan dijadikan rujukan.

**Dokumen ini masih hidup.** Keputusan bisa bertambah atau berubah; perbarui berkas
ini setiap kali pengguna mengunci sesuatu yang baru.

## Cara memakai skill ini

Baca "Keputusan terkunci" dan "Aturan bisnis" sebelum mengerjakan apa pun.
Jika sebuah kebutuhan tidak tercakup di sini, periksa daftar "Hal yang belum
diputuskan" di bawah. Bila memang termasuk di situ, **tanyakan ke pengguna, jangan
mengarang nilai sendiri**.

---

## Keputusan terkunci

| Hal | Keputusan |
|---|---|
| Platform | Aplikasi desktop `.exe`, arsitektur klien-server di jaringan lokal |
| Bahasa | Python 3.11+ |
| Antarmuka | PySide6 |
| Basis data | SQLite (mode WAL) via SQLAlchemy, hanya disentuh proses server |
| Perangkat | 5 PC — 1 ruang kepala sekolah, 3 ruang TU, 1 lobi untuk scan |
| Letak server | Ruang TU |
| Jaringan | LAN dan WiFi sudah ada, semua ruang satu jaringan; server pakai IP statis |
| Kapasitas | Dirancang untuk 1.000 siswa |
| Titik scan | Satu klaster di lobi; 6 unit reader (4 lobi, 1 ruang guru, 1 cadangan) |
| Gerbang dibuka | 06:30, satu gerbang masuk |
| Hari sekolah | Senin sampai Sabtu. Minggu libur |
| Jam masuk | 07:15 |
| Batas terlambat | 07:21:00 ke atas dihitung TERLAMBAT |
| Penetapan ALPA pagi | 07:30 |
| Kirim notifikasi ALPA pagi | 09:15 |
| Jam pulang | 13:30; hari Jumat 11:10 |
| Jendela scan pulang | 13:20–15:00; Jumat 11:00–14:00 |
| Tidak scan pulang | Dihitung ALPA |
| Toleransi guru | 09:30 |
| Input data master | Manual satu per satu. **Fitur impor/unggah berkas dibatalkan** — termasuk rencana impor Dapodik dari draf PRD lama (Bagian 20.1), sekarang tidak berlaku sama sekali |
| Hari ujian/MPLS/Porseni | Tetap memakai jam normal, tanpa jadwal khusus |
| Pengaman ALPA massal otomatis | **Tidak ada.** Admin yang mengoreksi bila terjadi |
| Koreksi massal | Fitur inti, hanya admin dan kepala sekolah |
| Jalur WhatsApp | Modul dibangun penuh, koneksi **belum diaktifkan** |
| Cakupan tahap awal | Absensi siswa dan absensi guru |

### Prinsip yang tidak boleh dilanggar

- Absensi tetap berjalan tanpa internet dan tanpa LAN. PC lobi menyimpan scan ke
  antrean lokalnya lebih dulu, lalu mengirim ke server.
- Data absensi disimpan lebih dahulu, sebelum hal lain terjadi.
- Notifikasi WhatsApp proses terpisah. Kegagalan kirim tidak boleh membatalkan absensi.
- Peristiwa scan mentah disimpan permanen dan tidak pernah diubah.
- Seluruh nilai waktu, toleransi, ambang, dan template adalah konfigurasi.
  Tidak ada satu pun yang ditulis tetap di dalam kode.
- Admin boleh mengubah **aturan**, tidak boleh mengubah **kenyataan**.
- `app/core/` tidak pernah mengimpor dari `app/ui/` maupun `app/data/`.
- Seluruh perbandingan waktu memakai waktu lokal WITA, tidak pernah UTC.
- Data pribadi peserta didik dan pegawai diperlakukan sesuai **UU No. 27 Tahun
  2022 tentang Pelindungan Data Pribadi** — aplikasi hanya menyimpan data yang
  benar-benar diperlukan untuk keperluan absensi.

---

## Aturan bisnis

### Status masuk

```
scan <= 07:20:59            -> HADIR
scan >= 07:21:00            -> TERLAMBAT
tidak ada scan sampai 07:30 -> ALPA
```

Status siswa: `HADIR`, `TERLAMBAT`, `ALPA`, `IZIN`, `SAKIT`, `DISPENSASI`.
Tiga terakhir diinput manual, tidak pernah dihasilkan dari scan atau cutoff.

Siswa yang sudah dicap ALPA lalu datang pukul 07:45 **tetap dicatat scan-nya**,
status tidak berubah otomatis. Koreksi manual, karena sekolah ingin ada penilaian
manusia atas alasan keterlambatan.

### Absensi pulang dan ALPA pulang

Status disimpan dalam **dua kolom**: `status_masuk` (hasil scan pagi) dan
`status_final` (status resmi untuk laporan). Tidak scan pulang menghasilkan
`status_masuk = HADIR`, `status_final = ALPA`, alasan `TIDAK_SCAN_PULANG`.
Laporan menghitungnya ALPA. Dua kolom dipertahankan agar "tidak pernah datang"
tetap bisa dibedakan dari "datang tapi tidak absen pulang".

Cutoff: 15:00 (Senin–Kamis, Sabtu), 14:00 (Jumat).

Dikecualikan: status IZIN/SAKIT/DISPENSASI, siswa bertanda "izin pulang cepat",
dan guru.

Ambang 30% menahan konversi bila terlalu banyak yang tidak scan pulang —
melindungi dari alat yang mati, bukan dari siswa. Admin memutuskan apakah tetap
dijalankan. Layar absensi menampilkan penghitung langsung sepanjang periode pulang.

Scan setelah cutoff disimpan bertanda `DI_LUAR_JENDELA`, muncul sebagai usulan
koreksi, tidak mengubah status sendiri.

### Scan berulang

Scan UID yang sama dalam 10 detik dihitung sekali. Berlaku **global per UID**,
bukan per jalur — supaya tap di jalur 1 lalu jalur 3 tidak tercatat dua kali.

### Kartu tidak dikenal

Tidak pernah membentuk absensi. Tampilkan peringatan, catat peristiwanya, lanjutkan
menerima scan berikutnya tanpa perlu menutup dialog.

### Perubahan jadwal

Wajib punya tanggal mulai berlaku. Perhitungan status selalu memakai jadwal yang
berlaku pada tanggal absensi itu, bukan jadwal terkini. Jam masuk, jam pulang, dan
cutoff dapat diatur per hari dalam seminggu.

### Absensi guru

Status: `HADIR`, `TERLAMBAT`, `DINAS_LUAR`, `IZIN`, `SAKIT`, `CUTI`.
Tidak ada ALPA untuk guru. Toleransi 09:30.

Status `DINAS_LUAR`/`IZIN`/`SAKIT`/`CUTI` **sebaiknya** disertai lampiran (foto
surat tugas atau surat keterangan) — rekomendasi PRD, bukan kewajiban tegas.
Kolom `lampiran` sudah ada di struktur data `koreksi` (lihat "Struktur data
inti") tapi belum diimplementasikan di kode manapun saat ini.

---

## Kalender akademik

Dua jalur terpisah: **kalender siswa dan kalender guru**. Satu tanggal bisa LIBUR
bagi siswa sekaligus EFEKTIF bagi guru — contoh nyata dari kalender sekolah,
14–19 Desember 2026 (pengolahan nilai LHBPD) dan 22 Desember (penyerahan LHBPD).

Jenis hari: `EFEKTIF`, `LIBUR_NASIONAL`, `LIBUR_SEMESTER`, `KEGIATAN_TANPA_KBM`,
`LIBUR_MENDADAK`.

Tanggal yang tidak diatur otomatis EFEKTIF; Minggu selalu libur. Admin hanya
memasukkan pengecualiannya — sekitar sepuluh entri per semester.

**Kalender adalah bagian dari keluarga koreksi massal, bukan modul dengan aturan
sendiri.** Konsekuensinya:

- Kewenangan sama: hanya admin dan kepala sekolah
- Perubahan berlaku surut dengan pratinjau dampak: "Perubahan ini memengaruhi
  6 hari dan 6.000 baris absensi. Hitung ulang statusnya?"
- Hasilnya masuk ke Riwayat Tindakan Massal, lengkap dengan tombol batalkan
- Seluruh perubahan tercatat di log audit

Tombol "hari ini bukan hari efektif" bekerja **surut**: status hari itu dibatalkan
dan notifikasi yang belum terkirim ditarik. Scan yang terlanjur masuk tetap
tersimpan sebagai peristiwa mentah.

Kalender terikat tahun ajaran; beberapa tahun boleh tersimpan bersamaan. Yang
berlaku ditentukan tanggalnya, sehingga menyiapkan kalender tahun depan tidak
mengganggu absensi berjalan. Ada tombol "Salin dari tahun sebelumnya" (menyalin
data internal aplikasi, bukan impor berkas).

Hari ujian, MPLS, dan Porseni **tetap jam normal**. Fitur jadwal khusus per tanggal
tetap dibangun tapi dibiarkan tidak terpakai.

---

## Koreksi massal

Modul inti, bukan pelengkap. Hanya **admin dan kepala sekolah**. Wali kelas tetap
koreksi satuan untuk kelasnya.

Tiga lapis penyaring yang bisa dikombinasikan:

- **Waktu** — satu tanggal atau rentang
- **Orang** — seluruh sekolah, satu tingkat, satu kelas, atau centang siswa
- **Status saat ini** — hanya ALPA, hanya TERLAMBAT, atau semuanya

Bawaannya IZIN, SAKIT, dan DISPENSASI selalu dikecualikan kecuali sengaja dicentang.
Tanpa penyaring status, tindakan massal akan menimpa siswa yang sudah bersurat dokter.

Tindakan: ubah status, tandai izin pulang cepat, batalkan konversi ALPA pulang,
batalkan tindakan massal sebelumnya.

Rem tangan yang tidak mengurangi kewenangan:

- Pratinjau wajib dengan jumlah baris terdampak
- Alasan wajib, satu kali untuk seluruh tindakan
- Satu entri log audit induk berisi penyaring dan jumlah baris — bukan ribuan entri
- Tombol batalkan pada setiap tindakan
- Snapshot basis data otomatis sebelum tindakan di atas 5.000 baris
- Konfirmasi ketik ulang untuk tindakan di atas 5.000 baris

**Layar Riwayat Tindakan Massal** memuat seluruh tindakan: tanggal, pelaku, cakupan,
jumlah baris, alasan, tombol batalkan. Ini yang membuat ketiadaan kunci bulan tidak
menjadi masalah.

Kasus yang ditangani modul ini: hari ujian dengan jam pulang berbeda (22 hari per
semester), libur mendadak yang terlambat ditandai, reader mati semalaman, satu kelas
ikut lomba, listrik padam, kalender yang lupa diisi.

---

## Peran dan hak akses

Empat peran. **Satu akun satu peran. Tidak ada akun bersama.** Staf TU memegang
peran Admin.

| Kewenangan | Kepsek | Admin (TU) | Wali Kelas | Operator |
|---|---|---|---|---|
| Pengaturan jadwal dan jam | ✓ | ✓ | — | — |
| Data master dan kelas | ✓ | ✓ | — | — |
| Kalender akademik | ✓ | ✓ | lihat | lihat |
| Manajemen kartu RFID | ✓ | ✓ | — | — |
| Uji Baca Kartu (§18, tanpa membentuk absensi) | ✓ | ✓ | — | — |
| Wizard Ganti Jalur Alat | ✓ | ✓ | — | — |
| Koreksi satuan semua kelas | ✓ | ✓ | — | — |
| Koreksi satuan kelas sendiri | ✓ | ✓ | ✓ | — |
| **Koreksi massal** | ✓ | ✓ | — | — |
| Input izin/sakit/dispensasi | ✓ | ✓ | ✓ (kelasnya) | — |
| Izin darurat alat RFID | ✓ | ✓ | ✓ | — |
| Template dan antrean WhatsApp | ✓ | ✓ | — | — |
| Saklar modul | ✓ | ✓ | — | — |
| Dashboard dan laporan | ✓ | ✓ | ✓ (kelasnya) | riwayat scan |
| Menjalankan layar absensi | ✓ | ✓ | ✓ | ✓ |
| Backup dan pemulihan | ✓ | ✓ | — | — |
| Buat akun wali kelas & operator | ✓ | ✓ | — | — |
| Buat akun Admin & Kepsek | ✓ | — | — | — |
| Hapus data massal / reset | ✓ | — | — | — |

Kata sandi hash argon2, tidak pernah bisa dilihat siapa pun termasuk kepala sekolah —
hanya direset. Salah 5 kali memberi jeda 30 detik. Kata sandi awal wajib diganti saat
login pertama. Pembatasan kelas wali kelas ditegakkan di lapisan data.

Tindakan kepala sekolah **tetap tercatat di log audit**.

Satu pengecualian di luar tabel di atas: **cek status izin darurat aktif per
jalur** (dipakai layar lobi untuk menyalakan banner MODE DARURAT) TIDAK
dibatasi peran — termasuk Operator kiosk lobi, karena murni informasional,
bukan kewenangan mengubah apa pun.

---

## Data master

### Input manual

Fitur impor/unggah berkas **dibatalkan**. Karena itu layar input harus cepat:

- Bentuk tabel isian, bukan formulir per orang
- Tab antar kolom, Enter antar baris
- Tersimpan otomatis per baris, bisa dilanjutkan, ada penanda progres
- Validasi seketika: NISN 10 digit unik, NIK 16 digit unik, nomor HP dirapikan
- Pendaftaran kartu RFID digabung: selesai satu baris, kartu ditempel, UID terikat,
  kursor lompat ke baris berikutnya

### Kolom siswa

Nama, NISN, NIPD, jenis kelamin, tempat lahir, tanggal lahir, NIK, alamat,
nama salah satu orang tua, nomor telepon/WA orang tua.

NISN kunci utama, tidak boleh kembar. NIPD boleh kosong.

**Perlakuan NIK:** tersamar di layar biasa, lengkap hanya saat diedit; tidak pernah
ikut laporan cetak, ekspor, atau pesan WhatsApp; utuh hanya bagi admin dan kepsek.

### Kolom guru

Nama, NIP, pangkat/jabatan. NIP boleh kosong untuk guru honor, tidak boleh kembar.

### Kelas dan wali kelas — kondisional

Form input manual: tingkat, nama rombel, wali kelas, tahun ajaran. Nama rombel
diketik bebas (sekolah memakai istilah teknologi seperti "VIII. PASCAL").

Keduanya boleh dikosongkan dan sistem tetap berjalan penuh:

- Kelas belum dibuat → siswa masuk "Belum ditempatkan", absensi tetap normal
- Wali kelas belum ada → koreksi ditangani admin
- Siswa pindah kelas → riwayat absensi tetap menempel pada kelas lamanya

Kelas yang punya riwayat tidak pernah dihapus, hanya diarsipkan.

---

## RFID dan perangkat

Aplikasi mendukung **semua jalur dan semua jenis kartu**. Empat pengemudi di balik
satu antarmuka: `PcscReader`, `SerialReader`, `HidReader` (hanya untuk SATU unit),
`SimulatorReader`.

```python
@dataclass(frozen=True)
class ScanEvent:
    uid: str            # sudah dinormalisasi
    uid_mentah: str     # apa adanya dari alat, untuk diagnosis
    station_id: str
    waktu: datetime
    jalur: JalurAlat
```

UID disimpan ternormalisasi (hex huruf besar). Panjang 4 byte, 7 byte, dan 10 digit
sama-sama diterima; format hex/desimal dan urutan byte dikoreksi lewat pengaturan
per alat.

### Daftar pengadaan perangkat

| Perangkat | Jumlah | Keterangan |
|---|---|---|
| Reader RFID | 6 unit | 4 lobi, 1 ruang guru, 1 cadangan |
| Kartu RFID | Sesuai jumlah siswa & pegawai | Dicetak bertahap |
| USB hub bertenaga | 1 unit | Wajib bertenaga (powered), bukan pasif |
| UPS | 2 unit | Server dan PC lobi |
| Layar tampilan lobi | 1 unit | Monitor atau televisi |
| Hard disk eksternal | 1 unit | Untuk pencadangan |

### Syarat pengadaan reader

- **Dwi-frekuensi** (125 kHz + 13.56 MHz) — batas fisik: satu reader hanya membaca
  frekuensi yang didukung antenanya, ini tidak bisa diselesaikan kode
- **Dapat dibedakan per unit** — PC/SC atau serial, bukan HID
- Satu merek, satu batch, termasuk cadangan

### Kunci jalur alat

Admin menetapkan satu Jalur Alat Aktif saat pemasangan. Alat jalur lain **ditolak**:
jalur itu tidak diaktifkan, muncul peringatan yang menyebut tipe alat yang benar,
jalur lain tetap berjalan.

| Situasi | Perlakuan |
|---|---|
| Alat beda jalur | Jalur diblokir, peringatan tegas |
| Alat sejalur beda merek | Diizinkan, wajib uji baca, peringatan kuning |
| Alat tercabut atau mati | Jalur nonaktif, pesan "alat terputus" |
| HID di lebih dari satu jalur | Ditolak |

**Izin darurat:** kepsek, admin, atau wali kelas dapat mengizinkan alat beda jalur
maksimal 1×24 jam, wajib beralasan, tercatat, dan layar lobi menampilkan
"MODE DARURAT — alat jalur N tidak sesuai standar". Kepsek dan admin diberi tahu saat
diaktifkan. Perpanjangan hanya oleh kepsek atau admin.

**Wizard "Ganti Jalur Alat"** (kepsek/admin), berurutan dan tidak bisa dilompati:
seluruh unit baru harus terpasang → uji baca tiap jalur → **verifikasi tiga kartu
terdaftar** → kalibrasi bila UID tidak cocok → konfirmasi beralasan. Langkah
verifikasi kartu inilah yang mencegah 1.000 kartu mendadak tidak dikenali.

### Umpan balik di lobi

**Sunyi berarti berhasil.** Scan sukses tidak berbunyi — layar besar terbagi empat
kolom menampilkan foto, nama, dan status berwarna selama ±1,5 detik. Scan gagal
membunyikan alarm dari satu speaker bersama dan kolomnya memerah. Layar berjalan
penuh layar dan **tidak pernah menuntut klik**.

### Siklus hidup kartu

Tabel `kartu` terpisah dari tabel orang; satu orang boleh punya riwayat beberapa
kartu. Kartu terikat pada **orangnya, bukan kelasnya** — naik kelas tidak perlu
cetak ulang. Ada layar Uji Baca Kartu yang menampilkan UID mentah dan ternormalisasi
berdampingan. Jangan membeli kartu "UID writable"/"magic card".

---

## Modul notifikasi WhatsApp

Dibangun penuh, koneksi dinonaktifkan. Antarmuka penyedia yang bisa ditukar:
`NullProvider` (bawaan), `LogProvider`, `MetaCloudProvider` (kerangka).

Antrean berjalan penuh sejak sekarang: `notification_queue` berstatus `PENDING`,
kunci idempotensi, waktu kejadian terpisah dari waktu pengiriman.

**Alur status pengiriman:**

```
PENDING -> SENDING -> SENT
PENDING -> SENDING -> FAILED -> (diambil ulang otomatis) -> SENT
                                                           -> FAILED (lagi, sampai batas)
```

Retry **bukan** status DB tersendiri — `StatusNotifikasi` hanya
`PENDING`/`SENDING`/`SENT`/`FAILED`/`CANCELLED`. "Retry" berarti baris
`FAILED` diambil ulang otomatis oleh `QueueProcessor` pada putaran berikutnya,
selama `jumlah_percobaan` belum mencapai `batas_percobaan_notifikasi` (bawaan
3). Begitu batas tercapai, baris berhenti diambil dan tetap `FAILED`
permanen. Ini setara secara perilaku dengan alur PENDING→SENDING→SENT/FAILED→
RETRY→SENT, hanya beda cara mewujudkannya.

**Empat template terpisah**, semuanya bisa diedit admin:

1. Terlambat
2. ALPA pagi — dikirim 09:15
3. ALPA karena tidak scan pulang — kalimatnya berbeda: ananda hadir namun tidak
   melakukan absen pulang
4. Rekap

**Jeda kirim ALPA pulang**, bawaan 60 menit setelah cutoff, memberi admin waktu
mengoreksi lebih dulu. Status diperiksa ulang sebelum kirim, sehingga koreksi dalam
jeda otomatis membatalkan pesannya. Untuk ALPA pagi jeda ini sudah ada dengan
sendirinya (cutoff 07:30, kirim 09:15).

**Mode uji wajib ada**: seluruh pesan dialihkan ke satu nomor admin.

Satu nomor kontak boleh terhubung ke beberapa siswa (kakak-beradik); bila keduanya
alpa di hari yang sama, kirim **satu pesan gabungan**.

---

## Fitur pendukung

### Backup dan pemulihan

- Harian otomatis 22:00, disalin ke **dua tujuan**: hard disk eksternal dan folder
  di PC lain. Bila satu tujuan tidak tersedia, backup tetap jalan dan dashboard
  memberi peringatan
- Simpan 30 harian terakhir dan 12 bulanan
- Snapshot otomatis sebelum tindakan massal di atas 5.000 baris
- Backup dienkripsi
- Indikator "Backup terakhir: 6 jam lalu", memerah bila lewat dua hari
- Pengingat tiga bulanan untuk menguji pemulihan

### Log audit

Mencatat login dan login gagal, perubahan pengaturan, koreksi satuan dan massal,
perubahan kalender, penerbitan dan penonaktifan kartu, pembuatan dan penghapusan
akun, serta pengaktifan izin darurat alat. Tidak bisa dihapus atau diedit siapa pun.
Bisa disaring dan diekspor.

### Pemantauan kesehatan alat

Status tiap jalur beserta waktu terakhir membaca; peringatan bila satu jalur diam
lebih dari 15 menit di jam sibuk sementara jalur lain bekerja; status koneksi tiap
klien; jumlah scan yang masih mengantre di PC lobi.

### Mode input manual darurat

Kartu tertinggal pasti terjadi (5–10% per hari di awal). Operator mencari nama atau
NISN, satu klik, tercatat `MANUAL_ENTRY` sehingga tetap terlacak.

### Dashboard monitoring

Ada sejak peluncuran. Ringkasan hari ini, grafik harian/mingguan/bulanan,
perbandingan kedisiplinan antar kelas, tren keterlambatan, daftar siswa dengan alpa
terbanyak. Untuk kepala sekolah tersedia satu layar ringkas.

### Laporan dan keabsahannya

**Tidak ada fitur kunci bulan.** Koreksi boleh kapan saja, termasuk bulan yang
laporannya sudah terbit. Penggantinya tiga pengaman: arsip PDF otomatis setiap
laporan dicetak, cap waktu di setiap halaman, dan layar "Koreksi setelah laporan
terbit".

Laporan harian, mingguan, bulanan, per kelas, per siswa; ekspor Excel dan PDF
berkop sekolah. Identitas sekolah (nama, NPSN, alamat, logo, nama dan NIP kepala
sekolah) diisi sekali di pengaturan.

### Pergantian tahun ajaran

Wizard: arsipkan kelas lama, buat kelas baru, pindahkan siswa, tandai kelas IX lulus.
Siswa lulus berstatus LULUS, datanya tersimpan, kartunya dinonaktifkan. Strukturnya
harus benar sejak awal walau baru dipakai tahun depan.

---

## Struktur data inti

```
siswa          nama, nisn*, nipd, jk, tempat_lahir, tanggal_lahir,
               nik*, alamat, nama_ortu, kontak_ortu, kelas_id, status
guru           nama, nip, pangkat_jabatan, status
kelas          tingkat, nama_rombel, wali_kelas_id, tahun_ajaran, arsip
kartu          uid*, pemilik, status, tanggal_terbit
peristiwa_scan uid_mentah, uid, station_id, waktu, hasil   (permanen)
absensi        orang, tanggal, status_masuk, status_final,
               jam_masuk, jam_pulang, alasan, izin_pulang_cepat
koreksi        siapa, kapan, nilai_lama, nilai_baru, alasan, lampiran
tindakan_massal siapa, kapan, penyaring, jumlah_baris, alasan, dibatalkan
kalender       mulai, selesai, keterangan, jenis_siswa, jenis_guru, tahun_ajaran
notification_queue tujuan, template, status, idempotency_key,
                   waktu_kejadian, waktu_kirim
log_audit      pelaku, peran, waktu, jenis, rincian
```

---

## Stack teknis

```
Python 3.11+
PySide6              antarmuka
SQLAlchemy + SQLite  basis data (WAL), hanya di sisi server
Alembic              migrasi skema
FastAPI + uvicorn    layanan lokal di server
httpx                sisi klien
pyscard              pembaca RFID jalur PC/SC
pyserial             pembaca RFID jalur serial
APScheduler          cutoff, notifikasi, backup
PyQtGraph            grafik dashboard
argon2-cffi          hash kata sandi
PyInstaller          membungkus jadi .exe
```

Satu `.exe` untuk semua mesin; saat pertama dijalankan ditanya "Server atau Klien?".
Layanan server hanya terbuka di jaringan lokal, tidak pernah menghadap internet.
Tiap klien membawa token sendiri.

```
app/
├── ui/           layar PySide6
├── core/         mesin absensi, aturan status, kalender      [SUDAH ADA]
├── config/       pengaturan dan feature flag                 [SUDAH ADA]
├── data/         model SQLAlchemy, repositori
├── rfid/         base + normalisasi + kunci jalur            [SUDAH ADA]
│                 pengemudi pcsc/serial/hid/simulator
├── notify/       antrean dan penyedia WhatsApp
├── scheduler/    tugas terjadwal
└── server/       layanan FastAPI
tests/            38 pengujian aturan, jalan tanpa pustaka    [SUDAH ADA]
```

Jalankan pengujian: `python3 -m tests.test_mesin_absensi`

---

## Kebutuhan internet vs jaringan lokal

| Fitur | Internet | Jaringan lokal |
|---|---|---|
| Pembacaan kartu RFID | Tidak wajib | Tidak wajib |
| Penyimpanan absensi | Tidak wajib | Antrean lokal bila terputus |
| Dashboard dan laporan | Tidak wajib | Wajib |
| Koreksi absensi | Tidak wajib | Wajib |
| Pengiriman WhatsApp | Wajib | Wajib |
| Pencadangan daring | Wajib bila dipakai | Wajib |

---

## Hal yang belum diputuskan

- **Jalur reader yang dipasang** di sekolah (PC/SC atau serial) — aplikasi menerima
  keduanya, jadi ini tidak menghambat pembangunan
- **Desain dan jumlah kartu** yang dicetak
- **Kalender semester genap 2026/2027** (Januari–Juni)
- **Tanggal 23–25 Desember 2026** belum tercantum di agenda resmi sekolah, padahal
  25 Desember libur nasional. Perlu dipastikan, jangan ditebak
- **Sumber foto siswa** untuk layar scan dan kartu. Sementara layar menampilkan
  inisial nama
- **Layanan penyimpanan online** untuk sinkronisasi

---

## Di luar cakupan tahap awal

Pelacakan keluar-masuk di tengah hari, izin keluar sekolah, deteksi pulang lebih
awal, pengenalan wajah, aplikasi untuk orang tua, chatbot WhatsApp, pembayaran,
nilai akademik, GPS, dan dashboard daring penuh.

Sinkronisasi ke penyimpanan online dikerjakan paling akhir. Basis data di server TU
adalah sumber kebenaran.

---

## Jebakan yang sering terjadi

- Menghitung status dari jadwal terkini, bukan jadwal yang berlaku pada tanggal itu
- Menaruh logika status di dalam kode antarmuka sehingga tidak bisa diuji
- Membiarkan kegagalan WhatsApp membatalkan transaksi penyimpanan absensi
- Menulis 07:15, 07:30, atau 15:00 langsung di dalam kode
- Memakai waktu UTC untuk perbandingan jam sekolah
- Membuat layar absensi yang menuntut klik
- **Membiarkan beberapa PC membuka satu berkas SQLite di folder berbagi** — ini
  merusak basis data. Klien wajib lewat layanan server
- Menjalankan konversi ALPA pulang tanpa memeriksa ambang 30%
- Menyamakan "tidak pernah datang" dengan "tidak scan pulang" dalam satu kolom
- Melakukan koreksi massal tanpa penyaring status, sehingga menimpa siswa bersurat dokter
- Memperlakukan kalender sebagai modul terpisah dengan aturan sendiri — kalender
  adalah bagian dari keluarga koreksi massal

---

## Target Penerimaan Tahap 1

Checklist UAT resmi dari PRD (Bagian 20) — rujukan untuk uji end-to-end nanti,
bukan daftar status pengerjaan kode saat ini.

| Kelompok | Butir penerimaan |
|---|---|
| Perangkat | Reader terhubung, UID terbaca, kartu tidak dikenal terdeteksi, scan berulang tertangani, jalur terkunci berfungsi |
| Absensi siswa | Absen masuk dan pulang berjalan, hadir dan terlambat tepat, alpa ditetapkan sesuai cutoff, jadwal per hari berlaku |
| Absensi guru | Absen masuk berjalan, toleransi 09:30 berlaku, status manual dapat diinput |
| Kalender | Kalender siswa dan guru terpisah, hari libur tidak menghasilkan alpa |
| Koreksi | Koreksi satuan dan massal berjalan, seluruhnya beralasan dan dapat dibatalkan |
| Data master | Input manual siswa dan guru berjalan, pendaftaran kartu tergabung |
| Notifikasi | Antrean berjalan, empat template tersedia, mode uji berfungsi, tidak ada pengiriman ganda |
| Operasional | Dashboard, laporan, pencadangan, pemulihan, dan log audit tersedia |
