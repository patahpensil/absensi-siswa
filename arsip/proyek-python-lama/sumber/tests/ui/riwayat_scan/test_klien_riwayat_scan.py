"""Tes KlienRiwayatScan lewat httpx.MockTransport - tanpa server
sungguhan, tanpa Qt (pola sama dengan klien layar admin lainnya)."""

import httpx
import pytest

from app.ui.riwayat_scan.klien_riwayat_scan import GagalHubungiServer, KlienRiwayatScan, ServerMenolak


def _respons_login(token: str = "tok-1", peran: str = "WALI_KELAS") -> dict:
    return {"token": token, "peran": peran, "wajib_ganti_password": False, "kadaluarsa": "2026-09-10T19:00:00"}


def _klien(handler) -> KlienRiwayatScan:
    klien = KlienRiwayatScan(base_url="http://server-uji", username="wali", password="rahasia")
    klien._client = httpx.Client(base_url="http://server-uji", transport=httpx.MockTransport(handler))
    return klien


def test_cari_mengirim_hanya_param_terisi():
    tertangkap = {}

    def handler(request: httpx.Request) -> httpx.Response:
        if request.url.path == "/auth/login":
            return httpx.Response(200, json=_respons_login())
        tertangkap["query"] = str(request.url.params)
        return httpx.Response(200, json=[])

    klien = _klien(handler)
    klien.cari({"tanggal_mulai": "2026-09-08", "id_kelas": None, "id_pemilik": ""})

    assert "tanggal_mulai" in tertangkap["query"]
    assert "id_kelas" not in tertangkap["query"]
    assert "id_pemilik" not in tertangkap["query"]


def test_cari_mengembalikan_daftar():
    def handler(request: httpx.Request) -> httpx.Response:
        if request.url.path == "/auth/login":
            return httpx.Response(200, json=_respons_login())
        return httpx.Response(200, json=[{"id": 1, "label": "Absen Masuk"}])

    klien = _klien(handler)
    hasil = klien.cari({})

    assert hasil == [{"id": 1, "label": "Absen Masuk"}]


def test_403_melempar_server_menolak():
    def handler(request: httpx.Request) -> httpx.Response:
        if request.url.path == "/auth/login":
            return httpx.Response(200, json=_respons_login())
        return httpx.Response(403, json={"detail": "Wali kelas hanya berwenang atas kelasnya sendiri"})

    klien = _klien(handler)
    with pytest.raises(ServerMenolak) as exc_info:
        klien.cari({"id_kelas": 99})

    assert exc_info.value.status_code == 403


def test_jaringan_putus_melempar_gagal_hubungi_server():
    def handler(request: httpx.Request) -> httpx.Response:
        raise httpx.ConnectError("jaringan putus", request=request)

    klien = _klien(handler)
    with pytest.raises(GagalHubungiServer):
        klien.cari({})
