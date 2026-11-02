"""Entrypoint layar Kalender Akademik.

    python -m app.ui.kalender.main

Variabel lingkungan wajib (lihat app.ui.kalender.konfigurasi):
    ABSENSI_SERVER_URL, ABSENSI_KALENDER_USERNAME, ABSENSI_KALENDER_PASSWORD
"""

from __future__ import annotations

import sys

from PySide6.QtWidgets import QApplication

from app.ui.kalender.klien_kalender import KlienKalender
from app.ui.kalender.konfigurasi import KonfigurasiTidakLengkap, muat_dari_lingkungan
from app.ui.kalender.layar import LayarKalender


def main() -> None:
    try:
        konfigurasi = muat_dari_lingkungan()
    except KonfigurasiTidakLengkap as exc:
        print(f"Konfigurasi tidak lengkap: {exc}")
        raise SystemExit(1) from exc

    app = QApplication(sys.argv)

    klien = KlienKalender(
        base_url=konfigurasi.server_url, username=konfigurasi.username, password=konfigurasi.password
    )
    layar = LayarKalender(klien=klien)
    layar.resize(1100, 700)
    layar.show()
    layar.muat_ulang()

    sys.exit(app.exec())


if __name__ == "__main__":
    main()
