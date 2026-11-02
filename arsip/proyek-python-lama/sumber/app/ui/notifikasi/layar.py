"""Layar Notifikasi (PRD §13, menu §18 NOTIFIKASI): empat template pesan
terpisah, antrean pengiriman, dan mode uji. Akses kepala sekolah/admin
SAJA - kalau akun yang login bukan keduanya, setiap panggilan API gagal
403 dan layar menampilkan pesan yang jelas alih-alih diam-diam kosong.

KONEKSI WHATSAPP NYATA TETAP MATI (Fase 2) - layar ini hanya mengelola
struktur (template/antrean/mode uji). Antrean bisa tampak kosong terus
sampai pemicu nyata (Terlambat/ALPA pagi/ALPA pulang/Rekap) disambungkan
di slice lain - lihat README, "Utang aktif".
"""

from __future__ import annotations

from PySide6.QtWidgets import (
    QCheckBox,
    QComboBox,
    QFormLayout,
    QHBoxLayout,
    QLabel,
    QLineEdit,
    QMessageBox,
    QPushButton,
    QTableWidget,
    QTableWidgetItem,
    QTabWidget,
    QTextEdit,
    QVBoxLayout,
    QWidget,
)

from app.ui.notifikasi.format_payload import payload_mode_uji
from app.ui.notifikasi.klien_notifikasi import GagalHubungiServer, KlienNotifikasi, ServerMenolak

_LABEL_JENIS = {
    "TERLAMBAT": "Terlambat (segera setelah absensi tercatat)",
    "ALPA_PAGI": "Alpa pagi (09:15)",
    "ALPA_PULANG": "Alpa tidak scan pulang (60 menit setelah cutoff)",
    "REKAP": "Rekap (sesuai pengaturan)",
}
_VARIABEL_TERSEDIA = "{nama_siswa}, {kelas}, {tanggal}, {jam}, {status}"

KOLOM_ANTREAN = ["Waktu Kejadian", "Jenis", "Status", "Nomor Tujuan", "Percobaan", "Waktu Kirim", "Keterangan Gagal"]
_STATUS_FILTER = [
    ("Semua", None),
    ("Antrean (belum terkirim)", "PENDING"),
    ("Sedang dikirim", "SENDING"),
    ("Terkirim", "SENT"),
    ("Gagal", "FAILED"),
    ("Dibatalkan", "CANCELLED"),
]


def _pesan_akses(exc: Exception) -> str:
    if isinstance(exc, ServerMenolak) and exc.status_code == 403:
        return "Akun ini tidak berwenang mengakses Notifikasi - hanya kepala sekolah dan admin."
    return str(exc)


