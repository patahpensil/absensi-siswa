"""Dialog "Tambah Entri Kalender" - pratinjau dampak WAJIB sebelum simpan
(PRD §8.3: perubahan berlaku surut, wajib pratinjau dampak). Tombol
Simpan tetap nonaktif sampai dampak dihitung UNTUK NILAI FIELD YANG
SEDANG DIISI - mengubah tanggal/jenis setelah menghitung dampak membatalkan
pratinjau lama supaya angka yang ditampilkan tidak pernah basi.
"""

from __future__ import annotations

from datetime import date

from PySide6.QtCore import QDate
from PySide6.QtWidgets import (
    QComboBox,
    QDateEdit,
    QDialog,
    QDialogButtonBox,
    QFormLayout,
    QLabel,
    QLineEdit,
    QMessageBox,
    QPushButton,
    QVBoxLayout,
)

from app.ui.kalender.klien_kalender import GagalHubungiServer, KlienKalender, ServerMenolak
from app.ui.kalender.validasi_form import validasi_entri, validasi_salin_tahun

JENIS_HARI_PILIHAN = [
    ("EFEKTIF", "Efektif"),
    ("LIBUR_NASIONAL", "Libur Nasional"),
    ("LIBUR_SEMESTER", "Libur Semester"),
    ("KEGIATAN_TANPA_KBM", "Kegiatan Tanpa KBM"),
    ("LIBUR_MENDADAK", "Libur Mendadak"),
]


def _isi_combo_jenis_hari(combo: QComboBox) -> None:
    for nilai, label in JENIS_HARI_PILIHAN:
        combo.addItem(label, nilai)


class DialogTambahEntri(QDialog):
    def __init__(self, *, klien: KlienKalender, tahun_ajaran_bawaan: str = "", parent=None) -> None:
        super().__init__(parent)
        self.setWindowTitle("Tambah Entri Kalender")
        self._klien = klien
        self.entri_tersimpan: dict | None = None
        #: field yang sudah dihitung dampaknya - None berarti pratinjau
        #: belum ada/sudah basi karena field berubah.
        self._dampak_untuk: tuple | None = None

        self._mulai = QDateEdit(QDate.currentDate())
        self._mulai.setCalendarPopup(True)
        self._selesai = QDateEdit(QDate.currentDate())
        self._selesai.setCalendarPopup(True)
        self._keterangan = QLineEdit()
        self._jenis_siswa = QComboBox()
        _isi_combo_jenis_hari(self._jenis_siswa)
        self._jenis_guru = QComboBox()
        _isi_combo_jenis_hari(self._jenis_guru)
        self._tahun_ajaran = QLineEdit(tahun_ajaran_bawaan)

        self._label_dampak = QLabel("Isi tanggal dan jenis hari, lalu klik “Hitung Dampak”.")
        self._label_dampak.setWordWrap(True)
        self._tombol_dampak = QPushButton("Hitung Dampak")
        self._tombol_dampak.clicked.connect(self._hitung_dampak)

        self._alasan = QLineEdit()
        self._alasan.setEnabled(False)
        self._alasan.setPlaceholderText("Wajib diisi setelah dampak dihitung")

        self._tombol = QDialogButtonBox(QDialogButtonBox.StandardButton.Cancel)
        self._tombol_simpan = QPushButton("Simpan")
        self._tombol_simpan.setEnabled(False)
        self._tombol.addButton(self._tombol_simpan, QDialogButtonBox.ButtonRole.AcceptRole)
        self._tombol.rejected.connect(self.reject)
        self._tombol_simpan.clicked.connect(self._simpan)

        form = QFormLayout()
        form.addRow("Tanggal Mulai", self._mulai)
        form.addRow("Tanggal Selesai", self._selesai)
        form.addRow("Keterangan", self._keterangan)
        form.addRow("Jenis Hari (Siswa)", self._jenis_siswa)
        form.addRow("Jenis Hari (Guru)", self._jenis_guru)
        form.addRow("Tahun Ajaran", self._tahun_ajaran)
        form.addRow("Alasan", self._alasan)

        tata_letak = QVBoxLayout(self)
        tata_letak.addLayout(form)
        tata_letak.addWidget(self._tombol_dampak)
        tata_letak.addWidget(self._label_dampak)
        tata_letak.addWidget(self._tombol)

        for widget, sinyal in (
            (self._mulai, self._mulai.dateChanged),
            (self._selesai, self._selesai.dateChanged),
            (self._jenis_siswa, self._jenis_siswa.currentIndexChanged),
            (self._jenis_guru, self._jenis_guru.currentIndexChanged),
        ):
            sinyal.connect(self._batalkan_pratinjau_lama)

    def _field_saat_ini(self) -> tuple:
        return (
            self._mulai.date().toPython(),
            self._selesai.date().toPython(),
            self._jenis_siswa.currentData(),
            self._jenis_guru.currentData(),
        )

    def _batalkan_pratinjau_lama(self, *_args) -> None:
        if self._dampak_untuk is not None:
            self._dampak_untuk = None
            self._alasan.setEnabled(False)
            self._tombol_simpan.setEnabled(False)
            self._label_dampak.setText("Field berubah - hitung ulang dampaknya sebelum menyimpan.")

    def _hitung_dampak(self) -> None:
        mulai, selesai, jenis_siswa, jenis_guru = self._field_saat_ini()
        hasil_validasi = validasi_entri(
            mulai=mulai, selesai=selesai, keterangan=self._keterangan.text(), tahun_ajaran=self._tahun_ajaran.text()
        )
        if not hasil_validasi.valid:
            QMessageBox.warning(self, "Data belum lengkap", "\n".join(hasil_validasi.galat.values()))
            return

        try:
            dampak = self._klien.pratinjau_dampak(
                {
                    "mulai": mulai.isoformat(),
                    "selesai": selesai.isoformat(),
                    "jenis_siswa": jenis_siswa,
                    "jenis_guru": jenis_guru,
                }
            )
        except (ServerMenolak, GagalHubungiServer) as exc:
            QMessageBox.warning(self, "Gagal menghitung dampak", str(exc))
            return

        self._dampak_untuk = (mulai, selesai, jenis_siswa, jenis_guru)
        self._label_dampak.setText(
            f"Perubahan ini memengaruhi {dampak['jumlah_hari']} hari dan perkiraan "
            f"{dampak['perkiraan_baris']} baris absensi."
        )
        self._alasan.setEnabled(True)
        self._tombol_simpan.setEnabled(True)

    def _simpan(self) -> None:
        if self._dampak_untuk != self._field_saat_ini():
            QMessageBox.warning(self, "Dampak belum dihitung ulang", "Hitung dampak lagi untuk nilai saat ini.")
            return
        if not self._alasan.text().strip():
            QMessageBox.warning(self, "Alasan wajib diisi", "Tuliskan alasan perubahan kalender ini.")
            return

        mulai, selesai, jenis_siswa, jenis_guru = self._field_saat_ini()
        try:
            entri = self._klien.tambah(
                {
                    "mulai": mulai.isoformat(),
                    "selesai": selesai.isoformat(),
                    "keterangan": self._keterangan.text().strip(),
                    "jenis_siswa": jenis_siswa,
                    "jenis_guru": jenis_guru,
                    "tahun_ajaran": self._tahun_ajaran.text().strip(),
                    "alasan": self._alasan.text().strip(),
                }
            )
        except (ServerMenolak, GagalHubungiServer) as exc:
            QMessageBox.warning(self, "Gagal menyimpan", str(exc))
            return

        self.entri_tersimpan = entri
        self.accept()


