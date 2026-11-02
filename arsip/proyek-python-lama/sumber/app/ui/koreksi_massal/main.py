"""Entrypoint layar Koreksi Massal.

    python -m app.ui.koreksi_massal.main

Variabel lingkungan wajib (lihat app.ui.koreksi_massal.konfigurasi):
    ABSENSI_SERVER_URL, ABSENSI_KOREKSI_MASSAL_USERNAME, ABSENSI_KOREKSI_MASSAL_PASSWORD
"""

from __future__ import annotations

import sys

from PySide6.QtWidgets import QApplication

from app.ui.koreksi_massal.klien_koreksi_massal import KlienKoreksiMassal
from app.ui.koreksi_massal.konfigurasi import KonfigurasiTidakLengkap, muat_dari_lingkungan
from app.ui.koreksi_massal.layar import LayarKoreksiMassal


def main() -> None:
    try:
        konfigurasi = muat_dari_lingkungan()
    except KonfigurasiTidakLengkap as exc:
        print(f"Konfigurasi tidak lengkap: {exc}")
        raise SystemExit(1) from exc

    app = QApplication(sys.argv)

    klien = KlienKoreksiMassal(
        base_url=konfigurasi.server_url, username=konfigurasi.username, password=konfigurasi.password
    )
    layar = LayarKoreksiMassal(klien=klien)
    layar.resize(1000, 650)
    layar.show()
    layar.muat_ulang()

    sys.exit(app.exec())


if __name__ == "__main__":
    main()
