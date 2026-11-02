"""Tes integrasi app/server/ lewat FastAPI TestClient - login, scan, dan
koreksi massal lewat HTTP sungguhan (dalam proses, tanpa jaringan), bukan
memanggil service langsung. Membuktikan router, dependency auth, dan
serialisasi Pydantic benar-benar tersambung.
"""

from datetime import date, time

from fastapi.testclient import TestClient

from app.data.models.core import JenisKelamin
from app.data.models.enums import JenisPemilik
from app.data.models.system import PeranPengguna
from app.data.repositories.attendance import AttendanceRepository
from app.data.repositories.attendance_event import AttendanceEventRepository
from app.data.repositories.kelas import KelasRepository
from app.data.repositories.rfid_card import RfidCardRepository
from app.data.repositories.schedule import ScheduleRepository
from app.data.repositories.student import StudentRepository
from app.data.repositories.user import UserRepository
from app.server.app import buat_app
from app.server.security import hash_password

TANGGAL = date(2026, 9, 8)  # Selasa


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


def _seed_jadwal_siswa(session) -> None:
    ScheduleRepository(session).tambah(
        jenis_pemilik=JenisPemilik.SISWA,
        hari=TANGGAL.isoweekday(),
        jam_masuk=time(7, 15),
        batas_terlambat=time(7, 20, 59),
        cutoff_alpa=time(7, 30),
        pulang_mulai=time(13, 20),
        jam_pulang=time(15, 0),
        berlaku_mulai=date(2026, 1, 1),
    )


def _buat_siswa_dengan_kartu(session, *, uid: str) -> int:
    kelas = KelasRepository(session).tambah(tingkat=7, nama_kelas="7A", tahun_ajaran="2026/2027")
    siswa = StudentRepository(session).tambah(
        nisn="1000000001",
        nama_lengkap="Budi",
        id_kelas=kelas.id,
        jenis_kelamin=JenisKelamin.L,
        tempat_lahir="Pancarijang",
        tanggal_lahir=date(2012, 1, 1),
        alamat="Jl. Contoh No. 1",
    )
    RfidCardRepository(session).terbitkan(
        uid_kartu=uid, id_pemilik=siswa.id, jenis_pemilik=JenisPemilik.SISWA, tanggal_terbit=date(2026, 1, 1)
    )
    return siswa.id


# ---------------------------------------------------------------------------
# Health
# ---------------------------------------------------------------------------


def test_health(session_factory):
    resp = _client(session_factory).get("/health")
    assert resp.status_code == 200
    assert resp.json() == {"status": "ok"}


# ---------------------------------------------------------------------------
# Auth
# ---------------------------------------------------------------------------


def test_login_gagal_401(session_factory, session):
    _buat_user(session, username="admin", password="rahasia123", peran=PeranPengguna.ADMIN)
    client = _client(session_factory)

    resp = client.post("/auth/login", json={"username": "admin", "password": "salah"})
    assert resp.status_code == 401


def test_login_terkunci_setelah_lima_kali_gagal_423(session_factory, session):
    _buat_user(session, username="admin", password="rahasia123", peran=PeranPengguna.ADMIN)
    client = _client(session_factory)

    for _ in range(5):
        client.post("/auth/login", json={"username": "admin", "password": "salah"})

    resp = client.post("/auth/login", json={"username": "admin", "password": "rahasia123"})
    assert resp.status_code == 423


def test_endpoint_terproteksi_tanpa_token_401(session_factory):
    client = _client(session_factory)
    resp = client.post(
        "/scan",
        json={"uid_kartu": "AABBCC", "uid_mentah": "AABBCC", "jalur": "SIMULATOR", "waktu_scan": "2026-09-08T07:10:00"},
    )
    assert resp.status_code == 401


def test_ganti_password_lalu_login_ulang(session_factory, session):
    _buat_user(session, username="admin", password="rahasia123", peran=PeranPengguna.ADMIN)
    client = _client(session_factory)
    token = _login(client, username="admin", password="rahasia123")

    resp = client.post(
        "/auth/ganti-password",
        json={"password_lama": "rahasia123", "password_baru": "rahasiabaru456"},
        headers=_header(token),
    )
    assert resp.status_code == 204

    # Token lama sudah dicabut.
    resp_scan = client.post(
        "/scan",
        json={"uid_kartu": "X", "uid_mentah": "X", "jalur": "SIMULATOR", "waktu_scan": "2026-09-08T07:10:00"},
        headers=_header(token),
    )
    assert resp_scan.status_code == 401

    # Login ulang dengan password baru berhasil.
    token_baru = _login(client, username="admin", password="rahasiabaru456")
    assert token_baru


