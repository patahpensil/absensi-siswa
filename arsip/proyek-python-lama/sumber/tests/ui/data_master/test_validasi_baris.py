"""Tes murni app.ui.data_master.validasi_baris - tanpa Qt."""

from app.ui.data_master.validasi_baris import (
    cek_format_nik,
    cek_format_nisn,
    cek_format_nomor_whatsapp,
    validasi_baris_guru,
    validasi_baris_kelas,
    validasi_baris_siswa,
)

BARIS_SISWA_LENGKAP = {
    "nisn": "1234567890",
    "nama_lengkap": "Budi",
    "jenis_kelamin": "L",
    "tempat_lahir": "Pancarijang",
    "tanggal_lahir": "2012-05-01",
    "alamat": "Jl. Contoh No. 1",
    "nama_ortu": "Slamet",
    "nomor_whatsapp_ortu": "081234567890",
    "nik": "1234567890123456",
}


def test_baris_siswa_lengkap_valid():
    hasil = validasi_baris_siswa(BARIS_SISWA_LENGKAP, id_kelas_by_nama={})
    assert hasil.lengkap
    assert hasil.galat == {}
    assert hasil.payload["nisn"] == "1234567890"
    assert hasil.payload["nomor_whatsapp_ortu"] == "+6281234567890"
    assert hasil.payload["tanggal_lahir"] == "2012-05-01"
    assert hasil.payload["id_kelas"] is None


def test_baris_siswa_belum_lengkap_tidak_punya_payload():
    baris = dict(BARIS_SISWA_LENGKAP)
    del baris["alamat"]
    hasil = validasi_baris_siswa(baris, id_kelas_by_nama={})
    assert not hasil.lengkap
    assert "alamat" in hasil.galat


def test_baris_siswa_nisn_salah_format():
    baris = dict(BARIS_SISWA_LENGKAP, nisn="123")
    hasil = validasi_baris_siswa(baris, id_kelas_by_nama={})
    assert "nisn" in hasil.galat


def test_baris_siswa_kelas_ditemukan_lewat_nama():
    baris = dict(BARIS_SISWA_LENGKAP, nama_kelas="VII.PASCAL")
    hasil = validasi_baris_siswa(baris, id_kelas_by_nama={"VII.PASCAL": 5})
    assert hasil.lengkap
    assert hasil.payload["id_kelas"] == 5


def test_baris_siswa_kelas_tidak_ditemukan():
    baris = dict(BARIS_SISWA_LENGKAP, nama_kelas="TIDAK_ADA")
    hasil = validasi_baris_siswa(baris, id_kelas_by_nama={})
    assert not hasil.lengkap
    assert "nama_kelas" in hasil.galat


def test_baris_siswa_nik_kosong_boleh():
    baris = dict(BARIS_SISWA_LENGKAP)
    baris["nik"] = ""
    hasil = validasi_baris_siswa(baris, id_kelas_by_nama={})
    assert hasil.lengkap
    assert hasil.payload["nik"] is None


def test_cek_format_seketika_sel_kosong_tidak_galat():
    assert cek_format_nisn("") is None
    assert cek_format_nik("") is None
    assert cek_format_nomor_whatsapp("") is None


def test_cek_format_seketika_sel_salah_galat():
    assert cek_format_nisn("123") is not None
    assert cek_format_nik("123") is not None
    assert cek_format_nomor_whatsapp("123") is not None


def test_baris_guru_lengkap():
    hasil = validasi_baris_guru({"nama_lengkap": "Pak Anwar", "nip": "111", "pangkat_jabatan": "Pembina"})
    assert hasil.lengkap
    assert hasil.payload["nip"] == "111"


def test_baris_guru_honor_tanpa_nip():
    hasil = validasi_baris_guru({"nama_lengkap": "Guru Honor"})
    assert hasil.lengkap
    assert hasil.payload["nip"] is None


def test_baris_guru_nama_kosong_galat():
    hasil = validasi_baris_guru({"nama_lengkap": ""})
    assert not hasil.lengkap
    assert "nama_lengkap" in hasil.galat


def test_baris_kelas_lengkap():
    hasil = validasi_baris_kelas(
        {"tingkat": "7", "nama_kelas": "VII.PASCAL", "tahun_ajaran": "2026/2027"}, id_guru_by_nama={}
    )
    assert hasil.lengkap
    assert hasil.payload["tingkat"] == 7
    assert hasil.payload["wali_kelas_id"] is None


def test_baris_kelas_wali_ditemukan():
    hasil = validasi_baris_kelas(
        {"tingkat": "7", "nama_kelas": "VII.PASCAL", "tahun_ajaran": "2026/2027", "wali_kelas": "Bu Sri"},
        id_guru_by_nama={"Bu Sri": 9},
    )
    assert hasil.lengkap
    assert hasil.payload["wali_kelas_id"] == 9


def test_baris_kelas_tingkat_bukan_angka():
    hasil = validasi_baris_kelas(
        {"tingkat": "abc", "nama_kelas": "VII.PASCAL", "tahun_ajaran": "2026/2027"}, id_guru_by_nama={}
    )
    assert not hasil.lengkap
    assert "tingkat" in hasil.galat
