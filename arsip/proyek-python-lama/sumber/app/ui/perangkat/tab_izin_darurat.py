"""Tab Izin Darurat Alat (PRD §12.4): memberi/memperpanjang/mencabut izin
darurat, maksimum 1x24 jam, wajib beralasan, tercatat audit. Lingkup
peran ditegakkan SERVER (lihat app.server.routers.perangkat) - layar ini
hanya menampilkan galat 403 apa adanya kalau peran yang login tidak
berwenang untuk satu aksi (mis. wali kelas mencoba memperpanjang)."""

from __future__ import annotations

from PySide6.QtWidgets import (
    QDoubleSpinBox,
    QFormLayout,
    QHBoxLayout,
    QLineEdit,
    QMessageBox,
    QPlainTextEdit,
    QPushButton,
    QTableWidget,
    QTableWidgetItem,
    QVBoxLayout,
    QWidget,
)

from app.ui.perangkat.klien_perangkat import GagalHubungiServer, KlienPerangkat, ServerMenolak

_KOLOM = ["ID", "Jalur", "Berakhir", "Alasan"]


class TabIzinDarurat(QWidget):
    def __init__(self, *, klien: KlienPerangkat, parent: QWidget | None = None) -> None:
        super().__init__(parent)
        self._klien = klien

        self._input_station = QLineEdit()
        self._input_station.setPlaceholderText("mis. LOBI-1")
        self._input_jam = QDoubleSpinBox()
        self._input_jam.setRange(0.5, 24.0)
        self._input_jam.setValue(24.0)
        self._input_jam.setSuffix(" jam")
        self._input_alasan = QPlainTextEdit()
        self._input_alasan.setPlaceholderText("Alasan wajib diisi")
        self._input_alasan.setMaximumHeight(60)
        tombol_beri = QPushButton("Beri Izin Darurat")
        tombol_beri.clicked.connect(self._beri_izin)

        form = QFormLayout()
        form.addRow("Jalur (station)", self._input_station)
        form.addRow("Durasi", self._input_jam)
        form.addRow("Alasan", self._input_alasan)
        form.addRow(tombol_beri)

        self._tabel = QTableWidget(0, len(_KOLOM))
        self._tabel.setHorizontalHeaderLabels(_KOLOM)
        self._tabel.setEditTriggers(QTableWidget.EditTrigger.NoEditTriggers)
        self._tabel.setSelectionBehavior(QTableWidget.SelectionBehavior.SelectRows)

        tombol_muat = QPushButton("Muat Ulang")
        tombol_muat.clicked.connect(self.muat_ulang)
        tombol_perpanjang = QPushButton("Perpanjang Terpilih (+24 jam)")
        tombol_perpanjang.clicked.connect(self._perpanjang_terpilih)
        tombol_cabut = QPushButton("Cabut Terpilih")
        tombol_cabut.clicked.connect(self._cabut_terpilih)
        baris_tombol = QHBoxLayout()
        baris_tombol.addWidget(tombol_muat)
        baris_tombol.addWidget(tombol_perpanjang)
        baris_tombol.addWidget(tombol_cabut)

        tata = QVBoxLayout(self)
        tata.addLayout(form)
        tata.addWidget(self._tabel)
        tata.addLayout(baris_tombol)

    def muat_ulang(self) -> None:
        try:
            daftar = self._klien.daftar_izin_darurat_aktif()
        except (GagalHubungiServer, ServerMenolak) as exc:
            QMessageBox.warning(self, "Gagal memuat", str(exc))
            return

        self._tabel.setRowCount(0)
        for izin in daftar:
            baris = self._tabel.rowCount()
            self._tabel.insertRow(baris)
            self._tabel.setItem(baris, 0, QTableWidgetItem(str(izin["id"])))
            self._tabel.setItem(baris, 1, QTableWidgetItem(izin["station_id"]))
            self._tabel.setItem(baris, 2, QTableWidgetItem(izin["waktu_berakhir"]))
            self._tabel.setItem(baris, 3, QTableWidgetItem(izin["alasan"]))

    def _beri_izin(self) -> None:
        station_id = self._input_station.text().strip()
        alasan = self._input_alasan.toPlainText().strip()
        if not station_id or not alasan:
            QMessageBox.warning(self, "Data belum lengkap", "Jalur (station) dan alasan wajib diisi.")
            return
        try:
            self._klien.beri_izin_darurat(station_id=station_id, jam=self._input_jam.value(), alasan=alasan)
        except (GagalHubungiServer, ServerMenolak) as exc:
            QMessageBox.warning(self, "Gagal memberi izin", str(exc))
            return
        self._input_alasan.clear()
        self.muat_ulang()

    def _id_terpilih(self) -> int | None:
        baris = self._tabel.currentRow()
        if baris < 0:
            QMessageBox.warning(self, "Belum ada baris terpilih", "Pilih satu baris izin darurat dulu.")
            return None
        return int(self._tabel.item(baris, 0).text())

    def _perpanjang_terpilih(self) -> None:
        id_izin = self._id_terpilih()
        if id_izin is None:
            return
        alasan = self._input_alasan.toPlainText().strip()
        if not alasan:
            QMessageBox.warning(self, "Alasan wajib diisi", "Tuliskan alasan perpanjangan di kolom alasan di atas.")
            return
        try:
            self._klien.perpanjang_izin_darurat(id_izin, jam=self._input_jam.value(), alasan=alasan)
        except (GagalHubungiServer, ServerMenolak) as exc:
            QMessageBox.warning(self, "Gagal memperpanjang", str(exc))
            return
        self.muat_ulang()

    def _cabut_terpilih(self) -> None:
        id_izin = self._id_terpilih()
        if id_izin is None:
            return
        alasan = self._input_alasan.toPlainText().strip()
        if not alasan:
            QMessageBox.warning(self, "Alasan wajib diisi", "Tuliskan alasan pencabutan di kolom alasan di atas.")
            return
        try:
            self._klien.cabut_izin_darurat(id_izin, alasan=alasan)
        except (GagalHubungiServer, ServerMenolak) as exc:
            QMessageBox.warning(self, "Gagal mencabut", str(exc))
            return
        self.muat_ulang()
