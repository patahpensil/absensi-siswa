"""
Status grid N-jalur untuk layar absensi lobi - state MURNI (tanpa Qt), supaya
aturan "kartu hilang otomatis setelah durasi tampil, tapi penanda jalur
gagal tidak" bisa diuji tanpa QApplication.

Data-driven untuk N jalur: lobi nyata = 4 lajur (SKILL.md "Titik scan"),
slice ini baru 1 lajur SIMULATOR - render (app.ui.lobi.layar) mengikuti
jumlah entri di sini, bukan angka tetap.
"""

from __future__ import annotations

from dataclasses import dataclass, field
from datetime import datetime

from app.ui.lobi.tampilan import KartuTampilan

#: PRD §12.5: kartu sukses tampil ~1,5 detik lalu kembali senyap/idle.
DURASI_TAMPIL_DETIK = 1.5


@dataclass
class StatusJalur:
    """Satu jalur/lajur di grid - hasil scan terakhirnya dan status penanda."""

    jalur_id: str
    tampilan: KartuTampilan | None = None
    waktu_tampil: datetime | None = None
    gagal_ditandai: bool = False


@dataclass
class StatusGridLobi:
    """Seluruh grid - dict jalur_id -> StatusJalur, urutan penyisipan = urutan tampil."""

    jalur: dict[str, StatusJalur] = field(default_factory=dict)

    def daftarkan_jalur(self, jalur_id: str) -> None:
        self.jalur.setdefault(jalur_id, StatusJalur(jalur_id=jalur_id))

    def perbarui(self, jalur_id: str, tampilan: KartuTampilan, *, waktu: datetime) -> None:
        status = self.jalur.setdefault(jalur_id, StatusJalur(jalur_id=jalur_id))
        status.tampilan = tampilan
        status.waktu_tampil = waktu
        status.gagal_ditandai = tampilan.tandai_jalur_gagal

    def bersihkan_kedaluwarsa(
        self, *, sekarang: datetime, durasi_detik: float = DURASI_TAMPIL_DETIK
    ) -> list[str]:
        """Kembalikan kartu SUKSES ke idle setelah durasi tampil lewat.
        Mengembalikan daftar jalur_id yang berubah, supaya pemanggil (Qt
        timer di app.ui.lobi.layar) tahu slot mana yang perlu digambar ulang.

        Penanda jalur gagal (gagal_ditandai) SENGAJA tidak disentuh timer
        ini - dia baru hilang saat jalur itu berhasil scan lagi (lihat
        perbarui()), supaya "jalur tidak merespon" tetap terlihat sampai
        benar-benar diperbaiki, bukan menghilang sendiri seperti kartu biasa.
        """
        berubah: list[str] = []
        for jalur_id, status in self.jalur.items():
            if status.tampilan is None or status.gagal_ditandai:
                continue
            if status.waktu_tampil is None:
                continue
            if (sekarang - status.waktu_tampil).total_seconds() >= durasi_detik:
                status.tampilan = None
                status.waktu_tampil = None
                berubah.append(jalur_id)
        return berubah
