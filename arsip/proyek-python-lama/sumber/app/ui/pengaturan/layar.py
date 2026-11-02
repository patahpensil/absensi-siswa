"""Layar Pengaturan (PRD §4, §14.2): jadwal per hari, ambang, saklar
modul, identitas sekolah. Akses kepala sekolah/admin SAJA - kalau akun
yang login bukan keduanya, setiap panggilan API gagal 403 dan layar
menampilkan pesan yang jelas alih-alih diam-diam kosong.
"""

from __future__ import annotations

import base64
from pathlib import Path

from PySide6.QtCore import QDate, QTime
from PySide6.QtGui import QPixmap
from PySide6.QtWidgets import (
    QCheckBox,
    QComboBox,
    QDoubleSpinBox,
    QFileDialog,
    QFormLayout,
    QHBoxLayout,
    QLabel,
    QLineEdit,
    QMessageBox,
    QPushButton,
    QTabWidget,
    QTableWidget,
    QTableWidgetItem,
    QTextEdit,
    QTimeEdit,
    QVBoxLayout,
    QWidget,
)

from app.ui.pengaturan.dialog_jadwal import HARI_PILIHAN, DialogTambahJadwal
from app.ui.pengaturan.klien_pengaturan import GagalHubungiServer, KlienPengaturan, ServerMenolak

_NAMA_HARI = dict(HARI_PILIHAN)
KOLOM_JADWAL = ["Hari", "Jam Masuk", "Batas Terlambat", "Cutoff ALPA", "Pulang Mulai", "Jam Pulang", "Berlaku Mulai", "Berlaku Sampai"]


def _pesan_akses(exc: Exception) -> str:
    if isinstance(exc, ServerMenolak) and exc.status_code == 403:
        return "Akun ini tidak berwenang mengakses Pengaturan - hanya kepala sekolah dan admin."
    return str(exc)


class _TabJadwal(QWidget):
    def __init__(self, *, klien: KlienPengaturan) -> None:
        super().__init__()
        self._klien = klien

        self._jenis = QComboBox()
        self._jenis.addItem("Siswa", "SISWA")
        self._jenis.addItem("Guru", "GURU")
        self._jenis.currentIndexChanged.connect(lambda _i: self.muat_ulang())

        tombol_tambah = QPushButton("Tambah Jadwal")
        tombol_tambah.clicked.connect(self._tambah_jadwal)
        tombol_muat_ulang = QPushButton("Muat Ulang")
        tombol_muat_ulang.clicked.connect(self.muat_ulang)

        bar = QHBoxLayout()
        bar.addWidget(QLabel("Jenis:"))
        bar.addWidget(self._jenis)
        bar.addStretch(1)
        bar.addWidget(tombol_tambah)
        bar.addWidget(tombol_muat_ulang)

        self._tabel = QTableWidget(0, len(KOLOM_JADWAL))
        self._tabel.setHorizontalHeaderLabels(KOLOM_JADWAL)
        self._tabel.setEditTriggers(QTableWidget.EditTrigger.NoEditTriggers)
        self._tabel.verticalHeader().setDefaultSectionSize(24)

        tata_letak = QVBoxLayout(self)
        tata_letak.addLayout(bar)
        tata_letak.addWidget(self._tabel)

    def muat_ulang(self) -> None:
        try:
            daftar = self._klien.daftar_jadwal(jenis_pemilik=self._jenis.currentData())
        except (ServerMenolak, GagalHubungiServer) as exc:
            QMessageBox.warning(self, "Gagal memuat jadwal", _pesan_akses(exc))
            return

        daftar = sorted(daftar, key=lambda j: (j["hari"], j["berlaku_mulai"]))
        self._tabel.setRowCount(len(daftar))
        for baris, j in enumerate(daftar):
            nilai = [
                _NAMA_HARI.get(j["hari"], str(j["hari"])),
                j["jam_masuk"],
                j["batas_terlambat"],
                j.get("cutoff_alpa") or "-",
                j.get("pulang_mulai") or "-",
                j["jam_pulang"],
                j["berlaku_mulai"],
                j.get("berlaku_sampai") or "Masih berlaku",
            ]
            for kolom, teks in enumerate(nilai):
                self._tabel.setItem(baris, kolom, QTableWidgetItem(str(teks)))

    def _tambah_jadwal(self) -> None:
        dialog = DialogTambahJadwal(klien=self._klien, jenis_pemilik=self._jenis.currentData(), parent=self)
        if dialog.exec():
            self.muat_ulang()


