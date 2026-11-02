"""Tes integrasi endpoint Dashboard (PRD §14.1): ringkasan/grafik/
perbandingan kelas/tren/alpa-terbanyak/status operasional. Lingkup peran:
kepsek/admin bebas, wali kelas hanya kelasnya sendiri, operator tidak
berwenang sama sekali (sama seperti Laporan, PRD §10)."""

from datetime import date

from fastapi.testclient import TestClient

from app.data.models.attendance import JenisEventScan
from app.data.models.core import JenisKelamin
from app.data.models.enums import JenisPemilik
from app.data.models.system import PeranPengguna
from app.data.repositories.attendance import AttendanceRepository
from app.data.repositories.attendance_event import AttendanceEventRepository
from app.data.repositories.kelas import KelasRepository
from app.data.repositories.student import StudentRepository
from app.data.repositories.user import UserRepository
from app.rfid.base import JalurAlat
from app.server.app import buat_app
from app.server.security import hash_password


def _client(session_factory) -> TestClient:
    return TestClient(buat_app(session_factory))


def _buat_user(session, *, username: str, peran: PeranPengguna, id_kelas_diampu: int | None = None) -> int:
    return UserRepository(session).tambah(
        username=username, password_hash=hash_password("rahasia123"), peran=peran, id_kelas_diampu=id_kelas_diampu
    ).id


def _login(client: TestClient, *, username: str) -> str:
    resp = client.post("/auth/login", json={"username": username, "password": "rahasia123"})
    assert resp.status_code == 200, resp.text
    return resp.json()["token"]


def _header(token: str) -> dict:
    return {"Authorization": f"Bearer {token}"}


def _token(client, session, *, username: str, peran: PeranPengguna, id_kelas_diampu: int | None = None) -> str:
    _buat_user(session, username=username, peran=peran, id_kelas_diampu=id_kelas_diampu)
    return _login(client, username=username)


def _buat_kelas(session, *, nama_kelas: str) -> int:
    return KelasRepository(session).tambah(tingkat=8, nama_kelas=nama_kelas, tahun_ajaran="2026/2027").id


def _buat_siswa(session, *, id_kelas: int, nisn: str, nama: str) -> int:
    return StudentRepository(session).tambah(
        nisn=nisn, nama_lengkap=nama, jenis_kelamin=JenisKelamin.L, tempat_lahir="Sidrap",
        tanggal_lahir=date(2012, 1, 1), alamat="Jl. Contoh", id_kelas=id_kelas,
    ).id


def _tambah_absensi(session, *, id_siswa: int, tanggal: date, status: str) -> None:
    AttendanceRepository(session).tambah(
        id_pemilik=id_siswa, jenis_pemilik=JenisPemilik.SISWA, tanggal=tanggal, status_masuk=status
    )


# ---------------------------------------------------------------------------
# Lingkup peran
# ---------------------------------------------------------------------------


def test_operator_ditolak_seluruh_endpoint_dashboard(session_factory, session):
    client = _client(session_factory)
    token = _token(client, session, username="operator", peran=PeranPengguna.OPERATOR)

    assert client.get("/dashboard/ringkasan", headers=_header(token)).status_code == 403
    assert client.get(
        "/dashboard/grafik",
        params={"tanggal_mulai": "2026-09-01", "tanggal_selesai": "2026-09-11"},
        headers=_header(token),
    ).status_code == 403
    assert client.get("/dashboard/status-operasional", headers=_header(token)).status_code == 403


def test_kepsek_boleh_akses_ringkasan(session_factory, session):
    client = _client(session_factory)
    token = _token(client, session, username="kepsek", peran=PeranPengguna.KEPALA_SEKOLAH)

    resp = client.get("/dashboard/ringkasan", params={"tanggal": "2026-09-11"}, headers=_header(token))
    assert resp.status_code == 200, resp.text


def test_wali_kelas_belum_diampu_ditolak_409(session_factory, session):
    client = _client(session_factory)
    token = _token(client, session, username="wali_a", peran=PeranPengguna.WALI_KELAS, id_kelas_diampu=None)

    resp = client.get("/dashboard/ringkasan", headers=_header(token))
    assert resp.status_code == 409


# ---------------------------------------------------------------------------
# Ringkasan hari ini
# ---------------------------------------------------------------------------


