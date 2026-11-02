"""Layar Data Siswa/Guru/Kelas - tabel isian (PRD §11.4), akses kepala
sekolah/admin saja (hak akses ditegakkan server, lihat
app/server/routers/siswa.py dkk - layar ini TIDAK mengulang pemeriksaan
peran, hanya menampilkan pesan bila server menolak).

Susunan: tiga tab (Siswa, Guru, Kelas), masing-masing satu
TabelIsianView di atas TabelIsianModel. Baris tersimpan otomatis begitu
lengkap (lihat model_tabel.py) - tidak ada tombol "Simpan".

Pendaftaran kartu (§11.4, §12.6) dipicu tombol "Daftarkan Kartu", aktif
hanya untuk baris yang sudah tersimpan di server (punya id).
"""

from __future__ import annotations

from PySide6.QtWidgets import (
    QHBoxLayout,
    QLabel,
    QMainWindow,
    QMessageBox,
    QPushButton,
    QTabWidget,
    QVBoxLayout,
    QWidget,
)

from app.rfid.base import PembacaKartu
from app.ui.data_master.klien_data_master import GagalHubungiServer, KlienDataMaster, ServerMenolak
from app.ui.data_master.model_tabel import TabelIsianModel
from app.ui.data_master.pendaftaran_kartu import DialogPendaftaranKartu
from app.ui.data_master.tabel_view import TabelIsianView
from app.ui.data_master.validasi_baris import (
    cek_format_nik,
    cek_format_nisn,
    cek_format_nomor_whatsapp,
    cek_format_tanggal,
    validasi_baris_guru,
    validasi_baris_kelas,
    validasi_baris_siswa,
)

KOLOM_SISWA = [
    ("nama_lengkap", "Nama"),
    ("nisn", "NISN"),
    ("nipd", "NIPD"),
    ("jenis_kelamin", "JK"),
    ("tempat_lahir", "Tempat Lahir"),
    ("tanggal_lahir", "Tanggal Lahir"),
    ("nik", "NIK"),
    ("alamat", "Alamat"),
    ("nama_ortu", "Nama Orang Tua"),
    ("nomor_whatsapp_ortu", "No. HP/WA Ortu"),
    ("nama_kelas", "Kelas"),
]

KOLOM_GURU = [
    ("nama_lengkap", "Nama"),
    ("nip", "NIP"),
    ("pangkat_jabatan", "Pangkat/Jabatan"),
]

KOLOM_KELAS = [
    ("tingkat", "Tingkat"),
    ("nama_kelas", "Nama Rombel"),
    ("tahun_ajaran", "Tahun Ajaran"),
    ("wali_kelas", "Wali Kelas"),
]


class _TabSiswa(QWidget):
    def __init__(self, *, klien: KlienDataMaster, pembaca: PembacaKartu | None, layar_kelas: "_TabKelas") -> None:
        super().__init__()
        self._klien = klien
        self._pembaca = pembaca
        self._layar_kelas = layar_kelas

        self.model = TabelIsianModel(
            kolom=KOLOM_SISWA,
            validator=self._validasi,
            on_create=lambda payload: klien.buat_siswa(payload),
            on_update=lambda id_siswa, payload: klien.ubah_siswa(id_siswa, payload),
            format_checkers={
                "nisn": cek_format_nisn,
                "nik": cek_format_nik,
                "nomor_whatsapp_ortu": cek_format_nomor_whatsapp,
                "tanggal_lahir": cek_format_tanggal,
            },
        )
        self.view = TabelIsianView()
        self.view.setModel(self.model)

        self._label_progres = QLabel("0 baris")
        tombol_kartu = QPushButton("Daftarkan Kartu")
        tombol_kartu.clicked.connect(self._daftarkan_kartu)
        tombol_muat_ulang = QPushButton("Muat Ulang dari Server")
        tombol_muat_ulang.clicked.connect(self.muat_ulang)

        bar = QHBoxLayout()
        bar.addWidget(self._label_progres)
        bar.addStretch(1)
        bar.addWidget(tombol_kartu)
        bar.addWidget(tombol_muat_ulang)

        tata_letak = QVBoxLayout(self)
        tata_letak.addLayout(bar)
        tata_letak.addWidget(self.view)

        self.model.dataChanged.connect(self._perbarui_label)
        self.model.modelReset.connect(self._perbarui_label)
        self._perbarui_label()

    def _validasi(self, nilai: dict[str, str]):
        return validasi_baris_siswa(nilai, id_kelas_by_nama=self._layar_kelas.kelas_by_nama())

    def _perbarui_label(self, *_args) -> None:
        self._label_progres.setText(f"{self.model.jumlah_tersimpan()} dari {self.model.jumlah_baris_terisi()} baris tersimpan")

    def muat_ulang(self) -> None:
        try:
            daftar = self._klien.daftar_siswa()
        except (ServerMenolak, GagalHubungiServer) as exc:
            QMessageBox.warning(self, "Gagal memuat data siswa", str(exc))
            return
        self.model.muat_baris(
            [
                (
                    s["id"],
                    {
                        "nama_lengkap": s["nama_lengkap"],
                        "nisn": s["nisn"],
                        "nipd": s.get("nipd") or "",
                        "jenis_kelamin": s["jenis_kelamin"],
                        "tempat_lahir": s["tempat_lahir"],
                        "tanggal_lahir": s["tanggal_lahir"],
                        "nik": "",  # NIK tersamar di daftar (§11.1) - tidak dimuat balik ke sel isian
                        "alamat": s["alamat"],
                        "nama_ortu": s.get("nama_ortu") or "",
                        "nomor_whatsapp_ortu": s.get("nomor_whatsapp_ortu") or "",
                        "nama_kelas": s.get("nama_kelas") or "",
                    },
                )
                for s in daftar
            ]
        )
        self._perbarui_label()

    def _daftarkan_kartu(self) -> None:
        if self._pembaca is None:
            QMessageBox.information(self, "Daftarkan Kartu", "Tidak ada pembaca kartu terpasang di stasiun ini.")
            return
        index = self.view.currentIndex()
        if not index.isValid():
            QMessageBox.information(self, "Daftarkan Kartu", "Pilih baris siswa terlebih dahulu.")
            return
        id_siswa = self.model.id_server_pada_baris(index.row())
        if id_siswa is None:
            QMessageBox.information(self, "Daftarkan Kartu", "Lengkapi dan simpan data siswa ini terlebih dahulu.")
            return
        nama = self.model.nilai_pada_baris(index.row()).get("nama_lengkap", "")
        dialog = DialogPendaftaranKartu(
            klien=self._klien, pembaca=self._pembaca, id_pemilik=id_siswa, jenis_pemilik="SISWA", nama_pemilik=nama,
            parent=self,
        )
        dialog.exec()


