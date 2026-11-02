"""
LayarAbsensiLobi - layar fullscreen absensi RFID (PRD §12.5).

Sunyi = semua lancar. Sukses tidak berbunyi, tampil inisial/foto+nama+status
berwarna ~1,5 detik lalu kembali sunyi. Gagal (kartu tidak dikenal atau
gagal kirim ke server) berbunyi dari satu speaker bersama dan menandai
jalur yang bersangkutan - jalur lain tetap berjalan (§7.3: non-blok, tanpa
modal yang harus ditutup manual).

Callback pembaca kartu (app.rfid) berjalan di THREAD PEMBACA, bukan thread
GUI (lihat app.rfid.base.PembacaKartuBase._jalankan) - _JembatanScan
memancarkan sinyal Qt supaya update widget selalu terjadi di thread GUI,
tidak pernah dipanggil langsung dari callback pembaca.

Layar ini TIDAK PERNAH memanggil endpoint HTTP secara langsung - seluruh
scan lewat KlienScan.submit_scan() (app.ui.lobi.klien_scan), SATU seam yang
nanti diisi antrean lokal tanpa mengubah berkas ini.
"""

from __future__ import annotations

import logging

from PySide6.QtCore import QObject, Qt, QTimer, Signal
from PySide6.QtGui import QCloseEvent, QFont
from PySide6.QtWidgets import QApplication, QFrame, QGridLayout, QLabel, QVBoxLayout, QWidget

from app.core.timeutil import now_wita
from app.rfid.base import PembacaKartu, ScanEvent
from app.ui.lobi.forwarder import ForwarderAntrean
from app.ui.lobi.klien_scan import KlienScan
from app.ui.lobi.status_jalur import DURASI_TAMPIL_DETIK, StatusGridLobi
from app.ui.lobi.tampilan import KartuTampilan, WarnaStatus, petakan

_log = logging.getLogger(__name__)

_WARNA_LATAR = {
    WarnaStatus.HIJAU: "#1E8E3E",
    WarnaStatus.KUNING: "#F2A600",
    WarnaStatus.MERAH: "#C5221F",
    WarnaStatus.NETRAL: "#3C4043",
}
_WARNA_IDLE = "#202124"
_INTERVAL_TIMER_IDLE_MS = 200
#: Cukup sering supaya indikator antrean terasa langsung ("operator harus
#: sadar sedang buffering"), tapi murah - hitung_pending() hanya query
#: SQLite lokal, bukan panggilan jaringan (lihat app.ui.lobi.klien_scan).
_INTERVAL_TIMER_ANTREAN_MS = 1000
#: Jauh lebih jarang dari indikator antrean - ini panggilan JARINGAN
#: (KlienScan.cek_mode_darurat), dan status izin darurat tidak pernah
#: berubah dalam hitungan detik (perubahannya lewat admin, PRD §12.4).
_INTERVAL_TIMER_MODE_DARURAT_MS = 15_000
#: Sama alasan dengan mode darurat - ini juga panggilan jaringan
#: (KlienScan.laporkan_telemetri, PRD §14.1 status operasional Dashboard).
#: app.core.dashboard.AMBANG_KEDALUWARSA_TELEMETRI_MENIT (5 menit) jauh
#: lebih longgar dari interval ini, supaya satu-dua putaran gagal (mis.
#: jaringan sempat putus) tidak langsung membuat Dashboard menganggap
#: PC lobi ini berhenti.
_INTERVAL_TIMER_TELEMETRI_MS = 15_000


class _JembatanScan(QObject):
    """Memindahkan ScanEvent dari thread pembaca ke thread GUI lewat sinyal
    Qt (queued connection otomatis karena objek ini hidup di thread GUI)."""

    scan_diterima = Signal(object)  # ScanEvent

    def terima(self, event: ScanEvent) -> None:
        self.scan_diterima.emit(event)


