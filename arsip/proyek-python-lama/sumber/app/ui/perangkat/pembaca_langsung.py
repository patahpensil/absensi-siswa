"""WidgetPembacaLangsung: kontrol bersama untuk menguji baca kartu secara
LIVE dari layar Perangkat - dipakai baik tab Uji Baca Kartu maupun
langkah-langkah Wizard Ganti Jalur (uji baca tiap jalur, verifikasi tiga
kartu). Satu tempat yang membungkus app.rfid.buat_pembaca() + jembatan
sinyal Qt, supaya kedua pemakai tidak menduplikasi pola threading.

Callback pembaca kartu (app.rfid) berjalan di THREAD PEMBACA, bukan
thread GUI (lihat app.rfid.base.PembacaKartuBase._jalankan) - _Jembatan
memancarkan sinyal Qt supaya update widget selalu terjadi di thread GUI,
sama persis pola app.ui.lobi.layar._JembatanScan.
"""

from __future__ import annotations

from PySide6.QtCore import QObject, Signal
from PySide6.QtWidgets import (
    QComboBox,
    QFormLayout,
    QHBoxLayout,
    QLabel,
    QLineEdit,
    QPushButton,
    QWidget,
)

from app.rfid.base import JalurAlat, PembacaKartu, ScanEvent
from app.rfid.factory import buat_pembaca


class _Jembatan(QObject):
    scan_diterima = Signal(object)  # ScanEvent

    def terima(self, event: ScanEvent) -> None:
        self.scan_diterima.emit(event)


class WidgetPembacaLangsung(QWidget):
    """Pilih jalur -> Mulai -> tempel kartu -> scan_diterima(ScanEvent).
    Pemanggil (tab Uji Baca Kartu / halaman wizard) yang memutuskan apa
    yang dilakukan dengan tiap ScanEvent - widget ini TIDAK PERNAH
    memanggil endpoint server sendiri."""

    scan_diterima = Signal(object)  # ScanEvent

    def __init__(self, *, station_id: str = "UJI-PERANGKAT", parent: QWidget | None = None) -> None:
        super().__init__(parent)
        self._station_id = station_id
        self._pembaca: PembacaKartu | None = None
        self._jembatan = _Jembatan()
        self._jembatan.scan_diterima.connect(self.scan_diterima)

        self._pilihan_jalur = QComboBox()
        for jalur in (JalurAlat.PCSC, JalurAlat.SERIAL, JalurAlat.HID, JalurAlat.SIMULATOR):
            self._pilihan_jalur.addItem(jalur.value, jalur)

        self._input_port = QLineEdit()
        self._input_port.setPlaceholderText("Wajib untuk SERIAL (mis. COM3) - kosongkan untuk jalur lain")

        self._input_nama_reader = QLineEdit()
        self._input_nama_reader.setPlaceholderText("Opsional untuk PC/SC - kosongkan untuk reader pertama")

        self._tombol_mulai = QPushButton("Mulai")
        self._tombol_mulai.clicked.connect(self._mulai)
        self._tombol_berhenti = QPushButton("Berhenti")
        self._tombol_berhenti.clicked.connect(self._berhenti)
        self._tombol_berhenti.setEnabled(False)

        self._label_status = QLabel("Belum berjalan.")

        tata = QFormLayout(self)
        tata.addRow("Jalur", self._pilihan_jalur)
        tata.addRow("Port serial", self._input_port)
        tata.addRow("Nama reader PC/SC", self._input_nama_reader)
        baris_tombol = QHBoxLayout()
        baris_tombol.addWidget(self._tombol_mulai)
        baris_tombol.addWidget(self._tombol_berhenti)
        tata.addRow(baris_tombol)
        tata.addRow(self._label_status)

    def jalur_dipilih(self) -> JalurAlat:
        return self._pilihan_jalur.currentData()

    def sedang_berjalan(self) -> bool:
        return self._pembaca is not None

    def _mulai(self) -> None:
        if self._pembaca is not None:
            return
        jalur = self.jalur_dipilih()
        try:
            pembaca = buat_pembaca(
                jalur=jalur,
                station_id=self._station_id,
                port=self._input_port.text().strip() or None,
                nama_reader=self._input_nama_reader.text().strip() or None,
            )
        except ValueError as exc:
            self._label_status.setText(f"Gagal memulai: {exc}")
            return

        pembaca.saat_scan(self._jembatan.terima)
        pembaca.mulai()
        self._pembaca = pembaca
        self._tombol_mulai.setEnabled(False)
        self._tombol_berhenti.setEnabled(True)
        # Galat "reader tidak ditemukan" (mis. PcscReader tanpa perangkat
        # terpasang) terjadi DI THREAD PEMBACA saat mulai() memanggil
        # _jalankan(), bukan di sini secara sinkron - terhubung() dipakai
        # pemanggil (tab/halaman wizard) untuk memeriksa status sungguhan
        # kalau perlu, pesan ini hanya konfirmasi permintaan mulai terkirim.
        self._label_status.setText(f"Berjalan di jalur {jalur.value} - tempelkan kartu.")

    def _berhenti(self) -> None:
        if self._pembaca is not None:
            self._pembaca.berhenti()
            self._pembaca = None
        self._tombol_mulai.setEnabled(True)
        self._tombol_berhenti.setEnabled(False)
        self._label_status.setText("Berhenti.")

    def closeEvent(self, event) -> None:  # noqa: N802 - override Qt
        self._berhenti()
        super().closeEvent(event)
