"""Tes integrasi endpoint Koreksi Guru (PRD §6): status manual
DINAS_LUAR/IZIN/SAKIT/CUTI + lampiran, lingkup peran (hanya kepsek/
admin - TIDAK ADA pengecualian wali kelas, beda dari koreksi satuan
siswa), dan undo. Tidak menyentuh app.core.engine/mesin_absensi -
status_masuk (toleransi 09:30) tidak pernah disentuh.
"""

from datetime import date

from fastapi.testclient import TestClient

from app.data.models.enums import JenisPemilik
from app.data.models.system import PeranPengguna
from app.data.repositories.attendance import AttendanceRepository
from app.data.repositories.teacher import TeacherRepository
from app.data.repositories.user import UserRepository
from app.server.app import buat_app
from app.server.security import hash_password

TANGGAL = "2026-09-08"
TANGGAL_DATE = date(2026, 9, 8)


def _client(session_factory, tmp_path) -> TestClient:
    return TestClient(buat_app(session_factory, lampiran_dir=tmp_path))


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


def _token_admin(client: TestClient, session) -> tuple[str, int]:
    id_admin = _buat_user(session, username="admin", password="rahasia123", peran=PeranPengguna.ADMIN)
    return _login(client, username="admin", password="rahasia123"), id_admin


def _buat_guru(session, *, nama: str = "Pak Budi") -> int:
    return TeacherRepository(session).tambah(nama_lengkap=nama).id


def _buat_absensi_hadir(session, *, id_guru: int) -> int:
    return AttendanceRepository(session).tambah(
        id_pemilik=id_guru, jenis_pemilik=JenisPemilik.GURU, tanggal=TANGGAL_DATE, status_masuk="HADIR"
    ).id


def _form(**kwargs) -> dict:
    dasar = {"id_guru": None, "tanggal": TANGGAL, "status_baru": "SAKIT", "alasan": "Uji"}
    dasar.update(kwargs)
    return {k: str(v) for k, v in dasar.items() if v is not None}


# ---------------------------------------------------------------------------
# Lingkup peran (§6) - hanya kepsek/admin, TIDAK ADA wali kelas
# ---------------------------------------------------------------------------


def test_wali_kelas_ditolak_403(session_factory, session, tmp_path):
    id_guru = _buat_guru(session)
    _buat_user(session, username="wali_a", password="rahasia123", peran=PeranPengguna.WALI_KELAS)
    client = _client(session_factory, tmp_path)
    token = _login(client, username="wali_a", password="rahasia123")

    resp = client.post("/koreksi-guru", data=_form(id_guru=id_guru), headers=_header(token))
    assert resp.status_code == 403


def test_operator_ditolak_403(session_factory, session, tmp_path):
    id_guru = _buat_guru(session)
    _buat_user(session, username="operator", password="rahasia123", peran=PeranPengguna.OPERATOR)
    client = _client(session_factory, tmp_path)
    token = _login(client, username="operator", password="rahasia123")

    resp = client.get("/koreksi-guru", params={"id_guru": id_guru, "tanggal": TANGGAL}, headers=_header(token))
    assert resp.status_code == 403


def test_kepsek_boleh_menjalankan(session_factory, session, tmp_path):
    id_guru = _buat_guru(session)
    _buat_absensi_hadir(session, id_guru=id_guru)
    _buat_user(session, username="kepsek", password="rahasia123", peran=PeranPengguna.KEPALA_SEKOLAH)
    client = _client(session_factory, tmp_path)
    token = _login(client, username="kepsek", password="rahasia123")

    resp = client.post("/koreksi-guru", data=_form(id_guru=id_guru), headers=_header(token))
    assert resp.status_code == 201, resp.text


# ---------------------------------------------------------------------------
# Status saat ini
# ---------------------------------------------------------------------------


def test_status_saat_ini_guru_belum_pernah_scan(session_factory, session, tmp_path):
    id_guru = _buat_guru(session)
    client = _client(session_factory, tmp_path)
    token, _ = _token_admin(client, session)

    resp = client.get("/koreksi-guru/status", params={"id_guru": id_guru, "tanggal": TANGGAL}, headers=_header(token))
    assert resp.status_code == 200, resp.text
    body = resp.json()
    assert body["id_absensi"] is None
    assert body["status_masuk_sekarang"] is None


def test_guru_tidak_ditemukan_404(session_factory, session, tmp_path):
    client = _client(session_factory, tmp_path)
    token, _ = _token_admin(client, session)

    resp = client.get("/koreksi-guru/status", params={"id_guru": 999, "tanggal": TANGGAL}, headers=_header(token))
    assert resp.status_code == 404


# ---------------------------------------------------------------------------
# Buat koreksi - tanpa lampiran
# ---------------------------------------------------------------------------