def test_ringkasan_hari_ini_menghitung_status(session_factory, session):
    id_kelas = _buat_kelas(session, nama_kelas="8A")
    siswa1 = _buat_siswa(session, id_kelas=id_kelas, nisn="1000000001", nama="Budi")
    siswa2 = _buat_siswa(session, id_kelas=id_kelas, nisn="1000000002", nama="Siti")
    _tambah_absensi(session, id_siswa=siswa1, tanggal=date(2026, 9, 11), status="HADIR")
    _tambah_absensi(session, id_siswa=siswa2, tanggal=date(2026, 9, 11), status="TERLAMBAT")

    client = _client(session_factory)
    token = _token(client, session, username="admin", peran=PeranPengguna.ADMIN)
    resp = client.get("/dashboard/ringkasan", params={"tanggal": "2026-09-11"}, headers=_header(token))

    assert resp.status_code == 200, resp.text
    body = resp.json()
    assert body["per_status"] == {"HADIR": 1, "TERLAMBAT": 1}
    assert body["total_tercatat"] == 2


def test_wali_kelas_ringkasan_hanya_kelasnya(session_factory, session):
    id_kelas_a = _buat_kelas(session, nama_kelas="8A")
    id_kelas_b = _buat_kelas(session, nama_kelas="8B")
    siswa_a = _buat_siswa(session, id_kelas=id_kelas_a, nisn="1000000001", nama="Budi")
    siswa_b = _buat_siswa(session, id_kelas=id_kelas_b, nisn="1000000002", nama="Siti")
    _tambah_absensi(session, id_siswa=siswa_a, tanggal=date(2026, 9, 11), status="HADIR")
    _tambah_absensi(session, id_siswa=siswa_b, tanggal=date(2026, 9, 11), status="HADIR")

    client = _client(session_factory)
    token = _token(
        client, session, username="wali_a", peran=PeranPengguna.WALI_KELAS, id_kelas_diampu=id_kelas_a
    )
    resp = client.get("/dashboard/ringkasan", params={"tanggal": "2026-09-11"}, headers=_header(token))

    assert resp.json()["total_tercatat"] == 1


# ---------------------------------------------------------------------------
# Grafik
# ---------------------------------------------------------------------------


def test_grafik_granularitas_mingguan(session_factory, session):
    id_kelas = _buat_kelas(session, nama_kelas="8A")
    siswa = _buat_siswa(session, id_kelas=id_kelas, nisn="1000000001", nama="Budi")
    _tambah_absensi(session, id_siswa=siswa, tanggal=date(2026, 9, 7), status="HADIR")
    _tambah_absensi(session, id_siswa=siswa, tanggal=date(2026, 9, 9), status="HADIR")

    client = _client(session_factory)
    token = _token(client, session, username="admin", peran=PeranPengguna.ADMIN)
    resp = client.get(
        "/dashboard/grafik",
        params={"tanggal_mulai": "2026-09-07", "tanggal_selesai": "2026-09-09", "granularitas": "MINGGUAN"},
        headers=_header(token),
    )

    assert resp.status_code == 200, resp.text
    body = resp.json()
    assert len(body) == 1
    assert body[0]["periode"] == "2026-09-07"
    assert body[0]["per_status"] == {"HADIR": 2}


def test_grafik_granularitas_tidak_dikenal_422(session_factory, session):
    client = _client(session_factory)
    token = _token(client, session, username="admin", peran=PeranPengguna.ADMIN)
    resp = client.get(
        "/dashboard/grafik",
        params={"tanggal_mulai": "2026-09-01", "tanggal_selesai": "2026-09-11", "granularitas": "TAHUNAN"},
        headers=_header(token),
    )
    assert resp.status_code == 422


# ---------------------------------------------------------------------------
# Perbandingan kelas
# ---------------------------------------------------------------------------


def test_perbandingan_kelas_kepsek_melihat_semua_kelas(session_factory, session):
    id_kelas_a = _buat_kelas(session, nama_kelas="8A")
    id_kelas_b = _buat_kelas(session, nama_kelas="8B")
    siswa_a = _buat_siswa(session, id_kelas=id_kelas_a, nisn="1000000001", nama="Budi")
    siswa_b = _buat_siswa(session, id_kelas=id_kelas_b, nisn="1000000002", nama="Siti")
    _tambah_absensi(session, id_siswa=siswa_a, tanggal=date(2026, 9, 11), status="HADIR")
    _tambah_absensi(session, id_siswa=siswa_b, tanggal=date(2026, 9, 11), status="ALPA")

    client = _client(session_factory)
    token = _token(client, session, username="kepsek", peran=PeranPengguna.KEPALA_SEKOLAH)
    resp = client.get(
        "/dashboard/perbandingan-kelas",
        params={"tanggal_mulai": "2026-09-11", "tanggal_selesai": "2026-09-11"},
        headers=_header(token),
    )

    assert resp.status_code == 200, resp.text
    nama_kelas = {b["nama_kelas"] for b in resp.json()}
    assert nama_kelas == {"8A", "8B"}