class _TabGuru(QWidget):
    def __init__(self, *, klien: KlienDataMaster, pembaca: PembacaKartu | None) -> None:
        super().__init__()
        self._klien = klien
        self._pembaca = pembaca

        self.model = TabelIsianModel(
            kolom=KOLOM_GURU,
            validator=validasi_baris_guru,
            on_create=lambda payload: klien.buat_guru(payload),
            on_update=lambda id_guru, payload: klien.ubah_guru(id_guru, payload),
        )
        self.view = TabelIsianView()
        self.view.setModel(self.model)

        self._label_progres = QLabel("0 baris")
        tombol_kartu = QPushButton("Daftarkan Kartu")
        tombol_kartu.clicked.connect(self._daftarkan_kartu)
        tombol_muat_ulang = QPushButton("Muat Ulang dari Server")
        tombol_muat_ulang.clicked.connect(self.muat_ulang)

        bar = QHBoxLayout()
        bar.addWidget(self._label_progres)
        bar.addStretch(1)
        bar.addWidget(tombol_kartu)
        bar.addWidget(tombol_muat_ulang)

        tata_letak = QVBoxLayout(self)
        tata_letak.addLayout(bar)
        tata_letak.addWidget(self.view)

        self.model.dataChanged.connect(self._perbarui_label)
        self.model.modelReset.connect(self._perbarui_label)
        self._perbarui_label()

    def nama_by_id(self) -> dict[int, str]:
        hasil = {}
        for row in range(self.model.rowCount()):
            id_guru = self.model.id_server_pada_baris(row)
            if id_guru is not None:
                hasil[id_guru] = self.model.nilai_pada_baris(row).get("nama_lengkap", "")
        return hasil

    def guru_by_nama(self) -> dict[str, int]:
        return {nama: id_guru for id_guru, nama in self.nama_by_id().items() if nama}

    def _perbarui_label(self, *_args) -> None:
        self._label_progres.setText(f"{self.model.jumlah_tersimpan()} dari {self.model.jumlah_baris_terisi()} baris tersimpan")

    def muat_ulang(self) -> None:
        try:
            daftar = self._klien.daftar_guru()
        except (ServerMenolak, GagalHubungiServer) as exc:
            QMessageBox.warning(self, "Gagal memuat data guru", str(exc))
            return
        self.model.muat_baris(
            [
                (g["id"], {"nama_lengkap": g["nama_lengkap"], "nip": g.get("nip") or "", "pangkat_jabatan": g.get("pangkat_jabatan") or ""})
                for g in daftar
            ]
        )
        self._perbarui_label()

    def _daftarkan_kartu(self) -> None:
        if self._pembaca is None:
            QMessageBox.information(self, "Daftarkan Kartu", "Tidak ada pembaca kartu terpasang di stasiun ini.")
            return
        index = self.view.currentIndex()
        if not index.isValid():
            QMessageBox.information(self, "Daftarkan Kartu", "Pilih baris guru terlebih dahulu.")
            return
        id_guru = self.model.id_server_pada_baris(index.row())
        if id_guru is None:
            QMessageBox.information(self, "Daftarkan Kartu", "Lengkapi dan simpan data guru ini terlebih dahulu.")
            return
        nama = self.model.nilai_pada_baris(index.row()).get("nama_lengkap", "")
        dialog = DialogPendaftaranKartu(
            klien=self._klien, pembaca=self._pembaca, id_pemilik=id_guru, jenis_pemilik="GURU", nama_pemilik=nama,
            parent=self,
        )
        dialog.exec()


