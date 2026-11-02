"""Tes murni app.core.validasi_data_master - tanpa basis data, tanpa Qt."""

import pytest

from app.core.validasi_data_master import (
    NikTidakValid,
    NisnTidakValid,
    NomorWhatsappTidakValid,
    normalisasi_nomor_whatsapp,
    samarkan_nik,
    validasi_nik,
    validasi_nisn,
)


def test_nisn_10_digit_lolos():
    validasi_nisn("1234567890")


@pytest.mark.parametrize("nilai", ["123456789", "12345678901", "123456789a", ""])
def test_nisn_selain_10_digit_ditolak(nilai):
    with pytest.raises(NisnTidakValid):
        validasi_nisn(nilai)


def test_nik_16_digit_lolos():
    validasi_nik("1234567890123456")


@pytest.mark.parametrize("nilai", ["123456789012345", "12345678901234567", "abcd567890123456"])
def test_nik_selain_16_digit_ditolak(nilai):
    with pytest.raises(NikTidakValid):
        validasi_nik(nilai)


@pytest.mark.parametrize(
    "masukan,harapan",
    [
        ("081234567890", "+6281234567890"),
        ("81234567890", "+6281234567890"),
        ("6281234567890", "+6281234567890"),
        ("+6281234567890", "+6281234567890"),
        ("0812-3456-7890", "+6281234567890"),
        ("0812 3456 7890", "+6281234567890"),
    ],
)
def test_normalisasi_nomor_whatsapp_variasi_lazim(masukan, harapan):
    assert normalisasi_nomor_whatsapp(masukan) == harapan


@pytest.mark.parametrize("nilai", ["123", "021-5551234", "abcdefghij", "0812345"])
def test_normalisasi_nomor_whatsapp_ditolak(nilai):
    with pytest.raises(NomorWhatsappTidakValid):
        normalisasi_nomor_whatsapp(nilai)


def test_samarkan_nik_menyisakan_4_digit_terakhir():
    assert samarkan_nik("1234567890123456") == "************3456"


def test_samarkan_nik_none_tetap_none():
    assert samarkan_nik(None) is None
