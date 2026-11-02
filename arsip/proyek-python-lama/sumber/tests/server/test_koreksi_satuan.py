"""Tes integrasi endpoint Koreksi Satuan (PRD §9.1, §10): alasan wajib,
audit lengkap (pelaku/waktu/nilai lama/nilai baru), lingkup peran
(kepsek/admin bebas, wali kelas kelasnya sendiri, operator ditolak), dan
undo. Tidak menyentuh app.core.engine/mesin_absensi - status_masuk tidak
pernah disentuh.
"""

from datetime import date

from fastapi.testclient import TestClient

from app.data.models.core import JenisKelamin
from app.data.models.enums import JenisPemilik
from app.data.models.system import PeranPengguna
from app.data.repositories.attendance import AttendanceRepository
from app.data.repositories.audit_log import AuditLogRepository
from app.data.repositories.kelas import KelasRepository
from app.data.repositories.student import StudentRepository
from app.data.repositories.user import UserRepository
from app.server.app import buat_app
from app.server.security import hash_password

TANGGAL = "2026-09-08"
TANGGAL_DATE = date(2026, 9, 8)


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


def _buat_siswa(session, *, id_kelas: int | None, nisn: str, nama: str) -> int:
    return StudentRepository(session).tambah(
        nisn=nisn, nama_lengkap=nama, id_kelas=id_kelas, jenis_kelamin=JenisKelamin.L,
        tempat_lahir="Pancarijang", tanggal_lahir=date(2012, 1, 1), alamat="Jl. Contoh No. 1",
    ).id


def _siapkan_dua_kelas(session):
    kelas_a = KelasRepository(session).tambah(tingkat=7, nama_kelas="7A", tahun_ajaran="2026/2027").id
    kelas_b = KelasRepository(session).tambah(tingkat=7, nama_kelas="7B", tahun_ajaran="2026/2027").id
    siswa_a = _buat_siswa(session, id_kelas=kelas_a, nisn="1000000001", nama="Budi (7A)")
    siswa_b = _buat_siswa(session, id_kelas=kelas_b, nisn="1000000002", nama="Siti (7B)")
    return {"kelas_a": kelas_a, "kelas_b": kelas_b, "siswa_a": siswa_a, "siswa_b": siswa_b}


def _buat_absensi_alpa(session, *, id_siswa: int) -> int:
    return AttendanceRepository(session).tambah(
        id_pemilik=id_siswa, jenis_pemilik=JenisPemilik.SISWA, tanggal=TANGGAL_DATE, status_masuk="ALPA"
    ).id


def _token_admin(client: TestClient, session) -> str:
    _buat_user(session, username="admin", password="rahasia123", peran=PeranPengguna.ADMIN)
    return _login(client, username="admin", password="rahasia123")


# ---------------------------------------------------------------------------
# Alasan wajib (§9.1)
# ---------------------------------------------------------------------------


def test_koreksi_tanpa_alasan_422(session_factory, session):
    data = _siapkan_dua_kelas(session)
    _buat_absensi_alpa(session, id_siswa=data["siswa_a"])
    client = _client(session_factory)
    token = _token_admin(client, session)

    resp = client.post(
        "/koreksi-satuan",
        json={"id_siswa": data["siswa_a"], "tanggal": TANGGAL, "status_baru": "IZIN"},
        headers=_header(token),
    )
    assert resp.status_code == 422


def test_koreksi_alasan_kosong_422(session_factory, session):
    data = _siapkan_dua_kelas(session)
    _buat_absensi_alpa(session, id_siswa=data["siswa_a"])
    client = _client(session_factory)
    token = _token_admin(client, session)

    resp = client.post(
        "/koreksi-satuan",
        json={"id_siswa": data["siswa_a"], "tanggal": TANGGAL, "status_baru": "IZIN", "alasan": ""},
        headers=_header(token),
    )
    assert resp.status_code == 422


# ---------------------------------------------------------------------------
# Koreksi berjalan, tercatat penuh (pelaku/waktu/nilai lama/nilai baru)
# ---------------------------------------------------------------------------


