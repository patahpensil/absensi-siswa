"""Tes integrasi endpoint Notifikasi (PRD §13, menu §18 NOTIFIKASI):
empat template terpisah, antrean baca-saja, mode uji. Hanya kepala
sekolah/admin - sama seperti Koreksi Massal dan Koreksi Guru. Tidak
menyentuh app.core.engine/mesin_absensi.
"""

from datetime import date, datetime

from fastapi.testclient import TestClient

from app.data.models.enums import JenisPemilik
from app.data.models.notification import JenisNotifikasi
from app.data.models.system import PeranPengguna
from app.data.repositories.attendance import AttendanceRepository
from app.data.repositories.notification import NotificationRepository
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


# ---------------------------------------------------------------------------
# Hak akses - kepala sekolah/admin SAJA
# ---------------------------------------------------------------------------


def test_wali_kelas_ditolak_melihat_template(session_factory, session):
    _buat_user(session, username="wali", password="rahasia123", peran=PeranPengguna.WALI_KELAS)
    client = _client(session_factory)
    token = _login(client, username="wali", password="rahasia123")

    resp = client.get("/notifikasi/template", headers=_header(token))
    assert resp.status_code == 403


def test_operator_ditolak_melihat_antrean(session_factory, session):
    _buat_user(session, username="operator", password="rahasia123", peran=PeranPengguna.OPERATOR)
    client = _client(session_factory)
    token = _login(client, username="operator", password="rahasia123")

    resp = client.get("/notifikasi/antrean", headers=_header(token))
    assert resp.status_code == 403


# ---------------------------------------------------------------------------
# Template (PRD §13.1)
# ---------------------------------------------------------------------------


def test_daftar_template_mengembalikan_empat_jenis_kosong_pada_instalasi_baru(session_factory, session):
    client = _client(session_factory)
    token = _token_admin(client, session)

    resp = client.get("/notifikasi/template", headers=_header(token))
    assert resp.status_code == 200
    daftar = resp.json()

    assert {t["jenis"] for t in daftar} == {"TERLAMBAT", "ALPA_PAGI", "ALPA_PULANG", "REKAP"}
    assert all(t["konten"] is None and t["versi"] is None and t["aktif"] is False for t in daftar)


def test_ubah_template_membuat_versi_baru_dan_menaikkan_versi(session_factory, session):
    client = _client(session_factory)
    token = _token_admin(client, session)

    resp1 = client.put(
        "/notifikasi/template/TERLAMBAT",
        json={"konten": "Ananda {nama_siswa} terlambat pukul {jam}.", "alasan": "Isi awal"},
        headers=_header(token),
    )
    assert resp1.status_code == 200, resp1.text
    assert resp1.json()["versi"] == 1

    resp2 = client.put(
        "/notifikasi/template/TERLAMBAT",
        json={"konten": "Ananda {nama_siswa} tercatat terlambat pukul {jam} WITA.", "alasan": "Perbaiki kalimat"},
        headers=_header(token),
    )
    assert resp2.status_code == 200, resp2.text
    assert resp2.json()["versi"] == 2

    daftar = client.get("/notifikasi/template", headers=_header(token)).json()
    terlambat = next(t for t in daftar if t["jenis"] == "TERLAMBAT")
    assert terlambat["versi"] == 2
    assert "WITA" in terlambat["konten"]


def test_ubah_template_placeholder_tidak_dikenal_ditolak(session_factory, session):
    client = _client(session_factory)
    token = _token_admin(client, session)

    resp = client.put(
        "/notifikasi/template/ALPA_PAGI",
        json={"konten": "Ananda {nama_siswa} tinggal di {alamat}.", "alasan": "Coba placeholder salah"},
        headers=_header(token),
    )
    assert resp.status_code == 422

    # Tidak ada versi yang tersimpan sama sekali - gagal validasi terjadi
    # SEBELUM disimpan, bukan tersimpan lalu gagal dirakit belakangan.
    daftar = client.get("/notifikasi/template", headers=_header(token)).json()
    alpa_pagi = next(t for t in daftar if t["jenis"] == "ALPA_PAGI")
    assert alpa_pagi["konten"] is None


