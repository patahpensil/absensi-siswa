"""Membuktikan bahwa antrean_lokal.py, forwarder.py, klien_scan.py,
tampilan.py, dan status_jalur.py bisa diimpor dan diuji tanpa PySide6 - pola
yang sama dengan tests/core/test_status.py untuk app/core/. Hanya layar.py
dan main.py yang boleh menyentuh Qt; kalau suatu saat salah satu dari lima
modul ini diam-diam mulai mengimpor PySide6 (mis. lewat import tak sengaja),
tes ini gagal.
"""

import sys

from app.ui.lobi import antrean_lokal, forwarder, klien_scan, status_jalur, tampilan  # noqa: F401


def test_modul_lobi_murni_importable_tanpa_pyside6():
    assert "PySide6" not in sys.modules
