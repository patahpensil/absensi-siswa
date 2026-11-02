"""Tes integrasi endpoint Koreksi Massal (PRD §9.2, §10): lingkup peran
(hanya kepsek/admin), pratinjau, jalankan (dengan ambang snapshot/
konfirmasi ketik), batalkan (undo), dan riwayat. Aturan penerapan
tindakan sendiri sudah diuji lengkap lewat basis data sungguhan di
tests/core/test_koreksi_massal_service.py - berkas ini menguji
KONTRAK HTTP yang dipakai app.ui.koreksi_massal (kode status, bentuk
body galat 412/400/403/404/409), bukan mengulang aturan penerapannya.

Ambang snapshot/konfirmasi ketik (app.config.pengaturan.Pengaturan.
ambang_snapshot_sebelum_massal/ambang_konfirmasi_ketik) TIDAK bisa diatur
lewat basis data settings (belum ada KunciPengaturan untuk keduanya -
dicatat sebagai utang di README) - tes di sini menyuntikkan Pengaturan
tiruan lewat monkeypatch pada titik yang sama seperti duck-typing yang
sudah didokumentasikan app.core.koreksi_massal.buat_pratinjau(), alih-alih
membuat 5.000+ baris attendance sungguhan.
"""

from datetime import date

from fastapi.testclient import TestClient

import app.server.routers.koreksi_massal as koreksi_massal_router
from app.data.models.core import JenisKelamin
from app.data.models.enums import JenisPemilik
from app.data.models.system import PeranPengguna
from app.data.repositories.attendance import AttendanceRepository
from app.data.repositories.kelas import KelasRepository
from app.data.repositories.student import StudentRepository
from app.data.repositories.user import UserRepository
from app.server.app import buat_app
from app.server.security import hash_password

TANGGAL = "2026-09-08"
TANGGAL_DATE = date(2026, 9, 8)


class _PengaturanAmbangKecil:
    """Pengaturan tiruan - hanya dua atribut yang dibaca
    app.core.koreksi_massal.buat_pratinjau() lewat duck typing."""

    def __init__(self, *, ambang_snapshot: int, ambang_konfirmasi: int) -> None:
        self.ambang_snapshot_sebelum_massal = ambang_snapshot
        self.ambang_konfirmasi_ketik = ambang_konfirmasi


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


def _token_admin(client: TestClient, session) -> tuple[str, int]:
    id_admin = _buat_user(session, username="admin", password="rahasia123", peran=PeranPengguna.ADMIN)
    return _login(client, username="admin", password="rahasia123"), id_admin


def _buat_siswa(session, *, id_kelas: int, nisn: str, nama: str) -> int:
    return StudentRepository(session).tambah(
        nisn=nisn, nama_lengkap=nama, id_kelas=id_kelas, jenis_kelamin=JenisKelamin.L,
        tempat_lahir="Pancarijang", tanggal_lahir=date(2012, 1, 1), alamat="Jl. Contoh No. 1",
    ).id


def _siapkan_kelas_dengan_siswa(session, *, jumlah: int = 1) -> dict:
    kelas = KelasRepository(session).tambah(tingkat=7, nama_kelas="7A", tahun_ajaran="2026/2027").id
    siswa = [_buat_siswa(session, id_kelas=kelas, nisn=str(i).zfill(10), nama=f"Siswa {i}") for i in range(jumlah)]
    return {"kelas": kelas, "siswa": siswa}


def _buat_absensi_alpa(session, *, id_siswa: int) -> int:
    return AttendanceRepository(session).tambah(
        id_pemilik=id_siswa, jenis_pemilik=JenisPemilik.SISWA, tanggal=TANGGAL_DATE, status_masuk="ALPA"
    ).id


def _payload_penyaring(**kwargs) -> dict:
    dasar = dict(tanggal_mulai=TANGGAL, tanggal_selesai=TANGGAL, lingkup="SEKOLAH")
    dasar.update(kwargs)
    return dasar


# ---------------------------------------------------------------------------
# Lingkup peran (§10) - hanya kepsek/admin
# ---------------------------------------------------------------------------


def test_wali_kelas_ditolak_pratinjau_403(session_factory, session):
    data = _siapkan_kelas_dengan_siswa(session)
    _buat_absensi_alpa(session, id_siswa=data["siswa"][0])
    _buat_user(
        session, username="wali_a", password="rahasia123", peran=PeranPengguna.WALI_KELAS,
        id_kelas_diampu=data["kelas"],
    )
    client = _client(session_factory)
    token = _login(client, username="wali_a", password="rahasia123")

    resp = client.post("/koreksi-massal/pratinjau", json=_payload_penyaring(), headers=_header(token))
    assert resp.status_code == 403


