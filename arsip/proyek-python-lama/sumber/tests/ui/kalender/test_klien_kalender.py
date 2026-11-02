"""Tes KlienKalender lewat httpx.MockTransport - tanpa server sungguhan,
tanpa Qt (pola sama dengan tests/ui/data_master/test_klien_data_master.py)."""

import httpx
import pytest

from app.ui.kalender.klien_kalender import GagalHubungiServer, KlienKalender, ServerMenolak


def _respons_login(token: str = "tok-1", peran: str = "ADMIN") -> dict:
    return {"token": token, "peran": peran, "wajib_ganti_password": False, "kadaluarsa": "2026-09-10T19:00:00"}


def _klien(handler) -> KlienKalender:
    klien = KlienKalender(base_url="http://server-uji", username="admin", password="rahasia")
    klien._client = httpx.Client(base_url="http://server-uji", transport=httpx.MockTransport(handler))
    return klien


def test_login_menyimpan_peran():
    def handler(request: httpx.Request) -> httpx.Response:
        if request.url.path == "/auth/login":
            return httpx.Response(200, json=_respons_login(peran="KEPALA_SEKOLAH"))
        return httpx.Response(200, json=[])

    klien = _klien(handler)
    klien.daftar()

    assert klien.peran == "KEPALA_SEKOLAH"


def test_pratinjau_dampak_mengembalikan_body():
    def handler(request: httpx.Request) -> httpx.Response:
        if request.url.path == "/auth/login":
            return httpx.Response(200, json=_respons_login())
        assert request.url.path == "/kalender/pratinjau"
        return httpx.Response(200, json={"jumlah_hari": 6, "perkiraan_baris": 6000})

    klien = _klien(handler)
    hasil = klien.pratinjau_dampak(
        {"mulai": "2026-12-14", "selesai": "2026-12-19", "jenis_siswa": "KEGIATAN_TANPA_KBM", "jenis_guru": "EFEKTIF"}
    )

    assert hasil == {"jumlah_hari": 6, "perkiraan_baris": 6000}


def test_403_melempar_server_menolak():
    def handler(request: httpx.Request) -> httpx.Response:
        if request.url.path == "/auth/login":
            return httpx.Response(200, json=_respons_login())
        return httpx.Response(403, json={"detail": "Peran tidak berwenang untuk aksi ini"})

    klien = _klien(handler)
    with pytest.raises(ServerMenolak) as exc_info:
        klien.tambah({"mulai": "2026-09-09", "selesai": "2026-09-09"})

    assert exc_info.value.status_code == 403


def test_jaringan_putus_melempar_gagal_hubungi_server():
    def handler(request: httpx.Request) -> httpx.Response:
        raise httpx.ConnectError("jaringan putus", request=request)

    klien = _klien(handler)
    with pytest.raises(GagalHubungiServer):
        klien.daftar()


def test_daftar_dengan_filter_tahun_ajaran_mengirim_query_param():
    tertangkap = {}

    def handler(request: httpx.Request) -> httpx.Response:
        if request.url.path == "/auth/login":
            return httpx.Response(200, json=_respons_login())
        tertangkap["query"] = str(request.url.params)
        return httpx.Response(200, json=[])

    klien = _klien(handler)
    klien.daftar(tahun_ajaran="2026/2027")

    assert "2026" in tertangkap["query"]