def test_koreksi_tanpa_lampiran_tercatat_penuh(session_factory, session, tmp_path):
    id_guru = _buat_guru(session)
    _buat_absensi_hadir(session, id_guru=id_guru)
    client = _client(session_factory, tmp_path)
    token, id_admin = _token_admin(client, session)

    resp = client.post(
        "/koreksi-guru", data=_form(id_guru=id_guru, status_baru="DINAS_LUAR", alasan="Diklat kurikulum"),
        headers=_header(token),
    )
    assert resp.status_code == 201, resp.text
    body = resp.json()
    assert body["id_pengguna"] == id_admin
    assert body["nilai_sebelum"] == {"status_final": "HADIR", "alasan_final": None}
    assert body["nilai_sesudah"] == {"status_final": "DINAS_LUAR", "alasan_final": "KOREKSI_MANUAL"}
    assert body["lampiran"] is None
    assert body["dibatalkan"] is False

    # status_masuk (toleransi 09:30) TIDAK disentuh.
    resp_status = client.get(
        "/koreksi-guru/status", params={"id_guru": id_guru, "tanggal": TANGGAL}, headers=_header(token)
    )
    assert resp_status.json()["status_masuk_sekarang"] == "HADIR"
    assert resp_status.json()["status_final_sekarang"] == "DINAS_LUAR"


def test_koreksi_guru_belum_pernah_scan(session_factory, session, tmp_path):
    """PRD §6: input manual pada guru yang belum sempat scan sama sekali."""
    id_guru = _buat_guru(session)
    client = _client(session_factory, tmp_path)
    token, _ = _token_admin(client, session)

    resp = client.post(
        "/koreksi-guru", data=_form(id_guru=id_guru, status_baru="CUTI", alasan="Cuti melahirkan"),
        headers=_header(token),
    )
    assert resp.status_code == 201, resp.text
    assert resp.json()["nilai_sebelum"] == {"status_final": None, "alasan_final": None}
    assert resp.json()["nilai_sesudah"] == {"status_final": "CUTI", "alasan_final": "KOREKSI_MANUAL"}


def test_koreksi_ke_status_final_yang_sama_400(session_factory, session, tmp_path):
    id_guru = _buat_guru(session)
    AttendanceRepository(session).tambah(
        id_pemilik=id_guru, jenis_pemilik=JenisPemilik.GURU, tanggal=TANGGAL_DATE, status_masuk="SAKIT"
    )
    client = _client(session_factory, tmp_path)
    token, _ = _token_admin(client, session)

    resp = client.post(
        "/koreksi-guru", data=_form(id_guru=id_guru, status_baru="SAKIT", alasan="Uji"), headers=_header(token)
    )
    assert resp.status_code == 400


def test_alasan_kosong_422(session_factory, session, tmp_path):
    id_guru = _buat_guru(session)
    _buat_absensi_hadir(session, id_guru=id_guru)
    client = _client(session_factory, tmp_path)
    token, _ = _token_admin(client, session)

    resp = client.post(
        "/koreksi-guru", data=_form(id_guru=id_guru, alasan=""), headers=_header(token)
    )
    assert resp.status_code == 422


# ---------------------------------------------------------------------------
# Status manual dibatasi ke DINAS_LUAR/IZIN/SAKIT/CUTI (§6)
# ---------------------------------------------------------------------------


def test_hadir_ditolak_sebagai_status_manual_422(session_factory, session, tmp_path):
    id_guru = _buat_guru(session)
    client = _client(session_factory, tmp_path)
    token, _ = _token_admin(client, session)

    resp = client.post(
        "/koreksi-guru", data=_form(id_guru=id_guru, status_baru="HADIR", alasan="Uji"), headers=_header(token)
    )
    assert resp.status_code == 422


def test_terlambat_ditolak_sebagai_status_manual_422(session_factory, session, tmp_path):
    id_guru = _buat_guru(session)
    client = _client(session_factory, tmp_path)
    token, _ = _token_admin(client, session)

    resp = client.post(
        "/koreksi-guru", data=_form(id_guru=id_guru, status_baru="TERLAMBAT", alasan="Uji"), headers=_header(token)
    )
    assert resp.status_code == 422


# ---------------------------------------------------------------------------
# Lampiran
# ---------------------------------------------------------------------------


def test_koreksi_dengan_lampiran_tersimpan_dan_bisa_diunduh(session_factory, session, tmp_path):
    id_guru = _buat_guru(session)
    _buat_absensi_hadir(session, id_guru=id_guru)
    client = _client(session_factory, tmp_path)
    token, _ = _token_admin(client, session)

    resp = client.post(
        "/koreksi-guru",
        data=_form(id_guru=id_guru, status_baru="SAKIT", alasan="Sakit demam, lampiran surat dokter"),
        files={"lampiran": ("surat.jpg", b"isi-foto-surat", "image/jpeg")},
        headers=_header(token),
    )
    assert resp.status_code == 201, resp.text
    body = resp.json()
    assert body["lampiran"] is not None
    assert body["lampiran"].startswith("koreksi_guru/")

    resp_unduh = client.get(f"/koreksi-guru/{body['id']}/lampiran", headers=_header(token))
    assert resp_unduh.status_code == 200
    assert resp_unduh.content == b"isi-foto-surat"
    assert resp_unduh.headers["content-type"] == "image/jpeg"


