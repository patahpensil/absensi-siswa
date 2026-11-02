"""Layar Laporan (PRD §14.2, §9.3): rekap/detail absensi siswa, ekspor
Excel/PDF berkop, arsip PDF otomatis, dan Koreksi Setelah Laporan Terbit.
Akses kepala sekolah/admin/wali kelas - operator ditolak server (§10),
layar menampilkan pesan yang jelas begitu itu terjadi.

Tombol Harian/Mingguan/Bulanan hanya mengisi ulang tanggal mulai/selesai
di form (PRD §14.2 menyebut lima jenis laporan; harian/mingguan/bulanan/
per kelas/per siswa di sini SATU bentuk keluaran yang sama, dibedakan
rentang tanggal + saring kelas/siswa - lihat app.core.laporan).
"""

from __future__ import annotations

from datetime import timedelta
from pathlib import Path

from PySide6.QtCore import QDate
from PySide6.QtWidgets import (
    QComboBox,
    QDateEdit,
    QFileDialog,
    QHBoxLayout,
    QHeaderView,
    QLabel,
    QLineEdit,
    QMessageBox,
    QPushButton,
    QTableWidget,
    QTableWidgetItem,
    QTabWidget,
    QVBoxLayout,
    QWidget,
)

from app.ui.laporan.klien_laporan import GagalHubungiServer, KlienLaporan, ServerMenolak

_LABEL_STATUS = {
    "HADIR": "Hadir", "TERLAMBAT": "Terlambat", "ALPA": "Alpa", "IZIN": "Izin", "SAKIT": "Sakit",
    "DISPENSASI": "Dispensasi",
}
_URUTAN_STATUS = list(_LABEL_STATUS)
KOLOM_REKAP = ["Nama Siswa", "Kelas"] + [_LABEL_STATUS[s] for s in _URUTAN_STATUS] + ["Jumlah Hari"]
KOLOM_DETAIL = ["Tanggal", "Status Masuk", "Status Final", "Keterangan"]
KOLOM_ARSIP = ["ID", "Label", "Tanggal Mulai", "Tanggal Selesai", "Dicetak"]
KOLOM_KOREKSI = ["Waktu Koreksi", "Sumber", "Siswa", "Tanggal", "Status Sebelum", "Status Sesudah", "Alasan"]


def _pesan_akses(exc: Exception) -> str:
    if isinstance(exc, ServerMenolak) and exc.status_code == 403:
        return "Akun ini tidak berwenang mengakses Laporan - kepala sekolah, admin, atau wali kelas untuk kelasnya sendiri."
    return str(exc)


