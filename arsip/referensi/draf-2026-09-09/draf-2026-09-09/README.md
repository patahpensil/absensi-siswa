# Sistem Informasi Berbasis Digital UPT SMP Negeri 1 Pancarijang

Aplikasi absensi RFID untuk siswa dan guru. Berjalan sebagai `.exe` di jaringan
lokal sekolah: satu server di ruang TU, klien di ruang kepala sekolah dan TU,
serta satu PC di lobi untuk titik scan.

## Menjalankan pengujian

Tidak butuh pustaka apa pun. Cukup Python 3.11 ke atas:

```bash
python3 -m tests.test_mesin_absensi
```

Seluruh aturan absensi diuji tanpa basis data, tanpa jendela, dan tanpa alat
RFID. Ini disengaja — aturan status harus bisa dibuktikan benar sebelum satu
reader pun dibeli.

## Yang sudah ada

```
app/config/pengaturan.py     seluruh nilai waktu, ambang, dan saklar modul
app/core/status.py           kosakata status, jenis hari, peran
app/core/mesin_absensi.py    seluruh aturan penentuan status
app/core/kalender.py         kalender akademik siswa & guru
app/rfid/base.py             ScanEvent, normalisasi UID, kunci jalur alat
tests/                       38 pengujian aturan
```

## Yang belum dikerjakan

- `app/data/` model SQLAlchemy dan repositori
- `app/rfid/` pengemudi PC/SC, serial, HID, dan simulator
- `app/server/` layanan FastAPI untuk klien
- `app/ui/` layar PySide6: absensi, input data, koreksi, kalender, dashboard
- `app/notify/` antrean dan penyedia WhatsApp
- `app/scheduler/` cutoff, notifikasi, backup

## Aturan yang sudah dikodekan

| Aturan | Nilai |
|---|---|
| Hadir | scan sampai 07:20:59 |
| Terlambat | scan mulai 07:21:00 |
| Alpa pagi | tidak scan sampai 07:30 |
| Jendela pulang | 13:20–15:00; Jumat 11:00–14:00 |
| Tidak scan pulang | ALPA, dengan pengecualian izin/sakit/dispensasi dan izin pulang cepat |
| Toleransi guru | 09:30, guru tidak mengenal ALPA |
| Anti-duplikat | 10 detik, berlaku global per UID lintas jalur |
| Kalender | terpisah siswa dan guru |
| Kunci jalur alat | alat beda jalur ditolak, jalur lain tetap jalan |

Tidak ada satu pun angka di atas yang ditulis tetap di dalam logika. Semuanya
dibaca dari `Pengaturan`, yang nantinya tersimpan di basis data dan diubah
lewat menu admin.

## Prinsip yang tidak boleh dilanggar

1. `app/core/` tidak pernah mengimpor dari `app/ui/` maupun `app/data/`.
2. Peristiwa scan mentah disimpan permanen dan tidak pernah diubah.
3. Koreksi menghasilkan baris baru berisi nilai lama, pelaku, waktu, dan alasan.
4. Absensi tercatat lebih dulu; notifikasi menyusul dalam transaksi terpisah.
5. Seluruh perbandingan waktu memakai waktu lokal WITA, tidak pernah UTC.
