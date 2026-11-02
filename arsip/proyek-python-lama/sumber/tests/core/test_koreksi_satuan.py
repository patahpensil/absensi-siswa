"""Tes murni app.core.koreksi_satuan - tidak menyentuh basis data."""

import pytest

from app.core.koreksi_satuan import AksesKoreksiSatuanDitolak, pastikan_berwenang, terapkan_koreksi_satuan
from app.core.status import AlasanFinal, Peran, StatusSiswa


@pytest.mark.parametrize("peran", [Peran.KEPALA_SEKOLAH, Peran.ADMIN, Peran.WALI_KELAS])
def test_peran_berwenang_lolos(peran):
    pastikan_berwenang(peran)  # tidak melempar apa pun


def test_operator_ditolak():
    with pytest.raises(AksesKoreksiSatuanDitolak):
        pastikan_berwenang(Peran.OPERATOR)


def test_koreksi_dari_alpa_ke_izin():
    perubahan = terapkan_koreksi_satuan(
        status_final_sekarang=StatusSiswa.ALPA, alasan_final_sekarang=None, status_baru=StatusSiswa.IZIN
    )
    assert perubahan is not None
    assert perubahan.nilai_sebelum == {"status_final": "ALPA", "alasan_final": None}
    assert perubahan.nilai_sesudah == {"status_final": "IZIN", "alasan_final": "KOREKSI_MANUAL"}


def test_koreksi_dari_baris_belum_pernah_ada():
    """Siswa yang belum pernah scan (status_final_sekarang None) - input
    manual IZIN/SAKIT/DISPENSASI (PRD §10)."""
    perubahan = terapkan_koreksi_satuan(
        status_final_sekarang=None, alasan_final_sekarang=None, status_baru=StatusSiswa.SAKIT
    )
    assert perubahan is not None
    assert perubahan.nilai_sebelum == {"status_final": None, "alasan_final": None}
    assert perubahan.nilai_sesudah == {"status_final": "SAKIT", "alasan_final": "KOREKSI_MANUAL"}


def test_koreksi_menimpa_alasan_final_lama():
    """Baris yang sebelumnya sudah punya alasan_final lain (mis. konversi
    ALPA pulang) - koreksi satuan menimpanya dengan KOREKSI_MANUAL, tapi
    nilai LAMA tetap terekam di nilai_sebelum."""
    perubahan = terapkan_koreksi_satuan(
        status_final_sekarang=StatusSiswa.ALPA,
        alasan_final_sekarang=AlasanFinal.TIDAK_SCAN_PULANG,
        status_baru=StatusSiswa.HADIR,
    )
    assert perubahan.nilai_sebelum == {"status_final": "ALPA", "alasan_final": "TIDAK_SCAN_PULANG"}
    assert perubahan.nilai_sesudah == {"status_final": "HADIR", "alasan_final": "KOREKSI_MANUAL"}


def test_status_baru_sama_dengan_sekarang_tidak_ada_perubahan():
    perubahan = terapkan_koreksi_satuan(
        status_final_sekarang=StatusSiswa.HADIR, alasan_final_sekarang=None, status_baru=StatusSiswa.HADIR
    )
    assert perubahan is None
