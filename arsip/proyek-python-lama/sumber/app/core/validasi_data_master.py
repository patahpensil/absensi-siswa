"""Validasi dan normalisasi data master (PRD §11) - murni, tanpa basis
data maupun Qt. Diuji lewat tests/core/test_validasi_data_master.py
(Aturan Global #11: lapisan logika diuji murni).

Keunikan (NISN/NIK tidak boleh kembar) TIDAK ditegakkan di sini - itu
tanggung jawab lapisan data (indeks unik) dan lapisan server (pengecekan
sebelum tulis, supaya responsnya rapi). Modul ini hanya memeriksa BENTUK
nilainya.
"""

from __future__ import annotations

import re


class NisnTidakValid(ValueError):
    """PRD §11.1: NISN wajib, tepat 10 digit angka."""


class NikTidakValid(ValueError):
    """PRD §11.1: NIK, bila diisi, tepat 16 digit angka."""


class NomorWhatsappTidakValid(ValueError):
    """PRD §13.3: nomor disimpan dalam format baku, dirapikan otomatis saat input."""


_POLA_NISN = re.compile(r"\d{10}")
_POLA_NIK = re.compile(r"\d{16}")
_BUKAN_DIGIT = re.compile(r"\D")


def validasi_nisn(nisn: str) -> None:
    if not _POLA_NISN.fullmatch(nisn):
        raise NisnTidakValid("NISN harus tepat 10 digit angka")


def validasi_nik(nik: str) -> None:
    if not _POLA_NIK.fullmatch(nik):
        raise NikTidakValid("NIK harus tepat 16 digit angka")


def normalisasi_nomor_whatsapp(nomor: str) -> str:
    """Ubah variasi lazim nomor Indonesia menjadi format baku +628xxxxxxxxxx.

    Menerima awalan 0, 8, 62, atau +62. Nomor yang panjangnya di luar
    rentang wajar (kurang dari 9 digit setelah kode negara, atau lebih
    dari 13) ditolak - menebak salah lebih berbahaya daripada meminta
    pengguna memperbaikinya sendiri.
    """
    digit = _BUKAN_DIGIT.sub("", nomor)
    if digit.startswith("62"):
        pass
    elif digit.startswith("0"):
        digit = "62" + digit[1:]
    elif digit.startswith("8"):
        digit = "62" + digit
    else:
        raise NomorWhatsappTidakValid(f"Nomor telepon/WA tidak valid: {nomor!r}")

    ekor = digit[2:]
    if not ekor.startswith("8") or not (9 <= len(ekor) <= 13):
        # WhatsApp mensyaratkan nomor seluler (diawali 8 setelah kode
        # negara) - nomor rumah/kantor (mis. 021...) ditolak di sini,
        # bukan ditebak jadi seluler.
        raise NomorWhatsappTidakValid(f"Nomor telepon/WA tidak valid: {nomor!r}")
    return "+" + digit


def samarkan_nik(nik: str | None) -> str | None:
    """PRD §11.1: NIK "ditampilkan tersamar pada layar biasa, ditampilkan
    utuh hanya ketika baris sedang diedit". Dipakai di layar daftar/tabel;
    layar edit satu baris memakai nilai NIK asli."""
    if nik is None:
        return None
    return "*" * (len(nik) - 4) + nik[-4:]
