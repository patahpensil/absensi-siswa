"""Tes KlienLaporan lewat httpx.MockTransport - tanpa server sungguhan,
tanpa Qt (pola sama dengan klien_notifikasi/klien_pengaturan)."""

import httpx
import pytest

from app.ui.laporan.klien_laporan import GagalHubungiServer, KlienLaporan, ServerMenolak


def _respons_login(token: str = "tok-1", peran: str = "ADMIN") -> dict:
    return {"token": token, "peran": peran, "wajib_ganti_password": False, "kadaluarsa": "2026-09-10T19:00:00"}


def _klien(handler) -> KlienLaporan:
    klien = KlienLaporan(base_url="http://server-uji", username="admin", password="rahasia")
    klien._client = httpx.Client(base_url="http://server-uji", transport=httpx.MockTransport(handler))
    return klien


def test_lihat_laporan_membuang_param_kosong():
    tertangkap = {}

    def handler(request: httpx.Request) -> httpx.Response:
        if request.url.path == "/auth/login":
            return httpx.Response(200, json=_respons_login())
        tertangkap["query"] = str(request.url.params)
        return httpx.Response(200, json={"label": "Uji", "rekap": [], "detail": None})

    klien = _klien(handler)
    hasil = klien.lihat_laporan(
        {"tanggal_mulai": "2026-09-01", "tanggal_selesai": "2026-09-05", "id_kelas": None, "id_siswa": None}
    )

    assert hasil["label"] == "Uji"
    assert "id_kelas" not in tertangkap["query"]
    assert "tanggal_mulai" in tertangkap["query"]


def test_ekspor_excel_mengembalikan_bytes_mentah():
    isi_palsu = b"PK\x03\x04isi-xlsx-palsu"

    def handler(request: httpx.Request) -> httpx.Response:
        if request.url.path == "/auth/login":
            return httpx.Response(200, json=_respons_login())
        assert request.url.path == "/laporan/ekspor.xlsx"
        return httpx.Response(200, content=isi_palsu, headers={"content-type": "application/vnd.ms-excel"})

    klien = _klien(handler)
    hasil = klien.ekspor_excel({"tanggal_mulai": "2026-09-01", "tanggal_selesai": "2026-09-05"})

    assert hasil == isi_palsu


def test_ekspor_pdf_mengembalikan_bytes_mentah():
    isi_palsu = b"%PDF-1.3 isi-pdf-palsu"

    def handler(request: httpx.Request) -> httpx.Response:
        if request.url.path == "/auth/login":
            return httpx.Response(200, json=_respons_login())
        assert request.url.path == "/laporan/ekspor.pdf"
        return httpx.Response(200, content=isi_palsu, headers={"content-type": "application/pdf"})

    klien = _klien(handler)
    hasil = klien.ekspor_pdf({"tanggal_mulai": "2026-09-01", "tanggal_selesai": "2026-09-05"})

    assert hasil == isi_palsu


def test_daftar_arsip_dan_koreksi_setelah_terbit():
    def handler(request: httpx.Request) -> httpx.Response:
        if request.url.path == "/auth/login":
            return httpx.Response(200, json=_respons_login())
        if request.url.path == "/laporan/arsip":
            return httpx.Response(200, json=[{"id": 1, "label": "Laporan Uji"}])
        assert request.url.path == "/laporan/arsip/1/koreksi-setelah-terbit"
        return httpx.Response(200, json=[{"sumber": "SATUAN", "nama_siswa": "Budi"}])

    klien = _klien(handler)
    arsip = klien.daftar_arsip()
    assert arsip[0]["label"] == "Laporan Uji"

    koreksi = klien.koreksi_setelah_terbit(1)
    assert koreksi[0]["nama_siswa"] == "Budi"


def test_403_untuk_operator():
    def handler(request: httpx.Request) -> httpx.Response:
        if request.url.path == "/auth/login":
            return httpx.Response(200, json=_respons_login(peran="OPERATOR"))
        return httpx.Response(403, json={"detail": "Peran tidak berwenang untuk aksi ini"})

    klien = _klien(handler)
    with pytest.raises(ServerMenolak) as exc_info:
        klien.lihat_laporan({"tanggal_mulai": "2026-09-01", "tanggal_selesai": "2026-09-01"})

    assert klien.peran == "OPERATOR"
    assert exc_info.value.status_code == 403


def test_jaringan_putus_melempar_gagal_hubungi_server():
    def handler(request: httpx.Request) -> httpx.Response:
        raise httpx.ConnectError("jaringan putus", request=request)

    klien = _klien(handler)
    with pytest.raises(GagalHubungiServer):
        klien.daftar_arsip()
