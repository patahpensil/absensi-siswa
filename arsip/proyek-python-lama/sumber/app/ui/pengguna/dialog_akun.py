"""Dialog manajemen akun & peran (PRD §10): buat akun, reset sandi,
atur kelas diampu.

Pilihan peran yang ditawarkan DialogBuatAkun mengikuti peran akun yang
sedang login (kenyamanan tampilan) - kepala sekolah melihat keempat
peran, admin hanya wali kelas & operator. Penegakan sesungguhnya tetap
di server (app.server.pengguna_service.HIRARKI_WEWENANG_AKUN); dialog
ini tidak menduplikasi aturannya, hanya mempersempit pilihan supaya
pengguna tidak mencoba sesuatu yang pasti ditolak.
"""

from __future__ import annotations

from PySide6.QtWidgets import (
    QComboBox,
    QDialog,
    QDialogButtonBox,
    QFormLayout,
    QLineEdit,
    QMessageBox,
    QPushButton,
    QVBoxLayout,
)

from app.ui.pengguna.klien_pengguna import GagalHubungiServer, KlienPengguna, ServerMenolak

_PERAN_UNTUK_KEPALA_SEKOLAH = [
    ("KEPALA_SEKOLAH", "Kepala Sekolah"),
    ("ADMIN", "Admin (TU)"),
    ("WALI_KELAS", "Wali Kelas"),
    ("OPERATOR", "Operator"),
]
_PERAN_UNTUK_ADMIN = [("WALI_KELAS", "Wali Kelas"), ("OPERATOR", "Operator")]


class DialogBuatAkun(QDialog):
    def __init__(self, *, klien: KlienPengguna, daftar_kelas: list[dict], parent=None) -> None:
        super().__init__(parent)
        self.setWindowTitle("Buat Akun")
        self._klien = klien
        self.akun_tersimpan: dict | None = None

        self._username = QLineEdit()
        self._password_awal = QLineEdit()
        self._password_awal.setEchoMode(QLineEdit.EchoMode.Password)

        self._peran = QComboBox()
        pilihan = _PERAN_UNTUK_KEPALA_SEKOLAH if klien.peran == "KEPALA_SEKOLAH" else _PERAN_UNTUK_ADMIN
        for nilai, label in pilihan:
            self._peran.addItem(label, nilai)
        self._peran.currentIndexChanged.connect(self._perbarui_kelas_diampu_terlihat)

        self._kelas_diampu = QComboBox()
        self._kelas_diampu.addItem("(Belum ditugaskan)", None)
        for k in daftar_kelas:
            self._kelas_diampu.addItem(k["nama_kelas"], k["id"])

        form = QFormLayout()
        form.addRow("Username", self._username)
        form.addRow("Sandi Awal", self._password_awal)
        form.addRow("Peran", self._peran)
        self._baris_kelas_diampu = form.rowCount()
        form.addRow("Kelas Diampu", self._kelas_diampu)

        tombol = QDialogButtonBox(QDialogButtonBox.StandardButton.Cancel)
        tombol_simpan = QPushButton("Buat Akun")
        tombol.addButton(tombol_simpan, QDialogButtonBox.ButtonRole.AcceptRole)
        tombol.rejected.connect(self.reject)
        tombol_simpan.clicked.connect(self._simpan)

        tata_letak = QVBoxLayout(self)
        tata_letak.addLayout(form)
        tata_letak.addWidget(tombol)

        self._perbarui_kelas_diampu_terlihat()

    def _perbarui_kelas_diampu_terlihat(self, *_args) -> None:
        terlihat = self._peran.currentData() == "WALI_KELAS"
        self._kelas_diampu.setVisible(terlihat)

    def _simpan(self) -> None:
        username = self._username.text().strip()
        password_awal = self._password_awal.text()
        if not username:
            QMessageBox.warning(self, "Data belum lengkap", "Username wajib diisi.")
            return
        if len(password_awal) < 8:
            QMessageBox.warning(self, "Sandi terlalu pendek", "Sandi awal minimal 8 karakter.")
            return

        payload = {"username": username, "password_awal": password_awal, "peran": self._peran.currentData()}
        if self._peran.currentData() == "WALI_KELAS" and self._kelas_diampu.currentData() is not None:
            payload["id_kelas_diampu"] = self._kelas_diampu.currentData()

        try:
            self.akun_tersimpan = self._klien.buat_akun(payload)
        except (ServerMenolak, GagalHubungiServer) as exc:
            QMessageBox.warning(self, "Gagal membuat akun", str(exc))
            return
        self.accept()


