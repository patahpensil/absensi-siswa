"""Entrypoint layar Koreksi Satuan.

    python -m app.ui.koreksi_satuan.main

Variabel lingkungan wajib (lihat app.ui.koreksi_satuan.konfigurasi):
    ABSENSI_SERVER_URL, ABSENSI_KOREKSI_SATUAN_USERNAME, ABSENSI_KOREKSI_SATUAN_PASSWORD
"""

from __future__ import annotations

import sys

from PySide6.QtWidgets import QApplication

from app.ui.koreksi_satuan.klien_koreksi_satuan import KlienKoreksiSatuan
from app.ui.koreksi_satuan.konfigurasi import KonfigurasiTidakLengkap, muat_dari_lingkungan
from app.ui.koreksi_satuan.layar import LayarKoreksiSatuan


def main() -> None:
    try:
        konfigurasi = muat_dari_lingkungan()
    except KonfigurasiTidakLengkap as exc:
        print(f"Konfigurasi tidak lengkap: {exc}")
        raise SystemExit(1) from exc

    app = QApplication(sys.argv)

    klien = KlienKoreksiSatuan(
        base_url=konfigurasi.server_url, username=konfigurasi.username, password=konfigurasi.password
    )
    layar = LayarKoreksiSatuan(klien=klien)
    layar.resize(900, 650)
    layar.show()

    sys.exit(app.exec())


if __name__ == "__main__":
    main()
