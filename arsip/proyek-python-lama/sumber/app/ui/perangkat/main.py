"""Entrypoint layar Perangkat.

    python -m app.ui.perangkat.main

Variabel lingkungan wajib (lihat app.ui.perangkat.konfigurasi):
    ABSENSI_SERVER_URL, ABSENSI_PERANGKAT_USERNAME, ABSENSI_PERANGKAT_PASSWORD
"""

from __future__ import annotations

import sys

from PySide6.QtWidgets import QApplication

from app.ui.perangkat.klien_perangkat import KlienPerangkat
from app.ui.perangkat.konfigurasi import KonfigurasiTidakLengkap, muat_dari_lingkungan
from app.ui.perangkat.layar import LayarPerangkat


def main() -> None:
    try:
        konfigurasi = muat_dari_lingkungan()
    except KonfigurasiTidakLengkap as exc:
        print(f"Konfigurasi tidak lengkap: {exc}")
        raise SystemExit(1) from exc

    app = QApplication(sys.argv)

    klien = KlienPerangkat(
        base_url=konfigurasi.server_url, username=konfigurasi.username, password=konfigurasi.password
    )
    layar = LayarPerangkat(klien=klien)
    layar.resize(900, 650)
    layar.show()
    layar.muat_awal()

    sys.exit(app.exec())


if __name__ == "__main__":
    main()
