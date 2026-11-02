"""TabelIsianModel: model tabel isian generik untuk layar data master
(PRD §11.4) - dipakai untuk siswa, guru, dan kelas dengan konfigurasi
kolom dan validator berbeda.

Perilaku (PRD §11.4):
- Tersimpan otomatis per baris: begitu satu baris LENGKAP dan valid
  (validator mengembalikan payload, bukan galat), model langsung memanggil
  on_create/on_update. Pengguna tidak pernah menekan tombol "simpan baris".
- Penanda progres: kolom "Status" (read-only, kolom terakhir) menunjukkan
  Baru/Tersimpan/Galat, dipakai layar untuk label "X dari Y baris tersimpan".
- Selalu ada satu baris kosong tambahan di akhir, siap diisi.
- Galat FORMAT (mis. NISN bukan 10 digit) ditandai SEKETIKA per sel lewat
  format_checkers, tanpa menunggu baris lengkap - sel kosong tidak pernah
  dianggap galat sampai baris dicoba disimpan (lihat
  app.ui.data_master.validasi_baris).

Hanya bergantung pada PySide6.QtCore - tidak pernah membuka HTTP sendiri;
on_create/on_update disuntikkan dari luar (app.ui.data_master.layar), yang
memanggil app.ui.data_master.klien_data_master.KlienDataMaster. Ini
menjaga model tetap mudah dibaca dan galat jaringan tertangani di satu
tempat (layar), bukan tersebar di model.
"""

from __future__ import annotations

from collections.abc import Callable
from dataclasses import dataclass, field
from typing import Any

from PySide6.QtCore import QAbstractTableModel, QModelIndex, Qt
from PySide6.QtGui import QColor

from app.ui.data_master.klien_data_master import GagalHubungiServer, ServerMenolak
from app.ui.data_master.validasi_baris import HasilValidasiBaris

_WARNA_GALAT = QColor("#fde2e2")
_WARNA_TERSIMPAN = QColor("#eaf7ea")


@dataclass
class _Kolom:
    kunci: str
    label: str
    pengecek_format: Callable[[str], str | None] | None = None


@dataclass
class _Baris:
    nilai: dict[str, str] = field(default_factory=dict)
    id_server: int | None = None
    status: str = "Baru"
    galat: dict[str, str] = field(default_factory=dict)


