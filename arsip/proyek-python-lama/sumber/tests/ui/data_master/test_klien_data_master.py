"""Tes KlienDataMaster lewat httpx.MockTransport - tanpa server sungguhan,
tanpa Qt (pola sama dengan tests/ui/lobi/test_klien_scan.py)."""

import httpx
import pytest

from app.ui.data_master.klien_data_master import GagalHubungiServer, KlienDataMaster, ServerMenolak


def _respons_login(token: str = "tok-1") -> dict:
    return {"token": token, "peran": "ADMIN", "wajib_ganti_password": False, "kadaluarsa": "2026-09-10T19:00:00"}


def _klien(handler) -> KlienDataMaster:
    klien = KlienDataMaster(base_url="http://server-uji", username="admin", password="rahasia")
    klien._client = httpx.Client(base_url="http://server-uji", transport=httpx.MockTransport(handler))
    return klien


def test_login_lazy_lalu_panggil_endpoint():
    dipanggil = []

    def handler(request: httpx.Request) -> httpx.Response:
        dipanggil.append(request.url.path)
        if request.url.path == "/auth/login":
            return httpx.Response(200, json=_respons_login())
        assert request.headers["Authorization"] == "Bearer tok-1"
        return httpx.Response(200, json=[{"id": 1, "nama_lengkap": "Budi"}])

    klien = _klien(handler)
    hasil = klien.daftar_siswa()

    assert dipanggil == ["/auth/login", "/siswa"]
    assert hasil == [{"id": 1, "nama_lengkap": "Budi"}]


def test_401_memicu_login_ulang_lalu_ulangi_permintaan():
    token_dipakai = []

    def handler(request: httpx.Request) -> httpx.Response:
        if request.url.path == "/auth/login":
            return httpx.Response(200, json=_respons_login(token="tok-baru"))
        token = request.headers["Authorization"]
        token_dipakai.append(token)
        if token == "Bearer tok-lama":
            return httpx.Response(401, json={"detail": "Token tidak valid"})
        return httpx.Response(201, json={"id": 1})

    klien = _klien(handler)
    klien._token = "tok-lama"
    hasil = klien.buat_guru({"nama_lengkap": "Pak Anwar"})

    assert hasil == {"id": 1}
    assert token_dipakai == ["Bearer tok-lama", "Bearer tok-baru"]


def test_jaringan_putus_melempar_gagal_hubungi_server():
    def handler(request: httpx.Request) -> httpx.Response:
        raise httpx.ConnectError("jaringan putus", request=request)

    klien = _klien(handler)
    with pytest.raises(GagalHubungiServer):
        klien.daftar_kelas()


def test_409_melempar_server_menolak_dengan_detail():
    def handler(request: httpx.Request) -> httpx.Response:
        if request.url.path == "/auth/login":
            return httpx.Response(200, json=_respons_login())
        return httpx.Response(409, json={"detail": "NISN 1234567890 sudah dipakai siswa lain"})

    klien = _klien(handler)
    with pytest.raises(ServerMenolak) as exc_info:
        klien.buat_siswa({"nisn": "1234567890"})

    assert exc_info.value.status_code == 409
    assert "sudah dipakai" in exc_info.value.detail


def test_arsipkan_kelas_204_tidak_melempar():
    def handler(request: httpx.Request) -> httpx.Response:
        if request.url.path == "/auth/login":
            return httpx.Response(200, json=_respons_login())
        return httpx.Response(204)

    klien = _klien(handler)
    klien.arsipkan_kelas(1)  # tidak melempar apa pun
