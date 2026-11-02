"""Tes KlienScan.laporkan_telemetri (§14.1) - pelaporan berkala jumlah
antrean lokal ke server untuk status operasional Dashboard, lewat
httpx.MockTransport, tanpa server sungguhan, tanpa Qt. Pola sama persis
tests/ui/lobi/test_cek_mode_darurat.py."""

import httpx

from app.ui.lobi.klien_scan import KlienScan


def _respons_login(token: str = "tok-1") -> dict:
    return {"token": token, "peran": "OPERATOR", "wajib_ganti_password": False, "kadaluarsa": "2026-09-10T19:00:00"}


def _klien(handler) -> KlienScan:
    klien = KlienScan(
        base_url="http://server-uji", username="operator_lobi", password="rahasia", path_antrean=":memory:"
    )
    klien._client = httpx.Client(base_url="http://server-uji", transport=httpx.MockTransport(handler))
    return klien


def test_laporkan_telemetri_berhasil():
    def handler(request: httpx.Request) -> httpx.Response:
        if request.url.path == "/auth/login":
            return httpx.Response(200, json=_respons_login())
        assert request.url.path == "/perangkat/telemetri"
        return httpx.Response(201, json={"station_id": "LOBI-1", "jumlah_pending": 3, "waktu_lapor": "2026-09-11T10:00:00"})

    klien = _klien(handler)
    assert klien.laporkan_telemetri(station_id="LOBI-1", jumlah_pending=3) is True


def test_laporkan_telemetri_gagal_hubungi_server_tidak_melempar():
    def handler(request: httpx.Request) -> httpx.Response:
        raise httpx.ConnectError("jaringan putus", request=request)

    klien = _klien(handler)
    assert klien.laporkan_telemetri(station_id="LOBI-1", jumlah_pending=3) is False


def test_laporkan_telemetri_retry_sekali_pada_401():
    percobaan = {"n": 0}

    def handler(request: httpx.Request) -> httpx.Response:
        if request.url.path == "/auth/login":
            return httpx.Response(200, json=_respons_login())
        percobaan["n"] += 1
        if percobaan["n"] == 1:
            return httpx.Response(401)
        return httpx.Response(201, json={"station_id": "LOBI-1", "jumlah_pending": 0, "waktu_lapor": "2026-09-11T10:00:00"})

    klien = _klien(handler)
    klien._token = "token-kedaluwarsa"
    assert klien.laporkan_telemetri(station_id="LOBI-1", jumlah_pending=0) is True
    assert percobaan["n"] == 2
