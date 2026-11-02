"""Tes KlienDashboard lewat httpx.MockTransport - tanpa server
sungguhan, tanpa Qt (pola sama dengan tests/ui/perangkat)."""

import httpx
import pytest

from app.ui.dashboard.klien_dashboard import GagalHubungiServer, KlienDashboard, ServerMenolak


def _respons_login(token: str = "tok-1", peran: str = "ADMIN") -> dict:
    return {"token": token, "peran": peran, "wajib_ganti_password": False, "kadaluarsa": "2026-09-10T19:00:00"}


def _klien(handler, peran: str = "ADMIN") -> KlienDashboard:
    klien = KlienDashboard(base_url="http://server-uji", username="admin", password="rahasia")
    klien._client = httpx.Client(base_url="http://server-uji", transport=httpx.MockTransport(handler))
    return klien


def test_ringkasan():
    def handler(request: httpx.Request) -> httpx.Response:
        if request.url.path == "/auth/login":
            return httpx.Response(200, json=_respons_login())
        assert request.url.path == "/dashboard/ringkasan"
        return httpx.Response(200, json={"tanggal": "2026-09-11", "per_status": {"HADIR": 20}, "total_tercatat": 20})

    klien = _klien(handler)
    hasil = klien.ringkasan()

    assert hasil["total_tercatat"] == 20


def test_grafik_meneruskan_granularitas_dan_id_kelas():
    def handler(request: httpx.Request) -> httpx.Response:
        if request.url.path == "/auth/login":
            return httpx.Response(200, json=_respons_login())
        assert request.url.path == "/dashboard/grafik"
        assert request.url.params["granularitas"] == "MINGGUAN"
        assert request.url.params["id_kelas"] == "3"
        return httpx.Response(200, json=[{"periode": "2026-09-07", "per_status": {"HADIR": 5}}])

    klien = _klien(handler)
    hasil = klien.grafik(
        tanggal_mulai="2026-09-07", tanggal_selesai="2026-09-11", granularitas="MINGGUAN", id_kelas=3
    )

    assert hasil[0]["periode"] == "2026-09-07"


def test_tren_keterlambatan():
    def handler(request: httpx.Request) -> httpx.Response:
        if request.url.path == "/auth/login":
            return httpx.Response(200, json=_respons_login())
        assert request.url.path == "/dashboard/tren-keterlambatan"
        return httpx.Response(200, json=[{"tanggal": "2026-09-11", "jumlah": 3}])

    klien = _klien(handler)
    hasil = klien.tren_keterlambatan(tanggal_mulai="2026-09-01", tanggal_selesai="2026-09-11")

    assert hasil == [{"tanggal": "2026-09-11", "jumlah": 3}]


def test_alpa_terbanyak():
    def handler(request: httpx.Request) -> httpx.Response:
        if request.url.path == "/auth/login":
            return httpx.Response(200, json=_respons_login())
        assert request.url.path == "/dashboard/alpa-terbanyak"
        assert request.url.params["limit"] == "5"
        return httpx.Response(
            200, json=[{"id_siswa": 1, "nama_lengkap": "Budi", "nama_kelas": "8A", "jumlah_alpa": 4}]
        )

    klien = _klien(handler)
    hasil = klien.alpa_terbanyak(tanggal_mulai="2026-09-01", tanggal_selesai="2026-09-11", limit=5)

    assert hasil[0]["nama_lengkap"] == "Budi"


def test_status_operasional():
    def handler(request: httpx.Request) -> httpx.Response:
        if request.url.path == "/auth/login":
            return httpx.Response(200, json=_respons_login())
        assert request.url.path == "/dashboard/status-operasional"
        return httpx.Response(
            200, json={"jalur": [], "klien": [], "antrean_lobi": [], "pencadangan_terakhir": None}
        )

    klien = _klien(handler)
    hasil = klien.status_operasional()

    assert hasil["pencadangan_terakhir"] is None


def test_daftar_kelas():
    def handler(request: httpx.Request) -> httpx.Response:
        if request.url.path == "/auth/login":
            return httpx.Response(200, json=_respons_login())
        assert request.url.path == "/kelas"
        return httpx.Response(200, json=[{"id": 1, "nama_kelas": "8A"}])

    klien = _klien(handler)
    hasil = klien.daftar_kelas()

    assert hasil[0]["nama_kelas"] == "8A"


def test_operator_ditolak_menjadi_server_menolak():
    def handler(request: httpx.Request) -> httpx.Response:
        if request.url.path == "/auth/login":
            return httpx.Response(200, json=_respons_login(peran="OPERATOR"))
        return httpx.Response(403, json={"detail": "Peran OPERATOR tidak berwenang melihat laporan"})

    klien = _klien(handler)
    with pytest.raises(ServerMenolak) as exc_info:
        klien.ringkasan()
    assert exc_info.value.status_code == 403


def test_gagal_hubungi_server():
    def handler(request: httpx.Request) -> httpx.Response:
        raise httpx.ConnectError("tidak ada koneksi", request=request)

    klien = _klien(handler)
    with pytest.raises(GagalHubungiServer):
        klien.ringkasan()
