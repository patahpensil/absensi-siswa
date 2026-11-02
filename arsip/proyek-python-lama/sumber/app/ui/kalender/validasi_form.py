"""Validasi form "Tambah Entri Kalender" - murni, tanpa Qt."""

from __future__ import annotations

from dataclasses import dataclass, field
from datetime import date


@dataclass
class HasilValidasiEntri:
    galat: dict[str, str] = field(default_factory=dict)

    @property
    def valid(self) -> bool:
        return not self.galat


def validasi_entri(*, mulai: date, selesai: date, keterangan: str, tahun_ajaran: str) -> HasilValidasiEntri:
    galat: dict[str, str] = {}
    if selesai < mulai:
        galat["selesai"] = "Tanggal selesai tidak boleh sebelum tanggal mulai"
    if not keterangan.strip():
        galat["keterangan"] = "Wajib diisi"
    if not tahun_ajaran.strip():
        galat["tahun_ajaran"] = "Wajib diisi (mis. 2026/2027)"
    return HasilValidasiEntri(galat=galat)


def validasi_salin_tahun(*, tahun_asal: str, tahun_tujuan: str) -> HasilValidasiEntri:
    galat: dict[str, str] = {}
    if not tahun_asal.strip():
        galat["tahun_asal"] = "Wajib dipilih"
    if not tahun_tujuan.strip():
        galat["tahun_tujuan"] = "Wajib diisi (mis. 2027/2028)"
    elif tahun_asal.strip() == tahun_tujuan.strip():
        galat["tahun_tujuan"] = "Tahun tujuan harus berbeda dari tahun asal"
    return HasilValidasiEntri(galat=galat)
