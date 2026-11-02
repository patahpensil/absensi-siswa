"""Layar Koreksi Satuan (PRD §9.1, §10). "Simpan" tetap nonaktif sampai
alasan terisi - ditegakkan di layar ini SEBAGAI KENYAMANAN (server juga
menolak alasan kosong lewat validasi Pydantic, lihat
app/server/schemas.py). Lingkup peran (kepsek/admin bebas, wali kelas
kelasnya sendiri, operator ditolak) ditegakkan server - layar ini hanya
menampilkan pesan yang jelas kalau server menolak.
"""

from __future__ import annotations

from PySide6.QtCore import QDate
from PySide6.QtWidgets import (
    QComboBox,
    QDateEdit,
    QFormLayout,
    QHBoxLayout,
    QInputDialog,
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

from app.ui.koreksi_satuan.klien_koreksi_satuan import GagalHubungiServer, KlienKoreksiSatuan, ServerMenolak

STATUS_PILIHAN = [
    ("HADIR", "Hadir"),
    ("TERLAMBAT", "Terlambat"),
    ("ALPA", "Alpa"),
    ("IZIN", "Izin"),
    ("SAKIT", "Sakit"),
    ("DISPENSASI", "Dispensasi"),
]

KOLOM_RIWAYAT = ["Waktu", "Nilai Sebelum", "Nilai Sesudah", "Alasan", "Status"]


class LayarKoreksiSatuan(QMainWindow):
    def __init__(self, *, klien: KlienKoreksiSatuan) -> None:
        super().__init__()
        self.setWindowTitle("Koreksi Satuan")
        self._klien = klien
        self._id_absensi_saat_ini: int | None = None
        self._riwayat_saat_ini: list[dict] = []

        self._id_siswa = QLineEdit()
        self._id_siswa.setPlaceholderText("ID Siswa")
        self._tanggal = QDateEdit(QDate.currentDate())
        self._tanggal.setCalendarPopup(True)
        tombol_cek = QPushButton("Cek Status")
        tombol_cek.clicked.connect(self._cek_status)

        bar_cari = QHBoxLayout()
        bar_cari.addWidget(QLabel("ID Siswa:"))
        bar_cari.addWidget(self._id_siswa)
        bar_cari.addWidget(QLabel("Tanggal:"))
        bar_cari.addWidget(self._tanggal)
        bar_cari.addWidget(tombol_cek)
        bar_cari.addStretch(1)

        self._label_konfirmasi = QLabel("Cek status dulu sebelum mengoreksi.")
        self._label_konfirmasi.setWordWrap(True)

        self._status_baru = QComboBox()
        for nilai, label in STATUS_PILIHAN:
            self._status_baru.addItem(label, nilai)

        self._alasan = QLineEdit()
        self._alasan.setPlaceholderText("Alasan wajib diisi")
        self._alasan.textChanged.connect(self._perbarui_tombol_simpan)

        self._tombol_simpan = QPushButton("Simpan Koreksi")
        self._tombol_simpan.setEnabled(False)
        self._tombol_simpan.clicked.connect(self._simpan)

        form = QFormLayout()
        form.addRow("Status Baru", self._status_baru)
        form.addRow("Alasan", self._alasan)

        self._tabel_riwayat = QTableWidget(0, len(KOLOM_RIWAYAT))
        self._tabel_riwayat.setHorizontalHeaderLabels(KOLOM_RIWAYAT)
        self._tabel_riwayat.setEditTriggers(QTableWidget.EditTrigger.NoEditTriggers)
        self._tabel_riwayat.setSelectionBehavior(QTableWidget.SelectionBehavior.SelectRows)
        self._tabel_riwayat.setSelectionMode(QTableWidget.SelectionMode.SingleSelection)
        self._tabel_riwayat.verticalHeader().setDefaultSectionSize(24)

        self._tombol_batalkan = QPushButton("Batalkan Koreksi Terpilih")
        self._tombol_batalkan.clicked.connect(self._batalkan_terpilih)

        self._form_koreksi_widget = QWidget()
        tata_letak_form = QVBoxLayout(self._form_koreksi_widget)
        tata_letak_form.addLayout(form)
        tata_letak_form.addWidget(self._tombol_simpan)
        self._form_koreksi_widget.setEnabled(False)

        pusat = QWidget()
        tata_letak = QVBoxLayout(pusat)
        tata_letak.addLayout(bar_cari)
        tata_letak.addWidget(self._label_konfirmasi)
        tata_letak.addWidget(self._form_koreksi_widget)
        tata_letak.addWidget(QLabel("Riwayat Koreksi:"))
        tata_letak.addWidget(self._tabel_riwayat)
        tata_letak.addWidget(self._tombol_batalkan)
        self.setCentralWidget(pusat)

    def _id_siswa_int(self) -> int | None:
        teks = self._id_siswa.text().strip()
        if not teks.isdigit():
            return None
        return int(teks)

    def _tanggal_iso(self) -> str:
        return self._tanggal.date().toPython().isoformat()

    def _perbarui_tombol_simpan(self, *_args) -> None:
        alasan_terisi = bool(self._alasan.text().strip())
        status_siap = self._form_koreksi_widget.isEnabled()
        self._tombol_simpan.setEnabled(alasan_terisi and status_siap)

    def _cek_status(self) -> None:
        id_siswa = self._id_siswa_int()
        if id_siswa is None:
            QMessageBox.warning(self, "ID Siswa tidak valid", "Isi ID Siswa dengan angka.")
            return

        try:
            status = self._klien.status_saat_ini(id_siswa=id_siswa, tanggal=self._tanggal_iso())
        except (ServerMenolak, GagalHubungiServer) as exc:
            QMessageBox.warning(self, "Gagal memuat status", str(exc))
            self._form_koreksi_widget.setEnabled(False)
            self._label_konfirmasi.setText("Cek status dulu sebelum mengoreksi.")
            return

        self._id_absensi_saat_ini = status.get("id_absensi")
        self._label_konfirmasi.setText(
            f"{status['nama_lengkap']} (kelas id={status['id_kelas']}) - "
            f"status masuk saat ini: {status['status_masuk_sekarang'] or '(belum scan)'}, "
            f"status final saat ini: {status['status_final_sekarang'] or '(belum ada)'}"
        )
        self._form_koreksi_widget.setEnabled(True)
        self._perbarui_tombol_simpan()
        self._muat_riwayat(id_siswa)

    def _muat_riwayat(self, id_siswa: int) -> None:
        try:
            riwayat = self._klien.daftar_riwayat(id_siswa=id_siswa, tanggal=self._tanggal_iso())
        except (ServerMenolak, GagalHubungiServer) as exc:
            QMessageBox.warning(self, "Gagal memuat riwayat", str(exc))
            return
        self._riwayat_saat_ini = riwayat
        self._tabel_riwayat.setRowCount(len(riwayat))
        for i, k in enumerate(riwayat):
            nilai = [
                k["waktu"],
                str(k["nilai_sebelum"]),
                str(k["nilai_sesudah"]),
                k["alasan"],
                "Dibatalkan" if k["dibatalkan"] else "Aktif",
            ]
            for kolom, teks in enumerate(nilai):
                self._tabel_riwayat.setItem(i, kolom, QTableWidgetItem(str(teks)))

    def _simpan(self) -> None:
        id_siswa = self._id_siswa_int()
        if id_siswa is None or not self._alasan.text().strip():
            return  # tombol seharusnya sudah nonaktif - jaring pengaman saja

        payload = {
            "id_siswa": id_siswa,
            "tanggal": self._tanggal_iso(),
            "status_baru": self._status_baru.currentData(),
            "alasan": self._alasan.text().strip(),
        }
        try:
            self._klien.buat(payload)
        except (ServerMenolak, GagalHubungiServer) as exc:
            QMessageBox.warning(self, "Gagal menyimpan koreksi", str(exc))
            return

        self._alasan.clear()
        QMessageBox.information(self, "Tersimpan", "Koreksi berhasil disimpan.")
        self._cek_status()

    def _batalkan_terpilih(self) -> None:
        baris = self._tabel_riwayat.currentRow()
        if baris < 0 or baris >= len(self._riwayat_saat_ini):
            QMessageBox.information(self, "Batalkan Koreksi", "Pilih satu baris riwayat terlebih dahulu.")
            return
        koreksi = self._riwayat_saat_ini[baris]
        if koreksi["dibatalkan"]:
            QMessageBox.information(self, "Batalkan Koreksi", "Koreksi ini sudah dibatalkan sebelumnya.")
            return

        alasan, ok = QInputDialog.getText(self, "Batalkan Koreksi", "Alasan pembatalan:")
        if not ok or not alasan.strip():
            return

        try:
            self._klien.batalkan(koreksi["id"], alasan=alasan.strip())
        except (ServerMenolak, GagalHubungiServer) as exc:
            QMessageBox.warning(self, "Gagal membatalkan", str(exc))
            return

        QMessageBox.information(self, "Berhasil", "Koreksi dibatalkan.")
        self._cek_status()