# ---------------------------------------------------------------------------
# Scan
# ---------------------------------------------------------------------------


def test_scan_siswa_dikenal_hadir(session_factory, session):
    _buat_user(session, username="operator", password="rahasia123", peran=PeranPengguna.OPERATOR)
    _seed_jadwal_siswa(session)
    _buat_siswa_dengan_kartu(session, uid="AABBCC")
    client = _client(session_factory)
    token = _login(client, username="operator", password="rahasia123")

    resp = client.post(
        "/scan",
        json={"uid_kartu": "AABBCC", "uid_mentah": "AABBCC", "jalur": "SIMULATOR", "waktu_scan": "2026-09-08T07:10:00"},
        headers=_header(token),
    )

    assert resp.status_code == 200
    body = resp.json()
    assert body["jenis_event"] == "MASUK"
    assert body["status_masuk"] == "HADIR"
    assert body["status_final"] == "HADIR"
    assert body["id_absensi"] is not None
    # Identitas tampilan untuk layar absensi lobi (app/ui/lobi) - lihat
    # ScanResponse.nama/jenis_pemilik.
    assert body["nama"] == "Budi"
    assert body["jenis_pemilik"] == "SISWA"


def test_scan_kartu_tidak_dikenal(session_factory, session):
    _buat_user(session, username="operator", password="rahasia123", peran=PeranPengguna.OPERATOR)
    client = _client(session_factory)
    token = _login(client, username="operator", password="rahasia123")

    resp = client.post(
        "/scan",
        json={
            "uid_kartu": "TIDAK_DIKENAL", "uid_mentah": "TIDAK_DIKENAL", "jalur": "SIMULATOR",
            "waktu_scan": "2026-09-08T07:10:00",
        },
        headers=_header(token),
    )

    assert resp.status_code == 200
    body = resp.json()
    assert body["jenis_event"] == "UNKNOWN_CARD"
    assert body["id_absensi"] is None
    assert body["nama"] is None
    assert body["jenis_pemilik"] is None


def test_scan_uid_mentah_tersimpan_byte_persis_lookup_tetap_pakai_baku(session_factory, session):
    """PRD §17: peristiwa_scan wajib menyimpan UID mentah DAN UID baku.
    uid_mentah di sini sengaja "kotor" (huruf kecil + separator) -
    membuktikan server TIDAK menormalisasi ulang apa pun sebelum
    menyimpannya, dan pencarian kartu tetap berhasil lewat uid_kartu
    (baku) yang terpisah, persis seperti sebelum field ini ada.
    """
    _buat_user(session, username="operator", password="rahasia123", peran=PeranPengguna.OPERATOR)
    _seed_jadwal_siswa(session)
    _buat_siswa_dengan_kartu(session, uid="04A2B3C4")  # baku - inilah yang tersimpan di rfid_cards
    client = _client(session_factory)
    token = _login(client, username="operator", password="rahasia123")

    uid_mentah_kotor = "04:a2 b3-c4"  # apa adanya dari alat - huruf kecil, separator ":"/"-"

    resp = client.post(
        "/scan",
        json={
            "uid_kartu": "04A2B3C4", "uid_mentah": uid_mentah_kotor, "jalur": "SIMULATOR",
            "waktu_scan": "2026-09-08T07:10:00",
        },
        headers=_header(token),
    )

    assert resp.status_code == 200
    body = resp.json()
    # Lookup tetap berhasil lewat uid_kartu baku, tak peduli seberapa
    # "kotor" uid_mentah-nya.
    assert body["jenis_event"] == "MASUK"
    assert body["status_masuk"] == "HADIR"
    assert body["nama"] == "Budi"

    peristiwa = AttendanceEventRepository(session).daftar_by_uid("04A2B3C4")
    assert len(peristiwa) == 1
    assert peristiwa[0].uid_mentah == uid_mentah_kotor  # byte-persis, tidak dinormalisasi ulang


