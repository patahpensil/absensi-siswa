"""Tes validasi_form murni Koreksi Massal - tanpa Qt."""

from datetime import date

from app.ui.koreksi_massal.validasi_form import bangun_penyaring, parse_id_siswa_terpilih, validasi_penyaring

MULAI = date(2026, 9, 1)
SELESAI = date(2026, 9, 5)


def _dasar(**kwargs) -> dict:
    dasar = dict(
        tanggal_mulai=MULAI,
        tanggal_selesai=SELESAI,
        lingkup="SEKOLAH",
        tingkat_teks="",
        id_kelas_teks="",
        id_siswa_terpilih_teks="",
        status_termasuk=["ALPA"],
    )
    dasar.update(kwargs)
    return dasar


def test_parse_id_siswa_terpilih_normal():
    assert parse_id_siswa_terpilih("3, 5,9") == [3, 5, 9]


def test_parse_id_siswa_terpilih_kosong_none():
    assert parse_id_siswa_terpilih("") is None
    assert parse_id_siswa_terpilih("   ") is None


def test_parse_id_siswa_terpilih_bukan_angka_none():
    assert parse_id_siswa_terpilih("3, abc") is None


def test_penyaring_sekolah_valid():
    hasil = validasi_penyaring(**_dasar())
    assert hasil.valid


def test_tanggal_selesai_sebelum_mulai_tidak_valid():
    hasil = validasi_penyaring(**_dasar(tanggal_mulai=SELESAI, tanggal_selesai=MULAI))
    assert not hasil.valid
    assert "tanggal" in hasil.galat


def test_status_kosong_tidak_valid():
    hasil = validasi_penyaring(**_dasar(status_termasuk=[]))
    assert not hasil.valid
    assert "status_termasuk" in hasil.galat


def test_lingkup_tingkat_wajib_angka():
    hasil = validasi_penyaring(**_dasar(lingkup="TINGKAT", tingkat_teks=""))
    assert not hasil.valid
    assert "tingkat" in hasil.galat

    hasil_ok = validasi_penyaring(**_dasar(lingkup="TINGKAT", tingkat_teks="8"))
    assert hasil_ok.valid


def test_lingkup_kelas_wajib_angka():
    hasil = validasi_penyaring(**_dasar(lingkup="KELAS", id_kelas_teks="abc"))
    assert not hasil.valid
    assert "id_kelas" in hasil.galat


def test_lingkup_siswa_terpilih_wajib_daftar_id():
    hasil = validasi_penyaring(**_dasar(lingkup="SISWA_TERPILIH", id_siswa_terpilih_teks=""))
    assert not hasil.valid
    assert "id_siswa_terpilih" in hasil.galat

    hasil_ok = validasi_penyaring(**_dasar(lingkup="SISWA_TERPILIH", id_siswa_terpilih_teks="1,2"))
    assert hasil_ok.valid


def test_bangun_penyaring_sekolah():
    penyaring = bangun_penyaring(**_dasar())
    assert penyaring == {
        "tanggal_mulai": "2026-09-01",
        "tanggal_selesai": "2026-09-05",
        "lingkup": "SEKOLAH",
        "tingkat": None,
        "id_kelas": None,
        "id_siswa_terpilih": [],
        "status_termasuk": ["ALPA"],
    }


def test_bangun_penyaring_siswa_terpilih():
    penyaring = bangun_penyaring(**_dasar(lingkup="SISWA_TERPILIH", id_siswa_terpilih_teks="3, 5"))
    assert penyaring["id_siswa_terpilih"] == [3, 5]
    assert penyaring["tingkat"] is None
    assert penyaring["id_kelas"] is None
