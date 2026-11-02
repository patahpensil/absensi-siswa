from datetime import time

from app.ui.pengaturan.validasi_jadwal import validasi_jadwal


def test_jadwal_siswa_valid():
    hasil = validasi_jadwal(
        jenis_pemilik="SISWA",
        jam_masuk=time(7, 15),
        batas_terlambat=time(7, 20, 59),
        jam_pulang=time(15, 0),
        cutoff_alpa=time(7, 30),
        pulang_mulai=time(13, 20),
    )
    assert hasil.valid


def test_batas_terlambat_sebelum_jam_masuk_galat():
    hasil = validasi_jadwal(
        jenis_pemilik="SISWA", jam_masuk=time(7, 15), batas_terlambat=time(7, 0), jam_pulang=time(15, 0)
    )
    assert not hasil.valid
    assert "batas_terlambat" in hasil.galat


def test_cutoff_alpa_sebelum_batas_terlambat_galat():
    hasil = validasi_jadwal(
        jenis_pemilik="SISWA",
        jam_masuk=time(7, 15),
        batas_terlambat=time(7, 20, 59),
        jam_pulang=time(15, 0),
        cutoff_alpa=time(7, 10),
    )
    assert not hasil.valid
    assert "cutoff_alpa" in hasil.galat


def test_pulang_mulai_setelah_jam_pulang_galat():
    hasil = validasi_jadwal(
        jenis_pemilik="SISWA",
        jam_masuk=time(7, 15),
        batas_terlambat=time(7, 20, 59),
        jam_pulang=time(15, 0),
        pulang_mulai=time(16, 0),
    )
    assert not hasil.valid
    assert "pulang_mulai" in hasil.galat


def test_jadwal_guru_tidak_memeriksa_cutoff_alpa_maupun_pulang_mulai():
    hasil = validasi_jadwal(
        jenis_pemilik="GURU", jam_masuk=time(7, 15), batas_terlambat=time(9, 30), jam_pulang=time(15, 0)
    )
    assert hasil.valid
