"""Tes aturan murni app.core.koreksi_massal: wewenang, penyaring status
bawaan, ambang snapshot/konfirmasi, dan penerapan tindakan per baris.
"""

from dataclasses import dataclass
from datetime import date

import pytest

from app.core.koreksi_massal import (
    AksesKoreksiMassalDitolak,
    JenisTindakanMassal,
    LingkupOrang,
    PenyaringKoreksiMassal,
    buat_pratinjau,
    pastikan_berwenang,
    terapkan_tindakan,
)
from app.core.status import AlasanFinal, Peran, StatusSiswa


@dataclass
class _PengaturanPalsu:
    ambang_snapshot_sebelum_massal: int = 5000
    ambang_konfirmasi_ketik: int = 5000


# ---------------------------------------------------------------------------
# Wewenang
# ---------------------------------------------------------------------------


def test_kepsek_dan_admin_berwenang():
    pastikan_berwenang(Peran.KEPALA_SEKOLAH)
    pastikan_berwenang(Peran.ADMIN)


def test_wali_kelas_dan_operator_ditolak():
    for peran in (Peran.WALI_KELAS, Peran.OPERATOR):
        with pytest.raises(AksesKoreksiMassalDitolak):
            pastikan_berwenang(peran)


# ---------------------------------------------------------------------------
# Penyaring - status bawaan mengecualikan status manual
# ---------------------------------------------------------------------------


def _penyaring(**kwargs) -> PenyaringKoreksiMassal:
    dasar = dict(
        tanggal_mulai=date(2026, 9, 1),
        tanggal_selesai=date(2026, 9, 1),
        lingkup=LingkupOrang.SEKOLAH,
    )
    dasar.update(kwargs)
    return PenyaringKoreksiMassal(**dasar)


def test_status_efektif_bawaan_mengecualikan_manual():
    efektif = _penyaring().status_efektif()
    assert StatusSiswa.IZIN not in efektif
    assert StatusSiswa.SAKIT not in efektif
    assert StatusSiswa.DISPENSASI not in efektif
    assert StatusSiswa.ALPA in efektif
    assert StatusSiswa.TERLAMBAT in efektif
    assert StatusSiswa.HADIR in efektif


def test_status_efektif_bisa_menyertakan_manual_eksplisit():
    efektif = _penyaring(status_termasuk=frozenset({StatusSiswa.SAKIT})).status_efektif()
    assert efektif == frozenset({StatusSiswa.SAKIT})


# ---------------------------------------------------------------------------
# Pratinjau - ambang snapshot dan konfirmasi ketik
# ---------------------------------------------------------------------------


def test_pratinjau_di_bawah_ambang_tidak_perlu_apa_apa():
    p = buat_pratinjau(100, _PengaturanPalsu())
    assert p.perlu_snapshot is False
    assert p.perlu_konfirmasi_ketik is False


def test_pratinjau_di_atas_ambang_snapshot_dan_konfirmasi():
    p = buat_pratinjau(5001, _PengaturanPalsu())
    assert p.perlu_snapshot is True
    assert p.perlu_konfirmasi_ketik is True


def test_pratinjau_tepat_di_ambang_belum_perlu():
    p = buat_pratinjau(5000, _PengaturanPalsu())
    assert p.perlu_snapshot is False
    assert p.perlu_konfirmasi_ketik is False


# ---------------------------------------------------------------------------
# terapkan_tindakan - UBAH_STATUS
# ---------------------------------------------------------------------------


def test_ubah_status_menghasilkan_perubahan():
    perubahan = terapkan_tindakan(
        id_absensi=1,
        status_masuk=StatusSiswa.ALPA,
        status_final=StatusSiswa.ALPA,
        alasan_final=None,
        izin_pulang_cepat=False,
        jenis_tindakan=JenisTindakanMassal.UBAH_STATUS,
        status_baru=StatusSiswa.SAKIT,
    )
    assert perubahan is not None
    assert perubahan.nilai_sebelum == {"status_final": "ALPA", "alasan_final": None}
    assert perubahan.nilai_sesudah == {"status_final": "SAKIT", "alasan_final": "KOREKSI_MASSAL"}