class _TabKelas(QWidget):
    def __init__(self, *, klien: KlienDataMaster, tab_guru: _TabGuru) -> None:
        super().__init__()
        self._klien = klien
        self._tab_guru = tab_guru

        self.model = TabelIsianModel(
            kolom=KOLOM_KELAS,
            validator=self._validasi,
            on_create=lambda payload: klien.buat_kelas(payload),
            on_update=lambda id_kelas, payload: klien.ubah_kelas(id_kelas, payload),
        )
        self.view = TabelIsianView()
        self.view.setModel(self.model)

        self._label_progres = QLabel("0 baris")
        tombol_muat_ulang = QPushButton("Muat Ulang dari Server")
        tombol_muat_ulang.clicked.connect(self.muat_ulang)

        bar = QHBoxLayout()
        bar.addWidget(self._label_progres)
        bar.addStretch(1)
        bar.addWidget(tombol_muat_ulang)

        tata_letak = QVBoxLayout(self)
        tata_letak.addLayout(bar)
        tata_letak.addWidget(self.view)

        self.model.dataChanged.connect(self._perbarui_label)
        self.model.modelReset.connect(self._perbarui_label)
        self._perbarui_label()

    def _validasi(self, nilai: dict[str, str]):
        return validasi_baris_kelas(nilai, id_guru_by_nama=self._tab_guru.guru_by_nama())

    def kelas_by_nama(self) -> dict[str, int]:
        hasil: dict[str, int] = {}
        for row in range(self.model.rowCount()):
            id_kelas = self.model.id_server_pada_baris(row)
            if id_kelas is not None:
                nama = self.model.nilai_pada_baris(row).get("nama_kelas", "")
                if nama:
                    hasil[nama] = id_kelas
        return hasil

    def _perbarui_label(self, *_args) -> None:
        self._label_progres.setText(f"{self.model.jumlah_tersimpan()} dari {self.model.jumlah_baris_terisi()} baris tersimpan")

    def muat_ulang(self) -> None:
        try:
            daftar = self._klien.daftar_kelas()
        except (ServerMenolak, GagalHubungiServer) as exc:
            QMessageBox.warning(self, "Gagal memuat data kelas", str(exc))
            return
        self.model.muat_baris(
            [
                (
                    k["id"],
                    {
                        "tingkat": str(k["tingkat"]),
                        "nama_kelas": k["nama_kelas"],
                        "tahun_ajaran": k["tahun_ajaran"],
                        "wali_kelas": k.get("nama_wali_kelas") or "",
                    },
                )
                for k in daftar
            ]
        )
        self._perbarui_label()


class LayarDataMaster(QMainWindow):
    """Jendela Data Master - tiga tab (Siswa, Guru, Kelas). ``pembaca``
    opsional: None berarti stasiun ini tidak punya pembaca kartu terpasang
    (pendaftaran kartu dinonaktifkan, sisanya tetap berfungsi)."""

    def __init__(self, *, klien: KlienDataMaster, pembaca: PembacaKartu | None = None) -> None:
        super().__init__()
        self.setWindowTitle("Data Master - Siswa, Guru, Kelas")

        tab_guru = _TabGuru(klien=klien, pembaca=pembaca)
        tab_kelas = _TabKelas(klien=klien, tab_guru=tab_guru)
        tab_siswa = _TabSiswa(klien=klien, pembaca=pembaca, layar_kelas=tab_kelas)

        tabs = QTabWidget()
        tabs.addTab(tab_siswa, "Data Siswa")
        tabs.addTab(tab_guru, "Data Guru")
        tabs.addTab(tab_kelas, "Data Kelas")
        tabs.currentChanged.connect(lambda _i: (tab_guru.muat_ulang(), tab_kelas.muat_ulang()))
        self.setCentralWidget(tabs)

        self._tab_siswa = tab_siswa
        self._tab_guru = tab_guru
        self._tab_kelas = tab_kelas

    def muat_awal(self) -> None:
        """Panggil sekali setelah jendela dibuat, supaya galat jaringan
        (bila server belum terjangkau) tampil lewat QMessageBox milik
        jendela ini, bukan meledak sebelum jendela sempat tampil."""
        self._tab_guru.muat_ulang()
        self._tab_kelas.muat_ulang()
        self._tab_siswa.muat_ulang()
