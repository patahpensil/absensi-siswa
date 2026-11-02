"""Entrypoint layar Data Master.

    python -m app.ui.data_master.main

Variabel lingkungan wajib (lihat app.ui.data_master.konfigurasi):
    ABSENSI_SERVER_URL, ABSENSI_DATAMASTER_USERNAME, ABSENSI_DATAMASTER_PASSWORD

ABSENSI_DATAMASTER_DEV=1 memasang SimulatorReader (app.rfid.mock_reader.
MockReader) untuk pendaftaran kartu tanpa perangkat fisik - dipakai
pengembangan/pelatihan, BUKAN untuk PC sungguhan (PRD §12.3 - jalur
sungguhan disambungkan lewat app.rfid.factory begitu terpasang).
"""

from __future__ import annotations

import os
import sys

from PySide6.QtGui import QKeySequence, QShortcut
from PySide6.QtWidgets import QApplication, QInputDialog

from app.rfid.mock_reader import MockReader
from app.ui.data_master.klien_data_master import KlienDataMaster
from app.ui.data_master.konfigurasi import KonfigurasiTidakLengkap, muat_dari_lingkungan
from app.ui.data_master.layar import LayarDataMaster


def main() -> None:
    try:
        konfigurasi = muat_dari_lingkungan()
    except KonfigurasiTidakLengkap as exc:
        print(f"Konfigurasi tidak lengkap: {exc}")
        raise SystemExit(1) from exc

    app = QApplication(sys.argv)

    klien = KlienDataMaster(
        base_url=konfigurasi.server_url, username=konfigurasi.username, password=konfigurasi.password
    )
    pembaca = MockReader(station_id="DATAMASTER-1") if os.environ.get("ABSENSI_DATAMASTER_DEV") == "1" else None

    layar = LayarDataMaster(klien=klien, pembaca=pembaca)
    layar.resize(1280, 800)
    layar.show()
    layar.muat_awal()

    if pembaca is not None:
        _pasang_injeksi_dev(layar, pembaca)

    sys.exit(app.exec())


def _pasang_injeksi_dev(layar: LayarDataMaster, simulator: MockReader) -> None:
    """DEV-only (ABSENSI_DATAMASTER_DEV=1): Ctrl+Shift+U membuka input UID
    lalu menyuntikkannya lewat MockReader.pancarkan_uid_manual - simulasi
    tempel kartu tanpa perangkat fisik, sama seperti app.ui.lobi.main.
    Hanya berpengaruh selagi dialog "Daftarkan Kartu" sedang terbuka dan
    mendengarkan pembaca ini (lihat DialogPendaftaranKartu.saat_scan)."""

    def suntik() -> None:
        uid, ok = QInputDialog.getText(layar, "DEV: Suntik UID Kartu", "UID mentah:")
        if ok and uid:
            simulator.pancarkan_uid_manual(uid)

    pintasan = QShortcut(QKeySequence("Ctrl+Shift+U"), layar)
    pintasan.activated.connect(suntik)


if __name__ == "__main__":
    main()