class _TabTemplate(QWidget):
    def __init__(self, *, klien: KlienNotifikasi) -> None:
        super().__init__()
        self._klien = klien
        self._muat_terakhir: dict[str, dict] = {}

        self._jenis = QComboBox()
        for jenis, label in _LABEL_JENIS.items():
            self._jenis.addItem(label, jenis)
        self._jenis.currentIndexChanged.connect(lambda _i: self._tampilkan_terpilih())

        self._status_versi = QLabel("")

        self._konten = QTextEdit()
        self._konten.setPlaceholderText("Isi pesan, mis. Ananda {nama_siswa} tercatat terlambat pukul {jam}.")

        info_variabel = QLabel(f"Variabel yang tersedia: {_VARIABEL_TERSEDIA}")
        info_variabel.setWordWrap(True)

        self._alasan = QLineEdit()
        self._alasan.setPlaceholderText("Wajib diisi sebelum menyimpan")

        form = QFormLayout()
        form.addRow("Template", self._jenis)
        form.addRow("Versi aktif", self._status_versi)
        form.addRow("Isi pesan", self._konten)
        form.addRow(info_variabel)
        form.addRow("Alasan perubahan", self._alasan)

        tombol_simpan = QPushButton("Simpan Versi Baru")
        tombol_simpan.clicked.connect(self._simpan)
        tombol_muat_ulang = QPushButton("Muat Ulang")
        tombol_muat_ulang.clicked.connect(self.muat_ulang)
        bar = QHBoxLayout()
        bar.addWidget(tombol_simpan)
        bar.addWidget(tombol_muat_ulang)
        bar.addStretch(1)

        tata_letak = QVBoxLayout(self)
        tata_letak.addLayout(form)
        tata_letak.addLayout(bar)

    def muat_ulang(self) -> None:
        try:
            daftar = self._klien.daftar_template()
        except (ServerMenolak, GagalHubungiServer) as exc:
            QMessageBox.warning(self, "Gagal memuat template", _pesan_akses(exc))
            return

        self._muat_terakhir = {t["jenis"]: t for t in daftar}
        self._tampilkan_terpilih()

    def _tampilkan_terpilih(self) -> None:
        jenis = self._jenis.currentData()
        template = self._muat_terakhir.get(jenis)
        if template is None or template.get("konten") is None:
            self._status_versi.setText("Belum pernah diisi")
            self._konten.setPlainText("")
            return
        self._status_versi.setText(f"Versi {template['versi']}")
        self._konten.setPlainText(template["konten"])

    def _simpan(self) -> None:
        alasan = self._alasan.text().strip()
        konten = self._konten.toPlainText().strip()
        if not alasan:
            QMessageBox.warning(self, "Alasan wajib diisi", "Tuliskan alasan perubahan template ini.")
            return
        if not konten:
            QMessageBox.warning(self, "Isi pesan wajib diisi", "Template tidak boleh kosong.")
            return

        jenis = self._jenis.currentData()
        try:
            self._klien.ubah_template(jenis, {"konten": konten, "alasan": alasan})
        except (ServerMenolak, GagalHubungiServer) as exc:
            QMessageBox.warning(self, "Gagal menyimpan template", _pesan_akses(exc))
            return

        self._alasan.clear()
        QMessageBox.information(self, "Tersimpan", "Versi baru template disimpan.")
        self.muat_ulang()


class _TabAntrean(QWidget):
    def __init__(self, *, klien: KlienNotifikasi) -> None:
        super().__init__()
        self._klien = klien

        self._status_filter = QComboBox()
        for label, nilai in _STATUS_FILTER:
            self._status_filter.addItem(label, nilai)

        tombol_cari = QPushButton("Muat Ulang")
        tombol_cari.clicked.connect(self.muat_ulang)

        bar = QHBoxLayout()
        bar.addWidget(QLabel("Status:"))
        bar.addWidget(self._status_filter)
        bar.addWidget(tombol_cari)
        bar.addStretch(1)

        self._label_jumlah = QLabel("")

        self._tabel = QTableWidget(0, len(KOLOM_ANTREAN))
        self._tabel.setHorizontalHeaderLabels(KOLOM_ANTREAN)
        self._tabel.setEditTriggers(QTableWidget.EditTrigger.NoEditTriggers)
        self._tabel.setSelectionBehavior(QTableWidget.SelectionBehavior.SelectRows)
        self._tabel.verticalHeader().setDefaultSectionSize(24)

        tata_letak = QVBoxLayout(self)
        tata_letak.addLayout(bar)
        tata_letak.addWidget(self._label_jumlah)
        tata_letak.addWidget(self._tabel)

    def muat_ulang(self) -> None:
        try:
            baris = self._klien.daftar_antrean(status_filter=self._status_filter.currentData())
        except (ServerMenolak, GagalHubungiServer) as exc:
            QMessageBox.warning(self, "Gagal memuat antrean", _pesan_akses(exc))
            return

        self._isi_tabel(baris)
        self._label_jumlah.setText(f"{len(baris)} notifikasi")

    def _isi_tabel(self, baris: list[dict]) -> None:
        self._tabel.setRowCount(len(baris))
        for i, b in enumerate(baris):
            nilai = [
                b["waktu_kejadian"],
                _LABEL_JENIS.get(b["jenis"], b["jenis"]),
                b["status"],
                b["nomor_tujuan"],
                str(b["jumlah_percobaan"]),
                b["waktu_kirim"] or "-",
                b["pesan_galat"] or "-",
            ]
            for kolom, teks in enumerate(nilai):
                self._tabel.setItem(i, kolom, QTableWidgetItem(str(teks)))


