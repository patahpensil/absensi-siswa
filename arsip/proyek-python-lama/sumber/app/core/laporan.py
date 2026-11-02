"""Aturan murni modul Laporan (PRD §14.2): kewenangan lihat/ekspor, dan
tally status per siswa dalam rentang tanggal.

Fungsi murni - tidak menyentuh basis data maupun app.core.engine/
mesin_absensi. Dua bentuk keluaran laporan, keduanya dibangun dari baris
attendance yang sama (lihat app.server.laporan_service untuk query-nya):

- REKAP (menutupi Harian/Mingguan/Bulanan/per Kelas/seluruh sekolah):
  satu baris per siswa, tally jumlah status_final per jenis dalam
  rentang tanggal yang diminta. Laporan Harian adalah kasus rentang
  satu hari; Mingguan/Bulanan rentang lebih panjang; per Kelas menyaring
  siswa lewat id_kelas; seluruh sekolah tanpa saring - kelimanya SATU
  bentuk keluaran yang sama, dibedakan hanya rentang tanggal + saring
  kelas.
- DETAIL PER SISWA: daftar baris attendance harian APA ADANYA untuk SATU
  siswa (bukan tally) - laporan per siswa secara konvensi menunjukkan
  HARI MANA yang bolong, bukan cuma hitungannya.
"""

from __future__ import annotations

from dataclasses import dataclass
from datetime import date

from app.core.status import PERAN_LAPORAN, Peran, StatusSiswa


class AksesLaporanDitolak(Exception):
    """Peran pemanggil tidak termasuk PERAN_LAPORAN (PRD §10, baris
    "Dashboard dan laporan" - operator TIDAK termasuk, beda dari Riwayat
    Scan yang terbuka untuk operator)."""

    def __init__(self, peran: Peran) -> None:
        self.peran = peran
        super().__init__(f"Peran {peran.value} tidak berwenang melihat laporan")


def pastikan_berwenang(peran: Peran) -> None:
    if peran not in PERAN_LAPORAN:
        raise AksesLaporanDitolak(peran)


@dataclass(frozen=True)
class RekapSiswa:
    """Satu baris tabel rekap - satu siswa, tally status dalam rentang
    tanggal laporan."""

    id_siswa: int
    nama_lengkap: str
    nama_kelas: str | None
    jumlah_hari_tercatat: int
    jumlah: dict[str, int]  # StatusSiswa.value -> hitungan


def hitung_rekap_siswa(
    *, id_siswa: int, nama_lengkap: str, nama_kelas: str | None, daftar_status_final: list[str | None]
) -> RekapSiswa:
    """Tally status_final satu siswa dalam rentang tanggal laporan.

    Status di luar StatusSiswa yang dikenal (mis. None - baris attendance
    yang belum sempat dihitung statusnya) TIDAK ditambahkan ke jenis
    manapun, tapi tetap menambah jumlah_hari_tercatat - supaya total
    baris yang dilihat tetap jujur walau ada baris yang belum lengkap.
    """
    jumlah = {s.value: 0 for s in StatusSiswa}
    for status in daftar_status_final:
        if status in jumlah:
            jumlah[status] += 1
    return RekapSiswa(
        id_siswa=id_siswa,
        nama_lengkap=nama_lengkap,
        nama_kelas=nama_kelas,
        jumlah_hari_tercatat=len(daftar_status_final),
        jumlah=jumlah,
    )


@dataclass(frozen=True)
class BarisDetailHarian:
    tanggal: date
    status_masuk: str | None
    status_final: str | None
    alasan_final: str | None


@dataclass(frozen=True)
class LaporanDetailSiswa:
    nama_lengkap: str
    nisn: str
    nama_kelas: str | None
    baris: list[BarisDetailHarian]


@dataclass(frozen=True)
class HasilLaporan:
    """``id_kelas`` di sini SELALU berarti "kelas yang jadi lingkup
    laporan ini untuk keperluan hak akses" - terisi untuk rekap per
    kelas MAUPUN detail per siswa (diturunkan dari kelas siswa itu),
    None HANYA untuk rekap seluruh sekolah. Satu kolom ini yang dipakai
    ulang app.server.laporan_service.koreksi_setelah_terbit()/daftar_arsip()
    untuk membatasi wali kelas, supaya tidak perlu logika akses terpisah
    untuk tiap bentuk.
    """

    label: str
    tanggal_mulai: date
    tanggal_selesai: date
    id_kelas: int | None
    nama_kelas: str | None
    id_siswa: int | None
    rekap: list[RekapSiswa] | None
    detail: LaporanDetailSiswa | None
