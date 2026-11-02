"""Tes integrasi endpoint data master: siswa, guru, kelas, dan siklus
hidup kartu RFID (PRD §11, §12) - lewat FastAPI TestClient sungguhan,
membuktikan router, dependency wajib_peran, dan validasi Pydantic
benar-benar tersambung. Tidak menyentuh app.core.engine/mesin_absensi -
pendaftaran kartu di sini murni mengikat UID<->pemilik.
"""

from datetime import date

from fastapi.testclient import TestClient

from app.data.models.core import JenisKelamin
from app.data.models.system import PeranPengguna
from app.data.repositories.kelas import KelasRepository
from app.data.repositories.student import StudentRepository
from app.data.repositories.teacher import TeacherRepository
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


SISWA_VALID = {
    "nisn": "1234567890",
    "nama_lengkap": "Budi Santoso",
    "jenis_kelamin": "L",
    "tempat_lahir": "Pancarijang",
    "tanggal_lahir": "2012-05-01",
    "alamat": "Jl. Contoh No. 1",
    "nama_ortu": "Slamet",
    "nomor_whatsapp_ortu": "081234567890",
    "nik": "1234567890123456",
}


# ---------------------------------------------------------------------------
# Hak akses - kepala sekolah/admin saja (§10)
# ---------------------------------------------------------------------------


def test_siswa_ditolak_untuk_operator(session_factory, session):
    _buat_user(session, username="operator", password="rahasia123", peran=PeranPengguna.OPERATOR)
    client = _client(session_factory)
    token = _login(client, username="operator", password="rahasia123")

    resp = client.post("/siswa", json=SISWA_VALID, headers=_header(token))
    assert resp.status_code == 403


def test_siswa_ditolak_untuk_wali_kelas(session_factory, session):
    _buat_user(session, username="wali", password="rahasia123", peran=PeranPengguna.WALI_KELAS)
    client = _client(session_factory)
    token = _login(client, username="wali", password="rahasia123")

    resp = client.get("/siswa", headers=_header(token))
    assert resp.status_code == 403


# ---------------------------------------------------------------------------
# Siswa
# ---------------------------------------------------------------------------


def test_buat_siswa_berhasil_dan_nik_tersamar_di_daftar(session_factory, session):
    client = _client(session_factory)
    token = _token_admin(client, session)

    resp = client.post("/siswa", json=SISWA_VALID, headers=_header(token))
    assert resp.status_code == 201, resp.text
    body = resp.json()
    assert body["nisn"] == "1234567890"
    assert body["nama_ortu"] == "Slamet"
    assert body["nomor_whatsapp_ortu"] == "+6281234567890"
    # Endpoint tunggal (baru dibuat) mengembalikan NIK utuh (§11.1: "utuh
    # ketika baris sedang diedit").
    assert body["nik"] == "1234567890123456"

    resp_daftar = client.get("/siswa", headers=_header(token))
    assert resp_daftar.status_code == 200
    daftar = resp_daftar.json()
    assert len(daftar) == 1
    assert daftar[0]["nik_tersamar"] == "************3456"
    assert "nik" not in daftar[0]


def test_buat_siswa_nisn_kembar_ditolak_409(session_factory, session):
    client = _client(session_factory)
    token = _token_admin(client, session)
    client.post("/siswa", json=SISWA_VALID, headers=_header(token))

    duplikat = dict(SISWA_VALID, nik="6543210987654321")
    resp = client.post("/siswa", json=duplikat, headers=_header(token))
    assert resp.status_code == 409


def test_buat_siswa_nisn_bukan_10_digit_ditolak_422(session_factory, session):
    client = _client(session_factory)
    token = _token_admin(client, session)

    invalid = dict(SISWA_VALID, nisn="123")
    resp = client.post("/siswa", json=invalid, headers=_header(token))
    assert resp.status_code == 422


def test_buat_siswa_nik_bukan_16_digit_ditolak_422(session_factory, session):
    client = _client(session_factory)
    token = _token_admin(client, session)

    invalid = dict(SISWA_VALID, nik="123")
    resp = client.post("/siswa", json=invalid, headers=_header(token))
    assert resp.status_code == 422


def test_buat_siswa_tanpa_kelas_masuk_belum_ditempatkan(session_factory, session):
    """§11.3: siswa tanpa kelas tetap tercatat normal - id_kelas nullable."""
    client = _client(session_factory)
    token = _token_admin(client, session)

    resp = client.post("/siswa", json=SISWA_VALID, headers=_header(token))
    assert resp.status_code == 201
    assert resp.json()["id_kelas"] is None
    assert resp.json()["nama_kelas"] is None


