"""Dialog "Tambah Jadwal" - selalu menambah VERSI baru dengan tanggal
mulai berlaku (PRD §4), tidak pernah menimpa baris lama. Lihat
app/server/routers/pengaturan.py untuk alasan kenapa ini aman dilakukan
tanpa mengubah berlaku_sampai baris lama sama sekali.
"""

from __future__ import annotations

from PySide6.QtCore import QDate, QTime
from PySide6.QtWidgets import (
    QCheckBox,
    QComboBox,
    QDateEdit,
    QDialog,
    QDialogButtonBox,
    QFormLayout,
    QMessageBox,
    QPushButton,
    QTimeEdit,
    QVBoxLayout,
)

from app.ui.pengaturan.klien_pengaturan import GagalHubungiServer, KlienPengaturan, ServerMenolak
from app.ui.pengaturan.validasi_jadwal import validasi_jadwal

#: PRD/SKILL.md: hari sekolah Senin-Sabtu, Minggu selalu libur - tidak
#: ditawarkan sebagai pilihan jadwal sama sekali.
HARI_PILIHAN = [(1, "Senin"), (2, "Selasa"), (3, "Rabu"), (4, "Kamis"), (5, "Jumat"), (6, "Sabtu")]


def _ke_time(qtime: QTime):
    return qtime.toPython()


class DialogTambahJadwal(QDialog):
    def __init__(self, *, klien: KlienPengaturan, jenis_pemilik: str, parent=None) -> None:
        super().__init__(parent)
        self.setWindowTitle(f"Tambah Jadwal - {jenis_pemilik.capitalize()}")
        self._klien = klien
        self._jenis_pemilik = jenis_pemilik
        self.jadwal_tersimpan: dict | None = None

        self._hari = QComboBox()
        for nilai, label in HARI_PILIHAN:
            self._hari.addItem(label, nilai)

        self._jam_masuk = QTimeEdit(QTime(7, 15))
        self._batas_terlambat = QTimeEdit(QTime(7, 20, 59))
        self._jam_pulang = QTimeEdit(QTime(15, 0))
        self._berlaku_mulai = QDateEdit(QDate.currentDate())
        self._berlaku_mulai.setCalendarPopup(True)

        self._cutoff_alpa = QTimeEdit(QTime(7, 30))
        self._pulang_mulai = QTimeEdit(QTime(13, 20))

        self._pakai_berlaku_sampai = QCheckBox("Batasi sampai tanggal tertentu")
        self._berlaku_sampai = QDateEdit(QDate.currentDate())
        self._berlaku_sampai.setCalendarPopup(True)
        self._berlaku_sampai.setEnabled(False)
        self._pakai_berlaku_sampai.toggled.connect(self._berlaku_sampai.setEnabled)

        form = QFormLayout()
        form.addRow("Hari", self._hari)
        form.addRow("Jam Masuk", self._jam_masuk)
        form.addRow("Batas Terlambat", self._batas_terlambat)
        if jenis_pemilik == "SISWA":
            form.addRow("Penetapan ALPA", self._cutoff_alpa)
            form.addRow("Awal Jendela Pulang", self._pulang_mulai)
        form.addRow("Jam Pulang", self._jam_pulang)
        form.addRow("Berlaku Mulai", self._berlaku_mulai)
        form.addRow(self._pakai_berlaku_sampai)
        form.addRow("Berlaku Sampai", self._berlaku_sampai)

        tombol = QDialogButtonBox(QDialogButtonBox.StandardButton.Cancel)
        tombol_simpan = QPushButton("Simpan")
        tombol.addButton(tombol_simpan, QDialogButtonBox.ButtonRole.AcceptRole)
        tombol.rejected.connect(self.reject)
        tombol_simpan.clicked.connect(self._simpan)

        tata_letak = QVBoxLayout(self)
        tata_letak.addLayout(form)
        tata_letak.addWidget(tombol)

    def _simpan(self) -> None:
        jam_masuk = _ke_time(self._jam_masuk.time())
        batas_terlambat = _ke_time(self._batas_terlambat.time())
        jam_pulang = _ke_time(self._jam_pulang.time())
        cutoff_alpa = _ke_time(self._cutoff_alpa.time()) if self._jenis_pemilik == "SISWA" else None
        pulang_mulai = _ke_time(self._pulang_mulai.time()) if self._jenis_pemilik == "SISWA" else None

        hasil_validasi = validasi_jadwal(
            jenis_pemilik=self._jenis_pemilik,
            jam_masuk=jam_masuk,
            batas_terlambat=batas_terlambat,
            jam_pulang=jam_pulang,
            cutoff_alpa=cutoff_alpa,
            pulang_mulai=pulang_mulai,
        )
        if not hasil_validasi.valid:
            QMessageBox.warning(self, "Jam tidak wajar", "\n".join(hasil_validasi.galat.values()))
            return

        payload = {
            "jenis_pemilik": self._jenis_pemilik,
            "hari": self._hari.currentData(),
            "jam_masuk": jam_masuk.isoformat(),
            "batas_terlambat": batas_terlambat.isoformat(),
            "jam_pulang": jam_pulang.isoformat(),
            "berlaku_mulai": self._berlaku_mulai.date().toPython().isoformat(),
        }
        if cutoff_alpa is not None:
            payload["cutoff_alpa"] = cutoff_alpa.isoformat()
        if pulang_mulai is not None:
            payload["pulang_mulai"] = pulang_mulai.isoformat()
        if self._pakai_berlaku_sampai.isChecked():
            payload["berlaku_sampai"] = self._berlaku_sampai.date().toPython().isoformat()

        try:
            self.jadwal_tersimpan = self._klien.tambah_jadwal(payload)
        except (ServerMenolak, GagalHubungiServer) as exc:
            QMessageBox.warning(self, "Gagal menyimpan jadwal", str(exc))
            return
        self.accept()
