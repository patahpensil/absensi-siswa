"""LayarDashboard (PRD §14.1). Mengikuti ui-guidelines.md bagian
"Dashboard" secara sengaja: BUKAN lima widget grafik terpisah untuk
kelima sub-fitur PRD (ringkasan/grafik harian-mingguan-bulanan/
perbandingan kelas/tren keterlambatan/alpa terbanyak - seluruhnya SUDAH
ada di app/server/routers/dashboard.py dan teruji, tapi menumpuknya
semua jadi widget terpisah di satu layar persis pola "kartu-kartu
seragam" yang ui-guidelines.md minta dihindari). Layar ini menampilkan:
angka hari berjalan secukupnya (baris teks ringkas, bukan kartu KPI),
SATU grafik tren keterlambatan per kelas, dan SATU daftar siswa alpa
terbanyak - persis kalimat pedoman "Daftar yang bisa ditindaklanjuti
jauh lebih berguna daripada tiga grafik lingkaran."

Status operasional (§14.1 baris kedua) ditampilkan sebagai panel
ringkas terpisah - ini bukan "pola/tren" seperti isi guideline
"Dashboard" di atas, melainkan kesehatan alat, jadi wajar dipisah.
"""

from __future__ import annotations

from datetime import date, timedelta

import pyqtgraph as pg
from PySide6.QtCore import QDate
from PySide6.QtWidgets import (
    QDateEdit,
    QHBoxLayout,
    QLabel,
    QMessageBox,
    QPushButton,
    QTableWidget,
    QTableWidgetItem,
    QVBoxLayout,
    QWidget,
)

from app.ui.dashboard.klien_dashboard import GagalHubungiServer, KlienDashboard, ServerMenolak

# ui-guidelines.md "Latar terang sebagai bawaan": pyqtgraph memakai latar
# gelap secara bawaan, menyimpang dari palet Qt terang aplikasi ini.
# Disamakan DI SINI, SEBELUM PlotWidget mana pun dibuat - pyqtgraph
# membaca opsi ini saat konstruksi widget, bukan saat digambar ulang.
pg.setConfigOption("background", "w")
pg.setConfigOption("foreground", "k")

#: ui-guidelines.md "Tipografi dan warna": warna status konsisten di
#: seluruh aplikasi - dipakai jugalah warisan yang sama dengan
#: app.ui.lobi.layar._WARNA_LATAR untuk hijau/kuning/merah. Dipilih agar
#: tetap kontras terbaca di atas LATAR TERANG (lihat setConfigOption di
#: atas) - kuning murni akan tenggelam di latar putih, jadi baris kuning
#: aslinya (#F2A600, amber) diganti kuning-kecoklatan yang lebih gelap
#: supaya kontrasnya setara garis lain, bukan sekadar dipertahankan.
_WARNA_GARIS = ["#1A73E8", "#C5221F", "#B8860B", "#1E8E3E", "#8430CE", "#00897B", "#E67C00"]

_KOLOM_ALPA = ["Nama", "Kelas", "Jumlah Alpa"]


