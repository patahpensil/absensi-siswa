"""Tes murni app.core.dashboard (PRD §14.1) - bucketing grafik, tren,
perbandingan kelas, alpa terbanyak, dan penilaian status operasional.
Tidak menyentuh basis data, tidak menyentuh app.core.engine/mesin_absensi."""

from datetime import date, datetime

import pytest

from app.core.dashboard import (
    Granularitas,
    alpa_terbanyak_dari_baris,
    grafik_dari_seri,
    perbandingan_kelas_dari_baris,
    ringkasan_dari_seri,
    status_antrean_dari_baris,
    status_jalur_dari_peta,
    status_klien_dari_sesi,
    susun_seri_harian,
    tren_status_dari_seri,
)
from app.core.laporan import AksesLaporanDitolak, pastikan_berwenang
from app.core.status import Peran
from app.rfid.base import JalurAlat

SEKARANG = datetime(2026, 9, 11, 10, 0, 0)


# --------------------------------------------------------------- kewenangan (dipakai ulang dari laporan)


def test_dashboard_memakai_ulang_kewenangan_laporan():
    pastikan_berwenang(Peran.WALI_KELAS)  # tidak melempar
    with pytest.raises(AksesLaporanDitolak):
        pastikan_berwenang(Peran.OPERATOR)


# --------------------------------------------------------------- seri harian + ringkasan


def test_susun_seri_harian_mengabaikan_status_none():
    baris = [
        (date(2026, 9, 11), "HADIR", 20),
        (date(2026, 9, 11), "TERLAMBAT", 3),
        (date(2026, 9, 11), None, 1),
    ]
    seri = susun_seri_harian(baris)
    assert seri[date(2026, 9, 11)] == {"HADIR": 20, "TERLAMBAT": 3}


def test_ringkasan_dari_seri_hari_tanpa_data():
    seri = susun_seri_harian([(date(2026, 9, 10), "HADIR", 5)])
    ringkasan = ringkasan_dari_seri(seri, tanggal=date(2026, 9, 11))
    assert ringkasan.per_status == {}
    assert ringkasan.total_tercatat == 0


def test_ringkasan_dari_seri_menjumlah_total():
    seri = susun_seri_harian([(date(2026, 9, 11), "HADIR", 20), (date(2026, 9, 11), "TERLAMBAT", 3)])
    ringkasan = ringkasan_dari_seri(seri, tanggal=date(2026, 9, 11))
    assert ringkasan.total_tercatat == 23


# --------------------------------------------------------------- grafik: harian/mingguan/bulanan


def test_grafik_harian_satu_titik_per_tanggal():
    seri = susun_seri_harian(
        [(date(2026, 9, 11), "HADIR", 20), (date(2026, 9, 12), "HADIR", 18)]
    )
    titik = grafik_dari_seri(seri, granularitas=Granularitas.HARIAN)
    assert [t.periode for t in titik] == ["2026-09-11", "2026-09-12"]
    assert titik[0].per_status == {"HADIR": 20}


def test_grafik_mingguan_menjumlah_dalam_minggu_sama():
    # Senin 7 Sep - Minggu 13 Sep 2026 satu minggu ISO yang sama
    seri = susun_seri_harian(
        [
            (date(2026, 9, 7), "HADIR", 20),   # Senin
            (date(2026, 9, 9), "HADIR", 18),   # Rabu, minggu sama
            (date(2026, 9, 14), "HADIR", 22),  # Senin minggu berikutnya
        ]
    )
    titik = grafik_dari_seri(seri, granularitas=Granularitas.MINGGUAN)
    assert titik[0].periode == "2026-09-07"
    assert titik[0].per_status == {"HADIR": 38}
    assert titik[1].periode == "2026-09-14"
    assert titik[1].per_status == {"HADIR": 22}


def test_grafik_bulanan_menjumlah_dalam_bulan_sama():
    seri = susun_seri_harian(
        [
            (date(2026, 9, 1), "TERLAMBAT", 2),
            (date(2026, 9, 28), "TERLAMBAT", 4),
            (date(2026, 10, 1), "TERLAMBAT", 1),
        ]
    )
    titik = grafik_dari_seri(seri, granularitas=Granularitas.BULANAN)
    assert titik[0].periode == "2026-09"
    assert titik[0].per_status == {"TERLAMBAT": 6}
    assert titik[1].periode == "2026-10"
    assert titik[1].per_status == {"TERLAMBAT": 1}