class DialogBatalkanEntri(QDialog):
    def __init__(self, *, klien: KlienKalender, id_entri: int, keterangan: str, parent=None) -> None:
        super().__init__(parent)
        self.setWindowTitle("Batalkan Entri Kalender")
        self._klien = klien
        self._id_entri = id_entri
        self.dibatalkan = False

        self._alasan = QLineEdit()
        tombol = QDialogButtonBox(QDialogButtonBox.StandardButton.Cancel)
        tombol_batalkan = QPushButton("Batalkan Entri")
        tombol.addButton(tombol_batalkan, QDialogButtonBox.ButtonRole.AcceptRole)
        tombol.rejected.connect(self.reject)
        tombol_batalkan.clicked.connect(self._batalkan)

        form = QFormLayout()
        form.addRow(QLabel(f"Entri: {keterangan}"))
        form.addRow("Alasan", self._alasan)

        tata_letak = QVBoxLayout(self)
        tata_letak.addLayout(form)
        tata_letak.addWidget(tombol)

    def _batalkan(self) -> None:
        if not self._alasan.text().strip():
            QMessageBox.warning(self, "Alasan wajib diisi", "Tuliskan alasan pembatalan.")
            return
        try:
            self._klien.batalkan(self._id_entri, alasan=self._alasan.text().strip())
        except (ServerMenolak, GagalHubungiServer) as exc:
            QMessageBox.warning(self, "Gagal membatalkan", str(exc))
            return
        self.dibatalkan = True
        self.accept()


class DialogSalinTahun(QDialog):
    def __init__(self, *, klien: KlienKalender, daftar_tahun_ajaran: list[str], parent=None) -> None:
        super().__init__(parent)
        self.setWindowTitle("Salin dari Tahun Sebelumnya")
        self._klien = klien
        self.hasil: list[dict] | None = None

        self._tahun_asal = QComboBox()
        self._tahun_asal.addItems(daftar_tahun_ajaran)
        self._tahun_tujuan = QLineEdit()
        self._alasan = QLineEdit()

        tombol = QDialogButtonBox(QDialogButtonBox.StandardButton.Cancel)
        tombol_salin = QPushButton("Salin")
        tombol.addButton(tombol_salin, QDialogButtonBox.ButtonRole.AcceptRole)
        tombol.rejected.connect(self.reject)
        tombol_salin.clicked.connect(self._salin)

        form = QFormLayout()
        form.addRow("Tahun Ajaran Asal", self._tahun_asal)
        form.addRow("Tahun Ajaran Tujuan", self._tahun_tujuan)
        form.addRow("Alasan", self._alasan)

        tata_letak = QVBoxLayout(self)
        tata_letak.addLayout(form)
        tata_letak.addWidget(
            QLabel("Menyalin seluruh entri aktif dari tahun asal sebagai titik awal tahun tujuan.")
        )
        tata_letak.addWidget(tombol)

    def _salin(self) -> None:
        hasil_validasi = validasi_salin_tahun(
            tahun_asal=self._tahun_asal.currentText(), tahun_tujuan=self._tahun_tujuan.text()
        )
        if not hasil_validasi.valid:
            QMessageBox.warning(self, "Data belum lengkap", "\n".join(hasil_validasi.galat.values()))
            return
        if not self._alasan.text().strip():
            QMessageBox.warning(self, "Alasan wajib diisi", "Tuliskan alasan penyalinan.")
            return

        try:
            self.hasil = self._klien.salin_tahun(
                {
                    "tahun_asal": self._tahun_asal.currentText(),
                    "tahun_tujuan": self._tahun_tujuan.text().strip(),
                    "alasan": self._alasan.text().strip(),
                }
            )
        except (ServerMenolak, GagalHubungiServer) as exc:
            QMessageBox.warning(self, "Gagal menyalin", str(exc))
            return
        self.accept()
