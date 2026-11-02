"""Dialog "Jalankan Tindakan Massal" (PRD §9.2, SKILL.md "Koreksi
massal"). Pratinjau WAJIB sebelum alasan bisa diisi, sama seperti
DialogTambahEntri di app.ui.kalender: mengubah penyaring/tindakan setelah
pratinjau membatalkan pratinjau lama supaya angka yang ditampilkan tidak
pernah basi.

Penanganan SnapshotDiperlukan (backend: app.core.koreksi_massal_service.
SnapshotDiperlukan, HTTP 412) dilakukan SEBELUM pengguna sempat mengisi
alasan - pratinjau sudah membawa flag ``perlu_snapshot``, jadi dialog ini
bisa menampilkan pesan anggun ("modul pencadangan belum tersedia") tepat
saat pratinjau dihitung, tanpa perlu menunggu tombol Jalankan ditekan dan
menangkap galat mentah. Galat 412 tetap ditangani sebagai jaring pengaman
kalau data berubah di antara pratinjau dan Jalankan (mis. admin lain
menjalankan tindakan lain di saat bersamaan) - lihat
_tangani_penolakan_jalankan().

Modul pencadangan §15 sendiri TIDAK dibangun di sini (Slice Sistem
tersendiri) - dialog ini hanya menangani penolakannya dengan sopan.
"""

from __future__ import annotations

from datetime import date

from PySide6.QtCore import QDate
from PySide6.QtWidgets import (
    QCheckBox,
    QComboBox,
    QDateEdit,
    QDialog,
    QDialogButtonBox,
    QFormLayout,
    QGroupBox,
    QHBoxLayout,
    QInputDialog,
    QLabel,
    QLineEdit,
    QMessageBox,
    QPushButton,
    QVBoxLayout,
)

from app.ui.koreksi_massal.klien_koreksi_massal import GagalHubungiServer, KlienKoreksiMassal, ServerMenolak
from app.ui.koreksi_massal.validasi_form import bangun_penyaring, validasi_penyaring

STATUS_PILIHAN = [
    ("HADIR", "Hadir"),
    ("TERLAMBAT", "Terlambat"),
    ("ALPA", "Alpa"),
    ("IZIN", "Izin"),
    ("SAKIT", "Sakit"),
    ("DISPENSASI", "Dispensasi"),
]
#: Default dikecualikan (SKILL.md "Koreksi massal") - siswa yang sudah
#: bersurat dokter tidak boleh tertimpa tindakan massal kecuali sengaja
#: dicentang.
STATUS_DIKECUALIKAN_BAWAAN = {"IZIN", "SAKIT", "DISPENSASI"}

LINGKUP_PILIHAN = [
    ("SEKOLAH", "Seluruh Sekolah"),
    ("TINGKAT", "Satu Tingkat"),
    ("KELAS", "Satu Kelas"),
    ("SISWA_TERPILIH", "Siswa Terpilih"),
]

JENIS_TINDAKAN_PILIHAN = [
    ("UBAH_STATUS", "Ubah Status"),
    ("TANDAI_IZIN_PULANG_CEPAT", "Tandai Izin Pulang Cepat"),
    ("BATALKAN_KONVERSI_ALPA_PULANG", "Batalkan Konversi Alpa Pulang"),
]

PESAN_SNAPSHOT_DIPERLUKAN = (
    "Pencadangan otomatis diperlukan untuk tindakan sebesar ini, dan modul "
    "pencadangan belum tersedia (menyusul di slice Sistem). Tindakan tidak "
    "dapat dijalankan sampai modul pencadangan siap."
)


