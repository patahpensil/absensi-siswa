"""Tes KlienPerangkat lewat httpx.MockTransport - tanpa server sungguhan,
tanpa Qt (pola sama dengan tests/ui/koreksi_guru)."""

import httpx

from app.ui.perangkat.klien_perangkat import GagalHubungiServer, KlienPerangkat, ServerMenolak

import pytest


def _respons_login(token: str = "tok-1", peran: str = "ADMIN") -> dict:
    return {"token": token, "peran": peran, "wajib_ganti_password": False, "kadaluarsa": "2026-09-10T19:00:00"}


def _klien(handler) -> KlienPerangkat:
    klien = KlienPerangkat(base_url="http://server-uji", username="admin", password="rahasia")
    klien._client = httpx.Client(base_url="http://server-uji", transport=httpx.MockTransport(handler))
    return klien


def test_cek_uid_terdaftar():
    def handler(request: httpx.Request) -> httpx.Response:
        if request.url.path == "/auth/login":
            return httpx.Response(200, json=_respons_login())
        assert request.url.path == "/kartu/cek/AAAA1111"
        return httpx.Response(200, json={"uid": "AAAA1111", "terdaftar": True, "status": "AKTIF", "nama_pemilik": "Budi"})

    klien = _klien(handler)
    hasil = klien.cek_uid("AAAA1111")

    assert hasil["terdaftar"] is True
    assert hasil["nama_pemilik"] == "Budi"


def test_beri_izin_darurat():
    def handler(request: httpx.Request) -> httpx.Response:
        if request.url.path == "/auth/login":
            return httpx.Response(200, json=_respons_login())
        assert request.url.path == "/perangkat/izin-darurat"
        return httpx.Response(201, json={"id": 1, "station_id": "LOBI-1", "dicabut": False})

    klien = _klien(handler)
    hasil = klien.beri_izin_darurat(station_id="LOBI-1", jam=24, alasan="Uji")

    assert hasil["id"] == 1
    assert hasil["station_id"] == "LOBI-1"


def test_beri_izin_darurat_403_menjadi_server_menolak():
    def handler(request: httpx.Request) -> httpx.Response:
        if request.url.path == "/auth/login":
            return httpx.Response(200, json=_respons_login(peran="OPERATOR"))
        return httpx.Response(403, json={"detail": "Peran OPERATOR tidak berwenang"})

    klien = _klien(handler)
    with pytest.raises(ServerMenolak) as exc_info:
        klien.beri_izin_darurat(station_id="LOBI-1", jam=24, alasan="Uji")
    assert exc_info.value.status_code == 403
    assert "OPERATOR" in exc_info.value.detail


def test_ganti_jalur():
    def handler(request: httpx.Request) -> httpx.Response:
        if request.url.path == "/auth/login":
            return httpx.Response(200, json=_respons_login())
        assert request.url.path == "/perangkat/ganti-jalur"
        return httpx.Response(200, json={"jalur_lama": "PCSC", "jalur_baru": "SERIAL"})

    klien = _klien(handler)
    hasil = klien.ganti_jalur(jalur_baru="SERIAL", uid_kartu_verifikasi=["A1", "A2", "A3"], alasan="Uji")

    assert hasil == {"jalur_lama": "PCSC", "jalur_baru": "SERIAL"}


def test_cek_izin_darurat_aktif():
    def handler(request: httpx.Request) -> httpx.Response:
        if request.url.path == "/auth/login":
            return httpx.Response(200, json=_respons_login())
        assert request.url.path == "/perangkat/izin-darurat/aktif"
        assert request.url.params["station_id"] == "LOBI-1"
        return httpx.Response(200, json={"aktif": True, "station_id": "LOBI-1", "waktu_berakhir": "2026-09-12T08:00:00"})

    klien = _klien(handler)
    hasil = klien.cek_izin_darurat_aktif("LOBI-1")

    assert hasil["aktif"] is True


def test_gagal_hubungi_server_saat_login_gagal():
    def handler(request: httpx.Request) -> httpx.Response:
        raise httpx.ConnectError("tidak ada koneksi", request=request)

    klien = _klien(handler)
    with pytest.raises(GagalHubungiServer):
        klien.cek_uid("AAAA1111")