def test_koreksi_berhasil_dan_tercatat_penuh(session_factory, session):
    data = _siapkan_dua_kelas(session)
    _buat_absensi_alpa(session, id_siswa=data["siswa_a"])
    client = _client(session_factory)
    id_admin = None
    token = _token_admin(client, session)

    resp = client.post(
        "/koreksi-satuan",
        json={"id_siswa": data["siswa_a"], "tanggal": TANGGAL, "status_baru": "IZIN", "alasan": "Surat dokter"},
        headers=_header(token),
    )
    assert resp.status_code == 201, resp.text
    body = resp.json()
    assert body["nilai_sebelum"] == {"status_final": "ALPA", "alasan_final": None}
    assert body["nilai_sesudah"] == {"status_final": "IZIN", "alasan_final": "KOREKSI_MANUAL"}
    assert body["alasan"] == "Surat dokter"
    assert body["dibatalkan"] is False

    # status_masuk TIDAK disentuh - "kenyataan" scan tetap ALPA.
    resp_status = client.get(
        "/koreksi-satuan/status", params={"id_siswa": data["siswa_a"], "tanggal": TANGGAL}, headers=_header(token)
    )
    assert resp_status.json()["status_masuk_sekarang"] == "ALPA"
    assert resp_status.json()["status_final_sekarang"] == "IZIN"


def test_koreksi_status_sama_ditolak_400(session_factory, session):
    data = _siapkan_dua_kelas(session)
    _buat_absensi_alpa(session, id_siswa=data["siswa_a"])
    client = _client(session_factory)
    token = _token_admin(client, session)

    resp = client.post(
        "/koreksi-satuan",
        json={"id_siswa": data["siswa_a"], "tanggal": TANGGAL, "status_baru": "ALPA", "alasan": "Uji"},
        headers=_header(token),
    )
    assert resp.status_code == 400


def test_input_manual_izin_tanpa_baris_attendance_sebelumnya(session_factory, session):
    """PRD §10: input manual IZIN/SAKIT/DISPENSASI pada siswa yang belum
    pernah scan sama sekali."""
    data = _siapkan_dua_kelas(session)
    client = _client(session_factory)
    token = _token_admin(client, session)

    resp = client.post(
        "/koreksi-satuan",
        json={"id_siswa": data["siswa_a"], "tanggal": TANGGAL, "status_baru": "SAKIT", "alasan": "Sakit demam"},
        headers=_header(token),
    )
    assert resp.status_code == 201, resp.text
    assert resp.json()["nilai_sebelum"] == {"status_final": None, "alasan_final": None}
    assert resp.json()["nilai_sesudah"] == {"status_final": "SAKIT", "alasan_final": "KOREKSI_MANUAL"}


# ---------------------------------------------------------------------------
# Lingkup peran (§10) - inti slice ini
# ---------------------------------------------------------------------------


def test_operator_ditolak_403(session_factory, session):
    data = _siapkan_dua_kelas(session)
    _buat_absensi_alpa(session, id_siswa=data["siswa_a"])
    _buat_user(session, username="operator", password="rahasia123", peran=PeranPengguna.OPERATOR)
    client = _client(session_factory)
    token = _login(client, username="operator", password="rahasia123")

    resp = client.post(
        "/koreksi-satuan",
        json={"id_siswa": data["siswa_a"], "tanggal": TANGGAL, "status_baru": "IZIN", "alasan": "Uji"},
        headers=_header(token),
    )
    assert resp.status_code == 403


def test_kepsek_boleh_koreksi_siswa_kelas_manapun(session_factory, session):
    data = _siapkan_dua_kelas(session)
    _buat_absensi_alpa(session, id_siswa=data["siswa_b"])
    _buat_user(session, username="kepsek", password="rahasia123", peran=PeranPengguna.KEPALA_SEKOLAH)
    client = _client(session_factory)
    token = _login(client, username="kepsek", password="rahasia123")

    resp = client.post(
        "/koreksi-satuan",
        json={"id_siswa": data["siswa_b"], "tanggal": TANGGAL, "status_baru": "IZIN", "alasan": "Uji"},
        headers=_header(token),
    )
    assert resp.status_code == 201


