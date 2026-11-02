"""Tes murni app.core.perangkat (PRD §12.4) - izin darurat alat dan
verifikasi wizard ganti jalur. Tidak menyentuh basis data, tidak
menyentuh app.core.engine/mesin_absensi."""

from datetime import datetime

import pytest

from app.core.perangkat import (
    AksesGantiJalurDitolak,
    AksesIzinDaruratDitolak,
    AksesPerpanjangIzinDaruratDitolak,
    DurasiIzinMelebihiBatas,
    HasilCekKartu,
    JumlahKartuVerifikasiTidakCukup,
    KartuTidakTerdaftarSaatVerifikasi,
    KartuVerifikasiTidakUnik,
    hitung_waktu_berakhir,
    pastikan_berwenang_ganti_jalur,
    pastikan_berwenang_memberi_izin,
    pastikan_berwenang_memperpanjang,
    pastikan_verifikasi_kartu_cukup,
)
from app.core.status import Peran

MULAI = datetime(2026, 9, 11, 8, 0, 0)


# --------------------------------------------------------------- kewenangan izin darurat


@pytest.mark.parametrize("peran", [Peran.KEPALA_SEKOLAH, Peran.ADMIN, Peran.WALI_KELAS])
def test_peran_berwenang_memberi_izin_darurat(peran):
    pastikan_berwenang_memberi_izin(peran)  # tidak melempar


def test_operator_tidak_berwenang_memberi_izin_darurat():
    with pytest.raises(AksesIzinDaruratDitolak):
        pastikan_berwenang_memberi_izin(Peran.OPERATOR)


@pytest.mark.parametrize("peran", [Peran.KEPALA_SEKOLAH, Peran.ADMIN])
def test_peran_berwenang_memperpanjang(peran):
    pastikan_berwenang_memperpanjang(peran)  # tidak melempar


def test_wali_kelas_tidak_berwenang_memperpanjang():
    """Beda dari pemberian awal - wali kelas boleh MEMBERI, tidak boleh
    MEMPERPANJANG (SKILL.md 'Kunci jalur alat')."""
    with pytest.raises(AksesPerpanjangIzinDaruratDitolak):
        pastikan_berwenang_memperpanjang(Peran.WALI_KELAS)


# --------------------------------------------------------------- durasi izin darurat


def test_durasi_dalam_batas_dihitung_benar():
    berakhir = hitung_waktu_berakhir(waktu_mulai=MULAI, jam_diminta=24, jam_maks=24)
    assert berakhir.isoformat() == "2026-09-12T08:00:00"


def test_durasi_melebihi_batas_ditolak():
    with pytest.raises(DurasiIzinMelebihiBatas):
        hitung_waktu_berakhir(waktu_mulai=MULAI, jam_diminta=25, jam_maks=24)


def test_durasi_nol_ditolak():
    with pytest.raises(ValueError):
        hitung_waktu_berakhir(waktu_mulai=MULAI, jam_diminta=0, jam_maks=24)


def test_durasi_negatif_ditolak():
    with pytest.raises(ValueError):
        hitung_waktu_berakhir(waktu_mulai=MULAI, jam_diminta=-1, jam_maks=24)


def test_ambang_maks_bisa_disesuaikan_pengaturan():
    """jam_maks bukan angka tetap 24 di modul ini - dibaca dari
    Pengaturan.maks_jam_izin_darurat_alat() oleh pemanggil, modul ini
    murni menghormati parameter yang diberikan."""
    berakhir = hitung_waktu_berakhir(waktu_mulai=MULAI, jam_diminta=10, jam_maks=12)
    assert berakhir.isoformat() == "2026-09-11T18:00:00"


# --------------------------------------------------------------- wizard ganti jalur - kewenangan


@pytest.mark.parametrize("peran", [Peran.KEPALA_SEKOLAH, Peran.ADMIN])
def test_peran_berwenang_ganti_jalur(peran):
    pastikan_berwenang_ganti_jalur(peran)  # tidak melempar


@pytest.mark.parametrize("peran", [Peran.WALI_KELAS, Peran.OPERATOR])
def test_peran_lain_tidak_berwenang_ganti_jalur(peran):
    with pytest.raises(AksesGantiJalurDitolak):
        pastikan_berwenang_ganti_jalur(peran)


# --------------------------------------------------------------- wizard ganti jalur - verifikasi kartu


def test_verifikasi_tiga_kartu_terdaftar_lolos():
    hasil = [
        HasilCekKartu(uid="A1", terdaftar=True),
        HasilCekKartu(uid="A2", terdaftar=True),
        HasilCekKartu(uid="A3", terdaftar=True),
    ]
    pastikan_verifikasi_kartu_cukup(hasil)  # tidak melempar


def test_verifikasi_kurang_dari_tiga_kartu_ditolak():
    hasil = [HasilCekKartu(uid="A1", terdaftar=True), HasilCekKartu(uid="A2", terdaftar=True)]
    with pytest.raises(JumlahKartuVerifikasiTidakCukup):
        pastikan_verifikasi_kartu_cukup(hasil)


def test_verifikasi_lebih_dari_tiga_kartu_ditolak():
    hasil = [HasilCekKartu(uid=f"A{i}", terdaftar=True) for i in range(4)]
    with pytest.raises(JumlahKartuVerifikasiTidakCukup):
        pastikan_verifikasi_kartu_cukup(hasil)


def test_verifikasi_uid_duplikat_ditolak():
    hasil = [
        HasilCekKartu(uid="A1", terdaftar=True),
        HasilCekKartu(uid="A1", terdaftar=True),
        HasilCekKartu(uid="A3", terdaftar=True),
    ]
    with pytest.raises(KartuVerifikasiTidakUnik):
        pastikan_verifikasi_kartu_cukup(hasil)


def test_verifikasi_satu_kartu_tidak_terdaftar_ditolak_dan_disebutkan():
    hasil = [
        HasilCekKartu(uid="A1", terdaftar=True),
        HasilCekKartu(uid="A2", terdaftar=False),
        HasilCekKartu(uid="A3", terdaftar=True),
    ]
    with pytest.raises(KartuTidakTerdaftarSaatVerifikasi) as exc_info:
        pastikan_verifikasi_kartu_cukup(hasil)
    assert exc_info.value.uid_tidak_terdaftar == ["A2"]
