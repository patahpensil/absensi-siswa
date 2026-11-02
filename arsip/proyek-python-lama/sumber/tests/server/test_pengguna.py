"""Tes integrasi endpoint Manajemen Akun & Peran (PRD §10): hierarki
wewenang pembuatan akun, reset sandi tanpa sandi lama, dan penetapan
wali kelas ke kelas. Tidak menyentuh app.core.engine/mesin_absensi.
"""

from fastapi.testclient import TestClient

from app.data.models.system import PeranPengguna
from app.data.repositories.kelas import KelasRepository
from app.data.repositories.user import UserRepository
from app.server.app import buat_app
from app.server.security import hash_password


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


def _token_kepsek(client: TestClient, session) -> str:
    _buat_user(session, username="kepsek", password="rahasia123", peran=PeranPengguna.KEPALA_SEKOLAH)
    return _login(client, username="kepsek", password="rahasia123")


def _token_admin(client: TestClient, session) -> str:
    _buat_user(session, username="admin", password="rahasia123", peran=PeranPengguna.ADMIN)
    return _login(client, username="admin", password="rahasia123")


# ---------------------------------------------------------------------------
# Hak akses dasar - hanya kepsek/admin yang boleh memanggil endpoint ini
# ---------------------------------------------------------------------------


def test_wali_kelas_ditolak_membuat_akun(session_factory, session):
    _buat_user(session, username="wali", password="rahasia123", peran=PeranPengguna.WALI_KELAS)
    client = _client(session_factory)
    token = _login(client, username="wali", password="rahasia123")

    resp = client.post(
        "/pengguna",
        json={"username": "baru", "password_awal": "rahasia123", "peran": "OPERATOR"},
        headers=_header(token),
    )
    assert resp.status_code == 403


# ---------------------------------------------------------------------------
# Hierarki wewenang pembuatan akun (PRD §10, BUKAN eksklusif)
# ---------------------------------------------------------------------------


def test_kepsek_boleh_membuat_semua_peran(session_factory, session):
    client = _client(session_factory)
    token = _token_kepsek(client, session)

    for peran in ("ADMIN", "KEPALA_SEKOLAH", "WALI_KELAS", "OPERATOR"):
        resp = client.post(
            "/pengguna",
            json={"username": f"user_{peran.lower()}", "password_awal": "rahasia123", "peran": peran},
            headers=_header(token),
        )
        assert resp.status_code == 201, f"{peran}: {resp.text}"


def test_admin_boleh_membuat_wali_kelas_dan_operator(session_factory, session):
    client = _client(session_factory)
    token = _token_admin(client, session)

    for peran in ("WALI_KELAS", "OPERATOR"):
        resp = client.post(
            "/pengguna",
            json={"username": f"user_{peran.lower()}", "password_awal": "rahasia123", "peran": peran},
            headers=_header(token),
        )
        assert resp.status_code == 201, f"{peran}: {resp.text}"


def test_admin_ditolak_membuat_akun_admin(session_factory, session):
    client = _client(session_factory)
    token = _token_admin(client, session)

    resp = client.post(
        "/pengguna", json={"username": "admin_baru", "password_awal": "rahasia123", "peran": "ADMIN"},
        headers=_header(token),
    )
    assert resp.status_code == 403


def test_admin_ditolak_membuat_akun_kepala_sekolah(session_factory, session):
    client = _client(session_factory)
    token = _token_admin(client, session)

    resp = client.post(
        "/pengguna", json={"username": "kepsek_baru", "password_awal": "rahasia123", "peran": "KEPALA_SEKOLAH"},
        headers=_header(token),
    )
    assert resp.status_code == 403


def test_username_kembar_ditolak_409(session_factory, session):
    client = _client(session_factory)
    token = _token_admin(client, session)
    client.post(
        "/pengguna", json={"username": "operator1", "password_awal": "rahasia123", "peran": "OPERATOR"},
        headers=_header(token),
    )

    resp = client.post(
        "/pengguna", json={"username": "operator1", "password_awal": "lainlagi123", "peran": "OPERATOR"},
        headers=_header(token),
    )
    assert resp.status_code == 409


