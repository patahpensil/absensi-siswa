"""Layar Riwayat Scan (PRD §7.4, §10) - baca-saja. Sibling layar scan di
menu DASHBOARD/ABSENSI. Lingkup data mengikuti peran yang login,
ditegakkan server (operator/admin/kepala sekolah bebas, wali kelas hanya
kelasnya sendiri) - layar ini tidak menyaring apa pun sendiri, hanya
menampilkan apa yang dikembalikan server dan pesan yang jelas kalau
saring ditolak (mis. wali kelas mencoba kelas lain).

TIDAK ada tombol koreksi di layar ini - murni tabel baca-saja.
"""

from __future__ import annotations

from datetime import date

from PySide6.QtCore import QDate
from PySide6.QtWidgets import (
    QComboBox,
    QDateEdit,
    QHBoxLayout,
    QHeaderView,
    QLabel,
    QLineEdit,
    QMainWindow,
    QMessageBox,
    QPushButton,
    QTableWidget,
    QTableWidgetItem,
    QVBoxLayout,
    QWidget,
)

from app.ui.riwayat_scan.klien_riwayat_scan import GagalHubungiServer, KlienRiwayatScan, ServerMenolak

KOLOM = ["Waktu", "Nama", "Label", "UID Kartu", "Jenis Pemilik", "Jalur"]


class LayarRiwayatScan(QMainWindow):
    def __init__(self, *, klien: KlienRiwayatScan) -> None:
        super().__init__()
        self.setWindowTitle("Riwayat Scan")
        self._klien = klien

        hari_ini = QDate.currentDate()
        self._tanggal_mulai = QDateEdit(hari_ini)
        self._tanggal_mulai.setCalendarPopup(True)
        self._tanggal_selesai = QDateEdit(hari_ini)
        self._tanggal_selesai.setCalendarPopup(True)

        self._id_kelas = QLineEdit()
        self._id_kelas.setPlaceholderText("ID Kelas (opsional)")
        self._id_pemilik = QLineEdit()
        self._id_pemilik.setPlaceholderText("ID Orang (opsional)")
        self._jenis_pemilik = QComboBox()
        self._jenis_pemilik.addItem("Semua", None)
        self._jenis_pemilik.addItem("Siswa", "SISWA")
        self._jenis_pemilik.addItem("Guru", "GURU")

        tombol_cari = QPushButton("Cari")
        tombol_cari.clicked.connect(self.muat_ulang)

        bar = QHBoxLayout()
        bar.addWidget(QLabel("Dari:"))
        bar.addWidget(self._tanggal_mulai)
        bar.addWidget(QLabel("Sampai:"))
        bar.addWidget(self._tanggal_selesai)
        bar.addWidget(self._id_kelas)
        bar.addWidget(self._id_pemilik)
        bar.addWidget(self._jenis_pemilik)
        bar.addWidget(tombol_cari)
        bar.addStretch(1)

        self._label_jumlah = QLabel("")

        self._tabel = QTableWidget(0, len(KOLOM))
        self._tabel.setHorizontalHeaderLabels(KOLOM)
        self._tabel.setEditTriggers(QTableWidget.EditTrigger.NoEditTriggers)
        self._tabel.setSelectionBehavior(QTableWidget.SelectionBehavior.SelectRows)
        self._tabel.horizontalHeader().setSectionResizeMode(1, QHeaderView.ResizeMode.Stretch)
        self._tabel.verticalHeader().setDefaultSectionSize(24)

        pusat = QWidget()
        tata_letak = QVBoxLayout(pusat)
        tata_letak.addLayout(bar)
        tata_letak.addWidget(self._label_jumlah)
        tata_letak.addWidget(self._tabel)
        self.setCentralWidget(pusat)

    def _int_atau_none(self, teks: str) -> int | None:
        teks = teks.strip()
        if not teks:
            return None
        try:
            return int(teks)
        except ValueError:
            return None

    def muat_ulang(self) -> None:
        params = {
            "tanggal_mulai": self._tanggal_mulai.date().toPython().isoformat(),
            "tanggal_selesai": self._tanggal_selesai.date().toPython().isoformat(),
            "id_kelas": self._int_atau_none(self._id_kelas.text()),
            "id_pemilik": self._int_atau_none(self._id_pemilik.text()),
            "jenis_pemilik": self._jenis_pemilik.currentData(),
        }
        try:
            baris = self._klien.cari(params)
        except (ServerMenolak, GagalHubungiServer) as exc:
            QMessageBox.warning(self, "Gagal memuat riwayat scan", str(exc))
            return

        self._isi_tabel(baris)
        self._label_jumlah.setText(f"{len(baris)} peristiwa")

    def _isi_tabel(self, baris: list[dict]) -> None:
        self._tabel.setRowCount(len(baris))
        for i, b in enumerate(baris):
            nilai = [
                b["waktu_scan"],
                b["nama"] or "-",
                b["label"],
                b["uid_kartu"],
                b["jenis_pemilik"] or "-",
                b["jalur"] or "-",
            ]
            for kolom, teks in enumerate(nilai):
                self._tabel.setItem(i, kolom, QTableWidgetItem(str(teks)))