class _TabLaporan(QWidget):
    def __init__(self, *, klien: KlienLaporan) -> None:
        super().__init__()
        self._klien = klien
        self._hasil_terakhir: dict | None = None

        hari_ini = QDate.currentDate()
        self._tanggal_mulai = QDateEdit(hari_ini)
        self._tanggal_mulai.setCalendarPopup(True)
        self._tanggal_selesai = QDateEdit(hari_ini)
        self._tanggal_selesai.setCalendarPopup(True)

        tombol_harian = QPushButton("Harian")
        tombol_harian.clicked.connect(self._atur_harian)
        tombol_mingguan = QPushButton("Mingguan")
        tombol_mingguan.clicked.connect(self._atur_mingguan)
        tombol_bulanan = QPushButton("Bulanan")
        tombol_bulanan.clicked.connect(self._atur_bulanan)

        self._lingkup = QComboBox()
        self._lingkup.addItem("Seluruh Sekolah", "SEKOLAH")
        self._lingkup.addItem("Kelas Tertentu", "KELAS")
        self._lingkup.addItem("Siswa Tertentu", "SISWA")
        self._lingkup.currentIndexChanged.connect(lambda _i: self._perbarui_tampilan_lingkup())

        self._id_kelas = QLineEdit()
        self._id_kelas.setPlaceholderText("ID Kelas")
        self._id_siswa = QLineEdit()
        self._id_siswa.setPlaceholderText("ID Siswa")

        tombol_tampilkan = QPushButton("Tampilkan")
        tombol_tampilkan.clicked.connect(self.muat_ulang)
        tombol_excel = QPushButton("Ekspor Excel")
        tombol_excel.clicked.connect(self._ekspor_excel)
        tombol_pdf = QPushButton("Ekspor PDF")
        tombol_pdf.clicked.connect(self._ekspor_pdf)

        baris_periode = QHBoxLayout()
        baris_periode.addWidget(QLabel("Dari:"))
        baris_periode.addWidget(self._tanggal_mulai)
        baris_periode.addWidget(QLabel("Sampai:"))
        baris_periode.addWidget(self._tanggal_selesai)
        baris_periode.addWidget(tombol_harian)
        baris_periode.addWidget(tombol_mingguan)
        baris_periode.addWidget(tombol_bulanan)
        baris_periode.addStretch(1)

        baris_lingkup = QHBoxLayout()
        baris_lingkup.addWidget(QLabel("Lingkup:"))
        baris_lingkup.addWidget(self._lingkup)
        baris_lingkup.addWidget(self._id_kelas)
        baris_lingkup.addWidget(self._id_siswa)
        baris_lingkup.addWidget(tombol_tampilkan)
        baris_lingkup.addStretch(1)
        baris_lingkup.addWidget(tombol_excel)
        baris_lingkup.addWidget(tombol_pdf)

        self._label_judul = QLabel("")
        self._label_judul.setWordWrap(True)

        self._tabel = QTableWidget(0, len(KOLOM_REKAP))
        self._tabel.setHorizontalHeaderLabels(KOLOM_REKAP)
        self._tabel.setEditTriggers(QTableWidget.EditTrigger.NoEditTriggers)
        self._tabel.horizontalHeader().setSectionResizeMode(0, QHeaderView.ResizeMode.Stretch)
        self._tabel.verticalHeader().setDefaultSectionSize(24)

        tata_letak = QVBoxLayout(self)
        tata_letak.addLayout(baris_periode)
        tata_letak.addLayout(baris_lingkup)
        tata_letak.addWidget(self._label_judul)
        tata_letak.addWidget(self._tabel)

        self._perbarui_tampilan_lingkup()

    def _perbarui_tampilan_lingkup(self) -> None:
        lingkup = self._lingkup.currentData()
        self._id_kelas.setVisible(lingkup == "KELAS")
        self._id_siswa.setVisible(lingkup == "SISWA")

    def _atur_harian(self) -> None:
        self._tanggal_selesai.setDate(self._tanggal_mulai.date())

    def _atur_mingguan(self) -> None:
        mulai = self._tanggal_mulai.date().toPython()
        senin = mulai - timedelta(days=mulai.weekday())
        sabtu = senin + timedelta(days=5)
        self._tanggal_mulai.setDate(QDate(senin.year, senin.month, senin.day))
        self._tanggal_selesai.setDate(QDate(sabtu.year, sabtu.month, sabtu.day))

    def _atur_bulanan(self) -> None:
        mulai = self._tanggal_mulai.date()
        awal_bulan = QDate(mulai.year(), mulai.month(), 1)
        akhir_bulan = QDate(mulai.year(), mulai.month(), awal_bulan.daysInMonth())
        self._tanggal_mulai.setDate(awal_bulan)
        self._tanggal_selesai.setDate(akhir_bulan)

    def _int_atau_none(self, teks: str) -> int | None:
        teks = teks.strip()
        if not teks:
            return None
        try:
            return int(teks)
        except ValueError:
            return None

    def _params(self) -> dict:
        lingkup = self._lingkup.currentData()
        return {
            "tanggal_mulai": self._tanggal_mulai.date().toPython().isoformat(),
            "tanggal_selesai": self._tanggal_selesai.date().toPython().isoformat(),
            "id_kelas": self._int_atau_none(self._id_kelas.text()) if lingkup == "KELAS" else None,
            "id_siswa": self._int_atau_none(self._id_siswa.text()) if lingkup == "SISWA" else None,
        }

    def muat_ulang(self) -> None:
        try:
            hasil = self._klien.lihat_laporan(self._params())
        except (ServerMenolak, GagalHubungiServer) as exc:
            QMessageBox.warning(self, "Gagal memuat laporan", _pesan_akses(exc))
            return

        self._hasil_terakhir = hasil
        self._label_judul.setText(hasil["label"])
        if hasil["detail"] is not None:
            self._isi_tabel_detail(hasil["detail"])
        else:
            self._isi_tabel_rekap(hasil["rekap"] or [])

    def _isi_tabel_rekap(self, baris: list[dict]) -> None:
        self._tabel.setColumnCount(len(KOLOM_REKAP))
        self._tabel.setHorizontalHeaderLabels(KOLOM_REKAP)
        self._tabel.setRowCount(len(baris))
        for i, b in enumerate(baris):
            nilai = (
                [b["nama_lengkap"], b["nama_kelas"] or "-"]
                + [str(b["jumlah"].get(s, 0)) for s in _URUTAN_STATUS]
                + [str(b["jumlah_hari_tercatat"])]
            )
            for kolom, teks in enumerate(nilai):
                self._tabel.setItem(i, kolom, QTableWidgetItem(str(teks)))

    def _isi_tabel_detail(self, detail: dict) -> None:
        self._tabel.setColumnCount(len(KOLOM_DETAIL))
        self._tabel.setHorizontalHeaderLabels(KOLOM_DETAIL)
        baris = detail["baris"]
        self._tabel.setRowCount(len(baris))
        for i, b in enumerate(baris):
            nilai = [
                b["tanggal"],
                _LABEL_STATUS.get(b["status_masuk"] or "", b["status_masuk"] or "-"),
                _LABEL_STATUS.get(b["status_final"] or "", b["status_final"] or "-"),
                b["alasan_final"] or "-",
            ]
            for kolom, teks in enumerate(nilai):
                self._tabel.setItem(i, kolom, QTableWidgetItem(str(teks)))

    def _simpan_berkas(self, *, isi: bytes, filter_nama: str, ekstensi: str) -> None:
        path_str, _filter = QFileDialog.getSaveFileName(self, "Simpan Laporan", f"laporan.{ekstensi}", filter_nama)
        if not path_str:
            return
        Path(path_str).write_bytes(isi)
        QMessageBox.information(self, "Tersimpan", f"Laporan disimpan ke {path_str}")

    def _ekspor_excel(self) -> None:
        try:
            isi = self._klien.ekspor_excel(self._params())
        except (ServerMenolak, GagalHubungiServer) as exc:
            QMessageBox.warning(self, "Gagal mengekspor Excel", _pesan_akses(exc))
            return
        self._simpan_berkas(isi=isi, filter_nama="Excel (*.xlsx)", ekstensi="xlsx")

    def _ekspor_pdf(self) -> None:
        try:
            isi = self._klien.ekspor_pdf(self._params())
        except (ServerMenolak, GagalHubungiServer) as exc:
            QMessageBox.warning(self, "Gagal mengekspor PDF", _pesan_akses(exc))
            return
        self._simpan_berkas(isi=isi, filter_nama="PDF (*.pdf)", ekstensi="pdf")
        QMessageBox.information(
            self, "Diarsipkan", "Laporan ini juga tersimpan otomatis di Arsip Laporan (PRD §9.3)."
        )


