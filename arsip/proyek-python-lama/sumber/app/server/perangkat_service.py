"""Layanan Perangkat (PRD §12.4): izin darurat alat RFID dan wizard ganti
jalur. Lapisan integrasi DB (sama pola dengan app.server.koreksi_guru_service)
- orkestrasi repository + audit, TIDAK menyentuh app.core.engine/
mesin_absensi/klasifikasi kehadiran sama sekali.
"""

from __future__ import annotations

from dataclasses import dataclass

from app.config.pengaturan import Pengaturan
from app.core.perangkat import (
    HasilCekKartu,
    hitung_waktu_berakhir,
    pastikan_berwenang_ganti_jalur,
    pastikan_berwenang_memberi_izin,
    pastikan_berwenang_memperpanjang,
    pastikan_verifikasi_kartu_cukup,
)
from app.core.status import Peran
from app.core.timeutil import now_wita
from app.data.models.perangkat import IzinDaruratAlat
from app.data.models.system import User
from app.data.repositories.perangkat import IzinDaruratAlatRepository
from app.data.repositories.rfid_card import RfidCardRepository


class IzinTidakDitemukan(LookupError):
    pass


class IzinSudahTidakAktif(Exception):
    """Izin sudah dicabut atau sudah kedaluwarsa sendiri - tidak bisa
    diperpanjang/dicabut lagi. Perpanjang izin yang sudah lewat berarti
    memberi izin BARU (beri_izin), bukan memperpanjang yang lama."""


class SudahAdaIzinAktif(Exception):
    def __init__(self, station_id: str) -> None:
        self.station_id = station_id
        super().__init__(f"Jalur {station_id} sudah punya izin darurat aktif")


class IzinDaruratService:
    def __init__(self, *, izin_repo: IzinDaruratAlatRepository, pengaturan: Pengaturan) -> None:
        self._izin_repo = izin_repo
        self._pengaturan = pengaturan

    def beri_izin(self, *, pelaku: User, station_id: str, jam: float, alasan: str) -> IzinDaruratAlat:
        pastikan_berwenang_memberi_izin(Peran(pelaku.peran.value))
        sekarang = now_wita()
        if self._izin_repo.cari_aktif_by_station(station_id, sekarang=sekarang) is not None:
            raise SudahAdaIzinAktif(station_id)

        waktu_berakhir = hitung_waktu_berakhir(
            waktu_mulai=sekarang, jam_diminta=jam, jam_maks=self._pengaturan.maks_jam_izin_darurat_alat()
        )
        return self._izin_repo.buat(
            station_id=station_id,
            id_pengguna_pemberi=pelaku.id,
            waktu_mulai=sekarang,
            waktu_berakhir=waktu_berakhir,
            alasan=alasan,
        )

    def perpanjang(self, *, pelaku: User, id_izin: int, jam: float, alasan: str) -> IzinDaruratAlat:
        """Perpanjangan menghitung ulang waktu_berakhir dari SEKARANG (jam
        baru, bukan menambah ke waktu_berakhir lama) - tetap tunduk batas
        maks_jam_izin_darurat_alat yang sama seperti pemberian awal."""
        pastikan_berwenang_memperpanjang(Peran(pelaku.peran.value))
        izin = self._ambil_izin_aktif(id_izin)

        sekarang = now_wita()
        waktu_berakhir_baru = hitung_waktu_berakhir(
            waktu_mulai=sekarang, jam_diminta=jam, jam_maks=self._pengaturan.maks_jam_izin_darurat_alat()
        )
        return self._izin_repo.perpanjang(
            izin, waktu_berakhir_baru=waktu_berakhir_baru, id_pengguna=pelaku.id, alasan=alasan
        )

    def cabut(self, *, pelaku: User, id_izin: int, alasan: str) -> IzinDaruratAlat:
        # Wewenang cabut disamakan dengan wewenang perpanjang (kepsek/admin
        # saja) - PRD/SKILL.md diam soal pencabutan, keduanya sama-sama
        # aksi konsekuensial atas izin yang sudah berjalan.
        pastikan_berwenang_memperpanjang(Peran(pelaku.peran.value))
        izin = self._ambil_izin_aktif(id_izin)
        return self._izin_repo.cabut(izin, id_pengguna=pelaku.id, sekarang=now_wita(), alasan=alasan)

    def _ambil_izin_aktif(self, id_izin: int) -> IzinDaruratAlat:
        izin = self._izin_repo.cari_by_id(id_izin)
        if izin is None:
            raise IzinTidakDitemukan(id_izin)
        if izin.dicabut or izin.waktu_berakhir <= now_wita():
            raise IzinSudahTidakAktif(id_izin)
        return izin

    def daftar_aktif(self) -> list[IzinDaruratAlat]:
        return self._izin_repo.daftar_aktif(sekarang=now_wita())

    def cek_aktif_by_station(self, station_id: str) -> IzinDaruratAlat | None:
        """Dipakai lobi (app.ui.lobi.klien_scan) untuk menyalakan banner
        MODE DARURAT - endpoint boleh diakses SELURUH peran (termasuk
        Operator kiosk lobi), beda dari beri_izin/perpanjang/cabut yang
        dibatasi peran tertentu (lihat app.server.routers.perangkat)."""
        return self._izin_repo.cari_aktif_by_station(station_id, sekarang=now_wita())


@dataclass(frozen=True)
class HasilGantiJalur:
    jalur_lama: str
    jalur_baru: str


class GantiJalurService:
    def __init__(self, *, rfid_card_repo: RfidCardRepository, pengaturan: Pengaturan) -> None:
        self._rfid_card_repo = rfid_card_repo
        self._pengaturan = pengaturan

    def jalankan(
        self, *, pelaku: User, jalur_baru: str, uid_kartu_verifikasi: list[str], alasan: str
    ) -> HasilGantiJalur:
        """Commit Wizard Ganti Jalur (PRD §12.4): "seluruh unit terpasang" +
        "uji baca tiap jalur" adalah prosedur yang admin jalani DI LUAR
        panggilan ini (lihat app.core.perangkat, catatan modul) - satu-
        satunya yang diverifikasi SERVER adalah tiga kartu terdaftar,
        sebelum jalur_alat_aktif benar-benar berubah."""
        pastikan_berwenang_ganti_jalur(Peran(pelaku.peran.value))

        hasil_cek = [
            HasilCekKartu(uid=uid, terdaftar=self._rfid_card_repo.cari_by_uid(uid) is not None)
            for uid in uid_kartu_verifikasi
        ]
        pastikan_verifikasi_kartu_cukup(hasil_cek)

        jalur_lama = self._pengaturan.jalur_alat_aktif
        self._pengaturan.atur_jalur_alat_aktif(jalur_baru, id_pengguna=pelaku.id, alasan=alasan)
        return HasilGantiJalur(jalur_lama=jalur_lama, jalur_baru=jalur_baru)