def test_operator_ditolak_jalankan_403(session_factory, session):
    _buat_user(session, username="operator", password="rahasia123", peran=PeranPengguna.OPERATOR)
    client = _client(session_factory)
    token = _login(client, username="operator", password="rahasia123")

    resp = client.post(
        "/koreksi-massal/jalankan",
        json={"penyaring": _payload_penyaring(), "jenis_tindakan": "UBAH_STATUS", "status_baru": "SAKIT", "alasan": "Uji"},
        headers=_header(token),
    )
    assert resp.status_code == 403


def test_kepsek_boleh_menjalankan(session_factory, session):
    data = _siapkan_kelas_dengan_siswa(session)
    _buat_absensi_alpa(session, id_siswa=data["siswa"][0])
    _buat_user(session, username="kepsek", password="rahasia123", peran=PeranPengguna.KEPALA_SEKOLAH)
    client = _client(session_factory)
    token = _login(client, username="kepsek", password="rahasia123")

    resp = client.post(
        "/koreksi-massal/jalankan",
        json={"penyaring": _payload_penyaring(), "jenis_tindakan": "UBAH_STATUS", "status_baru": "SAKIT", "alasan": "Uji"},
        headers=_header(token),
    )
    assert resp.status_code == 200, resp.text


# ---------------------------------------------------------------------------
# Pratinjau dan jalankan - status terlindung bawaan (§9.2)
# ---------------------------------------------------------------------------


def test_pratinjau_menghitung_jumlah_baris(session_factory, session):
    data = _siapkan_kelas_dengan_siswa(session, jumlah=3)
    for id_siswa in data["siswa"]:
        _buat_absensi_alpa(session, id_siswa=id_siswa)
    client = _client(session_factory)
    token, _ = _token_admin(client, session)

    resp = client.post("/koreksi-massal/pratinjau", json=_payload_penyaring(), headers=_header(token))
    assert resp.status_code == 200, resp.text
    body = resp.json()
    assert body == {"jumlah_baris": 3, "perlu_snapshot": False, "perlu_konfirmasi_ketik": False}


def test_pratinjau_default_mengecualikan_status_manual(session_factory, session):
    data = _siapkan_kelas_dengan_siswa(session, jumlah=2)
    _buat_absensi_alpa(session, id_siswa=data["siswa"][0])
    AttendanceRepository(session).tambah(
        id_pemilik=data["siswa"][1], jenis_pemilik=JenisPemilik.SISWA, tanggal=TANGGAL_DATE, status_masuk="SAKIT"
    )
    client = _client(session_factory)
    token, _ = _token_admin(client, session)

    resp = client.post("/koreksi-massal/pratinjau", json=_payload_penyaring(), headers=_header(token))
    assert resp.status_code == 200, resp.text
    assert resp.json()["jumlah_baris"] == 1


def test_jalankan_ubah_status_tercatat_penuh(session_factory, session):
    data = _siapkan_kelas_dengan_siswa(session)
    _buat_absensi_alpa(session, id_siswa=data["siswa"][0])
    client = _client(session_factory)
    token, id_admin = _token_admin(client, session)

    resp = client.post(
        "/koreksi-massal/jalankan",
        json={
            "penyaring": _payload_penyaring(),
            "jenis_tindakan": "UBAH_STATUS",
            "status_baru": "SAKIT",
            "alasan": "Wabah demam berdarah",
        },
        headers=_header(token),
    )
    assert resp.status_code == 200, resp.text
    body = resp.json()
    assert body["jumlah_baris"] == 1
    assert body["id_pengguna"] == id_admin
    assert body["penyaring"]["lingkup"] == "SEKOLAH"
    assert body["dibatalkan"] is False


def test_jalankan_tanpa_alasan_422(session_factory, session):
    data = _siapkan_kelas_dengan_siswa(session)
    _buat_absensi_alpa(session, id_siswa=data["siswa"][0])
    client = _client(session_factory)
    token, _ = _token_admin(client, session)

    resp = client.post(
        "/koreksi-massal/jalankan",
        json={"penyaring": _payload_penyaring(), "jenis_tindakan": "UBAH_STATUS", "status_baru": "SAKIT"},
        headers=_header(token),
    )
    assert resp.status_code == 422


