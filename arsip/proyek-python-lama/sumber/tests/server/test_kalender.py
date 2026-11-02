"""Tes integrasi endpoint kalender akademik dua jalur (PRD §8) - lewat
FastAPI TestClient sungguhan. Tidak menyentuh app.core.engine/mesin_absensi.
"""

from fastapi.testclient import TestClient

from app.data.models.system import PeranPengguna
from app.data.repositories.user import UserRepository
from app.server.app import buat_app
from app.server.security import hash_password


def _client(session_factory) -> TestClient:
    return TestClient(buat_app(session_factory))


def _buat_user(session, *, username: str, password: str, peran: PeranPengguna) -> int:
    return UserRepository(session).tambah(username=username, password_hash=hash_password(password), peran=peran).id


def _login(client: TestClient, *, username: str, password: str) -> str:
    resp = client.post("/auth/login", json={"username": username, "password": password})
    assert resp.status_code == 200, resp.text
    return resp.json()["token"]


def _header(token: str) -> dict:
    return {"Authorization": f"Bearer {token}"}


def _token_admin(client: TestClient, session) -> str:
    _buat_user(session, username="admin", password="rahasia123", peran=PeranPengguna.ADMIN)
    return _login(client, username="admin", password="rahasia123")


ENTRI_LHBPD = {
    "mulai": "2026-12-14",
    "selesai": "2026-12-19",
    "keterangan": "Pengolahan nilai LHBPD",
    "jenis_siswa": "KEGIATAN_TANPA_KBM",
    "jenis_guru": "EFEKTIF",
    "tahun_ajaran": "2026/2027",
}


# ---------------------------------------------------------------------------
# Hak akses - wali kelas/operator hanya lihat (§8.3)
# ---------------------------------------------------------------------------


def test_wali_kelas_bisa_melihat_daftar(session_factory, session):
    _buat_user(session, username="wali", password="rahasia123", peran=PeranPengguna.WALI_KELAS)
    client = _client(session_factory)
    token = _login(client, username="wali", password="rahasia123")

    resp = client.get("/kalender", headers=_header(token))
    assert resp.status_code == 200


def test_operator_ditolak_menambah(session_factory, session):
    _buat_user(session, username="operator", password="rahasia123", peran=PeranPengguna.OPERATOR)
    client = _client(session_factory)
    token = _login(client, username="operator", password="rahasia123")

    resp = client.post("/kalender", json=dict(ENTRI_LHBPD, alasan="Uji"), headers=_header(token))
    assert resp.status_code == 403


def test_wali_kelas_ditolak_pratinjau(session_factory, session):
    _buat_user(session, username="wali", password="rahasia123", peran=PeranPengguna.WALI_KELAS)
    client = _client(session_factory)
    token = _login(client, username="wali", password="rahasia123")

    resp = client.post(
        "/kalender/pratinjau",
        json={"mulai": "2026-12-14", "selesai": "2026-12-19", "jenis_siswa": "KEGIATAN_TANPA_KBM", "jenis_guru": "EFEKTIF"},
        headers=_header(token),
    )
    assert resp.status_code == 403


# ---------------------------------------------------------------------------
# Dua jalur, pratinjau, tambah, batalkan
# ---------------------------------------------------------------------------


def test_pratinjau_menghitung_dampak(session_factory, session):
    client = _client(session_factory)
    token = _token_admin(client, session)

    resp = client.post(
        "/kalender/pratinjau",
        json={"mulai": "2026-12-14", "selesai": "2026-12-19", "jenis_siswa": "KEGIATAN_TANPA_KBM", "jenis_guru": "EFEKTIF"},
        headers=_header(token),
    )
    assert resp.status_code == 200, resp.text
    body = resp.json()
    assert body["jumlah_hari"] == 6
    assert body["perkiraan_baris"] == 0  # belum ada siswa/guru aktif di basis data uji


def test_tambah_lalu_jenis_hari_dua_jalur_berbeda(session_factory, session):
    client = _client(session_factory)
    token = _token_admin(client, session)

    resp = client.post("/kalender", json=dict(ENTRI_LHBPD, alasan="Sesuai kalender dinas"), headers=_header(token))
    assert resp.status_code == 201, resp.text
    id_entri = resp.json()["id"]

    resp_hari = client.get("/kalender/hari", params={"tanggal": "2026-12-16"}, headers=_header(token))
    assert resp_hari.status_code == 200
    assert resp_hari.json()["jenis_siswa"] == "KEGIATAN_TANPA_KBM"
    assert resp_hari.json()["jenis_guru"] == "EFEKTIF"

    resp_daftar = client.get("/kalender", headers=_header(token))
    assert len(resp_daftar.json()) == 1
    assert resp_daftar.json()[0]["id"] == id_entri


