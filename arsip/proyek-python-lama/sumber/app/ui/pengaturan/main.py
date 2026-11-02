"""Entrypoint layar Pengaturan.

    python -m app.ui.pengaturan.main

Variabel lingkungan wajib (lihat app.ui.pengaturan.konfigurasi):
    ABSENSI_SERVER_URL, ABSENSI_PENGATURAN_USERNAME, ABSENSI_PENGATURAN_PASSWORD
"""

from __future__ import annotations

import sys

from PySide6.QtWidgets import QApplication

from app.ui.pengaturan.klien_pengaturan import KlienPengaturan
from app.ui.pengaturan.konfigurasi import KonfigurasiTidakLengkap, muat_dari_lingkungan
from app.ui.pengaturan.layar import LayarPengaturan


def main() -> None:
    try:
        konfigurasi = muat_dari_lingkungan()
    except KonfigurasiTidakLengkap as exc:
        print(f"Konfigurasi tidak lengkap: {exc}")
        raise SystemExit(1) from exc

    app = QApplication(sys.argv)

    klien = KlienPengaturan(
        base_url=konfigurasi.server_url, username=konfigurasi.username, password=konfigurasi.password
    )
    layar = LayarPengaturan(klien=klien)
    layar.resize(900, 650)
    layar.show()
    layar.muat_awal()

    sys.exit(app.exec())


if __name__ == "__main__":
    main()