# ---------------------------------------------------------------------------
# Ambang snapshot - penolakan ANGGUN (bukan crash), bukan modul pencadangan
# ---------------------------------------------------------------------------


def test_di_atas_ambang_snapshot_412(session_factory, session, monkeypatch):
    data = _siapkan_kelas_dengan_siswa(session)
    _buat_absensi_alpa(session, id_siswa=data["siswa"][0])
    monkeypatch.setattr(
        koreksi_massal_router,
        "Pengaturan",
        lambda _repo: _PengaturanAmbangKecil(ambang_snapshot=0, ambang_konfirmasi=1_000_000),
    )
    client = _client(session_factory)
    token, _ = _token_admin(client, session)

    resp = client.post(
        "/koreksi-massal/jalankan",
        json={"penyaring": _payload_penyaring(), "jenis_tindakan": "UBAH_STATUS", "status_baru": "SAKIT", "alasan": "Uji"},
        headers=_header(token),
    )
    assert resp.status_code == 412
    assert "snapshot" in resp.json()["detail"].lower()


def test_di_atas_ambang_snapshot_berhasil_setelah_flag_disetel(session_factory, session, monkeypatch):
    data = _siapkan_kelas_dengan_siswa(session)
    _buat_absensi_alpa(session, id_siswa=data["siswa"][0])
    monkeypatch.setattr(
        koreksi_massal_router,
        "Pengaturan",
        lambda _repo: _PengaturanAmbangKecil(ambang_snapshot=0, ambang_konfirmasi=1_000_000),
    )
    client = _client(session_factory)
    token, _ = _token_admin(client, session)

    resp = client.post(
        "/koreksi-massal/jalankan",
        json={
            "penyaring": _payload_penyaring(),
            "jenis_tindakan": "UBAH_STATUS",
            "status_baru": "SAKIT",
            "alasan": "Uji",
            "snapshot_sudah_diambil": True,
        },
        headers=_header(token),
    )
    assert resp.status_code == 200, resp.text


def test_pratinjau_menandai_perlu_snapshot(session_factory, session, monkeypatch):
    data = _siapkan_kelas_dengan_siswa(session)
    _buat_absensi_alpa(session, id_siswa=data["siswa"][0])
    monkeypatch.setattr(
        koreksi_massal_router,
        "Pengaturan",
        lambda _repo: _PengaturanAmbangKecil(ambang_snapshot=0, ambang_konfirmasi=1_000_000),
    )
    client = _client(session_factory)
    token, _ = _token_admin(client, session)

    resp = client.post("/koreksi-massal/pratinjau", json=_payload_penyaring(), headers=_header(token))
    assert resp.status_code == 200, resp.text
    assert resp.json()["perlu_snapshot"] is True


# ---------------------------------------------------------------------------
# Ambang konfirmasi ketik
# ---------------------------------------------------------------------------


def test_di_atas_ambang_konfirmasi_ketik_412_tanpa_teks(session_factory, session, monkeypatch):
    data = _siapkan_kelas_dengan_siswa(session)
    _buat_absensi_alpa(session, id_siswa=data["siswa"][0])
    monkeypatch.setattr(
        koreksi_massal_router,
        "Pengaturan",
        lambda _repo: _PengaturanAmbangKecil(ambang_snapshot=1_000_000, ambang_konfirmasi=0),
    )
    client = _client(session_factory)
    token, _ = _token_admin(client, session)

    resp = client.post(
        "/koreksi-massal/jalankan",
        json={"penyaring": _payload_penyaring(), "jenis_tindakan": "UBAH_STATUS", "status_baru": "SAKIT", "alasan": "Uji"},
        headers=_header(token),
    )
    assert resp.status_code == 412


def test_konfirmasi_ketik_salah_400(session_factory, session, monkeypatch):
    data = _siapkan_kelas_dengan_siswa(session)
    _buat_absensi_alpa(session, id_siswa=data["siswa"][0])
    monkeypatch.setattr(
        koreksi_massal_router,
        "Pengaturan",
        lambda _repo: _PengaturanAmbangKecil(ambang_snapshot=1_000_000, ambang_konfirmasi=0),
    )
    client = _client(session_factory)
    token, _ = _token_admin(client, session)

    resp = client.post(
        "/koreksi-massal/jalankan",
        json={
            "penyaring": _payload_penyaring(),
            "jenis_tindakan": "UBAH_STATUS",
            "status_baru": "SAKIT",
            "alasan": "Uji",
            "konfirmasi_ketik": "999",
        },
        headers=_header(token),
    )
    assert resp.status_code == 400


