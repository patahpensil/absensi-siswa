"""Tes murni app.core.akses_kelas - pembatasan kelas wali kelas
ditegakkan di lapisan data (SKILL.md "Peran dan hak akses"), bukan di UI.
Tidak menyentuh basis data maupun Qt.
"""

import pytest

from app.core.akses_kelas import AksesKelasDitolak, pastikan_akses_kelas
from app.core.status import Peran


def test_wali_kelas_boleh_akses_kelasnya_sendiri():
    pastikan_akses_kelas(peran=Peran.WALI_KELAS, id_kelas_diampu=5, id_kelas_target=5)  # tidak melempar apa pun


def test_wali_kelas_ditolak_akses_kelas_lain():
    with pytest.raises(AksesKelasDitolak) as exc_info:
        pastikan_akses_kelas(peran=Peran.WALI_KELAS, id_kelas_diampu=5, id_kelas_target=9)
    assert exc_info.value.id_kelas_diampu == 5
    assert exc_info.value.id_kelas_target == 9


def test_wali_kelas_tanpa_kelas_ditugaskan_selalu_ditolak():
    """id_kelas_diampu None BUKAN celah bebas akses - wali kelas yang
    belum ditugaskan tetap ditolak untuk kelas mana pun."""
    with pytest.raises(AksesKelasDitolak):
        pastikan_akses_kelas(peran=Peran.WALI_KELAS, id_kelas_diampu=None, id_kelas_target=1)


@pytest.mark.parametrize("peran", [Peran.ADMIN, Peran.KEPALA_SEKOLAH, Peran.OPERATOR])
def test_peran_selain_wali_kelas_tidak_dibatasi_kelas(peran):
    pastikan_akses_kelas(peran=peran, id_kelas_diampu=None, id_kelas_target=999)  # tidak melempar apa pun