class _SlotJalur(QFrame):
    """Satu kotak di grid - satu jalur/lajur pembaca kartu."""

    def __init__(self, jalur_id: str) -> None:
        super().__init__()
        self.jalur_id = jalur_id
        self.setAttribute(Qt.WidgetAttribute.WA_StyledBackground, True)
        self.setMinimumSize(360, 280)

        self._label_judul = QLabel(jalur_id)
        self._label_judul.setStyleSheet("color: #9AA0A6; font-size: 14px;")

        self._label_nama = QLabel("")
        self._label_nama.setWordWrap(True)
        self._label_nama.setAlignment(Qt.AlignmentFlag.AlignCenter)
        font_nama = QFont()
        font_nama.setPointSize(28)
        font_nama.setBold(True)
        self._label_nama.setFont(font_nama)
        self._label_nama.setStyleSheet("color: white;")

        self._label_keterangan = QLabel("")
        self._label_keterangan.setAlignment(Qt.AlignmentFlag.AlignCenter)
        self._label_keterangan.setStyleSheet("color: white; font-size: 18px;")

        self._label_penanda_gagal = QLabel("")
        self._label_penanda_gagal.setAlignment(Qt.AlignmentFlag.AlignCenter)
        self._label_penanda_gagal.setStyleSheet("color: #FFD2CC; font-size: 13px; font-weight: bold;")

        tata = QVBoxLayout(self)
        tata.addWidget(self._label_judul)
        tata.addStretch(1)
        tata.addWidget(self._label_nama)
        tata.addWidget(self._label_keterangan)
        tata.addStretch(1)
        tata.addWidget(self._label_penanda_gagal)

        self.tampilkan_idle()

    def tampilkan_idle(self) -> None:
        self._label_nama.setText("")
        self._label_keterangan.setText("")
        self.setStyleSheet(f"background-color: {_WARNA_IDLE}; border-radius: 12px;")

    def tampilkan_hasil(self, tampilan: KartuTampilan) -> None:
        # Placeholder inisial - belum ada sumber foto siswa (SKILL.md "Hal
        # yang belum diputuskan"); §12.5 tetap terpenuhi lewat inisial+nama.
        judul = tampilan.inisial or ""
        self._label_nama.setText(f"{judul}\n{tampilan.nama_tampil}".strip())
        self._label_keterangan.setText(tampilan.keterangan)
        self.setStyleSheet(f"background-color: {_WARNA_LATAR[tampilan.warna]}; border-radius: 12px;")

    def tandai_gagal(self, gagal: bool) -> None:
        self._label_penanda_gagal.setText("JALUR TIDAK MERESPON" if gagal else "")


