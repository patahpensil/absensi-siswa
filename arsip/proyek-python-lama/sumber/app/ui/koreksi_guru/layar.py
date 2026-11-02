"""Layar Koreksi Guru (PRD §6). "Simpan" tetap nonaktif sampai alasan
terisi - ditegakkan di layar ini SEBAGAI KENYAMANAN (server juga menolak
alasan kosong lewat validasi Form, lihat app/server/routers/koreksi_guru.py).
Lingkup peran (hanya kepsek/admin) ditegakkan server - layar ini hanya
menampilkan pesan yang jelas kalau server menolak.

Daftar Status Baru SENGAJA hanya empat nilai manual (Dinas Luar/Izin/
Sakit/Cuti) - HADIR/TERLAMBAT tidak ditawarkan sama sekali, sesuai PRD
§6 (murni hasil scan+toleransi, bukan sesuatu yang "diinput").

Lampiran dipilih lewat dialog berkas lokal (opsional, PRD §6:
"sebaiknya", bukan wajib), diunggah bersamaan dengan koreksi dalam satu
klik Simpan. Melihat lampiran yang sudah tersimpan mengunduhnya ke
berkas sementara lalu membukanya dengan aplikasi bawaan Windows -
layar ini tidak membangun penampil gambar/PDF sendiri.
"""

from __future__ import annotations

import os
import tempfile
from pathlib import Path

