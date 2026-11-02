"""Tes KlienScan.cek_mode_darurat (§12.4) - polling ringan untuk banner
MODE DARURAT lobi, lewat httpx.MockTransport, tanpa server sungguhan,
tanpa Qt. Sengaja TIDAK menyentuh antrean lokal/single-flight sama
sekali - murni informasional, terpisah dari pencatatan absensi."""

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


def test_mode_darurat_aktif_mengembalikan_pesan():
    def handler(request: httpx.Request) -> httpx.Response:
        if request.url.path == "/auth/login":
            return httpx.Response(200, json=_respons_login())
        assert request.url.path == "/perangkat/izin-darurat/aktif"
        assert request.url.params["station_id"] == "LOBI-1"
        return httpx.Response(
            200, json={"aktif": True, "station_id": "LOBI-1", "waktu_berakhir": "2026-09-12T08:00:00"}
        )

    klien = _klien(handler)
    aktif, pesan = klien.cek_mode_darurat("LOBI-1")

    assert aktif is True
    assert "LOBI-1" in pesan
    assert "2026-09-12T08:00:00" in pesan


def test_mode_darurat_tidak_aktif():
    def handler(request: httpx.Request) -> httpx.Response:
        if request.url.path == "/auth/login":
            return httpx.Response(200, json=_respons_login())
        return httpx.Response(200, json={"aktif": False, "station_id": "LOBI-1", "waktu_berakhir": None})

    klien = _klien(handler)
    aktif, pesan = klien.cek_mode_darurat("LOBI-1")

    assert aktif is False
    assert pesan is None


def test_mode_darurat_gagal_hubungi_server_diam_diam_aktif_false():
    """Kegagalan jaringan TIDAK PERNAH melempar exception ke pemanggil
    (timer di app.ui.lobi.layar) - banner cukup tidak berubah, absensi
    (submit_scan) sama sekali tidak tersentuh oleh kegagalan ini."""

    def handler(request: httpx.Request) -> httpx.Response:
        raise httpx.ConnectError("jaringan putus", request=request)

    klien = _klien(handler)
    aktif, pesan = klien.cek_mode_darurat("LOBI-1")

    assert aktif is False
    assert pesan is None


def test_mode_darurat_retry_sekali_pada_401():
    percobaan = {"n": 0}

    def handler(request: httpx.Request) -> httpx.Response:
        if request.url.path == "/auth/login":
            return httpx.Response(200, json=_respons_login())
        percobaan["n"] += 1
        if percobaan["n"] == 1:
            return httpx.Response(401)
        return httpx.Response(200, json={"aktif": False, "station_id": "LOBI-1", "waktu_berakhir": None})

    klien = _klien(handler)
    klien._token = "token-kedaluwarsa"
    aktif, pesan = klien.cek_mode_darurat("LOBI-1")

    assert aktif is False
    assert percobaan["n"] == 2
