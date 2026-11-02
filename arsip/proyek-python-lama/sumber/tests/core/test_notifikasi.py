"""Tes murni app.core.notifikasi - tidak menyentuh basis data."""

import pytest

from app.core.notifikasi import (
    AksesNotifikasiDitolak,
    ModeUjiTanpaNomorAdmin,
    pastikan_berwenang,
    tujuan_efektif,
)
from app.core.status import Peran


@pytest.mark.parametrize("peran", [Peran.KEPALA_SEKOLAH, Peran.ADMIN])
def test_peran_berwenang_lolos(peran):
    pastikan_berwenang(peran)  # tidak melempar apa pun


@pytest.mark.parametrize("peran", [Peran.WALI_KELAS, Peran.OPERATOR])
def test_wali_kelas_dan_operator_ditolak(peran):
    with pytest.raises(AksesNotifikasiDitolak):
        pastikan_berwenang(peran)


def test_mode_uji_nonaktif_mengembalikan_nomor_asli():
    hasil = tujuan_efektif("+6281111111111", mode_uji_aktif=False, nomor_admin_uji=None)
    assert hasil == "+6281111111111"


def test_mode_uji_nonaktif_mengembalikan_nomor_asli_walau_nomor_admin_terisi():
    """Nomor admin boleh sudah diisi tanpa mode uji aktif - itu tidak
    berarti pengalihan berlaku."""
    hasil = tujuan_efektif("+6281111111111", mode_uji_aktif=False, nomor_admin_uji="+6289999999999")
    assert hasil == "+6281111111111"


def test_mode_uji_aktif_mengalihkan_ke_nomor_admin():
    hasil = tujuan_efektif("+6281111111111", mode_uji_aktif=True, nomor_admin_uji="+6289999999999")
    assert hasil == "+6289999999999"


def test_mode_uji_aktif_tanpa_nomor_admin_melempar_error():
    with pytest.raises(ModeUjiTanpaNomorAdmin):
        tujuan_efektif("+6281111111111", mode_uji_aktif=True, nomor_admin_uji=None)


def test_mode_uji_aktif_dengan_nomor_admin_kosong_melempar_error():
    with pytest.raises(ModeUjiTanpaNomorAdmin):
        tujuan_efektif("+6281111111111", mode_uji_aktif=True, nomor_admin_uji="")
