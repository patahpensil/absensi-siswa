"""Entrypoint layar Manajemen Akun & Peran.

    python -m app.ui.pengguna.main

Variabel lingkungan wajib (lihat app.ui.pengguna.konfigurasi):
    ABSENSI_SERVER_URL, ABSENSI_PENGGUNA_USERNAME, ABSENSI_PENGGUNA_PASSWORD
"""

from __future__ import annotations

import sys

from PySide6.QtWidgets import QApplication

from app.ui.pengguna.klien_pengguna import KlienPengguna
from app.ui.pengguna.konfigurasi import KonfigurasiTidakLengkap, muat_dari_lingkungan
from app.ui.pengguna.layar import LayarPengguna


def main() -> None:
    try:
        konfigurasi = muat_dari_lingkungan()
    except KonfigurasiTidakLengkap as exc:
        print(f"Konfigurasi tidak lengkap: {exc}")
        raise SystemExit(1) from exc

    app = QApplication(sys.argv)

    klien = KlienPengguna(
        base_url=konfigurasi.server_url, username=konfigurasi.username, password=konfigurasi.password
    )
    layar = LayarPengguna(klien=klien)
    layar.resize(900, 600)
    layar.show()
    layar.muat_ulang()

    sys.exit(app.exec())


if __name__ == "__main__":
    main()
