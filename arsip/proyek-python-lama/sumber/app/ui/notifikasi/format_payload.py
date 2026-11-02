"""Penyusunan payload untuk layar Notifikasi - murni, tanpa Qt (pola sama
dengan app.ui.koreksi_massal.validasi_form).
"""

from __future__ import annotations


def payload_mode_uji(*, aktif: bool, nomor_input: str, nomor_tersimpan: str, alasan: str) -> dict:
    """Payload PUT /notifikasi/mode-uji.

    ``nomor_admin`` HANYA disertakan kalau isian nomor benar-benar
    berbeda dari nomor yang terakhir dimuat dari server - toggle
    aktif/nonaktif saja (tanpa mengubah nomor) tidak boleh ikut menulis
    ulang nomor dan membuat entri audit log kosong (nilai_sebelum ==
    nilai_sesudah).
    """
    payload: dict = {"aktif": aktif, "alasan": alasan}
    nomor = nomor_input.strip()
    if nomor and nomor != nomor_tersimpan:
        payload["nomor_admin"] = nomor
    return payload
