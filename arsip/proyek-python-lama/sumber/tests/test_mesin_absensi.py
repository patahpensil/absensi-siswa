"""
Pengujian aturan absensi.

Ditulis tanpa pytest supaya bisa dijalankan langsung dengan `python3 -m tests.test_mesin_absensi`
di komputer mana pun, termasuk PC sekolah yang belum dipasangi apa-apa.
"""

import sys
from datetime import date, datetime, time

from app.config.pengaturan import JUMAT, SENIN, Pengaturan
from app.core import mesin_absensi as m
from app.core.kalender import kalender_ganjil_2026_2027
from app.core.status import AlasanFinal, HasilScan, JenisHari, StatusGuru, StatusSiswa
from app.rfid.base import FormatUid, normalisasi_uid

P = Pengaturan()
JADWAL = P.jadwal_untuk(SENIN)
JADWAL_JUMAT = P.jadwal_untuk(JUMAT)
HARI = date(2026, 9, 14)  # Senin

lulus = 0
gagal = []


def cek(nama, syarat):
    global lulus
    if syarat:
        lulus += 1
    else:
        gagal.append(nama)


def waktu(jam, menit, detik=0):
    return datetime.combine(HARI, time(jam, menit, detik))


# --- batas terlambat -------------------------------------------------------

cek("07:14 hadir", m.tentukan_status_masuk(waktu(7, 14), JADWAL) == StatusSiswa.HADIR)
cek("07:20:59 masih hadir",
    m.tentukan_status_masuk(waktu(7, 20, 59), JADWAL) == StatusSiswa.HADIR)
cek("07:21:00 terlambat",
    m.tentukan_status_masuk(waktu(7, 21, 0), JADWAL) == StatusSiswa.TERLAMBAT)
cek("07:29 terlambat",
    m.tentukan_status_masuk(waktu(7, 29), JADWAL) == StatusSiswa.TERLAMBAT)

# --- cutoff alpa pagi ------------------------------------------------------

baris = m.BarisAbsensi(orang_id=1, tanggal=HARI)
m.terapkan_cutoff_alpa(baris, JenisHari.EFEKTIF)
cek("tanpa scan jadi alpa", baris.status_masuk == StatusSiswa.ALPA)

izin = m.BarisAbsensi(orang_id=2, tanggal=HARI, status_masuk=StatusSiswa.SAKIT)
m.terapkan_cutoff_alpa(izin, JenisHari.EFEKTIF)
cek("sakit tidak jadi alpa", izin.status_masuk == StatusSiswa.SAKIT)

libur = m.BarisAbsensi(orang_id=3, tanggal=HARI)
m.terapkan_cutoff_alpa(libur, JenisHari.LIBUR_NASIONAL)
cek("hari libur tidak menetapkan alpa", libur.status_masuk is None)

# --- sudah alpa lalu datang ------------------------------------------------

telat = m.BarisAbsensi(orang_id=4, tanggal=HARI, status_masuk=StatusSiswa.ALPA)
cek("alpa lalu datang, status tidak berubah sendiri",
    telat.status_masuk == StatusSiswa.ALPA)

# --- klasifikasi scan ------------------------------------------------------

s = m.ScanMasuk(orang_id=5, waktu=waktu(7, 10), station_id="1")
cek("scan pagi jadi absen masuk",
    m.klasifikasi_scan(s, JADWAL, JenisHari.EFEKTIF, False, None, P)
    == HasilScan.ABSEN_MASUK)

s2 = m.ScanMasuk(orang_id=5, waktu=waktu(13, 45), station_id="1")
cek("scan sore jadi absen pulang",
    m.klasifikasi_scan(s2, JADWAL, JenisHari.EFEKTIF, True, None, P)
    == HasilScan.ABSEN_PULANG)

s3 = m.ScanMasuk(orang_id=5, waktu=waktu(13, 45), station_id="1")
cek("absen pulang tanpa absen masuk ditolak",
    m.klasifikasi_scan(s3, JADWAL, JenisHari.EFEKTIF, False, None, P)
    == HasilScan.DI_LUAR_JENDELA)

s4 = m.ScanMasuk(orang_id=5, waktu=waktu(10, 0), station_id="1")
cek("scan tengah hari di luar jendela",
    m.klasifikasi_scan(s4, JADWAL, JenisHari.EFEKTIF, True, None, P)
    == HasilScan.DI_LUAR_JENDELA)

s5 = m.ScanMasuk(orang_id=5, waktu=waktu(7, 10, 5), station_id="3")
cek("tap kedua di jalur lain dalam 10 detik dianggap duplikat",
    m.klasifikasi_scan(s5, JADWAL, JenisHari.EFEKTIF, False, waktu(7, 10, 0), P)
    == HasilScan.DUPLIKAT)

s6 = m.ScanMasuk(orang_id=5, waktu=waktu(7, 10, 15), station_id="3")
cek("tap 15 detik kemudian bukan duplikat",
    m.klasifikasi_scan(s6, JADWAL, JenisHari.EFEKTIF, False, waktu(7, 10, 0), P)
    != HasilScan.DUPLIKAT)

# --- jendela pulang hari Jumat --------------------------------------------

