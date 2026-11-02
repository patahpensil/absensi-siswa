"""Layar Koreksi Massal (PRD §9.2, §10): tabel Riwayat Tindakan Massal
sebagai layar utama, dengan tombol untuk membuka DialogJalankanTindakan
dan membatalkan tindakan terpilih. Hanya kepala sekolah/admin yang
berwenang (SKILL.md "Koreksi massal") - tombol tindakan dinonaktifkan
untuk peran lain di sini SEBAGAI KENYAMANAN TAMPILAN saja, penegakan
sesungguhnya ada di server (lihat klien_koreksi_massal.py). Pada
praktiknya tidak ada peran lain yang bahkan berwenang melihat layar ini
(PRD §10: wali kelas dan operator sama-sama "-" untuk koreksi massal),
jadi ini murni jaring pengaman kalau layar ini pernah disalahgunakan.
"""

from __future__ import annotations

from PySide6.QtWidgets import (
    QHBoxLayout,
    QHeaderView,
    QInputDialog,
    QLabel,
    QMainWindow,
    QMessageBox,
    QPushButton,
    QTableWidget,
    QTableWidgetItem,
    QVBoxLayout,
    QWidget,
)

from app.ui.koreksi_massal.dialog_tindakan import DialogJalankanTindakan
from app.ui.koreksi_massal.format_tampilan import format_cakupan, format_jenis_tindakan
from app.ui.koreksi_massal.klien_koreksi_massal import GagalHubungiServer, KlienKoreksiMassal, ServerMenolak

_PERAN_BOLEH_UBAH = {"ADMIN", "KEPALA_SEKOLAH"}

KOLOM = ["ID", "Waktu", "Pelaku (ID Pengguna)", "Jenis Tindakan", "Cakupan", "Jumlah Baris", "Alasan", "Status"]


class LayarKoreksiMassal(QMainWindow):
    def __init__(self, *, klien: KlienKoreksiMassal) -> None:
        super().__init__()
        self.setWindowTitle("Koreksi Massal")
        self._klien = klien
        self._riwayat_saat_ini: list[dict] = []

        self._tombol_muat_ulang = QPushButton("Muat Ulang")
        self._tombol_muat_ulang.clicked.connect(self.muat_ulang)
        self._tombol_jalankan_baru = QPushButton("Jalankan Tindakan Baru")
        self._tombol_jalankan_baru.clicked.connect(self._jalankan_tindakan_baru)
        self._tombol_batalkan = QPushButton("Batalkan Terpilih")
        self._tombol_batalkan.clicked.connect(self._batalkan_terpilih)

        bar = QHBoxLayout()
        bar.addWidget(QLabel("Riwayat Tindakan Massal"))
        bar.addStretch(1)
        bar.addWidget(self._tombol_jalankan_baru)
        bar.addWidget(self._tombol_batalkan)
        bar.addWidget(self._tombol_muat_ulang)

        self._tabel = QTableWidget(0, len(KOLOM))
        self._tabel.setHorizontalHeaderLabels(KOLOM)
        self._tabel.setEditTriggers(QTableWidget.EditTrigger.NoEditTriggers)
        self._tabel.setSelectionBehavior(QTableWidget.SelectionBehavior.SelectRows)
        self._tabel.setSelectionMode(QTableWidget.SelectionMode.SingleSelection)
        self._tabel.horizontalHeader().setSectionResizeMode(6, QHeaderView.ResizeMode.Stretch)
        self._tabel.verticalHeader().setDefaultSectionSize(24)

        pusat = QWidget()
        tata_letak = QVBoxLayout(pusat)
        tata_letak.addLayout(bar)
        tata_letak.addWidget(self._tabel)
        self.setCentralWidget(pusat)

    # ------------------------------------------------------------------

    def muat_ulang(self) -> None:
        try:
            riwayat = self._klien.riwayat()
        except (ServerMenolak, GagalHubungiServer) as exc:
            QMessageBox.warning(self, "Gagal memuat riwayat", str(exc))
            return

        self._riwayat_saat_ini = riwayat
        self._isi_tabel(riwayat)
        self._terapkan_hak_akses()

    def _isi_tabel(self, riwayat: list[dict]) -> None:
        self._tabel.setRowCount(len(riwayat))
        for baris, t in enumerate(riwayat):
            nilai = [
                t["id"],
                t["waktu"],
                t["id_pengguna"],
                format_jenis_tindakan(t["jenis_tindakan"], t["status_baru"]),
                format_cakupan(t["penyaring"]),
                t["jumlah_baris"],
                t["alasan"],
                "Dibatalkan" if t["dibatalkan"] else "Aktif",
            ]
            for kolom, teks in enumerate(nilai):
                self._tabel.setItem(baris, kolom, QTableWidgetItem(str(teks)))

    def _terapkan_hak_akses(self) -> None:
        boleh_ubah = self._klien.peran in _PERAN_BOLEH_UBAH
        self._tombol_jalankan_baru.setEnabled(boleh_ubah)
        self._tombol_batalkan.setEnabled(boleh_ubah)

    def _baris_terpilih(self) -> dict | None:
        baris = self._tabel.currentRow()
        if baris < 0 or baris >= len(self._riwayat_saat_ini):
            return None
        return self._riwayat_saat_ini[baris]

    # ------------------------------------------------------------------

    def _jalankan_tindakan_baru(self) -> None:
        dialog = DialogJalankanTindakan(klien=self._klien, parent=self)
        if dialog.exec():
            QMessageBox.information(self, "Berhasil", "Tindakan massal berhasil dijalankan.")
            self.muat_ulang()

    def _batalkan_terpilih(self) -> None:
        tindakan = self._baris_terpilih()
        if tindakan is None:
            QMessageBox.information(self, "Batalkan Tindakan", "Pilih satu baris riwayat terlebih dahulu.")
            return
        if tindakan["dibatalkan"]:
            QMessageBox.information(self, "Batalkan Tindakan", "Tindakan ini sudah dibatalkan sebelumnya.")
            return

        alasan, ok = QInputDialog.getText(self, "Batalkan Tindakan Massal", "Alasan pembatalan:")
        if not ok or not alasan.strip():
            return

        try:
            self._klien.batalkan(tindakan["id"], alasan=alasan.strip())
        except (ServerMenolak, GagalHubungiServer) as exc:
            QMessageBox.warning(self, "Gagal membatalkan", str(exc))
            return

        QMessageBox.information(self, "Berhasil", "Tindakan massal dibatalkan; seluruh baris terdampak dikembalikan.")
        self.muat_ulang()
