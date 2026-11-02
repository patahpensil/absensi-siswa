"""Tes KlienPengaturan lewat httpx.MockTransport - tanpa server
sungguhan, tanpa Qt (pola sama dengan klien_kalender/klien_data_master)."""

import httpx
import pytest

from app.ui.pengaturan.klien_pengaturan import GagalHubungiServer, KlienPengaturan, ServerMenolak


def _respons_login(token: str = "tok-1", peran: str = "ADMIN") -> dict:
    return {"token": token, "peran": peran, "wajib_ganti_password": False, "kadaluarsa": "2026-09-10T19:00:00"}


def _klien(handler) -> KlienPengaturan:
    klien = KlienPengaturan(base_url="http://server-uji", username="admin", password="rahasia")
    klien._client = httpx.Client(base_url="http://server-uji", transport=httpx.MockTransport(handler))
    return klien


def test_ambil_ambang_mengembalikan_body():
    def handler(request: httpx.Request) -> httpx.Response:
        if request.url.path == "/auth/login":
            return httpx.Response(200, json=_respons_login())
        assert request.url.path == "/pengaturan/ambang"
        return httpx.Response(
            200,
            json={
                "jendela_scan_berulang_detik": 10.0,
                "batas_terlambat_guru": "09:30:00",
                "ambang_tahan_konversi_pulang": 0.3,
                "cutoff_alpa": None,
                "waktu_kirim_notif_alpa": None,
            },
        )

    klien = _klien(handler)
    hasil = klien.ambil_ambang()

    assert hasil["jendela_scan_berulang_detik"] == 10.0


def test_403_untuk_peran_tanpa_akses():
    def handler(request: httpx.Request) -> httpx.Response:
        if request.url.path == "/auth/login":
            return httpx.Response(200, json=_respons_login(peran="WALI_KELAS"))
        return httpx.Response(403, json={"detail": "Peran tidak berwenang untuk aksi ini"})

    klien = _klien(handler)
    with pytest.raises(ServerMenolak) as exc_info:
        klien.ambil_ambang()

    assert klien.peran == "WALI_KELAS"
    assert exc_info.value.status_code == 403


def test_jaringan_putus_melempar_gagal_hubungi_server():
    def handler(request: httpx.Request) -> httpx.Response:
        raise httpx.ConnectError("jaringan putus", request=request)

    klien = _klien(handler)
    with pytest.raises(GagalHubungiServer):
        klien.ambil_saklar()


def test_tambah_jadwal_mengirim_payload_lengkap():
    tertangkap = {}

    def handler(request: httpx.Request) -> httpx.Response:
        if request.url.path == "/auth/login":
            return httpx.Response(200, json=_respons_login())
        tertangkap["json"] = request.content
        return httpx.Response(201, json={"id": 1, "jenis_pemilik": "SISWA"})

    klien = _klien(handler)
    klien.tambah_jadwal({"jenis_pemilik": "SISWA", "hari": 1})

    assert b"SISWA" in tertangkap["json"]


def test_ubah_identitas_sekolah():
    def handler(request: httpx.Request) -> httpx.Response:
        if request.url.path == "/auth/login":
            return httpx.Response(200, json=_respons_login())
        return httpx.Response(200, json={"nama": "Sekolah Uji"})

    klien = _klien(handler)
    hasil = klien.ubah_identitas_sekolah({"nama": "Sekolah Uji", "alasan": "Uji"})

    assert hasil["nama"] == "Sekolah Uji"
