"""LayarPerangkat: layar menu PERANGKAT (PRD §18 - jalur reader, uji baca
kartu, izin darurat). Wizard Ganti Jalur dibuka sebagai dialog terpisah
dari tombol di sini, bukan tab, karena wizard adalah prosedur SEKALI
JALAN berurutan (SKILL.md: "berurutan dan tidak bisa dilompati") - beda
dari dua tab lain yang dipakai berulang-ulang kapan saja."""

from __future__ import annotations

from PySide6.QtWidgets import QPushButton, QTabWidget, QVBoxLayout, QWidget

from app.ui.perangkat.klien_perangkat import KlienPerangkat
from app.ui.perangkat.tab_izin_darurat import TabIzinDarurat
from app.ui.perangkat.tab_uji_baca_kartu import TabUjiBacaKartu
from app.ui.perangkat.wizard_ganti_jalur import WizardGantiJalur


class LayarPerangkat(QWidget):
    def __init__(self, *, klien: KlienPerangkat, parent: QWidget | None = None) -> None:
        super().__init__(parent)
        self.setWindowTitle("Perangkat - UPT SMP Negeri 1 Panca Rijang")
        self._klien = klien

        self._tab_uji_baca = TabUjiBacaKartu(klien=klien)
        self._tab_izin_darurat = TabIzinDarurat(klien=klien)

        tab = QTabWidget()
        tab.addTab(self._tab_uji_baca, "Uji Baca Kartu")
        tab.addTab(self._tab_izin_darurat, "Izin Darurat Alat")

        tombol_wizard = QPushButton("Wizard Ganti Jalur Alat...")
        tombol_wizard.clicked.connect(self._buka_wizard)

        tata = QVBoxLayout(self)
        tata.addWidget(tab)
        tata.addWidget(tombol_wizard)

    def muat_awal(self) -> None:
        self._tab_izin_darurat.muat_ulang()

    def _buka_wizard(self) -> None:
        wizard = WizardGantiJalur(klien=self._klien, parent=self)
        wizard.exec()
        self._tab_izin_darurat.muat_ulang()

    def closeEvent(self, event) -> None:  # noqa: N802 - override Qt
        self._klien.tutup()
        super().closeEvent(event)
