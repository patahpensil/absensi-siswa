"""
Lapisan pembacaan kartu RFID.

Aplikasi tidak pernah tahu jenis alat apa yang terpasang. Yang dilihatnya hanya
ScanEvent. Mau ACR122U, reader serial murah, atau simulator keyboard, mesin
absensi tidak berubah satu baris pun.

Empat pengemudi:
    PcscReader     - jalur PC/SC (pyscard)
    SerialReader   - jalur USB serial (pyserial)
    HidReader      - mode keyboard, HANYA untuk satu unit
    SimulatorReader- pengembangan dan pelatihan, tanpa hardware

Batas fisik yang tidak bisa diselesaikan kode: satu reader hanya membaca
frekuensi yang didukung antenanya. Untuk mendukung semua jenis kartu, alat
yang dibeli harus dwi-frekuensi (125 kHz + 13.56 MHz).
"""

from __future__ import annotations

from dataclasses import dataclass
from datetime import datetime
from enum import Enum
from typing import Callable, Protocol


class JalurAlat(str, Enum):
    PCSC = "PCSC"
    SERIAL = "SERIAL"
    HID = "HID"
    SIMULATOR = "SIMULATOR"


class FormatUid(str, Enum):
    HEX = "HEX"
    DESIMAL = "DESIMAL"


@dataclass(frozen=True)
class ScanEvent:
    """Bentuk seragam hasil bacaan, apa pun alatnya.

    ``uid_mentah`` disimpan apa adanya untuk diagnosis. Ini yang menyelamatkan
    ketika reader diganti merek dan formatnya ternyata berbeda.
    """

    uid: str
    uid_mentah: str
    station_id: str
    waktu: datetime
    jalur: JalurAlat


class PembacaKartu(Protocol):
    """Antarmuka yang wajib dipenuhi setiap pengemudi."""

    jalur: JalurAlat
    station_id: str

    def mulai(self) -> None: ...
    def berhenti(self) -> None: ...
    def saat_scan(self, callback: Callable[[ScanEvent], None]) -> None: ...
    def terhubung(self) -> bool: ...


# ---------------------------------------------------------------- normalisasi


def normalisasi_uid(
    mentah: str,
    format_uid: FormatUid = FormatUid.HEX,
    balik_urutan_byte: bool = False,
) -> str:
    """Ubah keluaran alat menjadi bentuk baku: hex huruf besar.

    Ini lapisan yang membuat kartu 4 byte, 7 byte, maupun 10 digit sama-sama
    diterima, dan membuat perbedaan format antar merek bisa dikoreksi dari
    menu pengaturan tanpa menyentuh kode.

    Jebakan yang ditangani di sini: dari kartu yang sama, satu merek mengirim
    "04A2B3C4", merek lain "3283994628", merek ketiga membalik urutan byte
    menjadi "C4B3A204".
    """
    bersih = mentah.strip().replace(" ", "").replace(":", "").replace("-", "")
    if not bersih:
        return ""

    if format_uid is FormatUid.DESIMAL:
        try:
            bersih = format(int(bersih), "X")
        except ValueError:
            return bersih.upper()

    bersih = bersih.upper()
    if len(bersih) % 2:
        bersih = "0" + bersih

    if balik_urutan_byte:
        byte = [bersih[i : i + 2] for i in range(0, len(bersih), 2)]
        bersih = "".join(reversed(byte))

    return bersih


# ---------------------------------------------------------------- kunci jalur


class AlatTidakSesuaiJalur(Exception):
    """Alat dari jalur lain dipasang pada sistem yang sudah dikunci."""

    def __init__(self, station_id: str, jalur_alat: JalurAlat, jalur_aktif: str):
        self.station_id = station_id
        self.jalur_alat = jalur_alat
        self.jalur_aktif = jalur_aktif
        super().__init__(
            f"Jalur {station_id} tidak aktif. Alat bertipe {jalur_alat.value}, "
            f"sedangkan sistem dikunci pada tipe {jalur_aktif}."
        )


class TerlaluBanyakHid(Exception):
    """Mode HID dipasang di lebih dari satu jalur.

    Tidak ada cara teknis membedakan beberapa keyboard yang mengetik ke jendela
    yang sama, jadi konfigurasi ini ditolak, bukan sekadar diperingatkan.
    """


def periksa_kesesuaian_jalur(
    pembaca: list[PembacaKartu],
    jalur_aktif: str,
    izin_darurat_station: set[str] | None = None,
) -> list[AlatTidakSesuaiJalur]:
    """Periksa seluruh alat terhadap jalur yang dikunci.

    Mengembalikan daftar pelanggaran, bukan melempar galat, supaya satu alat
    yang salah tidak mematikan seluruh sistem — jalur lain tetap berjalan.

    ``izin_darurat_station`` berisi station yang sedang memakai izin darurat
    1x24 jam dari kepala sekolah, admin, atau wali kelas.
    """
    izin = izin_darurat_station or set()
    hid = [p for p in pembaca if p.jalur is JalurAlat.HID]
    if len(hid) > 1:
        raise TerlaluBanyakHid(
            f"{len(hid)} alat mode HID terpasang. Mode HID hanya boleh untuk satu jalur."
        )

    pelanggaran = []
    for p in pembaca:
        if p.jalur is JalurAlat.SIMULATOR:
            continue
        if p.station_id in izin:
            continue
        if p.jalur.value != jalur_aktif:
            pelanggaran.append(
                AlatTidakSesuaiJalur(p.station_id, p.jalur, jalur_aktif)
            )
    return pelanggaran


def pesan_jalur_diblokir(pelanggaran: AlatTidakSesuaiJalur) -> str:
    """Teks yang tampil di layar lobi. Sengaja menyebut tipe alat yang benar
    supaya pembelian pengganti tidak salah lagi."""
    return (
        f"JALUR {pelanggaran.station_id} TIDAK AKTIF\n"
        f"Alat yang terpasang bertipe {pelanggaran.jalur_alat.value}, "
        f"sedangkan sistem ini dikunci pada tipe {pelanggaran.jalur_aktif}.\n"
        f"Absensi tetap berjalan di jalur lain.\n"
        f"Hubungi admin. Alat pengganti harus bertipe {pelanggaran.jalur_aktif}."
    )