def test_minggu_selalu_libur_tanpa_entri(session_factory, session):
    client = _client(session_factory)
    token = _token_admin(client, session)

    resp = client.get("/kalender/hari", params={"tanggal": "2026-09-13"}, headers=_header(token))  # Minggu
    assert resp.status_code == 200
    assert resp.json()["jenis_siswa"] == "LIBUR_NASIONAL"
    assert resp.json()["jenis_guru"] == "LIBUR_NASIONAL"


def test_tanggal_tak_diatur_default_efektif(session_factory, session):
    client = _client(session_factory)
    token = _token_admin(client, session)

    resp = client.get("/kalender/hari", params={"tanggal": "2026-09-15"}, headers=_header(token))  # Selasa biasa
    assert resp.status_code == 200
    assert resp.json()["jenis_siswa"] == "EFEKTIF"
    assert resp.json()["jenis_guru"] == "EFEKTIF"


def test_batalkan_mengembalikan_ke_efektif(session_factory, session):
    client = _client(session_factory)
    token = _token_admin(client, session)
    id_entri = client.post(
        "/kalender", json=dict(ENTRI_LHBPD, alasan="Uji"), headers=_header(token)
    ).json()["id"]

    resp = client.post(f"/kalender/{id_entri}/batalkan", json={"alasan": "Salah input"}, headers=_header(token))
    assert resp.status_code == 200
    assert resp.json()["dibatalkan"] is True

    resp_hari = client.get("/kalender/hari", params={"tanggal": "2026-12-16"}, headers=_header(token))
    assert resp_hari.json()["jenis_siswa"] == "EFEKTIF"


def test_batalkan_tidak_ditemukan_404(session_factory, session):
    client = _client(session_factory)
    token = _token_admin(client, session)

    resp = client.post("/kalender/999/batalkan", json={"alasan": "Uji"}, headers=_header(token))
    assert resp.status_code == 404


def test_batalkan_dua_kali_409(session_factory, session):
    client = _client(session_factory)
    token = _token_admin(client, session)
    id_entri = client.post(
        "/kalender", json=dict(ENTRI_LHBPD, alasan="Uji"), headers=_header(token)
    ).json()["id"]
    client.post(f"/kalender/{id_entri}/batalkan", json={"alasan": "Batal pertama"}, headers=_header(token))

    resp = client.post(f"/kalender/{id_entri}/batalkan", json={"alasan": "Batal kedua"}, headers=_header(token))
    assert resp.status_code == 409


def test_jenis_hari_tidak_dikenal_422(session_factory, session):
    client = _client(session_factory)
    token = _token_admin(client, session)

    payload = dict(ENTRI_LHBPD, jenis_siswa="TIDAK_ADA", alasan="Uji")
    resp = client.post("/kalender", json=payload, headers=_header(token))
    assert resp.status_code == 422


# ---------------------------------------------------------------------------
# Salin dari tahun sebelumnya
# ---------------------------------------------------------------------------


def test_salin_tahun_menduplikasi_ke_tahun_baru(session_factory, session):
    client = _client(session_factory)
    token = _token_admin(client, session)
    client.post("/kalender", json=dict(ENTRI_LHBPD, alasan="Uji"), headers=_header(token))

    resp = client.post(
        "/kalender/salin-tahun",
        json={"tahun_asal": "2026/2027", "tahun_tujuan": "2027/2028", "alasan": "Persiapan tahun baru"},
        headers=_header(token),
    )
    assert resp.status_code == 200, resp.text
    disalin = resp.json()
    assert len(disalin) == 1
    assert disalin[0]["tahun_ajaran"] == "2027/2028"

    resp_daftar = client.get("/kalender", params={"tahun_ajaran": "2026/2027"}, headers=_header(token))
    assert len(resp_daftar.json()) == 1  # tahun asal tidak terganggu


def test_salin_tahun_ditolak_untuk_wali_kelas(session_factory, session):
    _buat_user(session, username="wali", password="rahasia123", peran=PeranPengguna.WALI_KELAS)
    client = _client(session_factory)
    token = _login(client, username="wali", password="rahasia123")

    resp = client.post(
        "/kalender/salin-tahun",
        json={"tahun_asal": "2026/2027", "tahun_tujuan": "2027/2028", "alasan": "Uji"},
        headers=_header(token),
    )
    assert resp.status_code == 403