class _TabModeUji(QWidget):
    def __init__(self, *, klien: KlienNotifikasi) -> None:
        super().__init__()
        self._klien = klien

        info = QLabel(
            "Mode uji mengalihkan SELURUH pesan ke satu nomor admin (PRD §13.3). "
            "Wajib dipakai untuk menguji alur notifikasi sebelum fitur ini diaktifkan "
            "kepada orang tua siswa."
        )
        info.setWordWrap(True)

        self._aktif = QCheckBox("Mode uji aktif")
        self._nomor_admin = QLineEdit()
        self._nomor_admin.setPlaceholderText("mis. 081234567890")
        #: Nomor persis seperti terakhir dimuat dari server - dipakai
        #: _simpan() untuk tahu apakah admin benar-benar mengubahnya,
        #: supaya toggle aktif/nonaktif saja tidak ikut menulis ulang
        #: nomor (dan membuat entri audit log kosong tanpa perubahan
        #: nyata).
        self._nomor_admin_tersimpan: str = ""

        self._alasan = QLineEdit()
        self._alasan.setPlaceholderText("Wajib diisi sebelum menyimpan")

        form = QFormLayout()
        form.addRow(self._aktif)
        form.addRow("Nomor admin", self._nomor_admin)
        form.addRow("Alasan perubahan", self._alasan)

        tombol_simpan = QPushButton("Simpan")
        tombol_simpan.clicked.connect(self._simpan)
        tombol_muat_ulang = QPushButton("Muat Ulang")
        tombol_muat_ulang.clicked.connect(self.muat_ulang)
        bar = QHBoxLayout()
        bar.addWidget(tombol_simpan)
        bar.addWidget(tombol_muat_ulang)
        bar.addStretch(1)

        tata_letak = QVBoxLayout(self)
        tata_letak.addWidget(info)
        tata_letak.addLayout(form)
        tata_letak.addLayout(bar)
        tata_letak.addStretch(1)

    def muat_ulang(self) -> None:
        try:
            info = self._klien.ambil_mode_uji()
        except (ServerMenolak, GagalHubungiServer) as exc:
            QMessageBox.warning(self, "Gagal memuat mode uji", _pesan_akses(exc))
            return

        self._aktif.setChecked(info["aktif"])
        self._nomor_admin_tersimpan = info.get("nomor_admin") or ""
        self._nomor_admin.setText(self._nomor_admin_tersimpan)

    def _simpan(self) -> None:
        alasan = self._alasan.text().strip()
        if not alasan:
            QMessageBox.warning(self, "Alasan wajib diisi", "Tuliskan alasan perubahan mode uji.")
            return

        payload = payload_mode_uji(
            aktif=self._aktif.isChecked(),
            nomor_input=self._nomor_admin.text(),
            nomor_tersimpan=self._nomor_admin_tersimpan,
            alasan=alasan,
        )

        try:
            self._klien.ubah_mode_uji(payload)
        except (ServerMenolak, GagalHubungiServer) as exc:
            QMessageBox.warning(self, "Gagal menyimpan", _pesan_akses(exc))
            return

        self._alasan.clear()
        QMessageBox.information(self, "Tersimpan", "Pengaturan mode uji disimpan.")
        self.muat_ulang()


class LayarNotifikasi(QTabWidget):
    def __init__(self, *, klien: KlienNotifikasi) -> None:
        super().__init__()
        self.setWindowTitle("Notifikasi")

        self._tab_template = _TabTemplate(klien=klien)
        self._tab_antrean = _TabAntrean(klien=klien)
        self._tab_mode_uji = _TabModeUji(klien=klien)

        self.addTab(self._tab_template, "Template")
        self.addTab(self._tab_antrean, "Antrean")
        self.addTab(self._tab_mode_uji, "Mode Uji")

    def muat_awal(self) -> None:
        self._tab_template.muat_ulang()
        self._tab_antrean.muat_ulang()
        self._tab_mode_uji.muat_ulang()
