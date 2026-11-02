"""Entrypoint layar absensi lobi.

    python -m app.ui.lobi.main

Slice pertama: digerakkan driver SIMULATOR (app.rfid.mock_reader.MockReader),
bukan perangkat sungguhan - lihat app.rfid.factory untuk pengemudi lain
begitu jalurnya sudah dipasang dan Pengaturan.jalur_alat_aktif ditentukan.
Fullscreen, tanpa tombol (PRD §12.5).

Variabel lingkungan wajib (lihat app.ui.lobi.konfigurasi):
    ABSENSI_SERVER_URL, ABSENSI_LOBI_USERNAME, ABSENSI_LOBI_PASSWORD
Opsional: ABSENSI_LOBI_STATION_ID (bawaan "LOBI-1"),
ABSENSI_LOBI_ANTREAN_PATH (bawaan "antrean_lobi.db" - berkas SQLite lokal
untuk antrean store-and-forward, lihat app.ui.lobi.antrean_lokal),
ABSENSI_LOBI_TIMEOUT_DETIK (bawaan 1.5), ABSENSI_LOBI_FORWARDER_INTERVAL_DETIK
(bawaan 5.0), ABSENSI_LOBI_FORWARDER_INTERVAL_MAKS_DETIK (bawaan 60.0).

ABSENSI_LOBI_DEV=1 menambahkan pintasan Ctrl+Shift+U untuk menyuntik UID uji
lewat MockReader.pancarkan_uid_manual - HANYA untuk pengembangan/demo,
jendela tidak fullscreen selagi flag ini aktif. Kontrol ini sengaja dipasang
DARI LUAR LayarAbsensiLobi (bukan method di kelasnya sendiri), supaya kelas
layar produksi tidak pernah punya jalur masuk selain pembacaan kartu
sungguhan.
"""

from __future__ import annotations

import os
import sys

from PySide6.QtGui import QKeySequence, QShortcut
from PySide6.QtWidgets import QApplication, QInputDialog

from app.rfid.mock_reader import MockReader
from app.ui.lobi.forwarder import ForwarderAntrean
from app.ui.lobi.konfigurasi import KonfigurasiTidakLengkap, muat_dari_lingkungan
from app.ui.lobi.layar import LayarAbsensiLobi
from app.ui.lobi.klien_scan import KlienScan


def main() -> None:
    try:
        konfigurasi = muat_dari_lingkungan()
    except KonfigurasiTidakLengkap as exc:
        print(f"Konfigurasi tidak lengkap: {exc}")
        raise SystemExit(1) from exc

    app = QApplication(sys.argv)

    klien = KlienScan(
        base_url=konfigurasi.server_url,
        username=konfigurasi.username,
        password=konfigurasi.password,
        path_antrean=konfigurasi.path_antrean,
        timeout=konfigurasi.timeout_detik,
    )
    forwarder = ForwarderAntrean(
        klien,
        interval_detik=konfigurasi.forwarder_interval_detik,
        interval_maks_detik=konfigurasi.forwarder_interval_maks_detik,
    )
    pembaca = {konfigurasi.station_id: MockReader(station_id=konfigurasi.station_id)}

    layar = LayarAbsensiLobi(klien=klien, pembaca=pembaca, forwarder=forwarder)

    if os.environ.get("ABSENSI_LOBI_DEV") == "1":
        _pasang_injeksi_dev(layar, pembaca[konfigurasi.station_id])
        layar.resize(1280, 800)
        layar.show()
    else:
        layar.showFullScreen()

    sys.exit(app.exec())


def _pasang_injeksi_dev(layar: LayarAbsensiLobi, simulator: MockReader) -> None:
    """DEV-only: Ctrl+Shift+U membuka input UID lalu menyuntikkannya lewat
    MockReader.pancarkan_uid_manual. Tidak pernah dipasang tanpa
    ABSENSI_LOBI_DEV=1 - lihat docstring modul ini."""

    def suntik() -> None:
        uid, ok = QInputDialog.getText(layar, "DEV: Suntik UID", "UID mentah:")
        if ok and uid:
            simulator.pancarkan_uid_manual(uid)

    pintasan = QShortcut(QKeySequence("Ctrl+Shift+U"), layar)
    pintasan.activated.connect(suntik)


if __name__ == "__main__":
    main()
