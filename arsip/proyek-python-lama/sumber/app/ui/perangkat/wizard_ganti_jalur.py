"""Wizard Ganti Jalur Alat (PRD §12.4, SKILL.md "Wizard 'Ganti Jalur
Alat'" - kepsek/admin, berurutan dan tidak bisa dilompati):

    1. Seluruh unit terpasang (pernyataan admin - tidak ada registry
       perangkat terpusat untuk diverifikasi otomatis, lihat README)
    2. Uji baca tiap jalur (live, lewat WidgetPembacaLangsung)
    3. Verifikasi tiga kartu terdaftar (live, dicek ke server per tap)
    4. Konfirmasi beralasan -> commit ke server (POST /perangkat/ganti-jalur)

Server yang menegakkan validasi sesungguhnya (app.core.perangkat,
app.server.perangkat_service) - wizard ini hanya memandu urutannya dan
mencegah Finish diklik sebelum ketiga langkah pertama terpenuhi.
"""

from __future__ import annotations

from PySide6.QtWidgets import (
    QCheckBox,
    QComboBox,
    QFormLayout,
    QLabel,
    QListWidget,
    QMessageBox,
    QPlainTextEdit,
    QVBoxLayout,
    QWizard,
    QWizardPage,
)

from app.rfid.base import JalurAlat, ScanEvent
from app.ui.perangkat.klien_perangkat import GagalHubungiServer, KlienPerangkat, ServerMenolak
from app.ui.perangkat.pembaca_langsung import WidgetPembacaLangsung


class _HalamanUnitTerpasang(QWizardPage):
    def __init__(self) -> None:
        super().__init__()
        self.setTitle("1. Jalur baru dan unit terpasang")

        self.pilihan_jalur = QComboBox()
        for jalur in (JalurAlat.PCSC, JalurAlat.SERIAL, JalurAlat.HID):
            self.pilihan_jalur.addItem(jalur.value, jalur)

        self._centang = QCheckBox("Seluruh unit reader di sekolah sudah dipasang dengan jalur ini")
        self._centang.stateChanged.connect(self.completeChanged)

        tata = QVBoxLayout(self)
        form = QFormLayout()
        form.addRow("Jalur baru", self.pilihan_jalur)
        tata.addLayout(form)
        tata.addWidget(
            QLabel(
                "Penggantian jalur berlaku untuk SELURUH sistem, bukan satu PC. Pastikan\n"
                "reader baru sudah terpasang fisik di seluruh titik scan sebelum melanjutkan."
            )
        )
        tata.addWidget(self._centang)

    def isComplete(self) -> bool:  # noqa: N802 - override Qt
        return self._centang.isChecked()


class _HalamanUjiBaca(QWizardPage):
    def __init__(self) -> None:
        super().__init__()
        self.setTitle("2. Uji baca jalur baru")
        self._sudah_baca = False

        self._pembaca = WidgetPembacaLangsung(station_id="WIZARD-UJI-BACA")
        self._pembaca.scan_diterima.connect(self._pada_scan)
        self._label = QLabel("Pilih jalur di atas lalu tempelkan kartu apa pun untuk membuktikan pembacaan.")
        self._label.setWordWrap(True)

        tata = QVBoxLayout(self)
        tata.addWidget(self._pembaca)
        tata.addWidget(self._label)

    def initializePage(self) -> None:  # noqa: N802 - override Qt
        halaman_1: _HalamanUnitTerpasang = self.wizard().page(0)  # type: ignore[assignment]
        jalur = halaman_1.pilihan_jalur.currentData()
        index = self._pembaca._pilihan_jalur.findData(jalur)  # noqa: SLF001 - satu wizard, satu unit yang erat
        if index >= 0:
            self._pembaca._pilihan_jalur.setCurrentIndex(index)  # noqa: SLF001

    def _pada_scan(self, event: ScanEvent) -> None:
        self._sudah_baca = True
        self._label.setText(f"Berhasil membaca UID {event.uid} di jalur ini.")
        self.completeChanged.emit()

    def isComplete(self) -> bool:  # noqa: N802 - override Qt
        return self._sudah_baca


