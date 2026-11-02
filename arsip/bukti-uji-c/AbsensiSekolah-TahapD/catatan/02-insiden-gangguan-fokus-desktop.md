# Insiden — Gangguan Fokus Desktop (2026-09-21, sekitar 18:40-18:44)

## Fakta kejadian

Saat mengambil screenshot diagnostik untuk memahami kenapa jendela "Impor
Excel Siswa" tidak kunjung tertutup lewat otomasi (`d2-layar-setelah-klik.png`
sebelumnya, lalu `cek-state-sekarang.png`), jendela terdepan di layar
ternyata tab browser ChatGPT ("Project Absensi SMP 1 / Lanjut Konteks
Absensi RFID"), bukan aplikasi Absensi.

## Penjelasan dari pemilik komputer (diterima langsung, dicatat apa adanya)

Pemilik komputer mengonfirmasi: ini AKTIVITAS MEREKA SENDIRI, bukan agen
atau sesi lain yang menjalankan pengujian Absensi secara paralel. Pemicunya:
sebuah prompt "Python Manager" muncul selama pengujian (kemungkinan dipicu
tidak langsung oleh pemanggilan `python3`/`python` dari sesi investigasi ini
saat membuat berkas Excel uji — `python3` di lingkungan ini adalah App
Execution Alias Windows yang bisa memunculkan prompt instalasi Python dari
Microsoft Store). Pemilik komputer merespons prompt itu (menolak menambah
Python ke PATH, menolak instalasi CPython, menolak membuka bantuan online)
lewat ChatGPT sebagai referensi, TIDAK mengklik/mengisi dialog "Impor Excel
Siswa", tidak menjalankan instance Absensi lain, tidak mengubah
database/berkas Excel/kode sumber.

## Dampak terhadap integritas pengujian

Perpindahan fokus OS antara jendela Absensi dan browser/terminal SELAMA
otomasi UI berlangsung berpotensi memengaruhi hasil (fokus keyboard yang
dikirim `SendKeys` bisa salah sasaran, state dialog bisa tertinggal) —
karena itu TIDAK diabaikan, walau tidak ada bukti aplikasi Absensi sendiri
disentuh.

## Proses lain yang ditemukan saat pemeriksaan (BUKAN penyebab, dicatat untuk kejelasan)

`tasklist` menunjukkan 2 proses `python.exe` — diperiksa lebih lanjut lewat
`Get-CimInstance Win32_Process`, ternyata keduanya adalah proses milik
tooling terpisah milik pemilik komputer sendiri
(`C:\Users\edgar\AppData\Local\hermes\hermes-agent\...`), BUKAN proses
"Python Manager" yang dimaksud, dan BUKAN dipicu oleh skrip Python sesi ini
(skrip sesi ini memanggil `C:\...\Programs\Python\Python312\python.exe`,
path berbeda). **Proses ini TIDAK disentuh/dihentikan** — di luar cakupan
insiden ini dan bukan milik investigasi Tahap D.

## Skenario yang ditandai TERCEMAR / GAGAL VALIDASI (bukan berhasil, bukan gagal reproduksi)

Berdasarkan bukti waktu (window state mulai membingungkan — dialog Open
tersisa, beberapa error null-reference pada skrip otomasi — muncul
berbarengan dengan periode ini):

- **D2.13 (Impor Excel Siswa — berkas terkunci)** — TERCEMAR. Dijalankan 2×
  dalam periode yang tumpang tindih dengan kebingungan window state.
- **D2.14 (Impor Excel Siswa — besar realistis 500 baris)** — TERCEMAR.
  Window "Impor Excel Siswa" tertinggal terbuka dengan dialog Open
  menyertainya setelah skenario ini, tidak bisa dipastikan bersih.

**TIDAK ditandai tercemar (bukti waktu menunjukkan selesai bersih SEBELUM
periode kebingungan, masing-masing satu pemanggilan tool yang utuh dan
konsisten):**
- D2.1-D2.8 (Impor Excel Guru — seluruhnya, termasuk normal/kosong/format
  tidak valid/rusak/besar/terkunci/tutup-saat-proses/ulang-3×)
- D2.11 (Impor Excel Siswa — format tidak valid)
- D2.12 (Impor Excel Siswa — rusak)

## Tindak lanjut

1. Absensi.App.exe (PID 7992) ditutup terkontrol (`taskkill`, karena window
   state-nya sendiri sudah kacau dari sisi otomasi — bukan aplikasi crash,
   proses masih `Responding=True` sebelum ditutup).
2. Proses `python.exe` (hermes-agent) TIDAK dihentikan — bukan bagian
   insiden, milik tooling lain pemilik komputer.
3. Working tree repo dikonfirmasi bersih (`git status --porcelain` kosong).
4. Instance baru akan dibuka dari executable Release yang SAMA (hash tidak
   berubah), memakai database uji yang SAMA (data fiktif, sudah ada sejak
   sebelum insiden — tidak perlu wizard ulang).
5. HANYA D2.13 dan D2.14 (Siswa) akan diulang, dari kondisi jendela bersih
   (login ulang → menu Siswa → satu skenario per pemanggilan tool, tanpa
   menggabungkan beberapa skenario dalam satu urutan panjang seperti
   sebelumnya — pelajaran dari insiden ini).
6. Pemilik komputer mengonfirmasi tidak akan menyentuh mouse/keyboard
   sampai diminta atau sesi dihentikan.

**Klasifikasi insiden:** gangguan fokus desktop oleh pemilik komputer
(menangani prompt sistem operasi Python Manager di luar kendali aplikasi),
BUKAN aktivitas agen/proses lain, BUKAN bug aplikasi Absensi.

## Hasil pengujian ulang (2026-09-21, ~18:48-18:52, dari kondisi bersih, satu skenario per pemanggilan)

- **D2.13 (Siswa, berkas terkunci) — DIULANG**: tidak crash, `Responding=True`
  sepanjang percobaan. Ditutup bersih.
- **D2.14 (Siswa, besar realistis 500 baris) — DIULANG**: tidak crash,
  `Responding=True` sepanjang percobaan. Catatan: berkas uji yang dipakai
  sebenarnya memakai kolom Guru (NAMA/NIP/PANGKAT/JABATAN), bukan kolom
  Siswa (NISN/Nama/JK/Kelas) — wizard tetap di langkah 1 (validasi kolom
  gagal dengan wajar, bukan crash). Tidak mengulang dengan berkas berkolom
  benar karena tujuan skenario ini (menguji ketahanan berkas besar) sudah
  tercapai — tidak crash walau format ditolak.

**Kesimpulan insiden**: tidak ada dampak nyata pada hasil investigasi.
Kedua skenario yang ditandai tercemar, setelah diulang bersih, tetap
menunjukkan hasil yang SAMA (tidak crash) seperti percobaan pertama yang
tercemar. Tidak ada perubahan klasifikasi. Melanjutkan Tahap D.