def test_lampiran_ekstensi_tidak_didukung_422(session_factory, session, tmp_path):
    id_guru = _buat_guru(session)
    _buat_absensi_hadir(session, id_guru=id_guru)
    client = _client(session_factory, tmp_path)
    token, _ = _token_admin(client, session)

    resp = client.post(
        "/koreksi-guru",
        data=_form(id_guru=id_guru, status_baru="SAKIT", alasan="Uji"),
        files={"lampiran": ("video.mp4", b"isi", "video/mp4")},
        headers=_header(token),
    )
    assert resp.status_code == 422


def test_unduh_lampiran_tidak_ada_404(session_factory, session, tmp_path):
    id_guru = _buat_guru(session)
    _buat_absensi_hadir(session, id_guru=id_guru)
    client = _client(session_factory, tmp_path)
    token, _ = _token_admin(client, session)
    id_koreksi = client.post(
        "/koreksi-guru", data=_form(id_guru=id_guru, status_baru="SAKIT", alasan="Uji"), headers=_header(token)
    ).json()["id"]

    resp = client.get(f"/koreksi-guru/{id_koreksi}/lampiran", headers=_header(token))
    assert resp.status_code == 404


# ---------------------------------------------------------------------------
# Batalkan (undo)
# ---------------------------------------------------------------------------


def test_batalkan_mengembalikan_status_final_lama(session_factory, session, tmp_path):
    id_guru = _buat_guru(session)
    _buat_absensi_hadir(session, id_guru=id_guru)
    client = _client(session_factory, tmp_path)
    token, _ = _token_admin(client, session)
    id_koreksi = client.post(
        "/koreksi-guru", data=_form(id_guru=id_guru, status_baru="CUTI", alasan="Uji"), headers=_header(token)
    ).json()["id"]

    resp = client.post(f"/koreksi-guru/{id_koreksi}/batalkan", json={"alasan": "Salah input"}, headers=_header(token))
    assert resp.status_code == 200, resp.text
    assert resp.json()["dibatalkan"] is True

    resp_status = client.get(
        "/koreksi-guru/status", params={"id_guru": id_guru, "tanggal": TANGGAL}, headers=_header(token)
    )
    assert resp_status.json()["status_final_sekarang"] == "HADIR"  # kembali seperti semula


def test_batalkan_dua_kali_409(session_factory, session, tmp_path):
    id_guru = _buat_guru(session)
    _buat_absensi_hadir(session, id_guru=id_guru)
    client = _client(session_factory, tmp_path)
    token, _ = _token_admin(client, session)
    id_koreksi = client.post(
        "/koreksi-guru", data=_form(id_guru=id_guru, status_baru="CUTI", alasan="Uji"), headers=_header(token)
    ).json()["id"]
    client.post(f"/koreksi-guru/{id_koreksi}/batalkan", json={"alasan": "Batal pertama"}, headers=_header(token))

    resp = client.post(f"/koreksi-guru/{id_koreksi}/batalkan", json={"alasan": "Batal kedua"}, headers=_header(token))
    assert resp.status_code == 409


def test_batalkan_tidak_ditemukan_404(session_factory, session, tmp_path):
    client = _client(session_factory, tmp_path)
    token, _ = _token_admin(client, session)

    resp = client.post("/koreksi-guru/999/batalkan", json={"alasan": "Uji"}, headers=_header(token))
    assert resp.status_code == 404


# ---------------------------------------------------------------------------
# Riwayat & audit log
# ---------------------------------------------------------------------------


def test_riwayat_menampilkan_koreksi_terbaru_dulu(session_factory, session, tmp_path):
    id_guru = _buat_guru(session)
    _buat_absensi_hadir(session, id_guru=id_guru)
    client = _client(session_factory, tmp_path)
    token, _ = _token_admin(client, session)
    client.post(
        "/koreksi-guru", data=_form(id_guru=id_guru, status_baru="SAKIT", alasan="Uji 1"), headers=_header(token)
    )
    client.post(
        "/koreksi-guru", data=_form(id_guru=id_guru, status_baru="CUTI", alasan="Uji 2"), headers=_header(token)
    )

    resp = client.get("/koreksi-guru", params={"id_guru": id_guru, "tanggal": TANGGAL}, headers=_header(token))
    assert resp.status_code == 200
    assert len(resp.json()) == 2
    assert resp.json()[0]["alasan"] == "Uji 2"


def test_batalkan_tercatat_di_audit_log(session_factory, session, tmp_path):
    from app.data.repositories.audit_log import AuditLogRepository

    id_guru = _buat_guru(session)
    _buat_absensi_hadir(session, id_guru=id_guru)
    client = _client(session_factory, tmp_path)
    token, _ = _token_admin(client, session)
    id_koreksi = client.post(
        "/koreksi-guru", data=_form(id_guru=id_guru, status_baru="SAKIT", alasan="Uji"), headers=_header(token)
    ).json()["id"]

    client.post(f"/koreksi-guru/{id_koreksi}/batalkan", json={"alasan": "Salah input"}, headers=_header(token))

    entri = [e for e in AuditLogRepository(session).daftar_terbaru(limit=10) if e.aksi == "batalkan_koreksi_guru"]
    assert len(entri) == 1
    assert entri[0].alasan == "Salah input"
