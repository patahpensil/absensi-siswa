"""Tes KlienKoreksiGuru lewat httpx.MockTransport - tanpa server
sungguhan, tanpa Qt (pola sama dengan tests/ui/koreksi_satuan)."""

import httpx
import pytest

from app.ui.koreksi_guru.klien_koreksi_guru import GagalHubungiServer, KlienKoreksiGuru, ServerMenolak


def _respons_login(token: str = "tok-1", peran: str = "ADMIN") -> dict:
    return {"token": token, "peran": peran, "wajib_ganti_password": False, "kadaluarsa": "2026-09-10T19:00:00"}


def _klien(handler) -> KlienKoreksiGuru:
    klien = KlienKoreksiGuru(base_url="http://server-uji", username="admin", password="rahasia")
    klien._client = httpx.Client(base_url="http://server-uji", transport=httpx.MockTransport(handler))
    return klien


def test_status_saat_ini():
    def handler(request: httpx.Request) -> httpx.Response:
        if request.url.path == "/auth/login":
            return httpx.Response(200, json=_respons_login())
        assert request.url.path == "/koreksi-guru/status"
        return httpx.Response(200, json={"nama_lengkap": "Pak Budi", "status_final_sekarang": "SAKIT"})

    klien = _klien(handler)
    hasil = klien.status_saat_ini(id_guru=1, tanggal="2026-09-08")

    assert hasil["nama_lengkap"] == "Pak Budi"


def test_koreksi_tanpa_lampiran():
    def handler(request: httpx.Request) -> httpx.Response:
        if request.url.path == "/auth/login":
            return httpx.Response(200, json=_respons_login())
        assert request.url.path == "/koreksi-guru"
        assert b"lampiran" not in request.content
        return httpx.Response(201, json={"id": 1, "lampiran": None, "dibatalkan": False})

    klien = _klien(handler)
    hasil = klien.koreksi(id_guru=1, tanggal="2026-09-08", status_baru="SAKIT", alasan="Uji")

    assert hasil["id"] == 1
    assert hasil["lampiran"] is None


def test_koreksi_dengan_lampiran(tmp_path):
    berkas = tmp_path / "surat.jpg"
    berkas.write_bytes(b"isi-foto")

    def handler(request: httpx.Request) -> httpx.Response:
        if request.url.path == "/auth/login":
            return httpx.Response(200, json=_respons_login())
        assert request.url.path == "/koreksi-guru"
        assert request.headers["content-type"].startswith("multipart/form-data")
        assert b"surat.jpg" in request.content
        assert b"isi-foto" in request.content
        return httpx.Response(201, json={"id": 2, "lampiran": "koreksi_guru/abc.jpg", "dibatalkan": False})

    klien = _klien(handler)
    hasil = klien.koreksi(
        id_guru=1, tanggal="2026-09-08", status_baru="DINAS_LUAR", alasan="Diklat", lampiran_path=berkas
    )

    assert hasil["lampiran"] == "koreksi_guru/abc.jpg"


def test_batalkan_berhasil():
    def handler(request: httpx.Request) -> httpx.Response:
        if request.url.path == "/auth/login":
            return httpx.Response(200, json=_respons_login())
        assert request.url.path == "/koreksi-guru/1/batalkan"
        return httpx.Response(200, json={"id": 1, "dibatalkan": True})

    klien = _klien(handler)
    hasil = klien.batalkan(1, alasan="Salah input")

    assert hasil["dibatalkan"] is True


def test_daftar_riwayat():
    def handler(request: httpx.Request) -> httpx.Response:
        if request.url.path == "/auth/login":
            return httpx.Response(200, json=_respons_login())
        assert request.url.path == "/koreksi-guru"
        return httpx.Response(200, json=[{"id": 2, "dibatalkan": False}])

    klien = _klien(handler)
    hasil = klien.daftar_riwayat(id_guru=1, tanggal="2026-09-08")

    assert len(hasil) == 1


def test_unduh_lampiran_berhasil():
    def handler(request: httpx.Request) -> httpx.Response:
        if request.url.path == "/auth/login":
            return httpx.Response(200, json=_respons_login())
        assert request.url.path == "/koreksi-guru/2/lampiran"
        return httpx.Response(200, content=b"isi-foto-surat", headers={"content-type": "image/jpeg"})

    klien = _klien(handler)
    isi = klien.unduh_lampiran(2)

    assert isi == b"isi-foto-surat"


def test_unduh_lampiran_tidak_ada_404():
    def handler(request: httpx.Request) -> httpx.Response:
        if request.url.path == "/auth/login":
            return httpx.Response(200, json=_respons_login())
        return httpx.Response(404, json={"detail": "Lampiran tidak ditemukan"})

    klien = _klien(handler)
    with pytest.raises(ServerMenolak) as exc_info:
        klien.unduh_lampiran(2)

    assert exc_info.value.status_code == 404


def test_403_melempar_server_menolak():
    def handler(request: httpx.Request) -> httpx.Response:
        if request.url.path == "/auth/login":
            return httpx.Response(200, json=_respons_login())
        return httpx.Response(403, json={"detail": "Peran wali_kelas tidak berwenang"})

    klien = _klien(handler)
    with pytest.raises(ServerMenolak) as exc_info:
        klien.daftar_riwayat(id_guru=1, tanggal="2026-09-08")

    assert exc_info.value.status_code == 403


def test_jaringan_putus_melempar_gagal_hubungi_server():
    def handler(request: httpx.Request) -> httpx.Response:
        raise httpx.ConnectError("jaringan putus", request=request)

    klien = _klien(handler)
    with pytest.raises(GagalHubungiServer):
        klien.daftar_riwayat(id_guru=1, tanggal="2026-09-08")