class _HalamanVerifikasiKartu(QWizardPage):
    def __init__(self, *, klien: KlienPerangkat) -> None:
        super().__init__()
        self.setTitle("3. Verifikasi tiga kartu terdaftar")
        self._klien = klien
        self.uid_terverifikasi: list[str] = []

        self._pembaca = WidgetPembacaLangsung(station_id="WIZARD-VERIFIKASI")
        self._pembaca.scan_diterima.connect(self._pada_scan)
        self._daftar = QListWidget()
        self._label = QLabel("Tempelkan tiga kartu YANG SUDAH TERDAFTAR secara berurutan (0/3).")
        self._label.setWordWrap(True)

        tata = QVBoxLayout(self)
        tata.addWidget(self._pembaca)
        tata.addWidget(self._label)
        tata.addWidget(self._daftar)

    def initializePage(self) -> None:  # noqa: N802 - override Qt
        halaman_1: _HalamanUnitTerpasang = self.wizard().page(0)  # type: ignore[assignment]
        jalur = halaman_1.pilihan_jalur.currentData()
        index = self._pembaca._pilihan_jalur.findData(jalur)  # noqa: SLF001
        if index >= 0:
            self._pembaca._pilihan_jalur.setCurrentIndex(index)  # noqa: SLF001

    def _pada_scan(self, event: ScanEvent) -> None:
        if event.uid in self.uid_terverifikasi:
            return
        if len(self.uid_terverifikasi) >= 3:
            return
        try:
            hasil = self._klien.cek_uid(event.uid)
        except (GagalHubungiServer, ServerMenolak) as exc:
            QMessageBox.warning(self, "Gagal memeriksa kartu", str(exc))
            return

        if not hasil["terdaftar"]:
            QMessageBox.warning(self, "Kartu tidak terdaftar", f"UID {event.uid} tidak terdaftar, coba kartu lain.")
            return

        self.uid_terverifikasi.append(event.uid)
        self._daftar.addItem(f"{event.uid} - {hasil['nama_pemilik'] or 'terdaftar'}")
        self._label.setText(f"Tempelkan kartu berikutnya ({len(self.uid_terverifikasi)}/3).")
        self.completeChanged.emit()

    def isComplete(self) -> bool:  # noqa: N802 - override Qt
        return len(self.uid_terverifikasi) == 3


class _HalamanKonfirmasi(QWizardPage):
    def __init__(self, *, klien: KlienPerangkat) -> None:
        super().__init__()
        self.setTitle("4. Konfirmasi")
        self._klien = klien

        self._ringkasan = QLabel()
        self._ringkasan.setWordWrap(True)
        self._alasan = QPlainTextEdit()
        self._alasan.setPlaceholderText("Alasan penggantian jalur (wajib)")
        self._alasan.textChanged.connect(self.completeChanged)

        tata = QVBoxLayout(self)
        tata.addWidget(self._ringkasan)
        tata.addWidget(QLabel("Alasan"))
        tata.addWidget(self._alasan)

    def initializePage(self) -> None:  # noqa: N802 - override Qt
        halaman_1: _HalamanUnitTerpasang = self.wizard().page(0)  # type: ignore[assignment]
        halaman_3: _HalamanVerifikasiKartu = self.wizard().page(2)  # type: ignore[assignment]
        jalur = halaman_1.pilihan_jalur.currentData()
        self._ringkasan.setText(
            f"Jalur baru: {jalur.value}\nKartu terverifikasi: {', '.join(halaman_3.uid_terverifikasi)}"
        )

    def isComplete(self) -> bool:  # noqa: N802 - override Qt
        return bool(self._alasan.toPlainText().strip())

    def validatePage(self) -> bool:  # noqa: N802 - override Qt
        halaman_1: _HalamanUnitTerpasang = self.wizard().page(0)  # type: ignore[assignment]
        halaman_3: _HalamanVerifikasiKartu = self.wizard().page(2)  # type: ignore[assignment]
        jalur = halaman_1.pilihan_jalur.currentData()
        try:
            self._klien.ganti_jalur(
                jalur_baru=jalur.value,
                uid_kartu_verifikasi=halaman_3.uid_terverifikasi,
                alasan=self._alasan.toPlainText().strip(),
            )
        except (GagalHubungiServer, ServerMenolak) as exc:
            QMessageBox.warning(self, "Gagal mengganti jalur", str(exc))
            return False
        QMessageBox.information(self, "Berhasil", f"Jalur alat aktif kini {jalur.value}.")
        return True


class WizardGantiJalur(QWizard):
    def __init__(self, *, klien: KlienPerangkat, parent=None) -> None:
        super().__init__(parent)
        self.setWindowTitle("Wizard Ganti Jalur Alat")
        self.setOption(QWizard.WizardOption.NoBackButtonOnLastPage, False)
        self.addPage(_HalamanUnitTerpasang())
        self.addPage(_HalamanUjiBaca())
        self.addPage(_HalamanVerifikasiKartu(klien=klien))
        self.addPage(_HalamanKonfirmasi(klien=klien))
