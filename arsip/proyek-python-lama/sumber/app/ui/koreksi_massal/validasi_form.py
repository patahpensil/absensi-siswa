"""Validasi form "Jalankan Tindakan Massal" dan penyusunan penyaring -
murni, tanpa Qt (pola sama dengan app.ui.kalender.validasi_form).
"""

from __future__ import annotations

from dataclasses import dataclass, field
from datetime import date


@dataclass
class HasilValidasiPenyaring:
    galat: dict[str, str] = field(default_factory=dict)

    @property
    def valid(self) -> bool:
        return not self.galat


def parse_id_siswa_terpilih(teks: str) -> list[int] | None:
    """Ubah "3, 5,9" jadi [3, 5, 9]. None kalau ada token yang bukan
    angka atau teksnya kosong."""
    token = [t.strip() for t in teks.split(",") if t.strip()]
    if not token:
        return None
    if not all(t.isdigit() for t in token):
        return None
    return [int(t) for t in token]


def validasi_penyaring(
    *,
    tanggal_mulai: date,
    tanggal_selesai: date,
    lingkup: str,
    tingkat_teks: str,
    id_kelas_teks: str,
    id_siswa_terpilih_teks: str,
    status_termasuk: list[str],
) -> HasilValidasiPenyaring:
    galat: dict[str, str] = {}
    if tanggal_selesai < tanggal_mulai:
        galat["tanggal"] = "Tanggal selesai tidak boleh sebelum tanggal mulai"

    if lingkup == "TINGKAT":
        if not tingkat_teks.strip().isdigit():
            galat["tingkat"] = "Isi tingkat dengan angka (mis. 7, 8, 9)"
    elif lingkup == "KELAS":
        if not id_kelas_teks.strip().isdigit():
            galat["id_kelas"] = "Isi ID Kelas dengan angka"
    elif lingkup == "SISWA_TERPILIH":
        if parse_id_siswa_terpilih(id_siswa_terpilih_teks) is None:
            galat["id_siswa_terpilih"] = "Isi ID Siswa dipisah koma (mis. 3, 5, 9)"

    if not status_termasuk:
        galat["status_termasuk"] = "Pilih minimal satu status"

    return HasilValidasiPenyaring(galat=galat)


def bangun_penyaring(
    *,
    tanggal_mulai: date,
    tanggal_selesai: date,
    lingkup: str,
    tingkat_teks: str,
    id_kelas_teks: str,
    id_siswa_terpilih_teks: str,
    status_termasuk: list[str],
) -> dict:
    """Panggil hanya setelah validasi_penyaring() menyatakan valid."""
    return {
        "tanggal_mulai": tanggal_mulai.isoformat(),
        "tanggal_selesai": tanggal_selesai.isoformat(),
        "lingkup": lingkup,
        "tingkat": int(tingkat_teks.strip()) if lingkup == "TINGKAT" else None,
        "id_kelas": int(id_kelas_teks.strip()) if lingkup == "KELAS" else None,
        "id_siswa_terpilih": parse_id_siswa_terpilih(id_siswa_terpilih_teks) if lingkup == "SISWA_TERPILIH" else [],
        "status_termasuk": status_termasuk,
    }