class _TabAmbangSaklar(QWidget):
    def __init__(self, *, klien: KlienPengaturan) -> None:
        super().__init__()
        self._klien = klien

        self._jendela_scan = QDoubleSpinBox()
        self._jendela_scan.setSuffix(" detik")
        self._jendela_scan.setRange(0, 300)

        self._toleransi_guru = QTimeEdit()
        self._ambang_pulang = QDoubleSpinBox()
        self._ambang_pulang.setSuffix(" %")
        self._ambang_pulang.setRange(0, 100)

        self._cutoff_alpa = QTimeEdit()
        self._waktu_notif_alpa = QTimeEdit()

        self._modul_whatsapp = QCheckBox("Modul WhatsApp aktif")
        self._modul_absen_pulang = QCheckBox("Modul absen pulang aktif")

        self._alasan = QLineEdit()
        self._alasan.setPlaceholderText("Wajib diisi sebelum menyimpan")

        form = QFormLayout()
        form.addRow("Jendela anti-duplikat scan", self._jendela_scan)
        form.addRow("Toleransi terlambat guru", self._toleransi_guru)
        form.addRow("Ambang pengaman gagal alat pulang", self._ambang_pulang)
        form.addRow("Waktu penetapan ALPA (pemicu scheduler)", self._cutoff_alpa)
        form.addRow("Waktu kirim notifikasi ALPA pagi", self._waktu_notif_alpa)
        form.addRow(self._modul_whatsapp)
        form.addRow(self._modul_absen_pulang)
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
        tata_letak.addLayout(form)
        tata_letak.addLayout(bar)
        tata_letak.addStretch(1)

    def muat_ulang(self) -> None:
        try:
            ambang = self._klien.ambil_ambang()
            saklar = self._klien.ambil_saklar()
        except (ServerMenolak, GagalHubungiServer) as exc:
            QMessageBox.warning(self, "Gagal memuat pengaturan", _pesan_akses(exc))
            return

        self._jendela_scan.setValue(ambang["jendela_scan_berulang_detik"])
        self._toleransi_guru.setTime(QTime.fromString(ambang["batas_terlambat_guru"], "HH:mm:ss"))
        self._ambang_pulang.setValue(ambang["ambang_tahan_konversi_pulang"] * 100)
        if ambang.get("cutoff_alpa"):
            self._cutoff_alpa.setTime(QTime.fromString(ambang["cutoff_alpa"], "HH:mm:ss"))
        if ambang.get("waktu_kirim_notif_alpa"):
            self._waktu_notif_alpa.setTime(QTime.fromString(ambang["waktu_kirim_notif_alpa"], "HH:mm:ss"))

        self._modul_whatsapp.setChecked(saklar["modul_whatsapp_aktif"])
        self._modul_absen_pulang.setChecked(saklar["modul_absen_pulang"])

    def _simpan(self) -> None:
        alasan = self._alasan.text().strip()
        if not alasan:
            QMessageBox.warning(self, "Alasan wajib diisi", "Tuliskan alasan perubahan pengaturan ini.")
            return

        payload_ambang = {
            "jendela_scan_berulang_detik": self._jendela_scan.value(),
            "batas_terlambat_guru": self._toleransi_guru.time().toPython().isoformat(),
            "ambang_tahan_konversi_pulang": self._ambang_pulang.value() / 100,
            "cutoff_alpa": self._cutoff_alpa.time().toPython().isoformat(),
            "waktu_kirim_notif_alpa": self._waktu_notif_alpa.time().toPython().isoformat(),
            "alasan": alasan,
        }
        payload_saklar = {
            "modul_whatsapp_aktif": self._modul_whatsapp.isChecked(),
            "modul_absen_pulang": self._modul_absen_pulang.isChecked(),
            "alasan": alasan,
        }
        try:
            self._klien.ubah_ambang(payload_ambang)
            self._klien.ubah_saklar(payload_saklar)
        except (ServerMenolak, GagalHubungiServer) as exc:
            QMessageBox.warning(self, "Gagal menyimpan", _pesan_akses(exc))
            return

        self._alasan.clear()
        QMessageBox.information(self, "Tersimpan", "Pengaturan ambang dan saklar modul disimpan.")
        self.muat_ulang()


