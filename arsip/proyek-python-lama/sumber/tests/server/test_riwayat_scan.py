"""Tes integrasi endpoint Riwayat Scan (PRD §7.4, §10) - lingkup baca
per peran: admin/kepala sekolah/operator bebas, wali kelas hanya
kelasnya sendiri. Tidak menyentuh app.core.engine/mesin_absensi - murni
membaca attendance_events yang sudah ada.
"""

from datetime import date, datetime

from fastapi.testclient import TestClient

from app.data.models.attendance import JenisEventScan
from app.data.models.core import JenisKelamin
from app.data.models.enums import JenisPemilik
from app.data.models.system import PeranPengguna
from app.data.repositories.attendance_event import AttendanceEventRepository
from app.data.repositories.kelas import KelasRepository
from app.data.repositories.student import StudentRepository
from app.data.repositories.user import UserRepository
from app.rfid.base import JalurAlat
from app.server.app import buat_app
from app.server.security import hash_password

TANGGAL = date(2026, 9, 8)


def _client(session_factory) -> TestClient:
    return TestClient(buat_app(session_factory))


def _buat_user(session, *, username: str, password: str, peran: PeranPengguna, **kwargs) -> int:
    return UserRepository(session).tambah(
        username=username, password_hash=hash_password(password), peran=peran, **kwargs
    ).id


def _login(client: TestClient, *, username: str, password: str) -> str:
    resp = client.post("/auth/login", json={"username": username, "password": password})
    assert resp.status_code == 200, resp.text
    return resp.json()["token"]


def _header(token: str) -> dict:
    return {"Authorization": f"Bearer {token}"}


def _buat_siswa(session, *, id_kelas: int, nisn: str, nama: str) -> int:
    return StudentRepository(session).tambah(
        nisn=nisn, nama_lengkap=nama, id_kelas=id_kelas, jenis_kelamin=JenisKelamin.L,
        tempat_lahir="Pancarijang", tanggal_lahir=date(2012, 1, 1), alamat="Jl. Contoh No. 1",
    ).id


def _buat_event(session, *, uid: str, id_pemilik: int | None, jenis_pemilik, jenis_event, waktu) -> None:
    AttendanceEventRepository(session).buat(
        uid_kartu=uid, uid_mentah=uid, id_pemilik=id_pemilik, jenis_pemilik=jenis_pemilik,
        jenis_event=jenis_event, waktu_scan=waktu,
    )


# ---------------------------------------------------------------------------
# Setup bersama: dua kelas, satu siswa per kelas, satu guru, beragam event
# ---------------------------------------------------------------------------


def _siapkan_data(session):
    kelas_a = KelasRepository(session).tambah(tingkat=7, nama_kelas="7A", tahun_ajaran="2026/2027").id
    kelas_b = KelasRepository(session).tambah(tingkat=7, nama_kelas="7B", tahun_ajaran="2026/2027").id
    siswa_a = _buat_siswa(session, id_kelas=kelas_a, nisn="1000000001", nama="Budi (7A)")
    siswa_b = _buat_siswa(session, id_kelas=kelas_b, nisn="1000000002", nama="Siti (7B)")

    waktu = datetime(2026, 9, 8, 7, 10)
    _buat_event(session, uid="A1", id_pemilik=siswa_a, jenis_pemilik=JenisPemilik.SISWA, jenis_event=JenisEventScan.MASUK, waktu=waktu)
    _buat_event(session, uid="B1", id_pemilik=siswa_b, jenis_pemilik=JenisPemilik.SISWA, jenis_event=JenisEventScan.MASUK, waktu=waktu)
    _buat_event(session, uid="TIDAK_DIKENAL", id_pemilik=None, jenis_pemilik=None, jenis_event=JenisEventScan.UNKNOWN_CARD, waktu=waktu)
    return {"kelas_a": kelas_a, "kelas_b": kelas_b, "siswa_a": siswa_a, "siswa_b": siswa_b}


# ---------------------------------------------------------------------------
# Lingkup peran - inti slice ini
# ---------------------------------------------------------------------------


