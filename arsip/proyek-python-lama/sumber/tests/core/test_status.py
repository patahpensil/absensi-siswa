"""Membuktikan bahwa app/core/ bisa diimpor dan diuji tanpa PySide6.

Mesin absensi di app/core/ tidak boleh mengimpor apa pun dari app/ui/.
Tes ini gagal jika suatu saat app.core.status diam-diam mulai bergantung
pada PySide6.
"""

import sys

from app.core.status import StatusSiswa


def test_status_importable_without_pyside6():
    assert "PySide6" not in sys.modules


def test_status_values_dikenal_sistem():
    nilai = {status.value for status in StatusSiswa}
    assert nilai == {"HADIR", "TERLAMBAT", "ALPA", "IZIN", "SAKIT", "DISPENSASI"}
