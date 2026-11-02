"""Aturan koreksi satuan (PRD §9.1): koreksi status_final SATU baris
attendance siswa, dengan alasan wajib dan jejak nilai lama/baru. Jalur
yang sama dipakai untuk input manual IZIN/SAKIT/DISPENSASI (PRD §10),
baik pada baris yang sudah ada maupun yang belum pernah tercatat sama
sekali (siswa yang belum sempat scan).

Fungsi murni - tidak menyentuh basis data. Mirror
app.core.koreksi_massal PERSIS untuk kolom yang disentuh: status_final +
alasan_final, BUKAN status_masuk - "kenyataan" hasil scan tidak pernah
diubah (SKILL.md, "Admin boleh mengubah aturan, tidak boleh mengubah
kenyataan"). AlasanFinal.KOREKSI_MANUAL sudah disediakan sejak awal
persis untuk kasus ini (app.core.status), belum pernah dipakai sebelum
slice ini.

Cakupan slice ini SISWA SAJA - koreksi satuan guru (DINAS_LUAR/IZIN/
SAKIT/CUTI) di luar cakupan, tidak diminta task ini (PRD §10 "Koreksi
satuan kelas sendiri" hanya bermakna untuk siswa - wali kelas mengampu
kelas, bukan guru).
"""

from __future__ import annotations

from dataclasses import dataclass

from app.core.status import PERAN_KOREKSI_SATUAN, AlasanFinal, Peran, StatusSiswa


class AksesKoreksiSatuanDitolak(Exception):
    """Peran pemanggil tidak berwenang koreksi satuan (operator - SKILL.md
    "Peran dan hak akses": koreksi satuan hanya kepsek/admin/wali kelas)."""

    def __init__(self, peran: Peran) -> None:
        self.peran = peran
        super().__init__(f"Peran {peran.value} tidak berwenang menjalankan koreksi satuan")


def pastikan_berwenang(peran: Peran) -> None:
    if peran not in PERAN_KOREKSI_SATUAN:
        raise AksesKoreksiSatuanDitolak(peran)


@dataclass(frozen=True)
class PerubahanKoreksiSatuan:
    """Nilai sebelum/sesudah SATU koreksi - dipakai penyambung DB untuk
    menulis KoreksiSatuan (bahan "batalkan koreksi") dan menerapkan
    perubahannya lewat AttendanceRepository.koreksi()."""

    nilai_sebelum: dict[str, str | None]
    nilai_sesudah: dict[str, str | None]


def terapkan_koreksi_satuan(
    *,
    status_final_sekarang: StatusSiswa | None,
    alasan_final_sekarang: AlasanFinal | None,
    status_baru: StatusSiswa,
) -> PerubahanKoreksiSatuan | None:
    """Mengembalikan None kalau status_baru sama dengan status_final
    sekarang - tidak ada perubahan sungguhan, pemanggil menolaknya
    sebagai galat (lihat app.server.koreksi_satuan_service) alih-alih
    diam-diam mencatat koreksi kosong."""
    if status_final_sekarang == status_baru:
        return None
    return PerubahanKoreksiSatuan(
        nilai_sebelum={
            "status_final": status_final_sekarang.value if status_final_sekarang is not None else None,
            "alasan_final": alasan_final_sekarang.value if alasan_final_sekarang is not None else None,
        },
        nilai_sesudah={"status_final": status_baru.value, "alasan_final": AlasanFinal.KOREKSI_MANUAL.value},
    )
