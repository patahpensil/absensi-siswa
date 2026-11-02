"""TabelIsianView: QTableView dengan navigasi Tab antar kolom, Enter
antar baris (PRD §11.4) - ditulis eksplisit (bukan mengandalkan perilaku
bawaan delegate Qt) supaya perilakunya pasti dan bisa dijelaskan baris
per baris.
"""

from __future__ import annotations

from PySide6.QtCore import QModelIndex, Qt
from PySide6.QtGui import QKeyEvent
from PySide6.QtWidgets import QAbstractItemView, QTableView


class TabelIsianView(QTableView):
    def __init__(self, parent=None) -> None:
        super().__init__(parent)
        self.setEditTriggers(
            QAbstractItemView.EditTrigger.CurrentChanged
            | QAbstractItemView.EditTrigger.DoubleClicked
            | QAbstractItemView.EditTrigger.AnyKeyPressed
        )
        self.setSelectionBehavior(QAbstractItemView.SelectionBehavior.SelectItems)
        self.setSelectionMode(QAbstractItemView.SelectionMode.SingleSelection)
        self.verticalHeader().setDefaultSectionSize(28)  # tabel rapat (ui-guidelines.md)

    def keyPressEvent(self, event: QKeyEvent) -> None:
        model = self.model()
        if model is None:
            super().keyPressEvent(event)
            return

        current = self.currentIndex()
        key = event.key()

        if key == Qt.Key.Key_Tab:
            self._pindah_kolom(current, mundur=False)
            event.accept()
            return
        if key == Qt.Key.Key_Backtab:
            self._pindah_kolom(current, mundur=True)
            event.accept()
            return
        if key in (Qt.Key.Key_Return, Qt.Key.Key_Enter):
            self._pindah_baris_berikutnya(current)
            event.accept()
            return

        super().keyPressEvent(event)

    def _pindah_kolom(self, current: QModelIndex, *, mundur: bool) -> None:
        model = self.model()
        jumlah_kolom_isian = model.columnCount() - 1  # kolom Status tidak diedit
        col = current.column() + (-1 if mundur else 1)
        row = current.row()
        if col >= jumlah_kolom_isian:
            col, row = 0, row + 1
        elif col < 0:
            col, row = jumlah_kolom_isian - 1, row - 1
        if row < 0:
            return
        if row >= model.rowCount():
            return  # baris kosong penambah akan muncul lewat setData baris sebelumnya
        self._pindah_ke(row, col)

    def _pindah_baris_berikutnya(self, current: QModelIndex) -> None:
        model = self.model()
        row = current.row() + 1
        if row >= model.rowCount():
            return
        self._pindah_ke(row, 0)

    def _pindah_ke(self, row: int, col: int) -> None:
        model = self.model()
        target = model.index(row, col)
        self.setCurrentIndex(target)
        self.edit(target)
