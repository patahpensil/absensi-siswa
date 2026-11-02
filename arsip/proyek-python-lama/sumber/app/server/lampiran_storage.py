"""Penyimpanan lampiran foto surat tugas/keterangan guru (PRD §6).

Berkas disimpan di FILESYSTEM LOKAL server (§3.3: server adalah
satu-satunya pemegang basis data dan, di sini, satu-satunya pemegang
berkas) di bawah satu direktori dasar yang bisa dikonfigurasi
(``--lampiran-dir`` di app.server.main, lihat app.server.app.buat_app()).
Yang disimpan di kolom ``koreksi_guru.lampiran`` adalah PATH RELATIF
terhadap direktori dasar itu, bukan isi berkas - beda sengaja dari
``logo`` sekolah (app.config.pengaturan.IdentitasSekolah) yang memang
base64 di kolom teks, karena logo satu-satunya dan kecil sedangkan
lampiran koreksi bertambah terus seiring waktu.

Nama berkas di-generate (UUID4 + ekstensi asli) - TIDAK PERNAH memakai
nama asli dari pengguna, supaya:
- tidak ada tabrakan nama antar unggahan
- tidak ada celah path traversal (nama berkas tidak pernah dipakai
  langsung dari input pengguna untuk membentuk path baca/tulis)
"""

from __future__ import annotations

import uuid
from pathlib import Path

#: Ekstensi yang diterima - foto (jpg/png) atau dokumen hasil pindai (pdf).
EKSTENSI_DIIZINKAN = frozenset({".jpg", ".jpeg", ".png", ".pdf"})

#: Batas ukuran berkas - melindungi disk server dari unggahan besar tak
#: sengaja (mis. video), bukan ambang bisnis yang perlu dikonfigurasi
#: admin (SKILL.md §4 bicara soal ambang WAKTU/absensi, bukan ini).
UKURAN_MAKS_BYTE = 10 * 1024 * 1024  # 10 MB

_SUBDIREKTORI_KOREKSI_GURU = "koreksi_guru"


class JenisBerkasTidakDidukung(Exception):
    def __init__(self, ekstensi: str) -> None:
        self.ekstensi = ekstensi
        super().__init__(
            f"Jenis berkas '{ekstensi}' tidak didukung - hanya "
            f"{', '.join(sorted(EKSTENSI_DIIZINKAN))} yang diterima"
        )


class BerkasTerlaluBesar(Exception):
    def __init__(self, ukuran: int) -> None:
        self.ukuran = ukuran
        super().__init__(
            f"Berkas berukuran {ukuran} byte melebihi batas {UKURAN_MAKS_BYTE} byte"
        )


def simpan_lampiran_koreksi_guru(isi: bytes, *, nama_asli: str, base_dir: Path) -> str:
    """Simpan satu berkas lampiran, kembalikan path RELATIF (terhadap
    base_dir) untuk disimpan di kolom koreksi_guru.lampiran.

    Melempar JenisBerkasTidakDidukung / BerkasTerlaluBesar tanpa menulis
    apa pun ke disk kalau validasi gagal.
    """
    ekstensi = Path(nama_asli).suffix.lower()
    if ekstensi not in EKSTENSI_DIIZINKAN:
        raise JenisBerkasTidakDidukung(ekstensi)
    if len(isi) > UKURAN_MAKS_BYTE:
        raise BerkasTerlaluBesar(len(isi))

    direktori = base_dir / _SUBDIREKTORI_KOREKSI_GURU
    direktori.mkdir(parents=True, exist_ok=True)

    nama_berkas = f"{uuid.uuid4().hex}{ekstensi}"
    (direktori / nama_berkas).write_bytes(isi)
    return f"{_SUBDIREKTORI_KOREKSI_GURU}/{nama_berkas}"


def path_lampiran(path_relatif: str, *, base_dir: Path) -> Path:
    """Path absolut untuk membaca kembali lampiran yang path relatifnya
    SUDAH tersimpan di basis data (bukan input pengguna langsung - lihat
    catatan modul soal path traversal)."""
    return base_dir / path_relatif