class LayarDashboard(QWidget):
    def __init__(self, *, klien: KlienDashboard, parent: QWidget | None = None) -> None:
        super().__init__(parent)
        self.setWindowTitle("Dashboard - UPT SMP Negeri 1 Panca Rijang")
        self._klien = klien

        hari_ini = QDate.currentDate()
        self._tanggal_mulai = QDateEdit(hari_ini.addDays(-13))
        self._tanggal_mulai.setCalendarPopup(True)
        self._tanggal_selesai = QDateEdit(hari_ini)
        self._tanggal_selesai.setCalendarPopup(True)
        tombol_muat = QPushButton("Muat Ulang")
        tombol_muat.clicked.connect(self.muat_ulang)

        baris_rentang = QHBoxLayout()
        baris_rentang.addWidget(QLabel("Dari"))
        baris_rentang.addWidget(self._tanggal_mulai)
        baris_rentang.addWidget(QLabel("Sampai"))
        baris_rentang.addWidget(self._tanggal_selesai)
        baris_rentang.addWidget(tombol_muat)
        baris_rentang.addStretch(1)

        self._label_ringkasan = QLabel("Memuat ringkasan hari ini...")
        self._label_ringkasan.setWordWrap(True)

        self._grafik = pg.PlotWidget(title="Tren Keterlambatan per Kelas")
        self._grafik.setLabel("left", "Jumlah siswa terlambat")
        self._grafik.setLabel("bottom", "Tanggal")
        self._grafik.addLegend()
        self._grafik.showGrid(x=True, y=True, alpha=0.2)

        self._tabel_alpa = QTableWidget(0, len(_KOLOM_ALPA))
        self._tabel_alpa.setHorizontalHeaderLabels(_KOLOM_ALPA)
        self._tabel_alpa.setEditTriggers(QTableWidget.EditTrigger.NoEditTriggers)

        self._label_status_operasional = QLabel("Memuat status operasional...")
        self._label_status_operasional.setWordWrap(True)

        tata = QVBoxLayout(self)
        tata.addLayout(baris_rentang)
        tata.addWidget(self._label_ringkasan)
        tata.addWidget(self._grafik, stretch=2)
        tata.addWidget(QLabel("Siswa dengan Alpa Terbanyak"))
        tata.addWidget(self._tabel_alpa, stretch=1)
        tata.addWidget(QLabel("Status Operasional"))
        tata.addWidget(self._label_status_operasional)

    def muat_awal(self) -> None:
        self.muat_ulang()

    def muat_ulang(self) -> None:
        self._muat_ringkasan()
        self._muat_tren_per_kelas()
        self._muat_alpa_terbanyak()
        self._muat_status_operasional()

    # ------------------------------------------------------------------

    def _rentang(self) -> tuple[str, str]:
        return self._tanggal_mulai.date().toString("yyyy-MM-dd"), self._tanggal_selesai.date().toString("yyyy-MM-dd")

    def _muat_ringkasan(self) -> None:
        try:
            hasil = self._klien.ringkasan()
        except (GagalHubungiServer, ServerMenolak) as exc:
            self._label_ringkasan.setText(f"Gagal memuat ringkasan: {exc}")
            return
        if not hasil["per_status"]:
            self._label_ringkasan.setText(f"{hasil['tanggal']} - belum ada absensi tercatat.")
            return
        bagian = " · ".join(f"{status}: {jumlah}" for status, jumlah in hasil["per_status"].items())
        self._label_ringkasan.setText(f"{hasil['tanggal']} - {bagian} (total {hasil['total_tercatat']} siswa)")

    def _muat_tren_per_kelas(self) -> None:
        self._grafik.clear()
        tanggal_mulai, tanggal_selesai = self._rentang()
        try:
            daftar_kelas = self._daftar_kelas_untuk_tren()
        except (GagalHubungiServer, ServerMenolak) as exc:
            QMessageBox.warning(self, "Gagal memuat daftar kelas", str(exc))
            return

        for i, (id_kelas, nama_kelas) in enumerate(daftar_kelas):
            try:
                tren = self._klien.tren_keterlambatan(
                    tanggal_mulai=tanggal_mulai, tanggal_selesai=tanggal_selesai, id_kelas=id_kelas
                )
            except (GagalHubungiServer, ServerMenolak) as exc:
                QMessageBox.warning(self, "Gagal memuat tren keterlambatan", str(exc))
                return
            x = list(range(len(tren)))
            y = [titik["jumlah"] for titik in tren]
            warna = _WARNA_GARIS[i % len(_WARNA_GARIS)]
            self._grafik.plot(x, y, pen=pg.mkPen(warna, width=2), name=nama_kelas)

        if daftar_kelas:
            label_tanggal = [titik["tanggal"] for titik in tren]  # dari kelas terakhir - rentang sama untuk semua
            sumbu = self._grafik.getAxis("bottom")
            sumbu.setTicks([[(i, t) for i, t in enumerate(label_tanggal)]])

    def _daftar_kelas_untuk_tren(self) -> list[tuple[int | None, str]]:
        """Kepsek/admin: satu garis per kelas (perlu GET /kelas, kepsek/
        admin saja - lihat app.server.routers.kelas). Wali kelas: SATU
        garis untuk kelasnya sendiri, tanpa perlu daftar kelas sama
        sekali (server sudah auto-scope lewat DashboardService.
        _id_kelas_efektif - id_kelas=None di sini tetap benar karena
        server yang membatasi)."""
        if self._klien.peran == "WALI_KELAS":
            return [(None, "Kelas Anda")]
        try:
            kelas = self._klien.daftar_kelas()
        except ServerMenolak:
            return [(None, "Seluruh Sekolah")]
        return [(k["id"], k["nama_kelas"]) for k in kelas]

    def _muat_alpa_terbanyak(self) -> None:
        tanggal_mulai, tanggal_selesai = self._rentang()
        try:
            hasil = self._klien.alpa_terbanyak(tanggal_mulai=tanggal_mulai, tanggal_selesai=tanggal_selesai, limit=10)
        except (GagalHubungiServer, ServerMenolak) as exc:
            QMessageBox.warning(self, "Gagal memuat alpa terbanyak", str(exc))
            return

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

        baris = []
        for j in status["jalur"]:
            tanda = "DIAM" if j["diam"] else "aktif"
            baris.append(f"Jalur {j['jalur']}: {tanda} (terakhir {j['waktu_terakhir_baca']})")
        for k in status["klien"]:
            tanda = "diam" if k["diam"] else "aktif"
            baris.append(f"Klien {k['username']} ({k['peran']}): {tanda}")
        if status["antrean_lobi"]:
            for a in status["antrean_lobi"]:
                tanda = " (laporan kedaluwarsa)" if a["kedaluwarsa"] else ""
                baris.append(f"Antrean {a['station_id']}: {a['jumlah_pending']} tertunda{tanda}")
        else:
            baris.append("Antrean PC lobi: belum ada laporan")
        pencadangan = status["pencadangan_terakhir"] or "belum tersedia"
        baris.append(f"Pencadangan terakhir: {pencadangan}")

        self._label_status_operasional.setText("\n".join(baris))

    def closeEvent(self, event) -> None:  # noqa: N802 - override Qt
        self._klien.tutup()
        super().closeEvent(event)