# --------------------------------------------------------------- tren keterlambatan


def test_tren_status_mengisi_nol_untuk_hari_tanpa_data():
    seri = susun_seri_harian([(date(2026, 9, 11), "TERLAMBAT", 3)])
    tren = tren_status_dari_seri(
        seri, status="TERLAMBAT", tanggal_mulai=date(2026, 9, 10), tanggal_selesai=date(2026, 9, 12)
    )
    assert tren == [
        (date(2026, 9, 10), 0),
        (date(2026, 9, 11), 3),
        (date(2026, 9, 12), 0),
    ]


# --------------------------------------------------------------- perbandingan kelas


def test_perbandingan_kelas_kelompokkan_dan_beri_label_belum_ditempatkan():
    baris = [
        (1, "7A", "HADIR", 25),
        (1, "7A", "TERLAMBAT", 2),
        (2, "7B", "HADIR", 20),
        (None, None, "HADIR", 3),  # siswa tanpa kelas
    ]
    hasil = perbandingan_kelas_dari_baris(baris)
    assert [h.nama_kelas for h in hasil] == ["7A", "7B", "Belum ditempatkan"]
    kelas_7a = next(h for h in hasil if h.nama_kelas == "7A")
    assert kelas_7a.per_status == {"HADIR": 25, "TERLAMBAT": 2}
    assert kelas_7a.total_tercatat == 27


# --------------------------------------------------------------- alpa terbanyak


def test_alpa_terbanyak_mempertahankan_urutan_dan_label_kelas():
    baris = [(5, "Budi", "7A", 4), (9, "Siti", None, 3)]
    hasil = alpa_terbanyak_dari_baris(baris)
    assert hasil[0].nama_lengkap == "Budi"
    assert hasil[0].jumlah_alpa == 4
    assert hasil[1].nama_kelas == "Belum ditempatkan"


# --------------------------------------------------------------- status operasional: jalur


def test_status_jalur_diam_setelah_15_menit():
    peta = {
        JalurAlat.PCSC: datetime(2026, 9, 11, 9, 40, 0),  # 20 menit lalu -> diam
        JalurAlat.SERIAL: datetime(2026, 9, 11, 9, 50, 0),  # 10 menit lalu -> tidak diam
    }
    hasil = status_jalur_dari_peta(peta, sekarang=SEKARANG)
    per_jalur = {h.jalur: h for h in hasil}
    assert per_jalur["PCSC"].diam is True
    assert per_jalur["SERIAL"].diam is False


def test_status_jalur_hanya_menampilkan_jalur_yang_pernah_membaca():
    hasil = status_jalur_dari_peta({JalurAlat.PCSC: SEKARANG}, sekarang=SEKARANG)
    assert len(hasil) == 1
    assert hasil[0].jalur == "PCSC"


# --------------------------------------------------------------- status operasional: klien


def test_status_klien_diam_setelah_15_menit():
    daftar = [
        ("operator_lobi", "OPERATOR", datetime(2026, 9, 11, 9, 40, 0)),
        ("admin", "ADMIN", datetime(2026, 9, 11, 9, 58, 0)),
    ]
    hasil = status_klien_dari_sesi(daftar, sekarang=SEKARANG)
    per_user = {h.username: h for h in hasil}
    assert per_user["operator_lobi"].diam is True
    assert per_user["admin"].diam is False


# --------------------------------------------------------------- status operasional: antrean lobi


def test_status_antrean_kedaluwarsa_setelah_5_menit():
    daftar = [
        ("LOBI-1", 3, datetime(2026, 9, 11, 9, 54, 0)),  # 6 menit lalu -> kedaluwarsa
        ("LOBI-2", 0, datetime(2026, 9, 11, 9, 58, 0)),  # 2 menit lalu -> segar
    ]
    hasil = status_antrean_dari_baris(daftar, sekarang=SEKARANG)
    per_station = {h.station_id: h for h in hasil}
    assert per_station["LOBI-1"].kedaluwarsa is True
    assert per_station["LOBI-2"].kedaluwarsa is False
    assert per_station["LOBI-2"].jumlah_pending == 0