class DialogJalankanTindakan(QDialog):
    def __init__(self, *, klien: KlienKoreksiMassal, parent=None) -> None:
        super().__init__(parent)
        self.setWindowTitle("Jalankan Tindakan Massal")
        self._klien = klien
        self.tindakan_tersimpan: dict | None = None
        #: Penyaring dan hasil dari pratinjau TERAKHIR - None berarti
        #: pratinjau belum ada/sudah basi karena field berubah.
        self._pratinjau_untuk: tuple | None = None
        self._penyaring_pratinjau: dict | None = None
        self._hasil_pratinjau: dict | None = None

        self._bangun_widget_filter()
        self._bangun_widget_tindakan()
        self._bangun_widget_pratinjau_dan_alasan()
        self._susun_tata_letak()
        self._perbarui_visibilitas_lingkup()
        self._perbarui_visibilitas_status_baru()

    # ------------------------------------------------------------------
    # Susunan widget
    # ------------------------------------------------------------------

    def _bangun_widget_filter(self) -> None:
        self._tanggal_mulai = QDateEdit(QDate.currentDate())
        self._tanggal_mulai.setCalendarPopup(True)
        self._tanggal_selesai = QDateEdit(QDate.currentDate())
        self._tanggal_selesai.setCalendarPopup(True)

        self._lingkup = QComboBox()
        for nilai, label in LINGKUP_PILIHAN:
            self._lingkup.addItem(label, nilai)
        self._lingkup.currentIndexChanged.connect(self._perbarui_visibilitas_lingkup)

        self._tingkat = QLineEdit()
        self._tingkat.setPlaceholderText("mis. 7")
        self._id_kelas = QLineEdit()
        self._id_kelas.setPlaceholderText("ID Kelas")
        self._id_siswa_terpilih = QLineEdit()
        self._id_siswa_terpilih.setPlaceholderText("ID Siswa dipisah koma, mis. 3, 5, 9")

        self._cek_status: dict[str, QCheckBox] = {}
        kotak_status = QHBoxLayout()
        for nilai, label in STATUS_PILIHAN:
            cek = QCheckBox(label)
            cek.setChecked(nilai not in STATUS_DIKECUALIKAN_BAWAAN)
            self._cek_status[nilai] = cek
            kotak_status.addWidget(cek)
        self._kotak_status_widget = kotak_status

        for widget, sinyal in (
            (self._tanggal_mulai, self._tanggal_mulai.dateChanged),
            (self._tanggal_selesai, self._tanggal_selesai.dateChanged),
            (self._lingkup, self._lingkup.currentIndexChanged),
            (self._tingkat, self._tingkat.textChanged),
            (self._id_kelas, self._id_kelas.textChanged),
            (self._id_siswa_terpilih, self._id_siswa_terpilih.textChanged),
        ):
            sinyal.connect(self._batalkan_pratinjau_lama)
        for cek in self._cek_status.values():
            cek.toggled.connect(self._batalkan_pratinjau_lama)

    def _bangun_widget_tindakan(self) -> None:
        self._jenis_tindakan = QComboBox()
        for nilai, label in JENIS_TINDAKAN_PILIHAN:
            self._jenis_tindakan.addItem(label, nilai)
        self._jenis_tindakan.currentIndexChanged.connect(self._perbarui_visibilitas_status_baru)
        self._jenis_tindakan.currentIndexChanged.connect(self._batalkan_pratinjau_lama)

        self._status_baru = QComboBox()
        for nilai, label in STATUS_PILIHAN:
            self._status_baru.addItem(label, nilai)
        self._status_baru.currentIndexChanged.connect(self._batalkan_pratinjau_lama)

    def _bangun_widget_pratinjau_dan_alasan(self) -> None:
        self._tombol_pratinjau = QPushButton("Hitung Pratinjau")
        self._tombol_pratinjau.clicked.connect(self._hitung_pratinjau)

        self._label_pratinjau = QLabel("Atur penyaring dan tindakan, lalu klik “Hitung Pratinjau”.")
        self._label_pratinjau.setWordWrap(True)

        self._alasan = QLineEdit()
        self._alasan.setEnabled(False)
        self._alasan.setPlaceholderText("Wajib diisi setelah pratinjau dihitung")

        self._tombol = QDialogButtonBox(QDialogButtonBox.StandardButton.Cancel)
        self._tombol_jalankan = QPushButton("Jalankan")
        self._tombol_jalankan.setEnabled(False)
        self._tombol.addButton(self._tombol_jalankan, QDialogButtonBox.ButtonRole.AcceptRole)
        self._tombol.rejected.connect(self.reject)
        self._tombol_jalankan.clicked.connect(self._jalankan)

    def _susun_tata_letak(self) -> None:
        form_filter = QFormLayout()
        form_filter.addRow("Tanggal Mulai", self._tanggal_mulai)
        form_filter.addRow("Tanggal Selesai", self._tanggal_selesai)
        form_filter.addRow("Cakupan Orang", self._lingkup)
        form_filter.addRow("Tingkat", self._tingkat)
        form_filter.addRow("ID Kelas", self._id_kelas)
        form_filter.addRow("ID Siswa Terpilih", self._id_siswa_terpilih)
        form_filter.addRow("Status Saat Ini", self._kotak_status_widget)
        grup_filter = QGroupBox("Penyaring")
        grup_filter.setLayout(form_filter)

        form_tindakan = QFormLayout()
        form_tindakan.addRow("Jenis Tindakan", self._jenis_tindakan)
        form_tindakan.addRow("Status Baru", self._status_baru)
        grup_tindakan = QGroupBox("Tindakan")
        grup_tindakan.setLayout(form_tindakan)

        tata_letak = QVBoxLayout(self)
        tata_letak.addWidget(grup_filter)
        tata_letak.addWidget(grup_tindakan)
        tata_letak.addWidget(self._tombol_pratinjau)
        tata_letak.addWidget(self._label_pratinjau)
        tata_letak.addWidget(QLabel("Alasan"))
        tata_letak.addWidget(self._alasan)
        tata_letak.addWidget(self._tombol)

    # ------------------------------------------------------------------
    # Visibilitas field kondisional
    # ------------------------------------------------------------------

    def _perbarui_visibilitas_lingkup(self, *_args) -> None:
        lingkup = self._lingkup.currentData()
        self._tingkat.setEnabled(lingkup == "TINGKAT")
        self._id_kelas.setEnabled(lingkup == "KELAS")
        self._id_siswa_terpilih.setEnabled(lingkup == "SISWA_TERPILIH")

    def _perbarui_visibilitas_status_baru(self, *_args) -> None:
        self._status_baru.setEnabled(self._jenis_tindakan.currentData() == "UBAH_STATUS")

    # ------------------------------------------------------------------
    # Field saat ini - dipakai membandingkan "pratinjau sudah basi?"
    # ------------------------------------------------------------------

    def _status_termasuk_terpilih(self) -> list[str]:
        return [nilai for nilai, cek in self._cek_status.items() if cek.isChecked()]

    def _field_saat_ini(self) -> tuple:
        return (
            self._tanggal_mulai.date().toPython(),
            self._tanggal_selesai.date().toPython(),
            self._lingkup.currentData(),
            self._tingkat.text().strip(),
            self._id_kelas.text().strip(),
            self._id_siswa_terpilih.text().strip(),
            tuple(sorted(self._status_termasuk_terpilih())),
            self._jenis_tindakan.currentData(),
            self._status_baru.currentData(),
        )

    def _batalkan_pratinjau_lama(self, *_args) -> None:
        if self._pratinjau_untuk is not None:
            self._pratinjau_untuk = None
            self._penyaring_pratinjau = None
            self._hasil_pratinjau = None
            self._alasan.setEnabled(False)
            self._tombol_jalankan.setEnabled(False)
            self._label_pratinjau.setText("Penyaring atau tindakan berubah - hitung ulang pratinjau sebelum menjalankan.")

    # ------------------------------------------------------------------
    # Pratinjau
    # ------------------------------------------------------------------

    def _hitung_pratinjau(self) -> None:
        mulai: date = self._tanggal_mulai.date().toPython()
        selesai: date = self._tanggal_selesai.date().toPython()
        lingkup = self._lingkup.currentData()
        status_termasuk = self._status_termasuk_terpilih()

        hasil_validasi = validasi_penyaring(
            tanggal_mulai=mulai,
            tanggal_selesai=selesai,
            lingkup=lingkup,
            tingkat_teks=self._tingkat.text(),
            id_kelas_teks=self._id_kelas.text(),
            id_siswa_terpilih_teks=self._id_siswa_terpilih.text(),
            status_termasuk=status_termasuk,
        )
        if not hasil_validasi.valid:
            QMessageBox.warning(self, "Data belum lengkap", "\n".join(hasil_validasi.galat.values()))
            return

        penyaring = bangun_penyaring(
            tanggal_mulai=mulai,
            tanggal_selesai=selesai,
            lingkup=lingkup,
            tingkat_teks=self._tingkat.text(),
            id_kelas_teks=self._id_kelas.text(),
            id_siswa_terpilih_teks=self._id_siswa_terpilih.text(),
            status_termasuk=status_termasuk,
        )
        try:
            hasil = self._klien.pratinjau(penyaring)
        except (ServerMenolak, GagalHubungiServer) as exc:
            QMessageBox.warning(self, "Gagal menghitung pratinjau", str(exc))
            return

        self._penyaring_pratinjau = penyaring
        self._pratinjau_untuk = self._field_saat_ini()
        self._terapkan_hasil_pratinjau(hasil)

    def _terapkan_hasil_pratinjau(self, hasil: dict) -> None:
        self._hasil_pratinjau = hasil
        if hasil["perlu_snapshot"]:
            self._label_pratinjau.setText(
                f"Tindakan ini akan menyentuh {hasil['jumlah_baris']} baris absensi. {PESAN_SNAPSHOT_DIPERLUKAN}"
            )
            self._alasan.setEnabled(False)
            self._tombol_jalankan.setEnabled(False)
            return

        pesan = f"Tindakan ini akan menyentuh {hasil['jumlah_baris']} baris absensi."
        if hasil["perlu_konfirmasi_ketik"]:
            pesan += " Tindakan berskala besar - Anda akan diminta mengetik ulang jumlah baris saat menjalankan."
        self._label_pratinjau.setText(pesan)
        self._alasan.setEnabled(True)
        self._tombol_jalankan.setEnabled(True)

    # ------------------------------------------------------------------
    # Jalankan
    # ------------------------------------------------------------------

    def _jalankan(self) -> None:
        if self._pratinjau_untuk != self._field_saat_ini() or self._hasil_pratinjau is None:
            QMessageBox.warning(self, "Pratinjau belum dihitung ulang", "Hitung pratinjau lagi untuk nilai saat ini.")
            return
        alasan = self._alasan.text().strip()
        if not alasan:
            QMessageBox.warning(self, "Alasan wajib diisi", "Tuliskan alasan tindakan massal ini.")
            return

        konfirmasi_ketik = None
        if self._hasil_pratinjau["perlu_konfirmasi_ketik"]:
            jumlah = self._hasil_pratinjau["jumlah_baris"]
            teks, ok = QInputDialog.getText(
                self,
                "Konfirmasi Tindakan Besar",
                f"Tindakan ini menyentuh {jumlah} baris. Ketik ulang angka tersebut untuk melanjutkan:",
            )
            if not ok:
                return
            konfirmasi_ketik = teks.strip()

        payload = {
            "penyaring": self._penyaring_pratinjau,
            "jenis_tindakan": self._jenis_tindakan.currentData(),
            "status_baru": (
                self._status_baru.currentData() if self._jenis_tindakan.currentData() == "UBAH_STATUS" else None
            ),
            "alasan": alasan,
            "konfirmasi_ketik": konfirmasi_ketik,
        }
        try:
            tindakan = self._klien.jalankan(payload)
        except ServerMenolak as exc:
            self._tangani_penolakan_jalankan(exc)
            return
        except GagalHubungiServer as exc:
            QMessageBox.warning(self, "Gagal menjalankan", str(exc))
            return

        self.tindakan_tersimpan = tindakan
        self.accept()

    def _tangani_penolakan_jalankan(self, exc: ServerMenolak) -> None:
        if exc.status_code == 400:
            QMessageBox.warning(self, "Konfirmasi tidak cocok", exc.detail)
            return
        if exc.status_code != 412:
            QMessageBox.warning(self, "Gagal menjalankan", str(exc))
            return

        # 412: data sudah berubah sejak pratinjau terakhir (mis. admin lain
        # menjalankan tindakan lain bersamaan). Hitung ulang pratinjau untuk
        # tahu kondisi terbaru, alih-alih menampilkan galat mentah.
        try:
            hasil_baru = self._klien.pratinjau(self._penyaring_pratinjau)
        except (ServerMenolak, GagalHubungiServer):
            QMessageBox.warning(
                self,
                "Data berubah",
                "Server menolak tindakan ini karena data sudah berubah sejak pratinjau terakhir. "
                "Hitung pratinjau lagi sebelum mencoba kembali.",
            )
            self._batalkan_pratinjau_lama()
            return

        self._terapkan_hasil_pratinjau(hasil_baru)
        if hasil_baru["perlu_snapshot"]:
            QMessageBox.warning(self, "Pencadangan diperlukan", PESAN_SNAPSHOT_DIPERLUKAN)
        else:
            QMessageBox.information(
                self,
                "Coba lagi",
                "Jumlah baris terdampak berubah sejak pratinjau terakhir. Pratinjau sudah diperbarui - "
                "periksa lagi lalu klik Jalankan.",
            )
