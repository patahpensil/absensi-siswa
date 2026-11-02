"""Entrypoint layar Riwayat Scan.

    python -m app.ui.riwayat_scan.main

Variabel lingkungan wajib (lihat app.ui.riwayat_scan.konfigurasi):
    ABSENSI_SERVER_URL, ABSENSI_RIWAYAT_SCAN_USERNAME, ABSENSI_RIWAYAT_SCAN_PASSWORD
"""

from __future__ import annotations

import sys

from PySide6.QtWidgets import QApplication

from app.ui.riwayat_scan.klien_riwayat_scan import KlienRiwayatScan
from app.ui.riwayat_scan.konfigurasi import KonfigurasiTidakLengkap, muat_dari_lingkungan
from app.ui.riwayat_scan.layar import LayarRiwayatScan


def main() -> None:
    try:
        konfigurasi = muat_dari_lingkungan()
    except KonfigurasiTidakLengkap as exc:
        print(f"Konfigurasi tidak lengkap: {exc}")
        raise SystemExit(1) from exc

    app = QApplication(sys.argv)

    klien = KlienRiwayatScan(
        base_url=konfigurasi.server_url, username=konfigurasi.username, password=konfigurasi.password
    )
    layar = LayarRiwayatScan(klien=klien)
    layar.resize(1000, 650)
    layar.show()
    layar.muat_ulang()

    sys.exit(app.exec())


if __name__ == "__main__":
    main()
