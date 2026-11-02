"""Penyimpanan arsip PDF laporan (PRD §9.3: "arsip PDF otomatis setiap
laporan dicetak"). Pola sama dengan app.server.lampiran_storage - berkas
di FILESYSTEM LOKAL server (§3.3), di bawah satu direktori dasar yang
bisa dikonfigurasi (``--laporan-dir`` di app.server.main).

Beda dari lampiran_storage: isi berkas di sini SELALU dibuat server
sendiri (app.server.laporan_export), bukan diunggah pengguna - jadi
tidak ada validasi jenis/ukuran berkas (tidak ada input pengguna untuk
divalidasi), hanya penamaan aman (UUID4, path traversal tidak relevan
karena nama tidak pernah berasal dari input pengguna).
"""

from __future__ import annotations

import uuid
from pathlib import Path

_SUBDIREKTORI = "laporan"


def simpan_arsip_laporan(isi_pdf: bytes, *, base_dir: Path) -> str:
    """Simpan satu PDF arsip, kembalikan path RELATIF (terhadap base_dir)
    untuk disimpan di kolom laporan_arsip.path_pdf."""
    direktori = base_dir / _SUBDIREKTORI
    direktori.mkdir(parents=True, exist_ok=True)

    nama_berkas = f"{uuid.uuid4().hex}.pdf"
    (direktori / nama_berkas).write_bytes(isi_pdf)
    return f"{_SUBDIREKTORI}/{nama_berkas}"


def path_arsip_laporan(path_relatif: str, *, base_dir: Path) -> Path:
    """Path absolut untuk membaca kembali arsip yang path relatifnya SUDAH
    tersimpan di basis data."""
    return base_dir / path_relatif