sj = m.ScanMasuk(orang_id=6, waktu=waktu(11, 30), station_id="1")
cek("Jumat 11:30 masuk jendela pulang",
    m.klasifikasi_scan(sj, JADWAL_JUMAT, JenisHari.EFEKTIF, True, None, P)
    == HasilScan.ABSEN_PULANG)
cek("Senin 11:30 di luar jendela pulang",
    m.klasifikasi_scan(sj, JADWAL, JenisHari.EFEKTIF, True, None, P)
    == HasilScan.DI_LUAR_JENDELA)

# --- cutoff pulang ---------------------------------------------------------

def buat(n, punya_pulang, status=StatusSiswa.HADIR, izin_cepat=False):
    return [
        m.BarisAbsensi(
            orang_id=100 + i,
            tanggal=HARI,
            status_masuk=status,
            status_final=status,
            jam_pulang=waktu(13, 40) if punya_pulang else None,
            izin_pulang_cepat=izin_cepat,
        )
        for i in range(n)
    ]

daftar = buat(90, True) + buat(10, False)
hasil_daftar, hasil = m.jalankan_cutoff_pulang(daftar, P)
tidak_pulang = [b for b in hasil_daftar if b.status_final == StatusSiswa.ALPA]
cek("10% tidak scan pulang jadi alpa", len(tidak_pulang) == 10)
cek("alasannya tercatat",
    tidak_pulang[0].alasan_final == AlasanFinal.TIDAK_SCAN_PULANG)
cek("status_masuk tetap HADIR",
    tidak_pulang[0].status_masuk == StatusSiswa.HADIR)
cek("konversi tidak ditahan di 10%", hasil.ditahan is False)

banyak = buat(40, True) + buat(60, False)
hasil_banyak, cek_banyak = m.jalankan_cutoff_pulang(banyak, P)
cek("60% tidak scan pulang menahan konversi", cek_banyak.ditahan is True)
cek("konversi tidak dijalankan saat ditahan",
    all(b.status_final == StatusSiswa.HADIR for b in hasil_banyak))

hasil_paksa, _ = m.jalankan_cutoff_pulang(banyak, P, paksa=True)
cek("konversi berjalan setelah admin memaksa",
    sum(1 for b in hasil_paksa if b.status_final == StatusSiswa.ALPA) == 60)

sakit = buat(5, False, status=StatusSiswa.SAKIT)
m.jalankan_cutoff_pulang(sakit, P)
cek("sakit tidak terkena alpa pulang",
    all(b.status_final == StatusSiswa.SAKIT for b in sakit))

cepat = buat(5, False, izin_cepat=True)
m.jalankan_cutoff_pulang(cepat, P)
cek("izin pulang cepat dikecualikan",
    all(b.status_final == StatusSiswa.HADIR for b in cepat))

# --- guru ------------------------------------------------------------------

cek("guru 09:00 hadir",
    m.tentukan_status_masuk_guru(waktu(9, 0), P) == StatusGuru.HADIR)
cek("guru 09:30 masih hadir",
    m.tentukan_status_masuk_guru(waktu(9, 30), P) == StatusGuru.HADIR)
cek("guru 09:31 terlambat",
    m.tentukan_status_masuk_guru(waktu(9, 31), P) == StatusGuru.TERLAMBAT)

# --- kalender --------------------------------------------------------------

kal = kalender_ganjil_2026_2027()
cek("14 Des siswa libur",
    kal.jenis_hari_siswa(date(2026, 12, 14)) == JenisHari.KEGIATAN_TANPA_KBM)
cek("14 Des guru efektif",
    kal.jenis_hari_guru(date(2026, 12, 14)) == JenisHari.EFEKTIF)
cek("17 Agustus libur nasional",
    kal.jenis_hari_siswa(date(2026, 8, 17)) == JenisHari.LIBUR_NASIONAL)
cek("hari biasa efektif",
    kal.jenis_hari_siswa(date(2026, 9, 14)) == JenisHari.EFEKTIF)
cek("Minggu selalu libur",
    kal.jenis_hari_siswa(date(2026, 9, 13)) == JenisHari.LIBUR_NASIONAL)
cek("sumatif tetap hari efektif jam normal",
    kal.jenis_hari_siswa(date(2026, 9, 22)) == JenisHari.EFEKTIF)

# --- normalisasi UID -------------------------------------------------------

cek("hex apa adanya", normalisasi_uid("04a2b3c4") == "04A2B3C4")
cek("desimal jadi hex",
    normalisasi_uid("3283994628", FormatUid.DESIMAL) == "C3BDC804")
cek("urutan byte dibalik",
    normalisasi_uid("04A2B3C4", balik_urutan_byte=True) == "C4B3A204")
cek("spasi dan pemisah dibuang",
    normalisasi_uid("04:A2 B3-C4") == "04A2B3C4")


# --- hasil -----------------------------------------------------------------

print(f"Lulus: {lulus}")
if gagal:
    print(f"Gagal: {len(gagal)}")
    for g in gagal:
        print("  -", g)
    sys.exit(1)
print("Semua aturan berjalan sesuai keputusan yang dikunci.")