def test_ubah_siswa_tidak_mengubah_nisn(session_factory, session):
    client = _client(session_factory)
    token = _token_admin(client, session)
    id_siswa = client.post("/siswa", json=SISWA_VALID, headers=_header(token)).json()["id"]

    perubahan = dict(SISWA_VALID)
    del perubahan["nisn"]
    perubahan["nama_lengkap"] = "Budi Santoso Wibowo"
    resp = client.put(f"/siswa/{id_siswa}", json=perubahan, headers=_header(token))
    assert resp.status_code == 200, resp.text
    assert resp.json()["nama_lengkap"] == "Budi Santoso Wibowo"
    assert resp.json()["nisn"] == "1234567890"


def test_buat_siswa_kelas_tidak_ada_404(session_factory, session):
    client = _client(session_factory)
    token = _token_admin(client, session)

    payload = dict(SISWA_VALID, id_kelas=999)
    resp = client.post("/siswa", json=payload, headers=_header(token))
    assert resp.status_code == 404


# ---------------------------------------------------------------------------
# Guru
# ---------------------------------------------------------------------------


def test_buat_guru_berhasil(session_factory, session):
    client = _client(session_factory)
    token = _token_admin(client, session)

    resp = client.post(
        "/guru",
        json={"nama_lengkap": "Pak Anwar", "nip": "198001012001121001", "pangkat_jabatan": "Pembina, IV/a"},
        headers=_header(token),
    )
    assert resp.status_code == 201, resp.text
    assert resp.json()["pangkat_jabatan"] == "Pembina, IV/a"


def test_buat_guru_nip_kembar_ditolak_409(session_factory, session):
    client = _client(session_factory)
    token = _token_admin(client, session)
    client.post("/guru", json={"nama_lengkap": "Pak Anwar", "nip": "111"}, headers=_header(token))

    resp = client.post("/guru", json={"nama_lengkap": "Bu Sri", "nip": "111"}, headers=_header(token))
    assert resp.status_code == 409


def test_buat_guru_honor_tanpa_nip(session_factory, session):
    client = _client(session_factory)
    token = _token_admin(client, session)

    resp = client.post("/guru", json={"nama_lengkap": "Guru Honor"}, headers=_header(token))
    assert resp.status_code == 201
    assert resp.json()["nip"] is None


# ---------------------------------------------------------------------------
# Kelas
# ---------------------------------------------------------------------------


def test_buat_kelas_dengan_wali_kelas(session_factory, session):
    client = _client(session_factory)
    token = _token_admin(client, session)
    id_guru = TeacherRepository(session).tambah(nama_lengkap="Bu Sri").id

    resp = client.post(
        "/kelas",
        json={"tingkat": 7, "nama_kelas": "VII.PASCAL", "tahun_ajaran": "2026/2027", "wali_kelas_id": id_guru},
        headers=_header(token),
    )
    assert resp.status_code == 201, resp.text
    assert resp.json()["nama_wali_kelas"] == "Bu Sri"


def test_buat_kelas_wali_tidak_ada_404(session_factory, session):
    client = _client(session_factory)
    token = _token_admin(client, session)

    resp = client.post(
        "/kelas",
        json={"tingkat": 7, "nama_kelas": "VII.PASCAL", "tahun_ajaran": "2026/2027", "wali_kelas_id": 999},
        headers=_header(token),
    )
    assert resp.status_code == 404


def test_buat_kelas_tanpa_wali_boleh(session_factory, session):
    client = _client(session_factory)
    token = _token_admin(client, session)

    resp = client.post(
        "/kelas", json={"tingkat": 7, "nama_kelas": "VII.PASCAL", "tahun_ajaran": "2026/2027"}, headers=_header(token)
    )
    assert resp.status_code == 201
    assert resp.json()["wali_kelas_id"] is None


def test_arsipkan_kelas_kosong_dihapus_fisik(session_factory, session):
    client = _client(session_factory)
    token = _token_admin(client, session)
    id_kelas = KelasRepository(session).tambah(tingkat=7, nama_kelas="7A", tahun_ajaran="2026/2027").id

    resp = client.delete(f"/kelas/{id_kelas}", headers=_header(token))
    assert resp.status_code == 204
    assert client.get(f"/kelas/{id_kelas}", headers=_header(token)).status_code == 404


def test_arsipkan_kelas_berisi_siswa_aktif_ditandai_tidak_aktif(session_factory, session):
    client = _client(session_factory)
    token = _token_admin(client, session)
    id_kelas = KelasRepository(session).tambah(tingkat=7, nama_kelas="7A", tahun_ajaran="2026/2027").id
    payload = dict(SISWA_VALID, id_kelas=id_kelas)
    client.post("/siswa", json=payload, headers=_header(token))

    resp = client.delete(f"/kelas/{id_kelas}", headers=_header(token))
    assert resp.status_code == 204

    resp_detail = client.get(f"/kelas/{id_kelas}", headers=_header(token))
    assert resp_detail.status_code == 200
    assert resp_detail.json()["aktif"] is False


