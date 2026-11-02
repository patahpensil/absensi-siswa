"""Tes KlienNotifikasi lewat httpx.MockTransport - tanpa server
sungguhan, tanpa Qt (pola sama dengan klien_pengaturan/klien_riwayat_scan)."""

import httpx
import pytest

from app.ui.notifikasi.klien_notifikasi import GagalHubungiServer, KlienNotifikasi, ServerMenolak


def _respons_login(token: str = "tok-1", peran: str = "ADMIN") -> dict:
    return {"token": token, "peran": peran, "wajib_ganti_password": False, "kadaluarsa": "2026-09-10T19:00:00"}


def _klien(handler) -> KlienNotifikasi:
    klien = KlienNotifikasi(base_url="http://server-uji", username="admin", password="rahasia")
    klien._client = httpx.Client(base_url="http://server-uji", transport=httpx.MockTransport(handler))
    return klien


def test_daftar_template_mengembalikan_body():
    def handler(request: httpx.Request) -> httpx.Response:
        if request.url.path == "/auth/login":
            return httpx.Response(200, json=_respons_login())
        assert request.url.path == "/notifikasi/template"
        return httpx.Response(200, json=[{"jenis": "TERLAMBAT", "konten": None, "versi": None, "aktif": False}])

    klien = _klien(handler)
    hasil = klien.daftar_template()

    assert hasil[0]["jenis"] == "TERLAMBAT"


def test_ubah_template_mengirim_ke_path_jenis():
    tertangkap = {}

    def handler(request: httpx.Request) -> httpx.Response:
        if request.url.path == "/auth/login":
            return httpx.Response(200, json=_respons_login())
        tertangkap["path"] = request.url.path
        tertangkap["json"] = request.content
        return httpx.Response(200, json={"jenis": "TERLAMBAT", "konten": "Halo", "versi": 1, "aktif": True})

    klien = _klien(handler)
    klien.ubah_template("TERLAMBAT", {"konten": "Halo", "alasan": "Uji"})

    assert tertangkap["path"] == "/notifikasi/template/TERLAMBAT"
    assert b"Halo" in tertangkap["json"]


def test_daftar_antrean_menyertakan_filter_status():
    tertangkap = {}

    def handler(request: httpx.Request) -> httpx.Response:
        if request.url.path == "/auth/login":
            return httpx.Response(200, json=_respons_login())
        tertangkap["query"] = str(request.url.params)
        return httpx.Response(200, json=[])

    klien = _klien(handler)
    klien.daftar_antrean(status_filter="FAILED")

    assert "FAILED" in tertangkap["query"]


def test_403_untuk_peran_tanpa_akses():
    def handler(request: httpx.Request) -> httpx.Response:
        if request.url.path == "/auth/login":
            return httpx.Response(200, json=_respons_login(peran="OPERATOR"))
        return httpx.Response(403, json={"detail": "Peran tidak berwenang untuk aksi ini"})

    klien = _klien(handler)
    with pytest.raises(ServerMenolak) as exc_info:
        klien.ambil_mode_uji()

    assert klien.peran == "OPERATOR"
    assert exc_info.value.status_code == 403


def test_jaringan_putus_melempar_gagal_hubungi_server():
    def handler(request: httpx.Request) -> httpx.Response:
        raise httpx.ConnectError("jaringan putus", request=request)

    klien = _klien(handler)
    with pytest.raises(GagalHubungiServer):
        klien.ambil_mode_uji()


def test_ubah_mode_uji_mengirim_payload():
    tertangkap = {}

    def handler(request: httpx.Request) -> httpx.Response:
        if request.url.path == "/auth/login":
            return httpx.Response(200, json=_respons_login())
        tertangkap["json"] = request.content
        return httpx.Response(200, json={"aktif": True, "nomor_admin": "+6281234567890"})

    klien = _klien(handler)
    hasil = klien.ubah_mode_uji({"aktif": True, "nomor_admin": "081234567890", "alasan": "Uji"})

    assert hasil["aktif"] is True
    assert b"081234567890" in tertangkap["json"]