def test_admin_melihat_semua_event(session_factory, session):
    data = _siapkan_data(session)
    _buat_user(session, username="admin", password="rahasia123", peran=PeranPengguna.ADMIN)
    client = _client(session_factory)
    token = _login(client, username="admin", password="rahasia123")

    resp = client.get("/riwayat-scan", params={"tanggal_mulai": "2026-09-08"}, headers=_header(token))
    assert resp.status_code == 200, resp.text
    assert len(resp.json()) == 3


def test_operator_melihat_semua_event_tanpa_batas_kelas(session_factory, session):
    _siapkan_data(session)
    _buat_user(session, username="operator", password="rahasia123", peran=PeranPengguna.OPERATOR)
    client = _client(session_factory)
    token = _login(client, username="operator", password="rahasia123")

    resp = client.get("/riwayat-scan", params={"tanggal_mulai": "2026-09-08"}, headers=_header(token))
    assert resp.status_code == 200
    assert len(resp.json()) == 3


def test_wali_kelas_hanya_melihat_kelasnya_sendiri(session_factory, session):
    data = _siapkan_data(session)
    _buat_user(
        session, username="wali_a", password="rahasia123", peran=PeranPengguna.WALI_KELAS,
        id_kelas_diampu=data["kelas_a"],
    )
    client = _client(session_factory)
    token = _login(client, username="wali_a", password="rahasia123")

    resp = client.get("/riwayat-scan", params={"tanggal_mulai": "2026-09-08"}, headers=_header(token))
    assert resp.status_code == 200, resp.text
    body = resp.json()
    assert len(body) == 1
    assert body[0]["nama"] == "Budi (7A)"


def test_wali_kelas_tanpa_kelas_diampu_melihat_kosong(session_factory, session):
    _siapkan_data(session)
    _buat_user(session, username="wali_tanpa_kelas", password="rahasia123", peran=PeranPengguna.WALI_KELAS)
    client = _client(session_factory)
    token = _login(client, username="wali_tanpa_kelas", password="rahasia123")

    resp = client.get("/riwayat-scan", params={"tanggal_mulai": "2026-09-08"}, headers=_header(token))
    assert resp.status_code == 200
    assert resp.json() == []


def test_wali_kelas_memaksakan_filter_kelas_lain_ditolak_403(session_factory, session):
    data = _siapkan_data(session)
    _buat_user(
        session, username="wali_a", password="rahasia123", peran=PeranPengguna.WALI_KELAS,
        id_kelas_diampu=data["kelas_a"],
    )
    client = _client(session_factory)
    token = _login(client, username="wali_a", password="rahasia123")

    resp = client.get(
        "/riwayat-scan", params={"tanggal_mulai": "2026-09-08", "id_kelas": data["kelas_b"]}, headers=_header(token)
    )
    assert resp.status_code == 403


def test_wali_kelas_filter_orang_di_luar_kelasnya_kosong(session_factory, session):
    data = _siapkan_data(session)
    _buat_user(
        session, username="wali_a", password="rahasia123", peran=PeranPengguna.WALI_KELAS,
        id_kelas_diampu=data["kelas_a"],
    )
    client = _client(session_factory)
    token = _login(client, username="wali_a", password="rahasia123")

    resp = client.get(
        "/riwayat-scan",
        params={"tanggal_mulai": "2026-09-08", "id_pemilik": data["siswa_b"]},
        headers=_header(token),
    )
    assert resp.status_code == 200
    assert resp.json() == []


def test_admin_filter_kelas_tidak_ada_404(session_factory, session):
    _siapkan_data(session)
    _buat_user(session, username="admin", password="rahasia123", peran=PeranPengguna.ADMIN)
    client = _client(session_factory)
    token = _login(client, username="admin", password="rahasia123")

    resp = client.get("/riwayat-scan", params={"tanggal_mulai": "2026-09-08", "id_kelas": 999}, headers=_header(token))
    assert resp.status_code == 404


def test_admin_filter_kelas_menyaring_hanya_kelas_itu(session_factory, session):
    data = _siapkan_data(session)
    _buat_user(session, username="admin", password="rahasia123", peran=PeranPengguna.ADMIN)
    client = _client(session_factory)
    token = _login(client, username="admin", password="rahasia123")

    resp = client.get(
        "/riwayat-scan", params={"tanggal_mulai": "2026-09-08", "id_kelas": data["kelas_b"]}, headers=_header(token)
    )
    assert resp.status_code == 200
    body = resp.json()
    assert len(body) == 1
    assert body[0]["nama"] == "Siti (7B)"


