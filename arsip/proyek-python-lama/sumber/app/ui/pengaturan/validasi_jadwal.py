"""Validasi form "Tambah Jadwal" - murni, tanpa Qt. Hanya pemeriksaan
KEWAJARAN URUTAN JAM (mis. batas terlambat tidak boleh sebelum jam
masuk) - bukan aturan klasifikasi absensi, yang tetap sepenuhnya
tanggung jawab app.core.mesin_absensi/app.core.engine (tidak disentuh).
"""

from __future__ import annotations

from dataclasses import dataclass, field
from datetime import time


@dataclass
class HasilValidasiJadwal:
    galat: dict[str, str] = field(default_factory=dict)

    @property
    def valid(self) -> bool:
        return not self.galat


def validasi_jadwal(
    *,
    jenis_pemilik: str,
    jam_masuk: time,
    batas_terlambat: time,
    jam_pulang: time,
    cutoff_alpa: time | None = None,
    pulang_mulai: time | None = None,
) -> HasilValidasiJadwal:
    galat: dict[str, str] = {}

    if batas_terlambat < jam_masuk:
        galat["batas_terlambat"] = "Batas terlambat tidak boleh sebelum jam masuk"

    if jenis_pemilik == "SISWA":
        if cutoff_alpa is not None and cutoff_alpa < batas_terlambat:
            galat["cutoff_alpa"] = "Penetapan ALPA tidak boleh sebelum batas terlambat"
        if pulang_mulai is not None and pulang_mulai > jam_pulang:
            galat["pulang_mulai"] = "Awal jendela pulang tidak boleh setelah jam pulang"

    return HasilValidasiJadwal(galat=galat)