class _TabArsip(QWidget):
    def __init__(self, *, klien: KlienLaporan) -> None:
        super().__init__()
        self._klien = klien
        self._arsip: list[dict] = []

        tombol_muat_ulang = QPushButton("Muat Ulang")
        tombol_muat_ulang.clicked.connect(self.muat_ulang)
        tombol_lihat_koreksi = QPushButton("Lihat Koreksi Setelah Terbit")
        tombol_lihat_koreksi.clicked.connect(self._lihat_koreksi_setelah_terbit)
        tombol_unduh = QPushButton("Unduh PDF")
        tombol_unduh.clicked.connect(self._unduh)

        bar = QHBoxLayout()
        bar.addWidget(tombol_muat_ulang)
        bar.addWidget(tombol_lihat_koreksi)
        bar.addWidget(tombol_unduh)
        bar.addStretch(1)

        self._tabel_arsip = QTableWidget(0, len(KOLOM_ARSIP))
        self._tabel_arsip.setHorizontalHeaderLabels(KOLOM_ARSIP)
        self._tabel_arsip.setEditTriggers(QTableWidget.EditTrigger.NoEditTriggers)
        self._tabel_arsip.setSelectionBehavior(QTableWidget.SelectionBehavior.SelectRows)
        self._tabel_arsip.verticalHeader().setDefaultSectionSize(24)

        self._label_koreksi = QLabel(
            "Pilih satu laporan lalu klik \"Lihat Koreksi Setelah Terbit\" untuk memeriksa "
            "apakah ada koreksi absensi setelah laporan itu dicetak (PRD §9.3)."
        )
        self._label_koreksi.setWordWrap(True)

        self._tabel_koreksi = QTableWidget(0, len(KOLOM_KOREKSI))
        self._tabel_koreksi.setHorizontalHeaderLabels(KOLOM_KOREKSI)
        self._tabel_koreksi.setEditTriggers(QTableWidget.EditTrigger.NoEditTriggers)
        self._tabel_koreksi.verticalHeader().setDefaultSectionSize(24)

        tata_letak = QVBoxLayout(self)
        tata_letak.addLayout(bar)
        tata_letak.addWidget(self._tabel_arsip)
        tata_letak.addWidget(self._label_koreksi)
        tata_letak.addWidget(self._tabel_koreksi)

    def muat_ulang(self) -> None:
        try:
            self._arsip = self._klien.daftar_arsip()
        except (ServerMenolak, GagalHubungiServer) as exc:
            QMessageBox.warning(self, "Gagal memuat arsip", _pesan_akses(exc))
            return

        self._tabel_arsip.setRowCount(len(self._arsip))
        for i, a in enumerate(self._arsip):
            nilai = [str(a["id"]), a["label"], a["tanggal_mulai"], a["tanggal_selesai"], a["waktu_cetak"]]
            for kolom, teks in enumerate(nilai):
                self._tabel_arsip.setItem(i, kolom, QTableWidgetItem(str(teks)))

    def _arsip_terpilih(self) -> dict | None:
        baris = self._tabel_arsip.currentRow()
        if baris < 0 or baris >= len(self._arsip):
            return None
        return self._arsip[baris]

    def _lihat_koreksi_setelah_terbit(self) -> None:
        arsip = self._arsip_terpilih()
        if arsip is None:
            QMessageBox.information(self, "Pilih laporan", "Pilih satu baris arsip laporan terlebih dahulu.")
            return

        try:
            daftar = self._klien.koreksi_setelah_terbit(arsip["id"])
        except (ServerMenolak, GagalHubungiServer) as exc:
            QMessageBox.warning(self, "Gagal memuat koreksi", _pesan_akses(exc))
            return

        self._tabel_koreksi.setRowCount(len(daftar))
        for i, k in enumerate(daftar):
            nilai = [
                k["waktu_koreksi"], k["sumber"], k["nama_siswa"], k["tanggal"],
                k["nilai_sebelum"].get("status_final", "-"), k["nilai_sesudah"].get("status_final", "-"),
                k["alasan"],
            ]
            for kolom, teks in enumerate(nilai):
                self._tabel_koreksi.setItem(i, kolom, QTableWidgetItem(str(teks)))

        if daftar:
            self._label_koreksi.setText(
                f"PERHATIAN: {len(daftar)} koreksi terjadi SETELAH laporan \"{arsip['label']}\" dicetak "
                f"({arsip['waktu_cetak']}) - laporan yang sudah dicetak ini kini basi."
            )
        else:
            self._label_koreksi.setText(
                f"Tidak ada koreksi setelah laporan \"{arsip['label']}\" dicetak ({arsip['waktu_cetak']})."
            )

    def _unduh(self) -> None:
        arsip = self._arsip_terpilih()
        if arsip is None:
            QMessageBox.information(self, "Pilih laporan", "Pilih satu baris arsip laporan terlebih dahulu.")
            return

        path_str, _filter = QFileDialog.getSaveFileName(self, "Unduh Arsip Laporan", "laporan.pdf", "PDF (*.pdf)")
        if not path_str:
            return
        try:
            isi = self._klien.unduh_arsip(arsip["id"])
        except (ServerMenolak, GagalHubungiServer) as exc:
            QMessageBox.warning(self, "Gagal mengunduh", _pesan_akses(exc))
            return
        Path(path_str).write_bytes(isi)
        QMessageBox.information(self, "Tersimpan", f"Arsip disimpan ke {path_str}")


class LayarLaporan(QTabWidget):
    def __init__(self, *, klien: KlienLaporan) -> None:
        super().__init__()
        self.setWindowTitle("Laporan")

        self._tab_laporan = _TabLaporan(klien=klien)
        self._tab_arsip = _TabArsip(klien=klien)

        self.addTab(self._tab_laporan, "Laporan")
        self.addTab(self._tab_arsip, "Arsip Laporan")

    def muat_awal(self) -> None:
        self._tab_arsip.muat_ulang()
