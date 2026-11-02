"""Layar Kalender Akademik - dua jalur siswa/guru (PRD §8). Hanya
kepala sekolah/admin yang bisa menambah/membatalkan/menyalin tahun
("wali kelas/operator hanya lihat" - §8.3); tombol-tombol itu
dinonaktifkan untuk peran lain di sini SEBAGAI KENYAMANAN TAMPILAN saja -
penegakan sesungguhnya ada di server (lihat klien_kalender.py).
"""

from __future__ import annotations

from PySide6.QtWidgets import (
    QComboBox,
    QHBoxLayout,
    QHeaderView,
    QLabel,
    QMainWindow,
    QMessageBox,
    QPushButton,
    QTableWidget,
    QTableWidgetItem,
    QVBoxLayout,
    QWidget,
)

from app.ui.kalender.dialog_entri import DialogBatalkanEntri, DialogSalinTahun, DialogTambahEntri
from app.ui.kalender.klien_kalender import GagalHubungiServer, KlienKalender, ServerMenolak

_PERAN_BOLEH_UBAH = {"ADMIN", "KEPALA_SEKOLAH"}

KOLOM = ["Mulai", "Selesai", "Keterangan", "Jenis (Siswa)", "Jenis (Guru)", "Tahun Ajaran", "Status"]


class LayarKalender(QMainWindow):
    def __init__(self, *, klien: KlienKalender) -> None:
        super().__init__()
        self.setWindowTitle("Kalender Akademik")
        self._klien = klien
        self._entri_saat_ini: list[dict] = []

        self._filter_tahun = QComboBox()
        self._filter_tahun.addItem("Semua Tahun Ajaran", None)
        self._filter_tahun.currentIndexChanged.connect(lambda _i: self.muat_ulang())

        self._tombol_muat_ulang = QPushButton("Muat Ulang")
        self._tombol_muat_ulang.clicked.connect(self.muat_ulang)
        self._tombol_tambah = QPushButton("Tambah Entri")
        self._tombol_tambah.clicked.connect(self._tambah_entri)
        self._tombol_batalkan = QPushButton("Batalkan Terpilih")
        self._tombol_batalkan.clicked.connect(self._batalkan_terpilih)
        self._tombol_salin = QPushButton("Salin dari Tahun Sebelumnya")
        self._tombol_salin.clicked.connect(self._salin_tahun)

        bar = QHBoxLayout()
        bar.addWidget(QLabel("Tahun Ajaran:"))
        bar.addWidget(self._filter_tahun)
        bar.addStretch(1)
        bar.addWidget(self._tombol_tambah)
        bar.addWidget(self._tombol_batalkan)
        bar.addWidget(self._tombol_salin)
        bar.addWidget(self._tombol_muat_ulang)

        self._tabel = QTableWidget(0, len(KOLOM))
        self._tabel.setHorizontalHeaderLabels(KOLOM)
        self._tabel.setEditTriggers(QTableWidget.EditTrigger.NoEditTriggers)
        self._tabel.setSelectionBehavior(QTableWidget.SelectionBehavior.SelectRows)
        self._tabel.setSelectionMode(QTableWidget.SelectionMode.SingleSelection)
        self._tabel.horizontalHeader().setSectionResizeMode(2, QHeaderView.ResizeMode.Stretch)
        self._tabel.verticalHeader().setDefaultSectionSize(24)

        pusat = QWidget()
        tata_letak = QVBoxLayout(pusat)
        tata_letak.addLayout(bar)
        tata_letak.addWidget(self._tabel)
        self.setCentralWidget(pusat)

    # ------------------------------------------------------------------

    def muat_ulang(self) -> None:
        tahun_terpilih = self._filter_tahun.currentData()
        try:
            entri = self._klien.daftar(tahun_ajaran=tahun_terpilih)
        except (ServerMenolak, GagalHubungiServer) as exc:
            QMessageBox.warning(self, "Gagal memuat kalender", str(exc))
            return

        self._entri_saat_ini = entri
        self._isi_tabel(entri)
        self._perbarui_filter_tahun(entri)
        self._terapkan_hak_akses()

    def _isi_tabel(self, entri: list[dict]) -> None:
        self._tabel.setRowCount(len(entri))
        for baris, e in enumerate(entri):
            nilai = [
                e["mulai"],
                e["selesai"],
                e["keterangan"],
                e["jenis_siswa"],
                e["jenis_guru"],
                e["tahun_ajaran"],
                "Dibatalkan" if e["dibatalkan"] else "Aktif",
            ]
            for kolom, teks in enumerate(nilai):
                self._tabel.setItem(baris, kolom, QTableWidgetItem(str(teks)))

    def _perbarui_filter_tahun(self, entri: list[dict]) -> None:
        terpilih_sebelumnya = self._filter_tahun.currentData()
        tahun_tersedia = sorted({e["tahun_ajaran"] for e in entri})

        self._filter_tahun.blockSignals(True)
        self._filter_tahun.clear()
        self._filter_tahun.addItem("Semua Tahun Ajaran", None)
        for tahun in tahun_tersedia:
            self._filter_tahun.addItem(tahun, tahun)
        indeks = self._filter_tahun.findData(terpilih_sebelumnya)
        self._filter_tahun.setCurrentIndex(indeks if indeks >= 0 else 0)
        self._filter_tahun.blockSignals(False)

    def _terapkan_hak_akses(self) -> None:
        boleh_ubah = self._klien.peran in _PERAN_BOLEH_UBAH
        self._tombol_tambah.setEnabled(boleh_ubah)
        self._tombol_batalkan.setEnabled(boleh_ubah)
        self._tombol_salin.setEnabled(boleh_ubah)

    def _daftar_tahun_ajaran(self) -> list[str]:
        return sorted({e["tahun_ajaran"] for e in self._entri_saat_ini})

    def _baris_terpilih(self) -> dict | None:
        baris = self._tabel.currentRow()
        if baris < 0 or baris >= len(self._entri_saat_ini):
            return None
        return self._entri_saat_ini[baris]

    # ------------------------------------------------------------------

    def _tambah_entri(self) -> None:
        tahun_bawaan = self._daftar_tahun_ajaran()[-1] if self._daftar_tahun_ajaran() else ""
        dialog = DialogTambahEntri(klien=self._klien, tahun_ajaran_bawaan=tahun_bawaan, parent=self)
        if dialog.exec():
            self.muat_ulang()

    def _batalkan_terpilih(self) -> None:
        entri = self._baris_terpilih()
        if entri is None:
            QMessageBox.information(self, "Batalkan Entri", "Pilih satu entri terlebih dahulu.")
            return
        if entri["dibatalkan"]:
            QMessageBox.information(self, "Batalkan Entri", "Entri ini sudah dibatalkan sebelumnya.")
            return
        dialog = DialogBatalkanEntri(klien=self._klien, id_entri=entri["id"], keterangan=entri["keterangan"], parent=self)
        if dialog.exec():
            self.muat_ulang()

    def _salin_tahun(self) -> None:
        tahun_tersedia = self._daftar_tahun_ajaran()
        if not tahun_tersedia:
            QMessageBox.information(self, "Salin dari Tahun Sebelumnya", "Belum ada kalender yang bisa disalin.")
            return
        dialog = DialogSalinTahun(klien=self._klien, daftar_tahun_ajaran=tahun_tersedia, parent=self)
        if dialog.exec():
            QMessageBox.information(
                self, "Berhasil", f"{len(dialog.hasil or [])} entri disalin ke tahun ajaran baru."
            )
            self.muat_ulang()
