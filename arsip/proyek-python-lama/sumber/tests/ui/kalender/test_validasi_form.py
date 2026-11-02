from datetime import date

from app.ui.kalender.validasi_form import validasi_entri, validasi_salin_tahun


def test_entri_valid():
    hasil = validasi_entri(
        mulai=date(2026, 12, 14), selesai=date(2026, 12, 19), keterangan="LHBPD", tahun_ajaran="2026/2027"
    )
    assert hasil.valid


def test_selesai_sebelum_mulai_galat():
    hasil = validasi_entri(
        mulai=date(2026, 12, 19), selesai=date(2026, 12, 14), keterangan="LHBPD", tahun_ajaran="2026/2027"
    )
    assert not hasil.valid
    assert "selesai" in hasil.galat


def test_keterangan_kosong_galat():
    hasil = validasi_entri(mulai=date(2026, 12, 14), selesai=date(2026, 12, 19), keterangan="  ", tahun_ajaran="2026/2027")
    assert not hasil.valid
    assert "keterangan" in hasil.galat


def test_salin_tahun_valid():
    hasil = validasi_salin_tahun(tahun_asal="2026/2027", tahun_tujuan="2027/2028")
    assert hasil.valid


def test_salin_tahun_sama_ditolak():
    hasil = validasi_salin_tahun(tahun_asal="2026/2027", tahun_tujuan="2026/2027")
    assert not hasil.valid
    assert "tahun_tujuan" in hasil.galat


def test_salin_tahun_asal_kosong_galat():
    hasil = validasi_salin_tahun(tahun_asal="", tahun_tujuan="2027/2028")
    assert not hasil.valid
    assert "tahun_asal" in hasil.galat
