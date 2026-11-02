"""LayarRingkasKepsek (PRD §14.1: "Bagi kepala sekolah disediakan satu
layar ringkas"). Trimmed dari LayarDashboard - ringkasan hari ini, lima
siswa alpa terbanyak (bukan sepuluh), dan status operasional. TANPA
grafik tren - kepala sekolah yang butuh pola per-kelas membuka
LayarDashboard penuh, layar ini murni untuk cek cepat sebelum rapat pagi.
"""

from __future__ import annotations

from datetime import date, timedelta

from PySide6.QtWidgets import QLabel, QPushButton, QTableWidget, QTableWidgetItem, QVBoxLayout, QWidget

from app.ui.dashboard.klien_dashboard import GagalHubungiServer, KlienDashboard, ServerMenolak

_KOLOM_ALPA = ["Nama", "Kelas", "Jumlah Alpa"]
_LIMIT_ALPA_RINGKAS = 5


class LayarRingkasKepsek(QWidget):
    def __init__(self, *, klien: KlienDashboard, parent: QWidget | None = None) -> None:
        super().__init__(parent)
        self.setWindowTitle("Ringkasan Kepala Sekolah - UPT SMP Negeri 1 Panca Rijang")
        self._klien = klien

        self._label_ringkasan = QLabel("Memuat...")
        self._label_ringkasan.setWordWrap(True)
        font = self._label_ringkasan.font()
        font.setPointSize(font.pointSize() + 2)
        self._label_ringkasan.setFont(font)

        tombol_muat = QPushButton("Muat Ulang")
        tombol_muat.clicked.connect(self.muat_ulang)
        self.tombol_dashboard_lengkap = QPushButton("Buka Dashboard Lengkap...")

        self._tabel_alpa = QTableWidget(0, len(_KOLOM_ALPA))
        self._tabel_alpa.setHorizontalHeaderLabels(_KOLOM_ALPA)
        self._tabel_alpa.setEditTriggers(QTableWidget.EditTrigger.NoEditTriggers)

        self._label_status_operasional = QLabel("Memuat status operasional...")
        self._label_status_operasional.setWordWrap(True)

        tata = QVBoxLayout(self)
        tata.addWidget(tombol_muat)
        tata.addWidget(self.tombol_dashboard_lengkap)
        tata.addWidget(QLabel("Kehadiran Hari Ini"))
        tata.addWidget(self._label_ringkasan)
        tata.addWidget(QLabel(f"{_LIMIT_ALPA_RINGKAS} Siswa dengan Alpa Terbanyak (30 hari terakhir)"))
        tata.addWidget(self._tabel_alpa)
        tata.addWidget(QLabel("Status Operasional"))
        tata.addWidget(self._label_status_operasional)

    def muat_awal(self) -> None:
        self.muat_ulang()

    def muat_ulang(self) -> None:
        self._muat_ringkasan()
        self._muat_alpa_terbanyak()
        self._muat_status_operasional()

    def _muat_ringkasan(self) -> None:
        try:
            hasil = self._klien.ringkasan()
        except (GagalHubungiServer, ServerMenolak) as exc:
            self._label_ringkasan.setText(f"Gagal memuat: {exc}")
            return
        if not hasil["per_status"]:
            self._label_ringkasan.setText(f"{hasil['tanggal']} - belum ada absensi tercatat.")
            return
        bagian = " · ".join(f"{status}: {jumlah}" for status, jumlah in hasil["per_status"].items())
        self._label_ringkasan.setText(f"{hasil['tanggal']} - {bagian} (total {hasil['total_tercatat']} siswa)")

    def _muat_alpa_terbanyak(self) -> None:
        hari_ini = date.today()
        tanggal_mulai = (hari_ini - timedelta(days=30)).isoformat()
        tanggal_selesai = hari_ini.isoformat()
        try:
            hasil = self._klien.alpa_terbanyak(
                tanggal_mulai=tanggal_mulai, tanggal_selesai=tanggal_selesai, limit=_LIMIT_ALPA_RINGKAS
            )
        except (GagalHubungiServer, ServerMenolak):
            hasil = []

        self._tabel_alpa.setRowCount(0)
        for siswa in hasil:
            baris = self._tabel_alpa.rowCount()
            self._tabel_alpa.insertRow(baris)
            self._tabel_alpa.setItem(baris, 0, QTableWidgetItem(siswa["nama_lengkap"]))
            self._tabel_alpa.setItem(baris, 1, QTableWidgetItem(siswa["nama_kelas"]))
            self._tabel_alpa.setItem(baris, 2, QTableWidgetItem(str(siswa["jumlah_alpa"])))

    def _muat_status_operasional(self) -> None:
        try:
            status = self._klien.status_operasional()
        except (GagalHubungiServer, ServerMenolak) as exc:
            self._label_status_operasional.setText(f"Gagal memuat status operasional: {exc}")
            return

        jalur_diam = sum(1 for j in status["jalur"] if j["diam"])
        klien_diam = sum(1 for k in status["klien"] if k["diam"])
        pencadangan = status["pencadangan_terakhir"] or "belum tersedia"
        self._label_status_operasional.setText(
            f"Jalur alat: {len(status['jalur'])} terpantau, {jalur_diam} diam\n"
            f"Klien: {len(status['klien'])} aktif, {klien_diam} diam\n"
            f"Pencadangan terakhir: {pencadangan}"
        )

    def closeEvent(self, event) -> None:  # noqa: N802 - override Qt
        self._klien.tutup()
        super().closeEvent(event)
