"""Tab Uji Baca Kartu (PRD §18): tempel kartu -> tampil UID mentah + baku
+ status terdaftar/tidak, TIDAK PERNAH membentuk absensi (beda dari layar
lobi - lihat app.ui.lobi.layar, yang memanggil KlienScan.submit_scan())."""

from __future__ import annotations

from PySide6.QtWidgets import QFormLayout, QLabel, QVBoxLayout, QWidget

from app.rfid.base import ScanEvent
from app.ui.perangkat.klien_perangkat import GagalHubungiServer, KlienPerangkat, ServerMenolak
from app.ui.perangkat.pembaca_langsung import WidgetPembacaLangsung


class TabUjiBacaKartu(QWidget):
    def __init__(self, *, klien: KlienPerangkat, parent: QWidget | None = None) -> None:
        super().__init__(parent)
        self._klien = klien

        self._pembaca = WidgetPembacaLangsung(station_id="UJI-BACA-KARTU")
        self._pembaca.scan_diterima.connect(self._pada_scan)

        self._label_uid_mentah = QLabel("-")
        self._label_uid_baku = QLabel("-")
        self._label_status = QLabel("Belum ada kartu ditempel.")
        self._label_status.setWordWrap(True)

        tata = QVBoxLayout(self)
        tata.addWidget(self._pembaca)
        form = QFormLayout()
        form.addRow("UID mentah", self._label_uid_mentah)
        form.addRow("UID baku", self._label_uid_baku)
        form.addRow("Status", self._label_status)
        tata.addLayout(form)
        tata.addStretch(1)

    def _pada_scan(self, event: ScanEvent) -> None:
        self._label_uid_mentah.setText(event.uid_mentah)
        self._label_uid_baku.setText(event.uid)
        try:
            hasil = self._klien.cek_uid(event.uid)
        except (GagalHubungiServer, ServerMenolak) as exc:
            self._label_status.setText(f"Gagal memeriksa ke server: {exc}")
            return

        if hasil["terdaftar"]:
            self._label_status.setText(
                f"TERDAFTAR - status {hasil['status']}, pemilik: {hasil['nama_pemilik'] or '-'}"
            )
        else:
            self._label_status.setText("TIDAK TERDAFTAR")