def test_wali_kelas_boleh_koreksi_siswa_kelasnya_sendiri(session_factory, session):
    data = _siapkan_dua_kelas(session)
    _buat_absensi_alpa(session, id_siswa=data["siswa_a"])
    _buat_user(
        session, username="wali_a", password="rahasia123", peran=PeranPengguna.WALI_KELAS,
        id_kelas_diampu=data["kelas_a"],
    )
    client = _client(session_factory)
    token = _login(client, username="wali_a", password="rahasia123")

    resp = client.post(
        "/koreksi-satuan",
        json={"id_siswa": data["siswa_a"], "tanggal": TANGGAL, "status_baru": "IZIN", "alasan": "Uji"},
        headers=_header(token),
    )
    assert resp.status_code == 201, resp.text


def test_wali_kelas_ditolak_koreksi_siswa_kelas_lain(session_factory, session):
    data = _siapkan_dua_kelas(session)
    _buat_absensi_alpa(session, id_siswa=data["siswa_b"])
    _buat_user(
        session, username="wali_a", password="rahasia123", peran=PeranPengguna.WALI_KELAS,
        id_kelas_diampu=data["kelas_a"],
    )
    client = _client(session_factory)
    token = _login(client, username="wali_a", password="rahasia123")

    resp = client.post(
        "/koreksi-satuan",
        json={"id_siswa": data["siswa_b"], "tanggal": TANGGAL, "status_baru": "IZIN", "alasan": "Uji"},
        headers=_header(token),
    )
    assert resp.status_code == 403


def test_wali_kelas_tanpa_kelas_diampu_ditolak(session_factory, session):
    data = _siapkan_dua_kelas(session)
    _buat_absensi_alpa(session, id_siswa=data["siswa_a"])
    _buat_user(session, username="wali_tanpa_kelas", password="rahasia123", peran=PeranPengguna.WALI_KELAS)
    client = _client(session_factory)
    token = _login(client, username="wali_tanpa_kelas", password="rahasia123")

    resp = client.post(
        "/koreksi-satuan",
        json={"id_siswa": data["siswa_a"], "tanggal": TANGGAL, "status_baru": "IZIN", "alasan": "Uji"},
        headers=_header(token),
    )
    assert resp.status_code == 403


def test_wali_kelas_ditolak_melihat_status_kelas_lain(session_factory, session):
    data = _siapkan_dua_kelas(session)
    _buat_user(
        session, username="wali_a", password="rahasia123", peran=PeranPengguna.WALI_KELAS,
        id_kelas_diampu=data["kelas_a"],
    )
    client = _client(session_factory)
    token = _login(client, username="wali_a", password="rahasia123")

    resp = client.get(
        "/koreksi-satuan/status", params={"id_siswa": data["siswa_b"], "tanggal": TANGGAL}, headers=_header(token)
    )
    assert resp.status_code == 403


# ---------------------------------------------------------------------------
# Undo (§20)
# ---------------------------------------------------------------------------


def test_batalkan_mengembalikan_status_final_lama(session_factory, session):
    data = _siapkan_dua_kelas(session)
    _buat_absensi_alpa(session, id_siswa=data["siswa_a"])
    client = _client(session_factory)
    token = _token_admin(client, session)
    id_koreksi = client.post(
        "/koreksi-satuan",
        json={"id_siswa": data["siswa_a"], "tanggal": TANGGAL, "status_baru": "IZIN", "alasan": "Surat dokter"},
        headers=_header(token),
    ).json()["id"]

    resp = client.post(f"/koreksi-satuan/{id_koreksi}/batalkan", json={"alasan": "Surat ternyata palsu"}, headers=_header(token))
    assert resp.status_code == 200, resp.text
    assert resp.json()["dibatalkan"] is True

    resp_status = client.get(
        "/koreksi-satuan/status", params={"id_siswa": data["siswa_a"], "tanggal": TANGGAL}, headers=_header(token)
    )
    assert resp_status.json()["status_final_sekarang"] == "ALPA"  # kembali seperti semula