class _TabIdentitasSekolah(QWidget):
    def __init__(self, *, klien: KlienPengaturan) -> None:
        super().__init__()
        self._klien = klien
        self._logo_data_uri: str | None = None

        self._nama = QLineEdit()
        self._npsn = QLineEdit()
        self._alamat = QTextEdit()
        self._alamat.setFixedHeight(60)
        self._nama_kepsek = QLineEdit()
        self._nip_kepsek = QLineEdit()

        self._label_logo = QLabel("Belum ada logo")
        self._label_logo.setFixedSize(96, 96)
        self._label_logo.setStyleSheet("border: 1px solid palette(mid);")
        self._label_logo.setScaledContents(True)
        tombol_pilih_logo = QPushButton("Pilih Berkas Logo...")
        tombol_pilih_logo.clicked.connect(self._pilih_logo)
        tombol_hapus_logo = QPushButton("Hapus Logo")
        tombol_hapus_logo.clicked.connect(self._hapus_logo)
        logo_bar = QHBoxLayout()
        logo_bar.addWidget(self._label_logo)
        logo_bar.addWidget(tombol_pilih_logo)
        logo_bar.addWidget(tombol_hapus_logo)
        logo_bar.addStretch(1)

        self._alasan = QLineEdit()
        self._alasan.setPlaceholderText("Wajib diisi sebelum menyimpan")

        form = QFormLayout()
        form.addRow("Nama Sekolah", self._nama)
        form.addRow("NPSN", self._npsn)
        form.addRow("Alamat", self._alamat)
        form.addRow("Logo", logo_bar)
        form.addRow("Nama Kepala Sekolah", self._nama_kepsek)
        form.addRow("NIP Kepala Sekolah", self._nip_kepsek)
        form.addRow("Alasan Perubahan", self._alasan)

        tombol_simpan = QPushButton("Simpan")
        tombol_simpan.clicked.connect(self._simpan)
        tombol_muat_ulang = QPushButton("Muat Ulang")
        tombol_muat_ulang.clicked.connect(self.muat_ulang)
        bar = QHBoxLayout()
        bar.addWidget(tombol_simpan)
        bar.addWidget(tombol_muat_ulang)
        bar.addStretch(1)

        info = QLabel(
            "Dipakai untuk kop laporan (PRD §14.2). Layar cetak laporan belum "
            "dibangun - data ini disimpan untuk dipakai begitu tersedia."
        )
        info.setWordWrap(True)

        tata_letak = QVBoxLayout(self)
        tata_letak.addWidget(info)
        tata_letak.addLayout(form)
        tata_letak.addLayout(bar)
        tata_letak.addStretch(1)

    def muat_ulang(self) -> None:
        try:
            identitas = self._klien.ambil_identitas_sekolah()
        except (ServerMenolak, GagalHubungiServer) as exc:
            QMessageBox.warning(self, "Gagal memuat identitas sekolah", _pesan_akses(exc))
            return

        self._nama.setText(identitas.get("nama") or "")
        self._npsn.setText(identitas.get("npsn") or "")
        self._alamat.setPlainText(identitas.get("alamat") or "")
        self._nama_kepsek.setText(identitas.get("nama_kepala_sekolah") or "")
        self._nip_kepsek.setText(identitas.get("nip_kepala_sekolah") or "")
        self._logo_data_uri = identitas.get("logo")
        self._perbarui_pratinjau_logo()

    def _perbarui_pratinjau_logo(self) -> None:
        if not self._logo_data_uri or "," not in self._logo_data_uri:
            self._label_logo.setText("Belum ada logo")
            self._label_logo.setPixmap(QPixmap())
            return
        try:
            data = base64.b64decode(self._logo_data_uri.split(",", 1)[1])
            pixmap = QPixmap()
            pixmap.loadFromData(data)
            self._label_logo.setPixmap(pixmap)
            self._label_logo.setText("")
        except Exception:
            self._label_logo.setText("Logo tidak terbaca")

    def _pilih_logo(self) -> None:
        path_str, _filter = QFileDialog.getOpenFileName(self, "Pilih Logo Sekolah", "", "Gambar (*.png *.jpg *.jpeg)")
        if not path_str:
            return
        path = Path(path_str)
        data = path.read_bytes()
        mime = "image/png" if path.suffix.lower() == ".png" else "image/jpeg"
        self._logo_data_uri = f"data:{mime};base64,{base64.b64encode(data).decode('ascii')}"
        self._perbarui_pratinjau_logo()

    def _hapus_logo(self) -> None:
        self._logo_data_uri = ""
        self._perbarui_pratinjau_logo()

    def _simpan(self) -> None:
        alasan = self._alasan.text().strip()
        if not alasan:
            QMessageBox.warning(self, "Alasan wajib diisi", "Tuliskan alasan perubahan identitas sekolah.")
            return

        payload = {
            "nama": self._nama.text().strip(),
            "npsn": self._npsn.text().strip(),
            "alamat": self._alamat.toPlainText().strip(),
            "nama_kepala_sekolah": self._nama_kepsek.text().strip(),
            "nip_kepala_sekolah": self._nip_kepsek.text().strip(),
            "alasan": alasan,
        }
        if self._logo_data_uri is not None:
            payload["logo"] = self._logo_data_uri

        try:
            self._klien.ubah_identitas_sekolah(payload)
        except (ServerMenolak, GagalHubungiServer) as exc:
            QMessageBox.warning(self, "Gagal menyimpan", _pesan_akses(exc))
            return

        self._alasan.clear()
        QMessageBox.information(self, "Tersimpan", "Identitas sekolah disimpan.")
        self.muat_ulang()


class LayarPengaturan(QTabWidget):
    def __init__(self, *, klien: KlienPengaturan) -> None:
        super().__init__()
        self.setWindowTitle("Pengaturan")

        self._tab_jadwal = _TabJadwal(klien=klien)
        self._tab_ambang_saklar = _TabAmbangSaklar(klien=klien)
        self._tab_identitas = _TabIdentitasSekolah(klien=klien)

        self.addTab(self._tab_jadwal, "Jadwal")
        self.addTab(self._tab_ambang_saklar, "Ambang && Saklar Modul")
        self.addTab(self._tab_identitas, "Identitas Sekolah")

    def muat_awal(self) -> None:
        self._tab_jadwal.muat_ulang()
        self._tab_ambang_saklar.muat_ulang()
        self._tab_identitas.muat_ulang()