from PySide6.QtCore import QDate
from PySide6.QtWidgets import (
    QComboBox,
    QDateEdit,
    QFileDialog,
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

from app.ui.koreksi_guru.klien_koreksi_guru import GagalHubungiServer, KlienKoreksiGuru, ServerMenolak

#: Hanya empat status manual (PRD §6) - lihat docstring modul.
STATUS_PILIHAN = [
    ("DINAS_LUAR", "Dinas Luar"),
    ("IZIN", "Izin"),
    ("SAKIT", "Sakit"),
    ("CUTI", "Cuti"),
]

KOLOM_RIWAYAT = ["Waktu", "Nilai Sebelum", "Nilai Sesudah", "Alasan", "Lampiran", "Status"]

FILTER_BERKAS_LAMPIRAN = "Gambar atau PDF (*.jpg *.jpeg *.png *.pdf)"


class LayarKoreksiGuru(QMainWindow):
    def __init__(self, *, klien: KlienKoreksiGuru) -> None:
        super().__init__()
        self.setWindowTitle("Koreksi Guru")
        self._klien = klien
        self._id_absensi_saat_ini: int | None = None
        self._riwayat_saat_ini: list[dict] = []
        self._lampiran_terpilih: Path | None = None

        self._id_guru = QLineEdit()
        self._id_guru.setPlaceholderText("ID Guru")
        self._tanggal = QDateEdit(QDate.currentDate())
        self._tanggal.setCalendarPopup(True)
        tombol_cek = QPushButton("Cek Status")
        tombol_cek.clicked.connect(self._cek_status)

        bar_cari = QHBoxLayout()
        bar_cari.addWidget(QLabel("ID Guru:"))
        bar_cari.addWidget(self._id_guru)
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

        self._label_lampiran = QLabel("(tidak ada lampiran dipilih)")
        tombol_pilih_lampiran = QPushButton("Pilih Lampiran...")
        tombol_pilih_lampiran.clicked.connect(self._pilih_lampiran)
        tombol_hapus_lampiran = QPushButton("Hapus Pilihan")
        tombol_hapus_lampiran.clicked.connect(self._hapus_lampiran_terpilih)

        bar_lampiran = QHBoxLayout()
        bar_lampiran.addWidget(self._label_lampiran, 1)
        bar_lampiran.addWidget(tombol_pilih_lampiran)
        bar_lampiran.addWidget(tombol_hapus_lampiran)

        self._tombol_simpan = QPushButton("Simpan Koreksi")
        self._tombol_simpan.setEnabled(False)
        self._tombol_simpan.clicked.connect(self._simpan)

        form = QFormLayout()
        form.addRow("Status Baru", self._status_baru)
        form.addRow("Alasan", self._alasan)
        form.addRow("Lampiran (foto surat, opsional)", bar_lampiran)

        self._tabel_riwayat = QTableWidget(0, len(KOLOM_RIWAYAT))
        self._tabel_riwayat.setHorizontalHeaderLabels(KOLOM_RIWAYAT)
        self._tabel_riwayat.setEditTriggers(QTableWidget.EditTrigger.NoEditTriggers)
        self._tabel_riwayat.setSelectionBehavior(QTableWidget.SelectionBehavior.SelectRows)
        self._tabel_riwayat.setSelectionMode(QTableWidget.SelectionMode.SingleSelection)
        self._tabel_riwayat.verticalHeader().setDefaultSectionSize(24)

        self._tombol_lihat_lampiran = QPushButton("Lihat Lampiran Terpilih")
        self._tombol_lihat_lampiran.clicked.connect(self._lihat_lampiran_terpilih)
        self._tombol_batalkan = QPushButton("Batalkan Koreksi Terpilih")
        self._tombol_batalkan.clicked.connect(self._batalkan_terpilih)

        bar_riwayat = QHBoxLayout()
        bar_riwayat.addWidget(self._tombol_lihat_lampiran)
        bar_riwayat.addWidget(self._tombol_batalkan)
        bar_riwayat.addStretch(1)

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
        tata_letak.addLayout(bar_riwayat)
        self.setCentralWidget(pusat)

    def _id_guru_int(self) -> int | None:
        teks = self._id_guru.text().strip()
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
        id_guru = self._id_guru_int()
        if id_guru is None:
            QMessageBox.warning(self, "ID Guru tidak valid", "Isi ID Guru dengan angka.")
            return

        try:
            status = self._klien.status_saat_ini(id_guru=id_guru, tanggal=self._tanggal_iso())
        except (ServerMenolak, GagalHubungiServer) as exc:
            QMessageBox.warning(self, "Gagal memuat status", str(exc))
            self._form_koreksi_widget.setEnabled(False)
            self._label_konfirmasi.setText("Cek status dulu sebelum mengoreksi.")
            return

        self._id_absensi_saat_ini = status.get("id_absensi")
        self._label_konfirmasi.setText(
            f"{status['nama_lengkap']} - "
            f"status masuk saat ini: {status['status_masuk_sekarang'] or '(belum scan)'}, "
            f"status final saat ini: {status['status_final_sekarang'] or '(belum ada)'}"
        )
        self._form_koreksi_widget.setEnabled(True)
        self._perbarui_tombol_simpan()
        self._muat_riwayat(id_guru)

    def _muat_riwayat(self, id_guru: int) -> None:
        try:
            riwayat = self._klien.daftar_riwayat(id_guru=id_guru, tanggal=self._tanggal_iso())
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
                "Ada" if k["lampiran"] else "-",
                "Dibatalkan" if k["dibatalkan"] else "Aktif",
            ]
            for kolom, teks in enumerate(nilai):
                self._tabel_riwayat.setItem(i, kolom, QTableWidgetItem(str(teks)))

    # ------------------------------------------------------------------
    # Lampiran (form Simpan)
    # ------------------------------------------------------------------

    def _pilih_lampiran(self) -> None:
        path_teks, _filter = QFileDialog.getOpenFileName(self, "Pilih Lampiran", "", FILTER_BERKAS_LAMPIRAN)
        if not path_teks:
            return
        self._lampiran_terpilih = Path(path_teks)
        self._label_lampiran.setText(self._lampiran_terpilih.name)

    def _hapus_lampiran_terpilih(self) -> None:
        self._lampiran_terpilih = None
        self._label_lampiran.setText("(tidak ada lampiran dipilih)")

    def _simpan(self) -> None:
        id_guru = self._id_guru_int()
        if id_guru is None or not self._alasan.text().strip():
            return  # tombol seharusnya sudah nonaktif - jaring pengaman saja

        try:
            self._klien.koreksi(
                id_guru=id_guru,
                tanggal=self._tanggal_iso(),
                status_baru=self._status_baru.currentData(),
                alasan=self._alasan.text().strip(),
                lampiran_path=self._lampiran_terpilih,
            )
        except (ServerMenolak, GagalHubungiServer) as exc:
            QMessageBox.warning(self, "Gagal menyimpan koreksi", str(exc))
            return

        self._alasan.clear()
        self._hapus_lampiran_terpilih()
        QMessageBox.information(self, "Tersimpan", "Koreksi berhasil disimpan.")
        self._cek_status()

    # ------------------------------------------------------------------
    # Riwayat - lihat lampiran, batalkan
    # ------------------------------------------------------------------

    def _baris_riwayat_terpilih(self) -> dict | None:
        baris = self._tabel_riwayat.currentRow()
        if baris < 0 or baris >= len(self._riwayat_saat_ini):
            return None
        return self._riwayat_saat_ini[baris]

    def _lihat_lampiran_terpilih(self) -> None:
        koreksi = self._baris_riwayat_terpilih()
        if koreksi is None:
            QMessageBox.information(self, "Lihat Lampiran", "Pilih satu baris riwayat terlebih dahulu.")
            return
        if not koreksi["lampiran"]:
            QMessageBox.information(self, "Lihat Lampiran", "Koreksi ini tidak memiliki lampiran.")
            return

        try:
            isi = self._klien.unduh_lampiran(koreksi["id"])
        except (ServerMenolak, GagalHubungiServer) as exc:
            QMessageBox.warning(self, "Gagal mengunduh lampiran", str(exc))
            return

        ekstensi = Path(koreksi["lampiran"]).suffix or ".bin"
        berkas_sementara = tempfile.NamedTemporaryFile(suffix=ekstensi, delete=False)
        try:
            berkas_sementara.write(isi)
        finally:
            berkas_sementara.close()

        try:
            os.startfile(berkas_sementara.name)  # noqa: S606 - membuka dengan aplikasi bawaan Windows
        except OSError as exc:
            QMessageBox.warning(self, "Tidak bisa membuka lampiran", f"Lampiran tersimpan di {berkas_sementara.name}: {exc}")

    def _batalkan_terpilih(self) -> None:
        koreksi = self._baris_riwayat_terpilih()
        if koreksi is None:
            QMessageBox.information(self, "Batalkan Koreksi", "Pilih satu baris riwayat terlebih dahulu.")
            return
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
