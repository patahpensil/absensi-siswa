"""Aturan koreksi satuan guru (PRD §6): input status manual DINAS_LUAR/
IZIN/SAKIT/CUTI, dengan alasan wajib dan jejak nilai lama/baru. Sengaja
MODUL TERPISAH dari app.core.koreksi_satuan (siswa) - pola yang sama
dengan app.core.koreksi_satuan sendiri dibuat terpisah dari
app.core.koreksi_massal, bukan generalisasi satu fungsi untuk dua
kosakata status (StatusSiswa vs StatusGuru).

Fungsi murni - tidak menyentuh basis data, tidak menyentuh
app.core.engine/mesin_absensi. Guru tidak mengenal ALPA dan toleransi
09:30-nya tetap sepenuhnya berasal dari tentukan_status_masuk_guru() -
modul ini hanya menerapkan status yang SUDAH DIPILIH manusia ke
status_final, sama sekali tidak menghitung status dari scan.

Beda dari koreksi satuan siswa: target koreksi DIBATASI ke
STATUS_MANUAL_GURU saja (DINAS_LUAR/IZIN/SAKIT/CUTI) - PRD §6 memberi
admin/kepsek kewenangan menginput EMPAT status itu, bukan kewenangan
bebas mengoreksi ke status apa pun (siswa punya kewenangan lebih luas,
lihat app.core.koreksi_satuan). Mengoreksi ke HADIR/TERLAMBAT ditolak -
dua status itu murni hasil scan+toleransi, tidak pernah "diinput".
"""

from __future__ import annotations

from dataclasses import dataclass

from app.core.status import PERAN_KOREKSI_GURU, STATUS_MANUAL_GURU, AlasanFinal, Peran, StatusGuru


class AksesKoreksiGuruDitolak(Exception):
    """Peran pemanggil bukan admin atau kepala sekolah (SKILL.md "Absensi
    guru": status manual guru diinput admin/kepsek)."""

    def __init__(self, peran: Peran) -> None:
        self.peran = peran
        super().__init__(f"Peran {peran.value} tidak berwenang menginput status manual guru")


def pastikan_berwenang(peran: Peran) -> None:
    if peran not in PERAN_KOREKSI_GURU:
        raise AksesKoreksiGuruDitolak(peran)


class StatusBukanManual(Exception):
    """status_baru bukan salah satu dari DINAS_LUAR/IZIN/SAKIT/CUTI -
    HADIR/TERLAMBAT hanya boleh dihasilkan scan+toleransi (PRD §6)."""

    def __init__(self, status_baru: StatusGuru) -> None:
        self.status_baru = status_baru
        super().__init__(
            f"{status_baru.value} bukan status manual - hanya "
            f"{', '.join(sorted(s.value for s in STATUS_MANUAL_GURU))} yang boleh diinput"
        )


def pastikan_status_manual(status_baru: StatusGuru) -> None:
    if status_baru not in STATUS_MANUAL_GURU:
        raise StatusBukanManual(status_baru)


@dataclass(frozen=True)
class PerubahanKoreksiGuru:
    """Nilai sebelum/sesudah SATU koreksi - dipakai penyambung DB untuk
    menulis KoreksiGuru (bahan "batalkan koreksi") dan menerapkan
    perubahannya lewat AttendanceRepository.koreksi()."""

    nilai_sebelum: dict[str, str | None]
    nilai_sesudah: dict[str, str | None]


def terapkan_koreksi_guru(
    *,
    status_final_sekarang: StatusGuru | None,
    alasan_final_sekarang: AlasanFinal | None,
    status_baru: StatusGuru,
) -> PerubahanKoreksiGuru | None:
    """Panggil hanya setelah pastikan_status_manual(status_baru) lolos.

    Mengembalikan None kalau status_baru sama dengan status_final
    sekarang - tidak ada perubahan sungguhan, pemanggil menolaknya
    sebagai galat (lihat app.server.koreksi_guru_service) alih-alih
    diam-diam mencatat koreksi kosong."""
    if status_final_sekarang == status_baru:
        return None
    return PerubahanKoreksiGuru(
        nilai_sebelum={
            "status_final": status_final_sekarang.value if status_final_sekarang is not None else None,
            "alasan_final": alasan_final_sekarang.value if alasan_final_sekarang is not None else None,
        },
        nilai_sesudah={"status_final": status_baru.value, "alasan_final": AlasanFinal.KOREKSI_MANUAL.value},
    )