class TabelIsianModel(QAbstractTableModel):
    def __init__(
        self,
        *,
        kolom: list[tuple[str, str]],
        validator: Callable[[dict[str, str]], HasilValidasiBaris],
        on_create: Callable[[dict[str, Any]], dict[str, Any]],
        on_update: Callable[[int, dict[str, Any]], dict[str, Any]],
        format_checkers: dict[str, Callable[[str], str | None]] | None = None,
        parent=None,
    ) -> None:
        super().__init__(parent)
        self._kolom = [_Kolom(kunci=k, label=lbl, pengecek_format=(format_checkers or {}).get(k)) for k, lbl in kolom]
        self._validator = validator
        self._on_create = on_create
        self._on_update = on_update
        self._baris: list[_Baris] = [_Baris()]

    # ------------------------------------------------------------------
    # Muat data awal (mis. daftar siswa yang sudah ada di server)
    # ------------------------------------------------------------------

    def muat_baris(self, daftar_nilai: list[tuple[int, dict[str, str]]]) -> None:
        self.beginResetModel()
        self._baris = [_Baris(nilai=nilai, id_server=id_server, status="Tersimpan") for id_server, nilai in daftar_nilai]
        self._baris.append(_Baris())
        self.endResetModel()

    # ------------------------------------------------------------------
    # QAbstractTableModel
    # ------------------------------------------------------------------

    def rowCount(self, parent: QModelIndex = QModelIndex()) -> int:  # noqa: B008 - pola resmi PySide6
        return 0 if parent.isValid() else len(self._baris)

    def columnCount(self, parent: QModelIndex = QModelIndex()) -> int:  # noqa: B008
        return 0 if parent.isValid() else len(self._kolom) + 1  # +1 kolom Status

    def headerData(self, section: int, orientation: Qt.Orientation, role: int = Qt.ItemDataRole.DisplayRole):
        if role != Qt.ItemDataRole.DisplayRole or orientation != Qt.Orientation.Horizontal:
            return None
        if section < len(self._kolom):
            return self._kolom[section].label
        return "Status"

    def flags(self, index: QModelIndex) -> Qt.ItemFlag:
        if not index.isValid():
            return Qt.ItemFlag.NoItemFlags
        dasar = Qt.ItemFlag.ItemIsEnabled | Qt.ItemFlag.ItemIsSelectable
        if index.column() < len(self._kolom):
            return dasar | Qt.ItemFlag.ItemIsEditable
        return dasar

    def data(self, index: QModelIndex, role: int = Qt.ItemDataRole.DisplayRole):
        if not index.isValid():
            return None
        baris = self._baris[index.row()]
        kolom_status = index.column() >= len(self._kolom)

        if role in (Qt.ItemDataRole.DisplayRole, Qt.ItemDataRole.EditRole):
            if kolom_status:
                return baris.status
            kunci = self._kolom[index.column()].kunci
            return baris.nilai.get(kunci, "")

        if role == Qt.ItemDataRole.BackgroundRole:
            if not kolom_status and self._kolom[index.column()].kunci in baris.galat:
                return _WARNA_GALAT
            if baris.status == "Tersimpan":
                return _WARNA_TERSIMPAN
            if baris.status.startswith("Galat"):
                return _WARNA_GALAT
            return None

        if role == Qt.ItemDataRole.ToolTipRole:
            if not kolom_status:
                kunci = self._kolom[index.column()].kunci
                return baris.galat.get(kunci)
            if baris.status.startswith("Galat"):
                return baris.status
            return None

        return None

    def setData(self, index: QModelIndex, value: Any, role: int = Qt.ItemDataRole.EditRole) -> bool:
        if role != Qt.ItemDataRole.EditRole or not index.isValid() or index.column() >= len(self._kolom):
            return False

        baris = self._baris[index.row()]
        kunci = self._kolom[index.column()].kunci
        baris.nilai[kunci] = str(value)

        pengecek = self._kolom[index.column()].pengecek_format
        if pengecek is not None:
            pesan = pengecek(str(value))
            if pesan:
                baris.galat[kunci] = pesan
            else:
                baris.galat.pop(kunci, None)

        self._coba_simpan_baris(index.row())
        self._pastikan_baris_kosong_di_akhir()
        self.dataChanged.emit(self.index(index.row(), 0), self.index(index.row(), self.columnCount() - 1))
        return True

    # ------------------------------------------------------------------
    # Autosave per baris (PRD §11.4)
    # ------------------------------------------------------------------

    def _coba_simpan_baris(self, row: int) -> None:
        baris = self._baris[row]
        hasil = self._validator(baris.nilai)
        baris.galat = dict(hasil.galat)

        if not hasil.lengkap:
            if baris.id_server is None:
                baris.status = "Baru" if not any(v.strip() for v in baris.nilai.values()) else "Belum lengkap"
            return

        try:
            if baris.id_server is None:
                respons = self._on_create(hasil.payload)
                baris.id_server = respons["id"]
            else:
                self._on_update(baris.id_server, hasil.payload)
            baris.status = "Tersimpan"
        except ServerMenolak as exc:
            baris.status = f"Galat: {exc.detail}"
        except GagalHubungiServer as exc:
            baris.status = f"Galat: {exc}"

    def _pastikan_baris_kosong_di_akhir(self) -> None:
        if not self._baris:
            self.beginInsertRows(QModelIndex(), 0, 0)
            self._baris.append(_Baris())
            self.endInsertRows()
            return
        terakhir = self._baris[-1]
        if any(v.strip() for v in terakhir.nilai.values()):
            posisi = len(self._baris)
            self.beginInsertRows(QModelIndex(), posisi, posisi)
            self._baris.append(_Baris())
            self.endInsertRows()

    # ------------------------------------------------------------------
    # Untuk label progres di layar
    # ------------------------------------------------------------------

    def jumlah_tersimpan(self) -> int:
        return sum(1 for b in self._baris if b.status == "Tersimpan")

    def jumlah_baris_terisi(self) -> int:
        return sum(1 for b in self._baris if any(v.strip() for v in b.nilai.values()))

    def id_server_pada_baris(self, row: int) -> int | None:
        return self._baris[row].id_server

    def nilai_pada_baris(self, row: int) -> dict[str, str]:
        return dict(self._baris[row].nilai)
