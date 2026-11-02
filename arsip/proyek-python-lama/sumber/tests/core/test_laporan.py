"""Tes murni app.core.laporan - tidak menyentuh basis data."""

import pytest

from app.core.laporan import AksesLaporanDitolak, hitung_rekap_siswa, pastikan_berwenang
from app.core.status import Peran, StatusSiswa


@pytest.mark.parametrize("peran", [Peran.KEPALA_SEKOLAH, Peran.ADMIN, Peran.WALI_KELAS])
def test_peran_berwenang_lolos(peran):
    pastikan_berwenang(peran)  # tidak melempar apa pun


def test_operator_ditolak():
    """PRD §10: sel operator di baris "Dashboard dan laporan" hanya
    berisi "riwayat scan" - beda dari Riwayat Scan yang terbuka untuknya."""
    with pytest.raises(AksesLaporanDitolak):
        pastikan_berwenang(Peran.OPERATOR)


def test_hitung_rekap_siswa_tally_per_status():
    rekap = hitung_rekap_siswa(
        id_siswa=1,
        nama_lengkap="Budi Santoso",
        nama_kelas="VIII. PASCAL",
        daftar_status_final=["HADIR", "HADIR", "TERLAMBAT", "ALPA", "HADIR"],
    )
    assert rekap.jumlah["HADIR"] == 3
    assert rekap.jumlah["TERLAMBAT"] == 1
    assert rekap.jumlah["ALPA"] == 1
    assert rekap.jumlah["IZIN"] == 0
    assert rekap.jumlah_hari_tercatat == 5


def test_hitung_rekap_siswa_seluruh_status_selalu_ada_walau_nol():
    rekap = hitung_rekap_siswa(id_siswa=1, nama_lengkap="Budi", nama_kelas=None, daftar_status_final=["HADIR"])
    assert set(rekap.jumlah) == {s.value for s in StatusSiswa}


def test_hitung_rekap_siswa_status_none_tidak_masuk_tally_tapi_tetap_dihitung_hari():
    """Baris attendance tanpa status (None) tidak menambah tally jenis
    manapun, tapi tetap menambah jumlah_hari_tercatat - jujur soal
    jumlah baris yang dilihat."""
    rekap = hitung_rekap_siswa(id_siswa=1, nama_lengkap="Budi", nama_kelas=None, daftar_status_final=["HADIR", None])
    assert rekap.jumlah["HADIR"] == 1
    assert sum(rekap.jumlah.values()) == 1
    assert rekap.jumlah_hari_tercatat == 2