class LayarAbsensiLobi(QWidget):
    """Layar absensi RFID, satu instance per lobi. Fullscreen, tanpa tombol."""

    def __init__(
        self,
        *,
        klien: KlienScan,
        pembaca: dict[str, PembacaKartu],
        forwarder: ForwarderAntrean | None = None,
        parent: QWidget | None = None,
    ) -> None:
        super().__init__(parent)
        self._klien = klien
        self._pembaca = pembaca
        self._forwarder = forwarder
        self._grid_status = StatusGridLobi()
        self._slot: dict[str, _SlotJalur] = {}

        self._jembatan = _JembatanScan()
        self._jembatan.scan_diterima.connect(self._pada_scan)

        self.setWindowTitle("Absensi - UPT SMP Negeri 1 Panca Rijang")
        self.setStyleSheet("background-color: #17181A;")

        tata_utama = QVBoxLayout(self)

        # Banner MODE DARURAT (§12.4) - disambungkan lewat _perbarui_mode_darurat()
        # (timer di bawah, poll KlienScan.cek_mode_darurat per station).
        # set_mode_darurat() sendiri tetap method publik terpisah supaya
        # tetap bisa dipanggil/diuji tanpa bergantung timer.
        self._banner_darurat = QLabel("MODE DARURAT — alat jalur tidak sesuai standar")
        self._banner_darurat.setAlignment(Qt.AlignmentFlag.AlignCenter)
        self._banner_darurat.setStyleSheet(
            "background-color: #B45309; color: white; font-weight: bold; padding: 6px;"
        )
        self._banner_darurat.hide()
        tata_utama.addWidget(self._banner_darurat)

        # Indikator antrean (SKILL.md §21 / ARAH DESAIN antrean lokal) -
        # BEDA dari prinsip senyap-per-scan: ini bukan kartu hasil satu
        # tap, melainkan penanda berdiri yang HANYA muncul saat ada
        # backlog, supaya operator sadar sedang buffering, bukan diam-diam
        # menganggap semua terkirim langsung.
        self._indikator_antrean = QLabel("")
        self._indikator_antrean.setAlignment(Qt.AlignmentFlag.AlignCenter)
        self._indikator_antrean.setStyleSheet(
            "background-color: #3C4043; color: #FDD663; font-weight: bold; padding: 4px;"
        )
        self._indikator_antrean.hide()
        tata_utama.addWidget(self._indikator_antrean)

        grid_widget = QWidget()
        tata_grid = QGridLayout(grid_widget)
        kolom = max(1, min(4, len(pembaca)))
        for i, jalur_id in enumerate(pembaca):
            self._grid_status.daftarkan_jalur(jalur_id)
            slot = _SlotJalur(jalur_id)
            self._slot[jalur_id] = slot
            tata_grid.addWidget(slot, i // kolom, i % kolom)
        tata_utama.addWidget(grid_widget)

        self._timer_idle = QTimer(self)
        self._timer_idle.setInterval(_INTERVAL_TIMER_IDLE_MS)
        self._timer_idle.timeout.connect(self._bersihkan_kedaluwarsa)
        self._timer_idle.start()

        self._timer_antrean = QTimer(self)
        self._timer_antrean.setInterval(_INTERVAL_TIMER_ANTREAN_MS)
        self._timer_antrean.timeout.connect(self._perbarui_indikator_antrean)
        self._timer_antrean.start()

        self._timer_mode_darurat = QTimer(self)
        self._timer_mode_darurat.setInterval(_INTERVAL_TIMER_MODE_DARURAT_MS)
        self._timer_mode_darurat.timeout.connect(self._perbarui_mode_darurat)
        self._timer_mode_darurat.start()
        # Sengaja TIDAK dipanggil segera di sini (beda dari indikator
        # antrean) - ini panggilan jaringan sinkron di thread GUI, memaksa
        # satu putaran saat konstruksi bisa menunda cat pertama layar
        # kiosk kalau server lambat merespons. Banner cukup muncul dalam
        # _INTERVAL_TIMER_MODE_DARURAT_MS pertama, tidak seketika.

        self._timer_telemetri = QTimer(self)
        self._timer_telemetri.setInterval(_INTERVAL_TIMER_TELEMETRI_MS)
        self._timer_telemetri.timeout.connect(self._laporkan_telemetri)
        self._timer_telemetri.start()  # sama alasan mode darurat - tidak dipanggil segera saat konstruksi

        for alat in self._pembaca.values():
            alat.saat_scan(self._jembatan.terima)
            alat.mulai()

        if self._forwarder is not None:
            self._forwarder.mulai()

    def set_mode_darurat(self, aktif: bool, *, pesan: str | None = None) -> None:
        """Titik sambung untuk flag izin darurat alat backend (§12.4) -
        belum dipanggil siapa pun di slice ini."""
        if pesan:
            self._banner_darurat.setText(pesan)
        self._banner_darurat.setVisible(aktif)

    def closeEvent(self, event: QCloseEvent) -> None:
        for alat in self._pembaca.values():
            alat.berhenti()
        if self._forwarder is not None:
            self._forwarder.berhenti()
        self._klien.tutup()
        super().closeEvent(event)

    # ------------------------------------------------------------------

    def _pada_scan(self, event: ScanEvent) -> None:
        # event.uid (TERNORMALISASI) dipakai server sebagai KUNCI PENCARIAN
        # kartu (RfidCardRepository.cari_by_uid mencocokkan persis terhadap
        # UID ternormalisasi yang tersimpan saat kartu didaftarkan). event.
        # uid_mentah (apa adanya dari alat) diteruskan JUGA - PRD §17
        # mewajibkan nilai mentah tersimpan utuh di attendance_events,
        # terlepas dari yang dipakai untuk pencarian.
        hasil = self._klien.submit_scan(
            uid_kartu=event.uid, uid_mentah=event.uid_mentah, jalur=event.jalur,
            waktu=event.waktu, station_id=event.station_id,
        )
        tampilan = petakan(hasil)
        self._grid_status.perbarui(event.station_id, tampilan, waktu=now_wita())

        slot = self._slot.get(event.station_id)
        if slot is None:
            _log.warning("Scan dari jalur yang tak terdaftar di grid: %s", event.station_id)
            return
        slot.tampilkan_hasil(tampilan)
        slot.tandai_gagal(tampilan.tandai_jalur_gagal)
        if tampilan.berbunyi:
            QApplication.beep()

    def _bersihkan_kedaluwarsa(self) -> None:
        berubah = self._grid_status.bersihkan_kedaluwarsa(sekarang=now_wita(), durasi_detik=DURASI_TAMPIL_DETIK)
        for jalur_id in berubah:
            self._slot[jalur_id].tampilkan_idle()

    def _perbarui_mode_darurat(self) -> None:
        """Sambungan nyata flag izin darurat alat backend (§12.4) ke
        set_mode_darurat() - polling per station yang berjalan di layar
        ini, banner tampil kalau SALAH SATU jalur sedang memakai izin
        darurat (pesan mengambil dari jalur aktif pertama yang ditemukan)."""
        for station_id in self._pembaca:
            aktif, pesan = self._klien.cek_mode_darurat(station_id)
            if aktif:
                self.set_mode_darurat(True, pesan=pesan)
                return
        self.set_mode_darurat(False)

    def _perbarui_indikator_antrean(self) -> None:
        """Indikator BERDIRI (bukan per-scan) - hanya muncul saat ada
        backlog PENDING, supaya operator sadar sedang buffering. Beda
        dari prinsip senyap-per-scan (§12.5) karena ini bukan hasil satu
        tap, melainkan status keseluruhan antrean."""
        jumlah = self._klien.hitung_pending()
        if jumlah > 0:
            self._indikator_antrean.setText(f"Menunggu koneksi — {jumlah} absensi tertunda terkirim")
            self._indikator_antrean.show()
        else:
            self._indikator_antrean.hide()

    def _laporkan_telemetri(self) -> None:
        """Sambungan nyata status operasional Dashboard (PRD §14.1:
        "jumlah scan yang masih mengantre di PC lobi") - angka yang SAMA
        (hitung_pending() total proses ini) dilaporkan untuk setiap
        station yang dilayani proses ini. Bootstrap saat ini
        (app.ui.lobi.main) hanya pernah mengonfigurasi SATU station per
        proses, jadi penyederhanaan ini belum pernah salah pada
        praktiknya - dicatat di sini kalau nanti satu proses melayani
        banyak reader sekaligus."""
        jumlah = self._klien.hitung_pending()
        for station_id in self._pembaca:
            self._klien.laporkan_telemetri(station_id=station_id, jumlah_pending=jumlah)