def test_ubah_status_ke_status_yang_sama_dilewati():
    perubahan = terapkan_tindakan(
        id_absensi=1,
        status_masuk=StatusSiswa.ALPA,
        status_final=StatusSiswa.ALPA,
        alasan_final=None,
        izin_pulang_cepat=False,
        jenis_tindakan=JenisTindakanMassal.UBAH_STATUS,
        status_baru=StatusSiswa.ALPA,
    )
    assert perubahan is None


def test_ubah_status_tanpa_status_baru_error():
    with pytest.raises(ValueError):
        terapkan_tindakan(
            id_absensi=1,
            status_masuk=StatusSiswa.ALPA,
            status_final=StatusSiswa.ALPA,
            alasan_final=None,
            izin_pulang_cepat=False,
            jenis_tindakan=JenisTindakanMassal.UBAH_STATUS,
        )


# ---------------------------------------------------------------------------
# terapkan_tindakan - TANDAI_IZIN_PULANG_CEPAT
# ---------------------------------------------------------------------------


def test_tandai_izin_pulang_cepat():
    perubahan = terapkan_tindakan(
        id_absensi=1,
        status_masuk=StatusSiswa.HADIR,
        status_final=StatusSiswa.HADIR,
        alasan_final=None,
        izin_pulang_cepat=False,
        jenis_tindakan=JenisTindakanMassal.TANDAI_IZIN_PULANG_CEPAT,
    )
    assert perubahan.nilai_sebelum == {"izin_pulang_cepat": False}
    assert perubahan.nilai_sesudah == {"izin_pulang_cepat": True}


def test_tandai_izin_pulang_cepat_sudah_ditandai_dilewati():
    perubahan = terapkan_tindakan(
        id_absensi=1,
        status_masuk=StatusSiswa.HADIR,
        status_final=StatusSiswa.HADIR,
        alasan_final=None,
        izin_pulang_cepat=True,
        jenis_tindakan=JenisTindakanMassal.TANDAI_IZIN_PULANG_CEPAT,
    )
    assert perubahan is None


# ---------------------------------------------------------------------------
# terapkan_tindakan - BATALKAN_KONVERSI_ALPA_PULANG
# ---------------------------------------------------------------------------


def test_batalkan_konversi_alpa_pulang_mengembalikan_ke_status_masuk():
    perubahan = terapkan_tindakan(
        id_absensi=1,
        status_masuk=StatusSiswa.HADIR,
        status_final=StatusSiswa.ALPA,
        alasan_final=AlasanFinal.TIDAK_SCAN_PULANG,
        izin_pulang_cepat=False,
        jenis_tindakan=JenisTindakanMassal.BATALKAN_KONVERSI_ALPA_PULANG,
    )
    assert perubahan.nilai_sebelum == {"status_final": "ALPA", "alasan_final": "TIDAK_SCAN_PULANG"}
    assert perubahan.nilai_sesudah == {"status_final": "HADIR", "alasan_final": None}


def test_batalkan_konversi_alpa_pulang_bukan_hasil_konversi_dilewati():
    """ALPA pagi (tidak pernah scan sama sekali) bukan hasil konversi
    pulang - alasan_final-nya None, bukan TIDAK_SCAN_PULANG."""
    perubahan = terapkan_tindakan(
        id_absensi=1,
        status_masuk=StatusSiswa.ALPA,
        status_final=StatusSiswa.ALPA,
        alasan_final=None,
        izin_pulang_cepat=False,
        jenis_tindakan=JenisTindakanMassal.BATALKAN_KONVERSI_ALPA_PULANG,
    )
    assert perubahan is None
