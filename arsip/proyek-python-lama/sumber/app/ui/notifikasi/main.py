"""Entrypoint layar Notifikasi.

    python -m app.ui.notifikasi.main

Variabel lingkungan wajib (lihat app.ui.notifikasi.konfigurasi):
    ABSENSI_SERVER_URL, ABSENSI_NOTIFIKASI_USERNAME, ABSENSI_NOTIFIKASI_PASSWORD
"""

from __future__ import annotations

import sys

from PySide6.QtWidgets import QApplication

from app.ui.notifikasi.klien_notifikasi import KlienNotifikasi
from app.ui.notifikasi.konfigurasi import KonfigurasiTidakLengkap, muat_dari_lingkungan
from app.ui.notifikasi.layar import LayarNotifikasi


def main() -> None:
    try:
        konfigurasi = muat_dari_lingkungan()
    except KonfigurasiTidakLengkap as exc:
        print(f"Konfigurasi tidak lengkap: {exc}")
        raise SystemExit(1) from exc

    app = QApplication(sys.argv)

    klien = KlienNotifikasi(
        base_url=konfigurasi.server_url, username=konfigurasi.username, password=konfigurasi.password
    )
    layar = LayarNotifikasi(klien=klien)
    layar.resize(900, 650)
    layar.show()
    layar.muat_awal()

    sys.exit(app.exec())


if __name__ == "__main__":
    main()
