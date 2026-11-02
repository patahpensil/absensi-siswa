"""Entrypoint layar Laporan.

    python -m app.ui.laporan.main

Variabel lingkungan wajib (lihat app.ui.laporan.konfigurasi):
    ABSENSI_SERVER_URL, ABSENSI_LAPORAN_USERNAME, ABSENSI_LAPORAN_PASSWORD
"""

from __future__ import annotations

import sys

from PySide6.QtWidgets import QApplication

from app.ui.laporan.klien_laporan import KlienLaporan
from app.ui.laporan.konfigurasi import KonfigurasiTidakLengkap, muat_dari_lingkungan
from app.ui.laporan.layar import LayarLaporan


def main() -> None:
    try:
        konfigurasi = muat_dari_lingkungan()
    except KonfigurasiTidakLengkap as exc:
        print(f"Konfigurasi tidak lengkap: {exc}")
        raise SystemExit(1) from exc

    app = QApplication(sys.argv)

    klien = KlienLaporan(
        base_url=konfigurasi.server_url, username=konfigurasi.username, password=konfigurasi.password
    )
    layar = LayarLaporan(klien=klien)
    layar.resize(1100, 700)
    layar.show()
    layar.muat_awal()

    sys.exit(app.exec())


if __name__ == "__main__":
    main()
