"""Tes KlienPengguna lewat httpx.MockTransport - tanpa server sungguhan,
tanpa Qt (pola sama dengan klien_kalender/klien_pengaturan/klien_data_master)."""

import httpx
import pytest

from app.ui.pengguna.klien_pengguna import GagalHubungiServer, KlienPengguna, ServerMenolak


def _respons_login(token: str = "tok-1", peran: str = "KEPALA_SEKOLAH") -> dict:
    return {"token": token, "peran": peran, "wajib_ganti_password": False, "kadaluarsa": "2026-09-10T19:00:00"}


def _klien(handler) -> KlienPengguna:
    klien = KlienPengguna(base_url="http://server-uji", username="kepsek", password="rahasia")
    klien._client = httpx.Client(base_url="http://server-uji", transport=httpx.MockTransport(handler))
    return klien


def test_login_menyimpan_peran():
    def handler(request: httpx.Request) -> httpx.Response:
        if request.url.path == "/auth/login":
            return httpx.Response(200, json=_respons_login())
        return httpx.Response(200, json=[])

    klien = _klien(handler)
    klien.daftar_akun()

    assert klien.peran == "KEPALA_SEKOLAH"


def test_buat_akun_berhasil():
    def handler(request: httpx.Request) -> httpx.Response:
        if request.url.path == "/auth/login":
            return httpx.Response(200, json=_respons_login())
        assert request.url.path == "/pengguna"
        return httpx.Response(201, json={"id": 1, "username": "op1", "peran": "OPERATOR"})

    klien = _klien(handler)
    hasil = klien.buat_akun({"username": "op1", "password_awal": "rahasia123", "peran": "OPERATOR"})

    assert hasil["id"] == 1


def test_403_melempar_server_menolak():
    def handler(request: httpx.Request) -> httpx.Response:
        if request.url.path == "/auth/login":
            return httpx.Response(200, json=_respons_login(peran="ADMIN"))
        return httpx.Response(403, json={"detail": "Peran ADMIN tidak berwenang atas akun berperan ADMIN"})

    klien = _klien(handler)
    with pytest.raises(ServerMenolak) as exc_info:
        klien.buat_akun({"username": "x", "password_awal": "rahasia123", "peran": "ADMIN"})

    assert exc_info.value.status_code == 403


def test_jaringan_putus_melempar_gagal_hubungi_server():
    def handler(request: httpx.Request) -> httpx.Response:
        raise httpx.ConnectError("jaringan putus", request=request)

    klien = _klien(handler)
    with pytest.raises(GagalHubungiServer):
        klien.daftar_akun()


def test_daftar_kelas_memanggil_endpoint_kelas():
    def handler(request: httpx.Request) -> httpx.Response:
        if request.url.path == "/auth/login":
            return httpx.Response(200, json=_respons_login())
        assert request.url.path == "/kelas"
        return httpx.Response(200, json=[{"id": 1, "nama_kelas": "7A"}])

    klien = _klien(handler)
    hasil = klien.daftar_kelas()

    assert hasil[0]["nama_kelas"] == "7A"