def test_ubah_template_kurung_kosong_ditolak_422_bukan_500(session_factory, session):
    """"{}" (indeks posisional kosong str.format) sempat lolos dari
    validasi lama (hanya menangkap KeyError) dan bocor jadi 500 mentah -
    regresi eksplisit untuk itu."""
    client = _client(session_factory)
    token = _token_admin(client, session)

    resp = client.put(
        "/notifikasi/template/TERLAMBAT",
        json={"konten": "Ananda {} terlambat pukul {jam}.", "alasan": "Coba kurung kosong"},
        headers=_header(token),
    )
    assert resp.status_code == 422

    daftar = client.get("/notifikasi/template", headers=_header(token)).json()
    terlambat = next(t for t in daftar if t["jenis"] == "TERLAMBAT")
    assert terlambat["konten"] is None


def test_ubah_template_jenis_tidak_dikenal_ditolak(session_factory, session):
    client = _client(session_factory)
    token = _token_admin(client, session)

    resp = client.put(
        "/notifikasi/template/TIDAK_ADA",
        json={"konten": "Halo {nama_siswa}", "alasan": "Coba jenis salah"},
        headers=_header(token),
    )
    assert resp.status_code == 422


# ---------------------------------------------------------------------------
# Antrean (PRD §13.2) - baca-saja
# ---------------------------------------------------------------------------


def test_daftar_antrean_menyaring_status(session_factory, session):
    id_absensi = AttendanceRepository(session).tambah(
        id_pemilik=1, jenis_pemilik=JenisPemilik.SISWA, tanggal=date(2026, 9, 8), status_masuk="TERLAMBAT"
    ).id
    notif_repo = NotificationRepository(session)
    pending = notif_repo.antrekan(
        id_absensi=id_absensi,
        id_penerima=None,
        nomor_tujuan="+6281111111111",
        isi_pesan="Ananda terlambat",
        jenis=JenisNotifikasi.TERLAMBAT,
        kunci_idempotensi="evt-api-1",
        waktu_kejadian=datetime(2026, 9, 8, 7, 25),
    )
    terkirim = notif_repo.antrekan(
        id_absensi=id_absensi,
        id_penerima=None,
        nomor_tujuan="+6281111111111",
        isi_pesan="Ananda alpa",
        jenis=JenisNotifikasi.ALPA_PAGI,
        kunci_idempotensi="evt-api-2",
        waktu_kejadian=datetime(2026, 9, 8, 9, 15),
    )
    notif_repo.tandai_terkirim(terkirim.id, waktu_kirim=datetime(2026, 9, 8, 9, 16))

    client = _client(session_factory)
    token = _token_admin(client, session)

    resp_semua = client.get("/notifikasi/antrean", headers=_header(token))
    assert resp_semua.status_code == 200
    assert {b["id"] for b in resp_semua.json()} == {pending.id, terkirim.id}

    resp_pending = client.get("/notifikasi/antrean", params={"status_filter": "PENDING"}, headers=_header(token))
    assert [b["id"] for b in resp_pending.json()] == [pending.id]


# ---------------------------------------------------------------------------
# Mode uji (PRD §13.3)
# ---------------------------------------------------------------------------


def test_mode_uji_bawaan_nonaktif_tanpa_nomor(session_factory, session):
    client = _client(session_factory)
    token = _token_admin(client, session)

    resp = client.get("/notifikasi/mode-uji", headers=_header(token))
    assert resp.status_code == 200
    assert resp.json() == {"aktif": False, "nomor_admin": None}


def test_mengaktifkan_mode_uji_tanpa_nomor_admin_ditolak(session_factory, session):
    client = _client(session_factory)
    token = _token_admin(client, session)

    resp = client.put(
        "/notifikasi/mode-uji", json={"aktif": True, "alasan": "Coba aktifkan"}, headers=_header(token)
    )
    assert resp.status_code == 422


def test_mengisi_nomor_admin_lalu_mengaktifkan_mode_uji(session_factory, session):
    client = _client(session_factory)
    token = _token_admin(client, session)

    resp_nomor = client.put(
        "/notifikasi/mode-uji",
        json={"nomor_admin": "081234567890", "alasan": "Isi nomor admin"},
        headers=_header(token),
    )
    assert resp_nomor.status_code == 200, resp_nomor.text
    assert resp_nomor.json()["nomor_admin"] == "+6281234567890"
    assert resp_nomor.json()["aktif"] is False

    resp_aktif = client.put(
        "/notifikasi/mode-uji", json={"aktif": True, "alasan": "Aktifkan mode uji"}, headers=_header(token)
    )
    assert resp_aktif.status_code == 200, resp_aktif.text
    assert resp_aktif.json() == {"aktif": True, "nomor_admin": "+6281234567890"}
