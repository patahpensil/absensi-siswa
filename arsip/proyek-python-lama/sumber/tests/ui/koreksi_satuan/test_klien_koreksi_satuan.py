"""Tes KlienKoreksiSatuan lewat httpx.MockTransport - tanpa server
sungguhan, tanpa Qt (pola sama dengan klien layar admin lainnya)."""

import httpx
import pytest

from app.ui.koreksi_satuan.klien_koreksi_satuan import GagalHubungiServer, KlienKoreksiSatuan, ServerMenolak


def _respons_login(token: str = "tok-1", peran: str = "WALI_KELAS") -> dict:
    return {"token": token, "peran": peran, "wajib_ganti_password": False, "kadaluarsa": "2026-09-10T19:00:00"}


def _klien(handler) -> KlienKoreksiSatuan:
    klien = KlienKoreksiSatuan(base_url="http://server-uji", username="wali", password="rahasia")
    klien._client = httpx.Client(base_url="http://server-uji", transport=httpx.MockTransport(handler))
    return klien


def test_status_saat_ini():
    def handler(request: httpx.Request) -> httpx.Response:
        if request.url.path == "/auth/login":
            return httpx.Response(200, json=_respons_login())
        assert request.url.path == "/koreksi-satuan/status"
        return httpx.Response(200, json={"nama_lengkap": "Budi", "status_final_sekarang": "ALPA"})

    klien = _klien(handler)
    hasil = klien.status_saat_ini(id_siswa=1, tanggal="2026-09-08")

    assert hasil["nama_lengkap"] == "Budi"


def test_buat_berhasil():
    def handler(request: httpx.Request) -> httpx.Response:
        if request.url.path == "/auth/login":
            return httpx.Response(200, json=_respons_login())
        return httpx.Response(201, json={"id": 1, "dibatalkan": False})

    klien = _klien(handler)
    hasil = klien.buat({"id_siswa": 1, "tanggal": "2026-09-08", "status_baru": "IZIN", "alasan": "Uji"})

    assert hasil["id"] == 1


def test_403_melempar_server_menolak():
    def handler(request: httpx.Request) -> httpx.Response:
        if request.url.path == "/auth/login":
            return httpx.Response(200, json=_respons_login())
        return httpx.Response(403, json={"detail": "Wali kelas hanya berwenang atas kelasnya sendiri"})

    klien = _klien(handler)
    with pytest.raises(ServerMenolak) as exc_info:
        klien.buat({"id_siswa": 99, "tanggal": "2026-09-08", "status_baru": "IZIN", "alasan": "Uji"})

    assert exc_info.value.status_code == 403


def test_jaringan_putus_melempar_gagal_hubungi_server():
    def handler(request: httpx.Request) -> httpx.Response:
        raise httpx.ConnectError("jaringan putus", request=request)

    klien = _klien(handler)
    with pytest.raises(GagalHubungiServer):
        klien.daftar_riwayat(id_siswa=1, tanggal="2026-09-08")