def test_perbandingan_kelas_wali_kelas_hanya_satu_baris(session_factory, session):
    id_kelas_a = _buat_kelas(session, nama_kelas="8A")
    id_kelas_b = _buat_kelas(session, nama_kelas="8B")
    siswa_a = _buat_siswa(session, id_kelas=id_kelas_a, nisn="1000000001", nama="Budi")
    siswa_b = _buat_siswa(session, id_kelas=id_kelas_b, nisn="1000000002", nama="Siti")
    _tambah_absensi(session, id_siswa=siswa_a, tanggal=date(2026, 9, 11), status="HADIR")
    _tambah_absensi(session, id_siswa=siswa_b, tanggal=date(2026, 9, 11), status="HADIR")

    client = _client(session_factory)
    token = _token(
        client, session, username="wali_a", peran=PeranPengguna.WALI_KELAS, id_kelas_diampu=id_kelas_a
    )
    resp = client.get(
        "/dashboard/perbandingan-kelas",
        params={"tanggal_mulai": "2026-09-11", "tanggal_selesai": "2026-09-11"},
        headers=_header(token),
    )

    body = resp.json()
    assert len(body) == 1
    assert body[0]["nama_kelas"] == "8A"


# ---------------------------------------------------------------------------
# Alpa terbanyak
# ---------------------------------------------------------------------------


def test_alpa_terbanyak_urut_dan_limit(session_factory, session):
    id_kelas = _buat_kelas(session, nama_kelas="8A")
    for i in range(3):
        siswa = _buat_siswa(session, id_kelas=id_kelas, nisn=f"100000000{i}", nama=f"Siswa{i}")
        for hari in range(i + 1):
            _tambah_absensi(session, id_siswa=siswa, tanggal=date(2026, 9, 1 + hari), status="ALPA")

    client = _client(session_factory)
    token = _token(client, session, username="admin", peran=PeranPengguna.ADMIN)
    resp = client.get(
        "/dashboard/alpa-terbanyak",
        params={"tanggal_mulai": "2026-09-01", "tanggal_selesai": "2026-09-05", "limit": 2},
        headers=_header(token),
    )

    assert resp.status_code == 200, resp.text
    body = resp.json()
    assert len(body) == 2
    assert body[0]["nama_lengkap"] == "Siswa2"
    assert body[0]["jumlah_alpa"] == 3


# ---------------------------------------------------------------------------
# Status operasional
# ---------------------------------------------------------------------------


def test_status_operasional_membaca_jalur_klien_dan_backup_belum_tersedia(session_factory, session):
    AttendanceEventRepository(session).buat(
        uid_kartu="AABBCC", jenis_event=JenisEventScan.MASUK, jalur=JalurAlat.PCSC,
    )

    client = _client(session_factory)
    token_admin = _token(client, session, username="admin", peran=PeranPengguna.ADMIN)

    resp = client.get("/dashboard/status-operasional", headers=_header(token_admin))

    assert resp.status_code == 200, resp.text
    body = resp.json()
    assert any(j["jalur"] == "PCSC" for j in body["jalur"])
    assert any(k["username"] == "admin" for k in body["klien"])
    assert body["antrean_lobi"] == []
    assert body["pencadangan_terakhir"] is None


def test_status_operasional_membaca_telemetri_antrean_lobi(session_factory, session):
    client = _client(session_factory)
    token_operator = _token(client, session, username="operator_lobi", peran=PeranPengguna.OPERATOR)
    client.post(
        "/perangkat/telemetri", json={"station_id": "LOBI-1", "jumlah_pending": 5}, headers=_header(token_operator)
    )

    token_admin = _token(client, session, username="admin", peran=PeranPengguna.ADMIN)
    resp = client.get("/dashboard/status-operasional", headers=_header(token_admin))

    assert resp.status_code == 200, resp.text
    antrean = resp.json()["antrean_lobi"]
    assert len(antrean) == 1
    assert antrean[0]["station_id"] == "LOBI-1"
    assert antrean[0]["jumlah_pending"] == 5