def test_akun_baru_wajib_ganti_password(session_factory, session):
    client = _client(session_factory)
    token = _token_admin(client, session)

    resp = client.post(
        "/pengguna", json={"username": "operator1", "password_awal": "rahasia123", "peran": "OPERATOR"},
        headers=_header(token),
    )
    assert resp.json()["wajib_ganti_password"] is True


# ---------------------------------------------------------------------------
# Penetapan wali kelas ke kelas - menyambung ke data kelas (Slice 1)
# ---------------------------------------------------------------------------


def test_buat_akun_wali_kelas_dengan_kelas_diampu(session_factory, session):
    client = _client(session_factory)
    token = _token_admin(client, session)
    id_kelas = KelasRepository(session).tambah(tingkat=7, nama_kelas="7A", tahun_ajaran="2026/2027").id

    resp = client.post(
        "/pengguna",
        json={"username": "wali7a", "password_awal": "rahasia123", "peran": "WALI_KELAS", "id_kelas_diampu": id_kelas},
        headers=_header(token),
    )
    assert resp.status_code == 201, resp.text
    assert resp.json()["id_kelas_diampu"] == id_kelas


def test_id_kelas_diampu_untuk_peran_selain_wali_kelas_ditolak(session_factory, session):
    client = _client(session_factory)
    token = _token_admin(client, session)
    id_kelas = KelasRepository(session).tambah(tingkat=7, nama_kelas="7A", tahun_ajaran="2026/2027").id

    resp = client.post(
        "/pengguna",
        json={"username": "op1", "password_awal": "rahasia123", "peran": "OPERATOR", "id_kelas_diampu": id_kelas},
        headers=_header(token),
    )
    assert resp.status_code == 422


def test_id_kelas_diampu_tidak_ada_404(session_factory, session):
    client = _client(session_factory)
    token = _token_admin(client, session)

    resp = client.post(
        "/pengguna",
        json={"username": "wali1", "password_awal": "rahasia123", "peran": "WALI_KELAS", "id_kelas_diampu": 999},
        headers=_header(token),
    )
    assert resp.status_code == 404


def test_atur_kelas_diampu_memindahkan_wali_kelas(session_factory, session):
    client = _client(session_factory)
    token = _token_admin(client, session)
    kelas_a = KelasRepository(session).tambah(tingkat=7, nama_kelas="7A", tahun_ajaran="2026/2027").id
    kelas_b = KelasRepository(session).tambah(tingkat=7, nama_kelas="7B", tahun_ajaran="2026/2027").id
    id_wali = client.post(
        "/pengguna",
        json={"username": "wali7", "password_awal": "rahasia123", "peran": "WALI_KELAS", "id_kelas_diampu": kelas_a},
        headers=_header(token),
    ).json()["id"]

    resp = client.put(f"/pengguna/{id_wali}/kelas-diampu", json={"id_kelas_diampu": kelas_b}, headers=_header(token))
    assert resp.status_code == 200, resp.text
    assert resp.json()["id_kelas_diampu"] == kelas_b


def test_atur_kelas_diampu_untuk_operator_ditolak_422(session_factory, session):
    client = _client(session_factory)
    token = _token_admin(client, session)
    id_operator = client.post(
        "/pengguna", json={"username": "op1", "password_awal": "rahasia123", "peran": "OPERATOR"},
        headers=_header(token),
    ).json()["id"]

    resp = client.put(f"/pengguna/{id_operator}/kelas-diampu", json={"id_kelas_diampu": 1}, headers=_header(token))
    assert resp.status_code == 422


# ---------------------------------------------------------------------------
# Reset sandi - tanpa sandi lama, wajib ganti di login berikutnya, sesi lama dicabut
# ---------------------------------------------------------------------------


