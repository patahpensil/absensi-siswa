"""Layar Manajemen Akun & Peran (PRD §10). Akses kepala sekolah/admin
SAJA - kalau akun yang login bukan keduanya, setiap panggilan API gagal
403 dan layar menampilkan pesan yang jelas.
"""

from __future__ import annotations

from PySide6.QtWidgets import (
    QHBoxLayout,
    QMainWindow,
    QMessageBox,
    QPushButton,
    QTableWidget,
    QTableWidgetItem,
    QVBoxLayout,
    QWidget,
)

from app.ui.pengguna.dialog_akun import DialogAturKelasDiampu, DialogBuatAkun, DialogResetPassword
from app.ui.pengguna.klien_pengguna import GagalHubungiServer, KlienPengguna, ServerMenolak

KOLOM = ["Username", "Peran", "Kelas Diampu", "Aktif", "Wajib Ganti Sandi"]


def _pesan_akses(exc: Exception) -> str:
    if isinstance(exc, ServerMenolak) and exc.status_code == 403:
        return "Akun ini tidak berwenang mengakses Manajemen Akun - hanya kepala sekolah dan admin."
    return str(exc)


class LayarPengguna(QMainWindow):
    def __init__(self, *, klien: KlienPengguna) -> None:
        super().__init__()
        self.setWindowTitle("Manajemen Akun & Peran")
        self._klien = klien
        self._akun_saat_ini: list[dict] = []
        self._kelas_by_id: dict[int, str] = {}

        tombol_buat = QPushButton("Buat Akun")
        tombol_buat.clicked.connect(self._buat_akun)
        tombol_reset = QPushButton("Reset Sandi")
        tombol_reset.clicked.connect(self._reset_password)
        tombol_kelas = QPushButton("Atur Kelas Diampu")
        tombol_kelas.clicked.connect(self._atur_kelas_diampu)
        tombol_muat_ulang = QPushButton("Muat Ulang")
        tombol_muat_ulang.clicked.connect(self.muat_ulang)

        bar = QHBoxLayout()
        bar.addWidget(tombol_buat)
        bar.addWidget(tombol_reset)
        bar.addWidget(tombol_kelas)
        bar.addStretch(1)
        bar.addWidget(tombol_muat_ulang)

        self._tabel = QTableWidget(0, len(KOLOM))
        self._tabel.setHorizontalHeaderLabels(KOLOM)
        self._tabel.setEditTriggers(QTableWidget.EditTrigger.NoEditTriggers)
        self._tabel.setSelectionBehavior(QTableWidget.SelectionBehavior.SelectRows)
        self._tabel.setSelectionMode(QTableWidget.SelectionMode.SingleSelection)
        self._tabel.verticalHeader().setDefaultSectionSize(24)

        pusat = QWidget()
        tata_letak = QVBoxLayout(pusat)
        tata_letak.addLayout(bar)
        tata_letak.addWidget(self._tabel)
        self.setCentralWidget(pusat)

    def muat_ulang(self) -> None:
        try:
            akun = self._klien.daftar_akun()
            kelas = self._klien.daftar_kelas()
        except (ServerMenolak, GagalHubungiServer) as exc:
            QMessageBox.warning(self, "Gagal memuat data", _pesan_akses(exc))
            return

        self._kelas_by_id = {k["id"]: k["nama_kelas"] for k in kelas}
        self._akun_saat_ini = akun
        self._isi_tabel(akun)

    def _isi_tabel(self, akun: list[dict]) -> None:
        self._tabel.setRowCount(len(akun))
        for baris, a in enumerate(akun):
            nama_kelas = self._kelas_by_id.get(a["id_kelas_diampu"], "-") if a["id_kelas_diampu"] else "-"
            nilai = [
                a["username"],
                a["peran"],
                nama_kelas,
                "Ya" if a["aktif"] else "Tidak",
                "Ya" if a["wajib_ganti_password"] else "Tidak",
            ]
            for kolom, teks in enumerate(nilai):
                self._tabel.setItem(baris, kolom, QTableWidgetItem(str(teks)))

    def _baris_terpilih(self) -> dict | None:
        baris = self._tabel.currentRow()
        if baris < 0 or baris >= len(self._akun_saat_ini):
            return None
        return self._akun_saat_ini[baris]

    def _daftar_kelas_untuk_dialog(self) -> list[dict]:
        return [{"id": id_kelas, "nama_kelas": nama} for id_kelas, nama in self._kelas_by_id.items()]

    def _buat_akun(self) -> None:
        dialog = DialogBuatAkun(klien=self._klien, daftar_kelas=self._daftar_kelas_untuk_dialog(), parent=self)
        if dialog.exec():
            self.muat_ulang()

    def _reset_password(self) -> None:
        akun = self._baris_terpilih()
        if akun is None:
            QMessageBox.information(self, "Reset Sandi", "Pilih satu akun terlebih dahulu.")
            return
        dialog = DialogResetPassword(klien=self._klien, id_target=akun["id"], username_target=akun["username"], parent=self)
        if dialog.exec():
            QMessageBox.information(self, "Berhasil", f"Sandi akun {akun['username']} sudah direset.")

    def _atur_kelas_diampu(self) -> None:
        akun = self._baris_terpilih()
        if akun is None:
            QMessageBox.information(self, "Atur Kelas Diampu", "Pilih satu akun terlebih dahulu.")
            return
        if akun["peran"] != "WALI_KELAS":
            QMessageBox.information(self, "Atur Kelas Diampu", "Hanya akun berperan Wali Kelas yang punya kelas diampu.")
            return
        dialog = DialogAturKelasDiampu(
            klien=self._klien,
            id_target=akun["id"],
            username_target=akun["username"],
            daftar_kelas=self._daftar_kelas_untuk_dialog(),
            id_kelas_saat_ini=akun["id_kelas_diampu"],
            parent=self,
        )
        if dialog.exec():
            self.muat_ulang()