def test_scan_jalur_tersimpan(session_factory, session):
    """PRD §17: peristiwa_scan wajib menyimpan jalur alat pembaca."""
    _buat_user(session, username="operator", password="rahasia123", peran=PeranPengguna.OPERATOR)
    _seed_jadwal_siswa(session)
    _buat_siswa_dengan_kartu(session, uid="AABBCC")
    client = _client(session_factory)
    token = _login(client, username="operator", password="rahasia123")

    resp = client.post(
        "/scan",
        json={"uid_kartu": "AABBCC", "uid_mentah": "AABBCC", "jalur": "PCSC", "waktu_scan": "2026-09-08T07:10:00"},
        headers=_header(token),
    )

    assert resp.status_code == 200
    peristiwa = AttendanceEventRepository(session).daftar_by_uid("AABBCC")
    assert len(peristiwa) == 1
    assert peristiwa[0].jalur.value == "PCSC"


def test_scan_jalur_tidak_dikenal_422(session_factory, session):
    _buat_user(session, username="operator", password="rahasia123", peran=PeranPengguna.OPERATOR)
    client = _client(session_factory)
    token = _login(client, username="operator", password="rahasia123")

    resp = client.post(
        "/scan",
        json={
            "uid_kartu": "AABBCC", "uid_mentah": "AABBCC", "jalur": "BLUETOOTH",
            "waktu_scan": "2026-09-08T07:10:00",
        },
        headers=_header(token),
    )

    assert resp.status_code == 422


# ---------------------------------------------------------------------------
# Koreksi massal
# ---------------------------------------------------------------------------


def test_koreksi_massal_ditolak_untuk_wali_kelas(session_factory, session):
    _buat_user(session, username="wali", password="rahasia123", peran=PeranPengguna.WALI_KELAS)
    client = _client(session_factory)
    token = _login(client, username="wali", password="rahasia123")

    resp = client.post(
        "/koreksi-massal/pratinjau",
        json={"tanggal_mulai": "2026-09-08", "tanggal_selesai": "2026-09-08", "lingkup": "SEKOLAH"},
        headers=_header(token),
    )
    assert resp.status_code == 403


def test_koreksi_massal_pratinjau_dan_jalankan_untuk_admin(session_factory, session):
    id_admin = _buat_user(session, username="admin", password="rahasia123", peran=PeranPengguna.ADMIN)
    kelas_id = KelasRepository(session).tambah(tingkat=7, nama_kelas="7A", tahun_ajaran="2026/2027").id
    siswa = StudentRepository(session).tambah(
        nisn="1000000002",
        nama_lengkap="Siti",
        id_kelas=kelas_id,
        jenis_kelamin=JenisKelamin.P,
        tempat_lahir="Pancarijang",
        tanggal_lahir=date(2012, 1, 1),
        alamat="Jl. Contoh No. 2",
    )
    AttendanceRepository(session).tambah(
        id_pemilik=siswa.id, jenis_pemilik=JenisPemilik.SISWA, tanggal=TANGGAL, status_masuk="ALPA"
    )
    client = _client(session_factory)
    token = _login(client, username="admin", password="rahasia123")

    penyaring = {"tanggal_mulai": "2026-09-08", "tanggal_selesai": "2026-09-08", "lingkup": "SEKOLAH"}

    resp_pratinjau = client.post("/koreksi-massal/pratinjau", json=penyaring, headers=_header(token))
    assert resp_pratinjau.status_code == 200
    assert resp_pratinjau.json()["jumlah_baris"] == 1

    resp_jalankan = client.post(
        "/koreksi-massal/jalankan",
        json={
            "penyaring": penyaring,
            "jenis_tindakan": "UBAH_STATUS",
            "status_baru": "SAKIT",
            "alasan": "Surat dokter menyusul",
        },
        headers=_header(token),
    )
    assert resp_jalankan.status_code == 200
    tindakan = resp_jalankan.json()
    assert tindakan["jumlah_baris"] == 1
    assert tindakan["dibatalkan"] is False

    resp_riwayat = client.get("/koreksi-massal", headers=_header(token))
    assert resp_riwayat.status_code == 200
    assert len(resp_riwayat.json()) == 1

    resp_batal = client.post(
        f"/koreksi-massal/{tindakan['id']}/batalkan",
        json={"alasan": "Salah surat"},
        headers=_header(token),
    )
    assert resp_batal.status_code == 200
    assert resp_batal.json()["dibatalkan"] is True

    absensi = AttendanceRepository(session).cari_by_pemilik_tanggal(
        id_pemilik=siswa.id, jenis_pemilik=JenisPemilik.SISWA, tanggal=TANGGAL
    )
    assert absensi.status_final == "ALPA"
