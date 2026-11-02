"""Tes KlienKoreksiMassal lewat httpx.MockTransport - tanpa server
sungguhan, tanpa Qt (pola sama dengan tests/ui/koreksi_satuan)."""

import httpx
import pytest

from app.ui.koreksi_massal.klien_koreksi_massal import GagalHubungiServer, KlienKoreksiMassal, ServerMenolak


def _respons_login(token: str = "tok-1", peran: str = "ADMIN") -> dict:
    return {"token": token, "peran": peran, "wajib_ganti_password": False, "kadaluarsa": "2026-09-10T19:00:00"}


def _klien(handler) -> KlienKoreksiMassal:
    klien = KlienKoreksiMassal(base_url="http://server-uji", username="admin", password="rahasia")
    klien._client = httpx.Client(base_url="http://server-uji", transport=httpx.MockTransport(handler))
    return klien


def test_pratinjau():
    def handler(request: httpx.Request) -> httpx.Response:
        if request.url.path == "/auth/login":
            return httpx.Response(200, json=_respons_login())
        assert request.url.path == "/koreksi-massal/pratinjau"
        return httpx.Response(200, json={"jumlah_baris": 3, "perlu_snapshot": False, "perlu_konfirmasi_ketik": False})

    klien = _klien(handler)
    hasil = klien.pratinjau({"tanggal_mulai": "2026-09-08", "tanggal_selesai": "2026-09-08", "lingkup": "SEKOLAH"})

    assert hasil["jumlah_baris"] == 3


def test_jalankan_berhasil():
    def handler(request: httpx.Request) -> httpx.Response:
        if request.url.path == "/auth/login":
            return httpx.Response(200, json=_respons_login())
        assert request.url.path == "/koreksi-massal/jalankan"
        return httpx.Response(200, json={"id": 1, "jumlah_baris": 3, "dibatalkan": False})

    klien = _klien(handler)
    hasil = klien.jalankan({"jenis_tindakan": "UBAH_STATUS", "alasan": "Uji"})

    assert hasil["id"] == 1


def test_jalankan_412_snapshot_diperlukan_melempar_server_menolak():
    def handler(request: httpx.Request) -> httpx.Response:
        if request.url.path == "/auth/login":
            return httpx.Response(200, json=_respons_login())
        return httpx.Response(412, json={"detail": "6000 baris terdampak, di atas ambang snapshot (5000)"})

    klien = _klien(handler)
    with pytest.raises(ServerMenolak) as exc_info:
        klien.jalankan({"jenis_tindakan": "UBAH_STATUS", "alasan": "Uji"})

    assert exc_info.value.status_code == 412
    assert "ambang snapshot" in exc_info.value.detail


def test_batalkan_berhasil():
    def handler(request: httpx.Request) -> httpx.Response:
        if request.url.path == "/auth/login":
            return httpx.Response(200, json=_respons_login())
        assert request.url.path == "/koreksi-massal/1/batalkan"
        return httpx.Response(200, json={"id": 1, "dibatalkan": True})

    klien = _klien(handler)
    hasil = klien.batalkan(1, alasan="Salah input")

    assert hasil["dibatalkan"] is True


def test_riwayat():
    def handler(request: httpx.Request) -> httpx.Response:
        if request.url.path == "/auth/login":
            return httpx.Response(200, json=_respons_login())
        assert request.url.path == "/koreksi-massal"
        return httpx.Response(200, json=[{"id": 2, "dibatalkan": False}])

    klien = _klien(handler)
    hasil = klien.riwayat()

    assert len(hasil) == 1


def test_403_melempar_server_menolak():
    def handler(request: httpx.Request) -> httpx.Response:
        if request.url.path == "/auth/login":
            return httpx.Response(200, json=_respons_login())
        return httpx.Response(403, json={"detail": "Peran wali_kelas tidak berwenang menjalankan koreksi massal"})

    klien = _klien(handler)
    with pytest.raises(ServerMenolak) as exc_info:
        klien.riwayat()

    assert exc_info.value.status_code == 403


def test_jaringan_putus_melempar_gagal_hubungi_server():
    def handler(request: httpx.Request) -> httpx.Response:
        raise httpx.ConnectError("jaringan putus", request=request)

    klien = _klien(handler)
    with pytest.raises(GagalHubungiServer):
        klien.riwayat()