class DialogResetPassword(QDialog):
    def __init__(self, *, klien: KlienPengguna, id_target: int, username_target: str, parent=None) -> None:
        super().__init__(parent)
        self.setWindowTitle("Reset Sandi")
        self._klien = klien
        self._id_target = id_target
        self.berhasil = False

        self._password_baru = QLineEdit()
        self._password_baru.setEchoMode(QLineEdit.EchoMode.Password)

        form = QFormLayout()
        form.addRow("Akun", QLineEdit(username_target, readOnly=True))
        form.addRow("Sandi Baru", self._password_baru)

        tombol = QDialogButtonBox(QDialogButtonBox.StandardButton.Cancel)
        tombol_reset = QPushButton("Reset Sandi")
        tombol.addButton(tombol_reset, QDialogButtonBox.ButtonRole.AcceptRole)
        tombol.rejected.connect(self.reject)
        tombol_reset.clicked.connect(self._simpan)

        tata_letak = QVBoxLayout(self)
        tata_letak.addLayout(form)
        tata_letak.addWidget(tombol)

    def _simpan(self) -> None:
        password_baru = self._password_baru.text()
        if len(password_baru) < 8:
            QMessageBox.warning(self, "Sandi terlalu pendek", "Sandi baru minimal 8 karakter.")
            return
        try:
            self._klien.reset_password(self._id_target, password_baru=password_baru)
        except (ServerMenolak, GagalHubungiServer) as exc:
            QMessageBox.warning(self, "Gagal mereset sandi", str(exc))
            return
        self.berhasil = True
        self.accept()


class DialogAturKelasDiampu(QDialog):
    def __init__(
        self, *, klien: KlienPengguna, id_target: int, username_target: str, daftar_kelas: list[dict],
        id_kelas_saat_ini: int | None, parent=None,
    ) -> None:
        super().__init__(parent)
        self.setWindowTitle("Atur Kelas Diampu")
        self._klien = klien
        self._id_target = id_target
        self.berhasil = False

        self._kelas = QComboBox()
        self._kelas.addItem("(Belum ditugaskan)", None)
        for k in daftar_kelas:
            self._kelas.addItem(k["nama_kelas"], k["id"])
        indeks = self._kelas.findData(id_kelas_saat_ini)
        self._kelas.setCurrentIndex(indeks if indeks >= 0 else 0)

        form = QFormLayout()
        form.addRow("Akun", QLineEdit(username_target, readOnly=True))
        form.addRow("Kelas Diampu", self._kelas)

        tombol = QDialogButtonBox(QDialogButtonBox.StandardButton.Cancel)
        tombol_simpan = QPushButton("Simpan")
        tombol.addButton(tombol_simpan, QDialogButtonBox.ButtonRole.AcceptRole)
        tombol.rejected.connect(self.reject)
        tombol_simpan.clicked.connect(self._simpan)

        tata_letak = QVBoxLayout(self)
        tata_letak.addLayout(form)
        tata_letak.addWidget(tombol)

    def _simpan(self) -> None:
        try:
            self._klien.atur_kelas_diampu(self._id_target, id_kelas_diampu=self._kelas.currentData())
        except (ServerMenolak, GagalHubungiServer) as exc:
            QMessageBox.warning(self, "Gagal menyimpan", str(exc))
            return
        self.berhasil = True
        self.accept()
