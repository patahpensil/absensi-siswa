"""Tes murni app.ui.lobi.status_jalur.StatusGridLobi: kartu sukses hilang
otomatis setelah durasi tampil, penanda jalur gagal TIDAK ikut hilang oleh
timer yang sama - hanya hilang saat jalur itu berhasil scan lagi.
"""

from datetime import datetime, timedelta

from app.ui.lobi.status_jalur import StatusGridLobi
from app.ui.lobi.tampilan import KartuTampilan, WarnaStatus

MULAI = datetime(2026, 9, 10, 7, 10, 0)


def _kartu(*, tandai_gagal: bool = False) -> KartuTampilan:
    return KartuTampilan(
        nama_tampil="Budi", keterangan="Hadir", warna=WarnaStatus.HIJAU,
        berbunyi=False, tandai_jalur_gagal=tandai_gagal, inisial="B",
    )


def test_kartu_sukses_hilang_setelah_durasi_tampil():
    grid = StatusGridLobi()
    grid.perbarui("LOBI-1", _kartu(), waktu=MULAI)

    berubah = grid.bersihkan_kedaluwarsa(sekarang=MULAI + timedelta(seconds=1.6))

    assert berubah == ["LOBI-1"]
    assert grid.jalur["LOBI-1"].tampilan is None


def test_kartu_belum_kedaluwarsa_tetap_tampil():
    grid = StatusGridLobi()
    grid.perbarui("LOBI-1", _kartu(), waktu=MULAI)

    berubah = grid.bersihkan_kedaluwarsa(sekarang=MULAI + timedelta(seconds=0.5))

    assert berubah == []
    assert grid.jalur["LOBI-1"].tampilan is not None


def test_penanda_jalur_gagal_tidak_hilang_oleh_timer():
    grid = StatusGridLobi()
    grid.perbarui("LOBI-1", _kartu(tandai_gagal=True), waktu=MULAI)

    berubah = grid.bersihkan_kedaluwarsa(sekarang=MULAI + timedelta(seconds=10))

    assert berubah == []
    assert grid.jalur["LOBI-1"].gagal_ditandai is True


def test_scan_sukses_berikutnya_membersihkan_penanda_gagal():
    grid = StatusGridLobi()
    grid.perbarui("LOBI-1", _kartu(tandai_gagal=True), waktu=MULAI)

    grid.perbarui("LOBI-1", _kartu(tandai_gagal=False), waktu=MULAI + timedelta(seconds=5))

    assert grid.jalur["LOBI-1"].gagal_ditandai is False


def test_dua_jalur_independen():
    grid = StatusGridLobi()
    grid.daftarkan_jalur("LOBI-1")
    grid.daftarkan_jalur("LOBI-2")
    grid.perbarui("LOBI-1", _kartu(), waktu=MULAI)

    assert grid.jalur["LOBI-2"].tampilan is None
    berubah = grid.bersihkan_kedaluwarsa(sekarang=MULAI + timedelta(seconds=2))
    assert berubah == ["LOBI-1"]