def test_batalkan_dua_kali_409(session_factory, session):
    data = _siapkan_dua_kelas(session)
    _buat_absensi_alpa(session, id_siswa=data["siswa_a"])
    client = _client(session_factory)
    token = _token_admin(client, session)
    id_koreksi = client.post(
        "/koreksi-satuan",
        json={"id_siswa": data["siswa_a"], "tanggal": TANGGAL, "status_baru": "IZIN", "alasan": "Uji"},
        headers=_header(token),
    ).json()["id"]
    client.post(f"/koreksi-satuan/{id_koreksi}/batalkan", json={"alasan": "Batal pertama"}, headers=_header(token))

    resp = client.post(f"/koreksi-satuan/{id_koreksi}/batalkan", json={"alasan": "Batal kedua"}, headers=_header(token))
    assert resp.status_code == 409


def test_batalkan_tidak_ditemukan_404(session_factory, session):
    client = _client(session_factory)
    token = _token_admin(client, session)

    resp = client.post("/koreksi-satuan/999/batalkan", json={"alasan": "Uji"}, headers=_header(token))
    assert resp.status_code == 404


def test_wali_kelas_ditolak_membatalkan_koreksi_kelas_lain(session_factory, session):
    data = _siapkan_dua_kelas(session)
    _buat_absensi_alpa(session, id_siswa=data["siswa_b"])
    client = _client(session_factory)
    token_admin = _token_admin(client, session)
    id_koreksi = client.post(
        "/koreksi-satuan",
        json={"id_siswa": data["siswa_b"], "tanggal": TANGGAL, "status_baru": "IZIN", "alasan": "Uji"},
        headers=_header(token_admin),
    ).json()["id"]

    _buat_user(
        session, username="wali_a", password="rahasia123", peran=PeranPengguna.WALI_KELAS,
        id_kelas_diampu=data["kelas_a"],
    )
    token_wali = _login(client, username="wali_a", password="rahasia123")

    resp = client.post(f"/koreksi-satuan/{id_koreksi}/batalkan", json={"alasan": "Uji"}, headers=_header(token_wali))
    assert resp.status_code == 403


# ---------------------------------------------------------------------------
# Riwayat & audit log
# ---------------------------------------------------------------------------


def test_riwayat_menampilkan_koreksi_terbaru_dulu(session_factory, session):
    data = _siapkan_dua_kelas(session)
    _buat_absensi_alpa(session, id_siswa=data["siswa_a"])
    client = _client(session_factory)
    token = _token_admin(client, session)
    client.post(
        "/koreksi-satuan",
        json={"id_siswa": data["siswa_a"], "tanggal": TANGGAL, "status_baru": "IZIN", "alasan": "Uji 1"},
        headers=_header(token),
    )
    client.post(
        "/koreksi-satuan",
        json={"id_siswa": data["siswa_a"], "tanggal": TANGGAL, "status_baru": "SAKIT", "alasan": "Uji 2"},
        headers=_header(token),
    )

    resp = client.get(
        "/koreksi-satuan", params={"id_siswa": data["siswa_a"], "tanggal": TANGGAL}, headers=_header(token)
    )
    assert resp.status_code == 200
    assert len(resp.json()) == 2
    assert resp.json()[0]["alasan"] == "Uji 2"


def test_koreksi_tercatat_di_audit_log(session_factory, session):
    data = _siapkan_dua_kelas(session)
    _buat_absensi_alpa(session, id_siswa=data["siswa_a"])
    client = _client(session_factory)
    token = _token_admin(client, session)

    client.post(
        "/koreksi-satuan",
        json={"id_siswa": data["siswa_a"], "tanggal": TANGGAL, "status_baru": "IZIN", "alasan": "Surat dokter"},
        headers=_header(token),
    )

    entri = AuditLogRepository(session).daftar_terbaru(limit=1)[0]
    assert entri.aksi == "koreksi_absensi"
    assert entri.alasan == "Surat dokter"
    assert entri.nilai_sesudah == {"status_final": "IZIN", "alasan_final": "KOREKSI_MANUAL"}