# ---------------------------------------------------------------------------
# Kartu RFID
# ---------------------------------------------------------------------------


def _buat_siswa_langsung(session, *, nisn: str = "1234567890") -> int:
    return StudentRepository(session).tambah(
        nisn=nisn,
        nama_lengkap="Budi",
        jenis_kelamin=JenisKelamin.L,
        tempat_lahir="Pancarijang",
        tanggal_lahir=date(2012, 1, 1),
        alamat="Jl. Contoh No. 1",
    ).id


def test_terbitkan_kartu_untuk_siswa(session_factory, session):
    client = _client(session_factory)
    token = _token_admin(client, session)
    id_siswa = _buat_siswa_langsung(session)

    resp = client.post(
        "/kartu",
        json={"uid_kartu": "04A2B3C4", "uid_mentah": "04:a2:b3:c4", "id_pemilik": id_siswa, "jenis_pemilik": "SISWA"},
        headers=_header(token),
    )
    assert resp.status_code == 201, resp.text
    body = resp.json()
    assert body["status"] == "AKTIF"
    assert body["uid_kartu"] == "04A2B3C4"


def test_terbitkan_kartu_pemilik_tidak_ada_404(session_factory, session):
    client = _client(session_factory)
    token = _token_admin(client, session)

    resp = client.post(
        "/kartu",
        json={"uid_kartu": "04A2B3C4", "uid_mentah": "04A2B3C4", "id_pemilik": 999, "jenis_pemilik": "SISWA"},
        headers=_header(token),
    )
    assert resp.status_code == 404


def test_terbitkan_kartu_uid_kembar_ditolak_409(session_factory, session):
    client = _client(session_factory)
    token = _token_admin(client, session)
    id_siswa1 = _buat_siswa_langsung(session, nisn="1111111111")
    id_siswa2 = _buat_siswa_langsung(session, nisn="2222222222")
    client.post(
        "/kartu",
        json={"uid_kartu": "SAMA", "uid_mentah": "SAMA", "id_pemilik": id_siswa1, "jenis_pemilik": "SISWA"},
        headers=_header(token),
    )

    resp = client.post(
        "/kartu",
        json={"uid_kartu": "SAMA", "uid_mentah": "SAMA", "id_pemilik": id_siswa2, "jenis_pemilik": "SISWA"},
        headers=_header(token),
    )
    assert resp.status_code == 409


def test_nonaktifkan_kartu(session_factory, session):
    client = _client(session_factory)
    token = _token_admin(client, session)
    id_siswa = _buat_siswa_langsung(session)
    id_kartu = client.post(
        "/kartu",
        json={"uid_kartu": "04A2B3C4", "uid_mentah": "04A2B3C4", "id_pemilik": id_siswa, "jenis_pemilik": "SISWA"},
        headers=_header(token),
    ).json()["id"]

    resp = client.post(f"/kartu/{id_kartu}/nonaktifkan", json={"alasan": "Kartu hilang"}, headers=_header(token))
    assert resp.status_code == 200
    assert resp.json()["status"] == "NONAKTIF"


def test_cetak_ulang_kartu_kartu_lama_diganti_kartu_baru_aktif(session_factory, session):
    client = _client(session_factory)
    token = _token_admin(client, session)
    id_siswa = _buat_siswa_langsung(session)
    id_kartu_lama = client.post(
        "/kartu",
        json={"uid_kartu": "LAMA", "uid_mentah": "LAMA", "id_pemilik": id_siswa, "jenis_pemilik": "SISWA"},
        headers=_header(token),
    ).json()["id"]

    resp = client.post(
        f"/kartu/{id_kartu_lama}/cetak-ulang",
        json={"uid_kartu_baru": "BARU", "uid_mentah_baru": "BARU", "alasan": "Kartu rusak"},
        headers=_header(token),
    )
    assert resp.status_code == 200, resp.text
    baru = resp.json()
    assert baru["uid_kartu"] == "BARU"
    assert baru["status"] == "AKTIF"

    daftar = client.get("/kartu/pemilik/SISWA/" + str(id_siswa), headers=_header(token)).json()
    status_by_uid = {k["uid_kartu"]: k["status"] for k in daftar}
    assert status_by_uid["LAMA"] == "DIGANTI"
    assert status_by_uid["BARU"] == "AKTIF"
