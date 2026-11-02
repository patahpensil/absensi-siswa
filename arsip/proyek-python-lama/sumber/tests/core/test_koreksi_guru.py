"""Tes murni app.core.koreksi_guru - tidak menyentuh basis data."""

import pytest

from app.core.koreksi_guru import (
    AksesKoreksiGuruDitolak,
    StatusBukanManual,
    pastikan_berwenang,
    pastikan_status_manual,
    terapkan_koreksi_guru,
)
from app.core.status import AlasanFinal, Peran, StatusGuru


@pytest.mark.parametrize("peran", [Peran.KEPALA_SEKOLAH, Peran.ADMIN])
def test_peran_berwenang_lolos(peran):
    pastikan_berwenang(peran)  # tidak melempar apa pun


@pytest.mark.parametrize("peran", [Peran.WALI_KELAS, Peran.OPERATOR])
def test_wali_kelas_dan_operator_ditolak(peran):
    with pytest.raises(AksesKoreksiGuruDitolak):
        pastikan_berwenang(peran)


@pytest.mark.parametrize(
    "status_baru", [StatusGuru.DINAS_LUAR, StatusGuru.IZIN, StatusGuru.SAKIT, StatusGuru.CUTI]
)
def test_status_manual_lolos(status_baru):
    pastikan_status_manual(status_baru)  # tidak melempar apa pun


@pytest.mark.parametrize("status_baru", [StatusGuru.HADIR, StatusGuru.TERLAMBAT])
def test_hadir_dan_terlambat_ditolak_sebagai_target_manual(status_baru):
    with pytest.raises(StatusBukanManual):
        pastikan_status_manual(status_baru)


def test_koreksi_dari_hadir_ke_sakit():
    perubahan = terapkan_koreksi_guru(
        status_final_sekarang=StatusGuru.HADIR, alasan_final_sekarang=None, status_baru=StatusGuru.SAKIT
    )
    assert perubahan is not None
    assert perubahan.nilai_sebelum == {"status_final": "HADIR", "alasan_final": None}
    assert perubahan.nilai_sesudah == {"status_final": "SAKIT", "alasan_final": "KOREKSI_MANUAL"}


def test_koreksi_dari_baris_belum_pernah_ada():
    """Guru yang belum pernah scan (status_final_sekarang None) - input
    manual DINAS_LUAR/IZIN/SAKIT/CUTI (PRD §6)."""
    perubahan = terapkan_koreksi_guru(
        status_final_sekarang=None, alasan_final_sekarang=None, status_baru=StatusGuru.CUTI
    )
    assert perubahan is not None
    assert perubahan.nilai_sebelum == {"status_final": None, "alasan_final": None}
    assert perubahan.nilai_sesudah == {"status_final": "CUTI", "alasan_final": "KOREKSI_MANUAL"}


def test_koreksi_menimpa_alasan_final_lama():
    perubahan = terapkan_koreksi_guru(
        status_final_sekarang=StatusGuru.DINAS_LUAR,
        alasan_final_sekarang=AlasanFinal.KOREKSI_MANUAL,
        status_baru=StatusGuru.IZIN,
    )
    assert perubahan.nilai_sebelum == {"status_final": "DINAS_LUAR", "alasan_final": "KOREKSI_MANUAL"}
    assert perubahan.nilai_sesudah == {"status_final": "IZIN", "alasan_final": "KOREKSI_MANUAL"}


def test_status_baru_sama_dengan_sekarang_tidak_ada_perubahan():
    perubahan = terapkan_koreksi_guru(
        status_final_sekarang=StatusGuru.CUTI, alasan_final_sekarang=None, status_baru=StatusGuru.CUTI
    )
    assert perubahan is None