def test_konfirmasi_ketik_benar_berhasil(session_factory, session, monkeypatch):
    data = _siapkan_kelas_dengan_siswa(session)
    _buat_absensi_alpa(session, id_siswa=data["siswa"][0])
    monkeypatch.setattr(
        koreksi_massal_router,
        "Pengaturan",
        lambda _repo: _PengaturanAmbangKecil(ambang_snapshot=1_000_000, ambang_konfirmasi=0),
    )
    client = _client(session_factory)
    token, _ = _token_admin(client, session)

    resp = client.post(
        "/koreksi-massal/jalankan",
        json={
            "penyaring": _payload_penyaring(),
            "jenis_tindakan": "UBAH_STATUS",
            "status_baru": "SAKIT",
            "alasan": "Uji",
            "konfirmasi_ketik": "1",
        },
        headers=_header(token),
    )
    assert resp.status_code == 200, resp.text


# ---------------------------------------------------------------------------
# Batalkan (undo)
# ---------------------------------------------------------------------------


def test_batalkan_mengembalikan_nilai_lama(session_factory, session):
    data = _siapkan_kelas_dengan_siswa(session)
    _buat_absensi_alpa(session, id_siswa=data["siswa"][0])
    client = _client(session_factory)
    token, _ = _token_admin(client, session)
    id_tindakan = client.post(
        "/koreksi-massal/jalankan",
        json={"penyaring": _payload_penyaring(), "jenis_tindakan": "UBAH_STATUS", "status_baru": "SAKIT", "alasan": "Uji"},
        headers=_header(token),
    ).json()["id"]

    resp = client.post(f"/koreksi-massal/{id_tindakan}/batalkan", json={"alasan": "Salah input"}, headers=_header(token))
    assert resp.status_code == 200, resp.text
    assert resp.json()["dibatalkan"] is True

    absensi = AttendanceRepository(session).cari_by_pemilik_tanggal(
        id_pemilik=data["siswa"][0], jenis_pemilik=JenisPemilik.SISWA, tanggal=TANGGAL_DATE
    )
    assert absensi.status_final == "ALPA"


def test_batalkan_dua_kali_409(session_factory, session):
    data = _siapkan_kelas_dengan_siswa(session)
    _buat_absensi_alpa(session, id_siswa=data["siswa"][0])
    client = _client(session_factory)
    token, _ = _token_admin(client, session)
    id_tindakan = client.post(
        "/koreksi-massal/jalankan",
        json={"penyaring": _payload_penyaring(), "jenis_tindakan": "UBAH_STATUS", "status_baru": "SAKIT", "alasan": "Uji"},
        headers=_header(token),
    ).json()["id"]
    client.post(f"/koreksi-massal/{id_tindakan}/batalkan", json={"alasan": "Batal pertama"}, headers=_header(token))

    resp = client.post(f"/koreksi-massal/{id_tindakan}/batalkan", json={"alasan": "Batal kedua"}, headers=_header(token))
    assert resp.status_code == 409


def test_batalkan_tidak_ditemukan_404(session_factory, session):
    client = _client(session_factory)
    token, _ = _token_admin(client, session)

    resp = client.post("/koreksi-massal/999/batalkan", json={"alasan": "Uji"}, headers=_header(token))
    assert resp.status_code == 404


# ---------------------------------------------------------------------------
# Riwayat
# ---------------------------------------------------------------------------


def test_riwayat_menampilkan_pelaku_dan_cakupan(session_factory, session):
    data = _siapkan_kelas_dengan_siswa(session)
    _buat_absensi_alpa(session, id_siswa=data["siswa"][0])
    client = _client(session_factory)
    token, id_admin = _token_admin(client, session)
    client.post(
        "/koreksi-massal/jalankan",
        json={"penyaring": _payload_penyaring(), "jenis_tindakan": "UBAH_STATUS", "status_baru": "SAKIT", "alasan": "Uji"},
        headers=_header(token),
    )

    resp = client.get("/koreksi-massal", headers=_header(token))
    assert resp.status_code == 200
    assert len(resp.json()) == 1
    baris = resp.json()[0]
    assert baris["id_pengguna"] == id_admin
    assert baris["penyaring"]["lingkup"] == "SEKOLAH"
    assert baris["alasan"] == "Uji"