def test_reset_password_tidak_perlu_sandi_lama(session_factory, session):
    client = _client(session_factory)
    token = _token_admin(client, session)
    id_operator = client.post(
        "/pengguna", json={"username": "op1", "password_awal": "rahasia123", "peran": "OPERATOR"},
        headers=_header(token),
    ).json()["id"]

    resp = client.post(f"/pengguna/{id_operator}/reset-password", json={"password_baru": "sandibaru456"}, headers=_header(token))
    assert resp.status_code == 200, resp.text
    assert resp.json()["wajib_ganti_password"] is True

    resp_login = client.post("/auth/login", json={"username": "op1", "password": "sandibaru456"})
    assert resp_login.status_code == 200


def test_reset_password_mencabut_sesi_lama(session_factory, session):
    client = _client(session_factory)
    token_admin = _token_admin(client, session)
    id_operator = client.post(
        "/pengguna", json={"username": "op1", "password_awal": "rahasia123", "peran": "OPERATOR"},
        headers=_header(token_admin),
    ).json()["id"]
    token_operator = _login(client, username="op1", password="rahasia123")

    client.post(f"/pengguna/{id_operator}/reset-password", json={"password_baru": "sandibaru456"}, headers=_header(token_admin))

    # Token lama sudah dicabut - endpoint terproteksi apa pun harus 401.
    resp = client.post("/auth/logout", headers=_header(token_operator))
    # logout selalu 204 (tidak memeriksa validitas token secara eksplisit) -
    # pakai endpoint yang benar-benar memeriksa token sebagai bukti.
    resp_scan = client.post(
        "/scan",
        json={"uid_kartu": "X", "uid_mentah": "X", "jalur": "SIMULATOR", "waktu_scan": "2026-09-08T07:10:00"},
        headers=_header(token_operator),
    )
    assert resp_scan.status_code == 401


def test_admin_ditolak_mereset_password_kepala_sekolah(session_factory, session):
    client = _client(session_factory)
    token_admin = _token_admin(client, session)
    id_kepsek = _buat_user(session, username="kepsek", password="rahasia123", peran=PeranPengguna.KEPALA_SEKOLAH)

    resp = client.post(
        f"/pengguna/{id_kepsek}/reset-password", json={"password_baru": "sandibaru456"}, headers=_header(token_admin)
    )
    assert resp.status_code == 403


def test_kepsek_boleh_mereset_password_admin(session_factory, session):
    client = _client(session_factory)
    token_kepsek = _token_kepsek(client, session)
    id_admin = _buat_user(session, username="admin", password="rahasia123", peran=PeranPengguna.ADMIN)

    resp = client.post(
        f"/pengguna/{id_admin}/reset-password", json={"password_baru": "sandibaru456"}, headers=_header(token_kepsek)
    )
    assert resp.status_code == 200


def test_reset_password_akun_tidak_ada_404(session_factory, session):
    client = _client(session_factory)
    token = _token_admin(client, session)

    resp = client.post("/pengguna/999/reset-password", json={"password_baru": "sandibaru456"}, headers=_header(token))
    assert resp.status_code == 404


# ---------------------------------------------------------------------------
# Log audit - seluruh tindakan tercatat, termasuk milik kepala sekolah
# ---------------------------------------------------------------------------


def test_buat_akun_oleh_kepsek_tercatat_di_audit_log(session_factory, session):
    from app.data.repositories.audit_log import AuditLogRepository

    client = _client(session_factory)
    token = _token_kepsek(client, session)

    client.post("/pengguna", json={"username": "admin_baru", "password_awal": "rahasia123", "peran": "ADMIN"}, headers=_header(token))

    entri = AuditLogRepository(session).daftar_terbaru(limit=1)[0]
    assert entri.aksi == "buat_akun"
    assert entri.nilai_sesudah["username"] == "admin_baru"


def test_daftar_akun(session_factory, session):
    client = _client(session_factory)
    token = _token_admin(client, session)
    client.post("/pengguna", json={"username": "op1", "password_awal": "rahasia123", "peran": "OPERATOR"}, headers=_header(token))

    resp = client.get("/pengguna", headers=_header(token))
    assert resp.status_code == 200
    usernames = {u["username"] for u in resp.json()}
    assert "admin" in usernames
    assert "op1" in usernames