# ---------------------------------------------------------------------------
# Label §7.4 dan field lain
# ---------------------------------------------------------------------------


def test_label_kartu_tidak_dikenal_dan_nama_kosong(session_factory, session):
    _siapkan_data(session)
    _buat_user(session, username="admin", password="rahasia123", peran=PeranPengguna.ADMIN)
    client = _client(session_factory)
    token = _login(client, username="admin", password="rahasia123")

    resp = client.get("/riwayat-scan", params={"tanggal_mulai": "2026-09-08"}, headers=_header(token))
    baris_tak_dikenal = next(b for b in resp.json() if b["uid_kartu"] == "TIDAK_DIKENAL")
    assert baris_tak_dikenal["label"] == "Kartu Tidak Dikenal / Nonaktif"
    assert baris_tak_dikenal["nama"] is None
    assert baris_tak_dikenal["jalur"] is None


def test_label_absen_masuk(session_factory, session):
    data = _siapkan_data(session)
    _buat_user(session, username="admin", password="rahasia123", peran=PeranPengguna.ADMIN)
    client = _client(session_factory)
    token = _login(client, username="admin", password="rahasia123")

    resp = client.get(
        "/riwayat-scan", params={"tanggal_mulai": "2026-09-08", "id_kelas": data["kelas_a"]}, headers=_header(token)
    )
    assert resp.json()[0]["label"] == "Absen Masuk"


def test_rentang_tanggal_di_luar_hasil_kosong(session_factory, session):
    _siapkan_data(session)
    _buat_user(session, username="admin", password="rahasia123", peran=PeranPengguna.ADMIN)
    client = _client(session_factory)
    token = _login(client, username="admin", password="rahasia123")

    resp = client.get(
        "/riwayat-scan", params={"tanggal_mulai": "2026-01-01", "tanggal_selesai": "2026-01-02"}, headers=_header(token)
    )
    assert resp.status_code == 200
    assert resp.json() == []


def test_jenis_pemilik_tidak_dikenal_422(session_factory, session):
    _siapkan_data(session)
    _buat_user(session, username="admin", password="rahasia123", peran=PeranPengguna.ADMIN)
    client = _client(session_factory)
    token = _login(client, username="admin", password="rahasia123")

    resp = client.get("/riwayat-scan", params={"jenis_pemilik": "TIDAK_ADA"}, headers=_header(token))
    assert resp.status_code == 422


def test_tanpa_token_401(session_factory, session):
    _siapkan_data(session)
    client = _client(session_factory)

    resp = client.get("/riwayat-scan")
    assert resp.status_code == 401


def test_jalur_tersimpan_ikut_tampil(session_factory, session):
    """PRD §17 - kolom jalur di attendance_events sekarang tersambung,
    layar Riwayat Scan sudah punya field jalur di skemanya sejak awal
    (dulu selalu None karena kolomnya belum ada, lihat README)."""
    data = _siapkan_data(session)
    AttendanceEventRepository(session).buat(
        uid_kartu="JALUR-UJI", uid_mentah="JALUR-UJI", id_pemilik=data["siswa_a"], jenis_pemilik=JenisPemilik.SISWA,
        jenis_event=JenisEventScan.MASUK, waktu_scan=datetime(2026, 9, 8, 7, 12), jalur=JalurAlat.PCSC,
    )
    _buat_user(session, username="admin", password="rahasia123", peran=PeranPengguna.ADMIN)
    client = _client(session_factory)
    token = _login(client, username="admin", password="rahasia123")

    resp = client.get(
        "/riwayat-scan", params={"tanggal_mulai": "2026-09-08", "id_pemilik": data["siswa_a"]}, headers=_header(token)
    )
    assert resp.status_code == 200
    baris = next(b for b in resp.json() if b["uid_kartu"] == "JALUR-UJI")
    assert baris["jalur"] == "PCSC"
