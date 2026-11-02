"""Entrypoint layar Koreksi Guru.

    python -m app.ui.koreksi_guru.main

Variabel lingkungan wajib (lihat app.ui.koreksi_guru.konfigurasi):
    ABSENSI_SERVER_URL, ABSENSI_KOREKSI_GURU_USERNAME, ABSENSI_KOREKSI_GURU_PASSWORD
"""

from __future__ import annotations

import sys

from PySide6.QtWidgets import QApplication

from app.ui.koreksi_guru.klien_koreksi_guru import KlienKoreksiGuru
from app.ui.koreksi_guru.konfigurasi import KonfigurasiTidakLengkap, muat_dari_lingkungan
from app.ui.koreksi_guru.layar import LayarKoreksiGuru


def main() -> None:
    try:
        konfigurasi = muat_dari_lingkungan()
    except KonfigurasiTidakLengkap as exc:
        print(f"Konfigurasi tidak lengkap: {exc}")
        raise SystemExit(1) from exc

    app = QApplication(sys.argv)

    klien = KlienKoreksiGuru(
        base_url=konfigurasi.server_url, username=konfigurasi.username, password=konfigurasi.password
    )
    layar = LayarKoreksiGuru(klien=klien)
    layar.resize(900, 650)
    layar.show()

    sys.exit(app.exec())


if __name__ == "__main__":
    main()
