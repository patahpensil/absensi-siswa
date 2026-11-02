"""
Aturan koreksi massal: penyaringan tiga lapis (waktu, orang, status),
pengecualian bawaan untuk status manual, ambang snapshot dan konfirmasi
ketik, dan penerapan satu tindakan pada satu baris.

Fungsi murni - tidak menyentuh basis data, tidak mengimpor apa pun dari
app.data. Lihat app.core.koreksi_massal_service untuk penyambungannya ke
basis data, dan SKILL.md bagian "Koreksi massal" untuk aturan lengkapnya.
"""

from __future__ import annotations

from dataclasses import dataclass, field
from datetime import date
from enum import Enum

from app.core.status import PERAN_KOREKSI_MASSAL, STATUS_MANUAL_SISWA, AlasanFinal, Peran, StatusSiswa


class AksesKoreksiMassalDitolak(Exception):
    """Peran pemanggil bukan admin atau kepala sekolah (SKILL.md "Koreksi massal")."""

    def __init__(self, peran: Peran) -> None:
        self.peran = peran
        super().__init__(f"Peran {peran.value} tidak berwenang menjalankan koreksi massal")


def pastikan_berwenang(peran: Peran) -> None:
    if peran not in PERAN_KOREKSI_MASSAL:
        raise AksesKoreksiMassalDitolak(peran)


class LingkupOrang(str, Enum):
    """Lapis kedua penyaring - "Orang" di SKILL.md."""

    SEKOLAH = "SEKOLAH"
    TINGKAT = "TINGKAT"
    KELAS = "KELAS"
    SISWA_TERPILIH = "SISWA_TERPILIH"


class JenisTindakanMassal(str, Enum):
    """Tiga dari empat tindakan di SKILL.md - "batalkan tindakan massal
    sebelumnya" bukan tindakan atas baris attendance baru, itu operasi
    undo tersendiri (lihat batalkan() di koreksi_massal_service)."""

    UBAH_STATUS = "UBAH_STATUS"
    TANDAI_IZIN_PULANG_CEPAT = "TANDAI_IZIN_PULANG_CEPAT"
    BATALKAN_KONVERSI_ALPA_PULANG = "BATALKAN_KONVERSI_ALPA_PULANG"


@dataclass(frozen=True)
class PenyaringKoreksiMassal:
    """Tiga lapis penyaring yang bisa dikombinasikan.

    status_termasuk kosong (None, bawaan) berarti SEMUA KECUALI status
    manual (IZIN/SAKIT/DISPENSASI) - lihat status_efektif(). Ini yang
    membuat tindakan massal tidak diam-diam menimpa siswa yang sudah
    bersurat dokter kalau admin lupa menyaring status. Untuk benar-benar
    menyertakan status manual, sebutkan eksplisit di status_termasuk.
    """

    tanggal_mulai: date
    tanggal_selesai: date
    lingkup: LingkupOrang
    tingkat: int | None = None
    id_kelas: int | None = None
    id_siswa_terpilih: frozenset[int] = field(default_factory=frozenset)
    status_termasuk: frozenset[StatusSiswa] | None = None

    def status_efektif(self) -> frozenset[StatusSiswa]:
        if self.status_termasuk is not None:
            return self.status_termasuk
        return frozenset(s for s in StatusSiswa if s not in STATUS_MANUAL_SISWA)


@dataclass(frozen=True)
class Pratinjau:
    """Hasil pratinjau sebelum tindakan dijalankan - wajib ditampilkan
    dulu (SKILL.md: "Pratinjau wajib dengan jumlah baris terdampak")."""

    jumlah_baris: int
    perlu_snapshot: bool
    perlu_konfirmasi_ketik: bool


def buat_pratinjau(jumlah_baris: int, pengaturan) -> Pratinjau:
    """pengaturan: app.config.pengaturan.Pengaturan - tidak diketik eksplisit
    di sini supaya modul ini tidak perlu mengimpor app.config (yang pada
    gilirannya mengimpor app.data.repositories.setting); cukup duck-typing
    dua atribut: ambang_snapshot_sebelum_massal, ambang_konfirmasi_ketik.
    """
    return Pratinjau(
        jumlah_baris=jumlah_baris,
        perlu_snapshot=jumlah_baris > pengaturan.ambang_snapshot_sebelum_massal,
        perlu_konfirmasi_ketik=jumlah_baris > pengaturan.ambang_konfirmasi_ketik,
    )


@dataclass(frozen=True)
class PerubahanBaris:
    """Perubahan satu baris attendance akibat satu tindakan massal.

    nilai_sebelum/nilai_sesudah hanya berisi kolom yang benar-benar
    disentuh - dipakai penyambung DB untuk menulis TindakanMassalBaris
    (bahan "batalkan tindakan") dan menerapkan perubahannya.
    """

    id_absensi: int
    nilai_sebelum: dict[str, str | bool | None]
    nilai_sesudah: dict[str, str | bool | None]


def terapkan_tindakan(
    *,
    id_absensi: int,
    status_masuk: StatusSiswa | None,
    status_final: StatusSiswa | None,
    alasan_final: AlasanFinal | None,
    izin_pulang_cepat: bool,
    jenis_tindakan: JenisTindakanMassal,
    status_baru: StatusSiswa | None = None,
) -> PerubahanBaris | None:
    """Terapkan satu tindakan pada bentuk atribut satu baris (bukan ORM).

    Mengembalikan None kalau tindakan ini tidak berlaku atau tidak
    mengubah apa pun untuk baris ini (mis. BATALKAN_KONVERSI_ALPA_PULANG
    pada baris yang bukan hasil konversi ALPA pulang, atau UBAH_STATUS ke
    status yang sama) - baris itu dilewati diam-diam, bukan error, supaya
    satu baris yang tidak relevan tidak menggagalkan seluruh tindakan.
    """
    if jenis_tindakan == JenisTindakanMassal.UBAH_STATUS:
        if status_baru is None:
            raise ValueError("UBAH_STATUS memerlukan status_baru")
        if status_final == status_baru:
            return None
        return PerubahanBaris(
            id_absensi=id_absensi,
            nilai_sebelum={"status_final": _nilai(status_final), "alasan_final": _nilai(alasan_final)},
            nilai_sesudah={"status_final": status_baru.value, "alasan_final": AlasanFinal.KOREKSI_MASSAL.value},
        )

    if jenis_tindakan == JenisTindakanMassal.TANDAI_IZIN_PULANG_CEPAT:
        if izin_pulang_cepat:
            return None
        return PerubahanBaris(
            id_absensi=id_absensi,
            nilai_sebelum={"izin_pulang_cepat": izin_pulang_cepat},
            nilai_sesudah={"izin_pulang_cepat": True},
        )

    if jenis_tindakan == JenisTindakanMassal.BATALKAN_KONVERSI_ALPA_PULANG:
        if alasan_final != AlasanFinal.TIDAK_SCAN_PULANG:
            return None
        return PerubahanBaris(
            id_absensi=id_absensi,
            nilai_sebelum={"status_final": _nilai(status_final), "alasan_final": _nilai(alasan_final)},
            nilai_sesudah={"status_final": _nilai(status_masuk), "alasan_final": None},
        )

    raise ValueError(f"Jenis tindakan tidak dikenal: {jenis_tindakan}")


def _nilai(status: StatusSiswa | AlasanFinal | None) -> str | None:
    return status.value if status is not None else None
