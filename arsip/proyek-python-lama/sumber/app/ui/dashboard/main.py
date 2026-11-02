"""Entrypoint layar Dashboard.

    python -m app.ui.dashboard.main

Variabel lingkungan wajib (lihat app.ui.dashboard.konfigurasi):
    ABSENSI_SERVER_URL, ABSENSI_DASHBOARD_USERNAME, ABSENSI_DASHBOARD_PASSWORD

Kepala sekolah (PRD §14.1: "disediakan satu layar ringkas") melihat
LayarRingkasKepsek lebih dulu, dengan tombol untuk membuka LayarDashboard
penuh kalau perlu. Admin dan wali kelas langsung ke LayarDashboard penuh.
"""

from __future__ import annotations

import sys

from PySide6.QtWidgets import QApplication

from app.ui.dashboard.klien_dashboard import KlienDashboard
from app.ui.dashboard.konfigurasi import KonfigurasiTidakLengkap, muat_dari_lingkungan
from app.ui.dashboard.layar import LayarDashboard
from app.ui.dashboard.layar_ringkas_kepsek import LayarRingkasKepsek


def main() -> None:
    try:
        konfigurasi = muat_dari_lingkungan()
    except KonfigurasiTidakLengkap as exc:
        print(f"Konfigurasi tidak lengkap: {exc}")
        raise SystemExit(1) from exc

    app = QApplication(sys.argv)

    klien = KlienDashboard(
        base_url=konfigurasi.server_url, username=konfigurasi.username, password=konfigurasi.password
    )
    # Memicu login sekarang (bukan lazy di panggilan pertama) supaya
    # klien.peran sudah terisi sebelum memutuskan layar mana yang dibuka.
    try:
        klien.ringkasan()
    except Exception:  # noqa: BLE001 - layar yang dipilih tetap akan melaporkan galatnya sendiri saat muat_awal()
        pass

    _jendela_tetap_hidup: list = []

    if klien.peran == "KEPALA_SEKOLAH":
        layar = LayarRingkasKepsek(klien=klien)

        def _buka_dashboard_lengkap() -> None:
            klien_lengkap = KlienDashboard(
                base_url=konfigurasi.server_url, username=konfigurasi.username, password=konfigurasi.password
            )
            dashboard_lengkap = LayarDashboard(klien=klien_lengkap)
            dashboard_lengkap.resize(1100, 750)
            dashboard_lengkap.show()
            dashboard_lengkap.muat_awal()
            _jendela_tetap_hidup.append(dashboard_lengkap)

        layar.tombol_dashboard_lengkap.clicked.connect(_buka_dashboard_lengkap)
        layar.resize(700, 600)
    else:
        layar = LayarDashboard(klien=klien)
        layar.resize(1100, 750)

    layar.show()
    layar.muat_awal()

    sys.exit(app.exec())


if __name__ == "__main__":
    main()
