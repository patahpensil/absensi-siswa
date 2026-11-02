"""Dialog pendaftaran kartu RFID: pilih siswa/guru yang sudah tersimpan,
tempel kartu, UID langsung terikat ke baris (PRD §11.4: "selesai satu
baris, kartu ditempel, UID terikat"). Kartu terikat ORANG, bukan kelas
(PRD §12.6).

Callback pembaca kartu (app.rfid.base.PembacaKartuBase.saat_scan)
berjalan di THREAD PEMBACAAN, bukan thread GUI - Aturan Global #9
melarang menyentuh widget langsung dari sana. _JembatanScan memancarkan
ulang ScanEvent lewat Qt Signal; slot yang terhubung (_pada_scan)
otomatis dieksekusi di thread pemilik jembatan (thread GUI, karena
_JembatanScan dibuat di __init__ dialog ini), bukan di thread pemanggil
emit().

Tidak pernah memanggil app.core.engine/mesin_absensi - pendaftaran murni
mengikat UID<->pemilik lewat KlienDataMaster.terbitkan_kartu().
"""

from __future__ import annotations

from PySide6.QtCore import QObject, Qt, Signal
from PySide6.QtWidgets import QDialog, QLabel, QMessageBox, QPushButton, QVBoxLayout

from app.rfid.base import PembacaKartu, ScanEvent
from app.ui.data_master.klien_data_master import GagalHubungiServer, KlienDataMaster, ServerMenolak


class _JembatanScan(QObject):
    diterima = Signal(object)  # ScanEvent - lihat docstring modul

    def terima(self, event: ScanEvent) -> None:
        self.diterima.emit(event)


class DialogPendaftaranKartu(QDialog):
    def __init__(
        self,
        *,
        klien: KlienDataMaster,
        pembaca: PembacaKartu,
        id_pemilik: int,
        jenis_pemilik: str,
        nama_pemilik: str,
        parent=None,
    ) -> None:
        super().__init__(parent)
        self.setWindowTitle("Daftarkan Kartu RFID")
        self.setModal(True)
        self._klien = klien
        self._id_pemilik = id_pemilik
        self._jenis_pemilik = jenis_pemilik
        self.kartu_tersimpan: dict | None = None

        self._jembatan = _JembatanScan(self)
        self._jembatan.diterima.connect(self._pada_scan, Qt.ConnectionType.QueuedConnection)
        pembaca.saat_scan(self._jembatan.terima)

        tata_letak = QVBoxLayout(self)
        tata_letak.addWidget(QLabel(f"Tempelkan kartu untuk: {nama_pemilik}"))
        self._label_status = QLabel("Menunggu kartu ditempel...")
        tata_letak.addWidget(self._label_status)
        tombol_batal = QPushButton("Batal")
        tombol_batal.clicked.connect(self.reject)
        tata_letak.addWidget(tombol_batal)

    def _pada_scan(self, event: ScanEvent) -> None:
        self._label_status.setText(f"Kartu terbaca: {event.uid} - menyimpan...")
        try:
            kartu = self._klien.terbitkan_kartu(
                {
                    "uid_kartu": event.uid,
                    "uid_mentah": event.uid_mentah,
                    "id_pemilik": self._id_pemilik,
                    "jenis_pemilik": self._jenis_pemilik,
                }
            )
        except ServerMenolak as exc:
            QMessageBox.warning(self, "Gagal mendaftarkan kartu", exc.detail)
            self._label_status.setText("Menunggu kartu ditempel...")
            return
        except GagalHubungiServer as exc:
            QMessageBox.warning(self, "Gagal mendaftarkan kartu", str(exc))
            self._label_status.setText("Menunggu kartu ditempel...")
            return

        self.kartu_tersimpan = kartu
        self.accept()
