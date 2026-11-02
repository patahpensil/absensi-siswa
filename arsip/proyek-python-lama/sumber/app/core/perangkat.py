"""Aturan Perangkat (PRD §12.4): izin darurat alat RFID dan verifikasi
wizard ganti jalur. Fungsi murni - tidak menyentuh basis data, tidak
menyentuh app.core.engine/mesin_absensi/klasifikasi kehadiran sama
sekali.

Wizard ganti jalur dibangun sebagai PROSEDUR TERPANDU, bukan registry
perangkat (lihat README, catatan Device/ModePerangkat) - "seluruh unit
terpasang" adalah pernyataan admin (bukan sesuatu yang bisa diverifikasi
otomatis tanpa registry), sedangkan "verifikasi tiga kartu terdaftar"
adalah satu-satunya bagian yang BISA diverifikasi software, dan itulah
yang divalidasi di sini.
"""

from __future__ import annotations

from dataclasses import dataclass
from datetime import datetime, timedelta

from app.core.status import PERAN_IZIN_DARURAT_ALAT, PERAN_PERPANJANG_IZIN_DARURAT_ALAT, Peran

#: PRD §12.4: verifikasi wizard ganti jalur memakai TIGA kartu terdaftar -
#: bukan angka yang menunggu keputusan sekolah (beda dari ambang/jam),
#: jadi konstanta murni, bukan lewat Pengaturan.
JUMLAH_KARTU_VERIFIKASI_WIZARD = 3


# --------------------------------------------------------------- izin darurat


class AksesIzinDaruratDitolak(Exception):
    def __init__(self, peran: Peran) -> None:
        self.peran = peran
        super().__init__(f"Peran {peran.value} tidak berwenang memberikan izin darurat alat")


class AksesPerpanjangIzinDaruratDitolak(Exception):
    def __init__(self, peran: Peran) -> None:
        self.peran = peran
        super().__init__(f"Peran {peran.value} tidak berwenang memperpanjang/mencabut izin darurat alat")


class DurasiIzinMelebihiBatas(Exception):
    def __init__(self, jam_diminta: float, jam_maks: int) -> None:
        self.jam_diminta = jam_diminta
        self.jam_maks = jam_maks
        super().__init__(
            f"Durasi izin darurat {jam_diminta} jam melebihi batas {jam_maks} jam (PRD §12.4: paling lama 1x24 jam)"
        )


class SudahAdaIzinAktif(Exception):
    """Station ini sudah punya izin darurat yang sedang berlaku - perpanjang
    atau cabut yang lama dulu, jangan bikin baris baru yang tumpang tindih."""

    def __init__(self, station_id: str) -> None:
        self.station_id = station_id
        super().__init__(f"Jalur {station_id} sudah punya izin darurat aktif")


def pastikan_berwenang_memberi_izin(peran: Peran) -> None:
    if peran not in PERAN_IZIN_DARURAT_ALAT:
        raise AksesIzinDaruratDitolak(peran)


def pastikan_berwenang_memperpanjang(peran: Peran) -> None:
    if peran not in PERAN_PERPANJANG_IZIN_DARURAT_ALAT:
        raise AksesPerpanjangIzinDaruratDitolak(peran)


def hitung_waktu_berakhir(*, waktu_mulai: datetime, jam_diminta: float, jam_maks: int) -> datetime:
    """Validasi durasi lalu kembalikan waktu_berakhir. Dipanggil baik saat
    pemberian awal maupun perpanjangan - keduanya tunduk batas yang sama."""
    if jam_diminta <= 0:
        raise ValueError("Durasi izin darurat harus lebih dari nol jam")
    if jam_diminta > jam_maks:
        raise DurasiIzinMelebihiBatas(jam_diminta, jam_maks)
    return waktu_mulai + timedelta(hours=jam_diminta)


# --------------------------------------------------------------- wizard ganti jalur


class AksesGantiJalurDitolak(Exception):
    def __init__(self, peran: Peran) -> None:
        self.peran = peran
        super().__init__(f"Peran {peran.value} tidak berwenang menjalankan wizard ganti jalur alat")


#: PRD §12.4 + SKILL.md "Wizard 'Ganti Jalur Alat'": hanya kepsek/admin -
#: sama seperti PERAN_PERPANJANG_IZIN_DARURAT_ALAT, tapi konstanta
#: terpisah supaya kedua aturan bisa berubah independen di masa depan
#: tanpa diam-diam saling mempengaruhi.
PERAN_GANTI_JALUR_ALAT = frozenset({Peran.KEPALA_SEKOLAH, Peran.ADMIN})


def pastikan_berwenang_ganti_jalur(peran: Peran) -> None:
    if peran not in PERAN_GANTI_JALUR_ALAT:
        raise AksesGantiJalurDitolak(peran)


class JumlahKartuVerifikasiTidakCukup(Exception):
    def __init__(self, jumlah: int) -> None:
        self.jumlah = jumlah
        super().__init__(
            f"Wizard memerlukan tepat {JUMLAH_KARTU_VERIFIKASI_WIZARD} kartu terverifikasi, diterima {jumlah}"
        )


class KartuVerifikasiTidakUnik(Exception):
    def __init__(self) -> None:
        super().__init__("Ketiga UID kartu verifikasi wizard harus berbeda satu sama lain")


@dataclass(frozen=True)
class HasilCekKartu:
    """Satu baris hasil pengecekan UID terhadap kartu terdaftar - dipakai
    baik layar Uji Baca Kartu maupun langkah verifikasi wizard."""

    uid: str
    terdaftar: bool


class KartuTidakTerdaftarSaatVerifikasi(Exception):
    def __init__(self, uid_tidak_terdaftar: list[str]) -> None:
        self.uid_tidak_terdaftar = uid_tidak_terdaftar
        super().__init__(
            "Kartu berikut tidak terdaftar, wizard tidak bisa dilanjutkan: "
            + ", ".join(uid_tidak_terdaftar)
        )


def pastikan_verifikasi_kartu_cukup(hasil: list[HasilCekKartu]) -> None:
    """Gerbang murni sebelum wizard boleh commit ganti jalur (PRD §12.4:
    "verifikasi terhadap tiga kartu yang telah terdaftar"). Pemanggil
    (app.server.perangkat_service) yang mengisi ``terdaftar`` lewat
    RfidCardRepository.cari_by_uid - modul ini hanya menilai hasilnya,
    tidak pernah menyentuh basis data sendiri."""
    if len(hasil) != JUMLAH_KARTU_VERIFIKASI_WIZARD:
        raise JumlahKartuVerifikasiTidakCukup(len(hasil))
    if len({h.uid for h in hasil}) != JUMLAH_KARTU_VERIFIKASI_WIZARD:
        raise KartuVerifikasiTidakUnik()
    tidak_terdaftar = [h.uid for h in hasil if not h.terdaftar]
    if tidak_terdaftar:
        raise KartuTidakTerdaftarSaatVerifikasi(tidak_terdaftar)
