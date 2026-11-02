"""Tes app.server.lampiran_storage - filesystem lokal via tmp_path,
tanpa server FastAPI/Qt sungguhan."""

import pytest

from app.server.lampiran_storage import (
    BerkasTerlaluBesar,
    JenisBerkasTidakDidukung,
    path_lampiran,
    simpan_lampiran_koreksi_guru,
)


def test_simpan_dan_baca_kembali(tmp_path):
    path_relatif = simpan_lampiran_koreksi_guru(b"isi-gambar", nama_asli="surat.jpg", base_dir=tmp_path)

    assert path_relatif.startswith("koreksi_guru/")
    assert path_relatif.endswith(".jpg")
    path_absolut = path_lampiran(path_relatif, base_dir=tmp_path)
    assert path_absolut.is_file()
    assert path_absolut.read_bytes() == b"isi-gambar"


def test_nama_berkas_di_generate_bukan_nama_asli(tmp_path):
    """Nama asli TIDAK PERNAH dipakai langsung sebagai nama berkas -
    mencegah tabrakan dan path traversal (lihat docstring modul)."""
    path_relatif = simpan_lampiran_koreksi_guru(b"x", nama_asli="../../rahasia.jpg", base_dir=tmp_path)

    assert "rahasia" not in path_relatif
    assert ".." not in path_relatif


def test_dua_unggahan_nama_asli_sama_tidak_bertabrakan(tmp_path):
    pertama = simpan_lampiran_koreksi_guru(b"satu", nama_asli="surat.jpg", base_dir=tmp_path)
    kedua = simpan_lampiran_koreksi_guru(b"dua", nama_asli="surat.jpg", base_dir=tmp_path)

    assert pertama != kedua
    assert path_lampiran(pertama, base_dir=tmp_path).read_bytes() == b"satu"
    assert path_lampiran(kedua, base_dir=tmp_path).read_bytes() == b"dua"


def test_ekstensi_diterima(tmp_path):
    for nama in ("foto.jpg", "foto.jpeg", "foto.PNG", "surat.pdf"):
        simpan_lampiran_koreksi_guru(b"x", nama_asli=nama, base_dir=tmp_path)  # tidak melempar apa pun


def test_ekstensi_tidak_didukung_ditolak(tmp_path):
    with pytest.raises(JenisBerkasTidakDidukung):
        simpan_lampiran_koreksi_guru(b"x", nama_asli="video.mp4", base_dir=tmp_path)


def test_ekstensi_tidak_didukung_tidak_menulis_apa_pun(tmp_path):
    with pytest.raises(JenisBerkasTidakDidukung):
        simpan_lampiran_koreksi_guru(b"x", nama_asli="video.mp4", base_dir=tmp_path)
    assert list(tmp_path.rglob("*")) == []


def test_berkas_terlalu_besar_ditolak(tmp_path):
    isi_besar = b"0" * (10 * 1024 * 1024 + 1)
    with pytest.raises(BerkasTerlaluBesar):
        simpan_lampiran_koreksi_guru(isi_besar, nama_asli="besar.jpg", base_dir=tmp_path)
    assert list(tmp_path.rglob("*")) == []
