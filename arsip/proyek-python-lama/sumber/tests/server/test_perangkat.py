"""Tes integrasi endpoint Perangkat (PRD §12.4): izin darurat alat dan
wizard ganti jalur, plus GET /kartu/cek/{uid_kartu} (PRD §18, Uji Baca
Kartu). Lingkup peran: beri izin darurat = kepsek/admin/wali kelas;
perpanjang/cabut/ganti jalur = kepsek/admin saja; cek status aktif per
station = seluruh peran (dipakai lobi/Operator).
"""

from datetime import date

from fastapi.testclient import TestClient

from app.data.models.core import JenisKelamin
from app.data.models.enums import JenisPemilik
from app.data.models.system import PeranPengguna
from app.data.repositories.rfid_card import RfidCardRepository
from app.data.repositories.student import StudentRepository
from app.data.repositories.kelas import KelasRepository
from app.data.repositories.user import UserRepository
from app.server.app import buat_app
from app.server.security import hash_password


def _client(session_factory) -> TestClient:
    return TestClient(buat_app(session_factory))


def _buat_user(session, *, username: str, peran: PeranPengguna) -> int:
    return UserRepository(session).tambah(
        username=username, password_hash=hash_password("rahasia123"), peran=peran
    ).id


def _login(client: TestClient, *, username: str) -> str:
    resp = client.post("/auth/login", json={"username": username, "password": "rahasia123"})
    assert resp.status_code == 200, resp.text
    return resp.json()["token"]


def _header(token: str) -> dict:
    return {"Authorization": f"Bearer {token}"}


def _token(client: TestClient, session, *, username: str, peran: PeranPengguna) -> str:
    _buat_user(session, username=username, peran=peran)
    return _login(client, username=username)


def _buat_siswa_dengan_kartu(session, *, uid: str, nisn: str = "1000000001") -> int:
    kelas = KelasRepository(session).tambah(tingkat=7, nama_kelas="7A", tahun_ajaran="2026/2027")
    id_siswa = StudentRepository(session).tambah(
        nisn=nisn,
        nama_lengkap="Budi",
        id_kelas=kelas.id,
        jenis_kelamin=JenisKelamin.L,
        tempat_lahir="Pancarijang",
        tanggal_lahir=date(2012, 1, 1),
        alamat="Jl. Contoh No. 1",
    ).id
    RfidCardRepository(session).terbitkan(
        uid_kartu=uid, id_pemilik=id_siswa, jenis_pemilik=JenisPemilik.SISWA, tanggal_terbit=date(2026, 1, 1)
    )
    return id_siswa


# ---------------------------------------------------------------------------
# Izin darurat - lingkup peran pemberian
# ---------------------------------------------------------------------------


def test_operator_ditolak_memberi_izin_darurat(session_factory, session):
    client = _client(session_factory)
    token = _token(client, session, username="operator", peran=PeranPengguna.OPERATOR)

    resp = client.post(
        "/perangkat/izin-darurat",
        json={"station_id": "LOBI-1", "jam": 24, "alasan": "Uji"},
        headers=_header(token),
    )
    assert resp.status_code == 403


def test_wali_kelas_boleh_memberi_izin_darurat(session_factory, session):
    client = _client(session_factory)
    token = _token(client, session, username="wali_a", peran=PeranPengguna.WALI_KELAS)

    resp = client.post(
        "/perangkat/izin-darurat",
        json={"station_id": "LOBI-1", "jam": 24, "alasan": "Reader lobi 1 rusak"},
        headers=_header(token),
    )
    assert resp.status_code == 201, resp.text
    assert resp.json()["station_id"] == "LOBI-1"
    assert resp.json()["dicabut"] is False


def test_durasi_melebihi_24_jam_ditolak_422(session_factory, session):
    client = _client(session_factory)
    token = _token(client, session, username="admin", peran=PeranPengguna.ADMIN)

    resp = client.post(
        "/perangkat/izin-darurat",
        json={"station_id": "LOBI-1", "jam": 25, "alasan": "Uji"},
        headers=_header(token),
    )
    assert resp.status_code == 422


def test_izin_aktif_kedua_di_station_sama_ditolak_409(session_factory, session):
    client = _client(session_factory)
    token = _token(client, session, username="admin", peran=PeranPengguna.ADMIN)
    body = {"station_id": "LOBI-1", "jam": 24, "alasan": "Uji"}

    resp1 = client.post("/perangkat/izin-darurat", json=body, headers=_header(token))
    assert resp1.status_code == 201

    resp2 = client.post("/perangkat/izin-darurat", json=body, headers=_header(token))
    assert resp2.status_code == 409


# ---------------------------------------------------------------------------
# Izin darurat - perpanjang/cabut HANYA kepsek/admin
# ---------------------------------------------------------------------------


def test_wali_kelas_ditolak_memperpanjang(session_factory, session):
    client = _client(session_factory)
    token_admin = _token(client, session, username="admin", peran=PeranPengguna.ADMIN)
    izin = client.post(
        "/perangkat/izin-darurat",
        json={"station_id": "LOBI-1", "jam": 12, "alasan": "Uji"},
        headers=_header(token_admin),
    ).json()

    token_wali = _token(client, session, username="wali_a", peran=PeranPengguna.WALI_KELAS)
    resp = client.post(
        f"/perangkat/izin-darurat/{izin['id']}/perpanjang",
        json={"jam": 12, "alasan": "Coba perpanjang"},
        headers=_header(token_wali),
    )
    assert resp.status_code == 403


def test_admin_boleh_memperpanjang_dan_mencabut(session_factory, session):
    client = _client(session_factory)
    token = _token(client, session, username="admin", peran=PeranPengguna.ADMIN)
    izin = client.post(
        "/perangkat/izin-darurat",
        json={"station_id": "LOBI-1", "jam": 12, "alasan": "Uji"},
        headers=_header(token),
    ).json()

    resp_perpanjang = client.post(
        f"/perangkat/izin-darurat/{izin['id']}/perpanjang",
        json={"jam": 24, "alasan": "Alat belum juga diganti"},
        headers=_header(token),
    )
    assert resp_perpanjang.status_code == 200, resp_perpanjang.text

    resp_cabut = client.post(
        f"/perangkat/izin-darurat/{izin['id']}/cabut",
        json={"alasan": "Alat sudah diperbaiki"},
        headers=_header(token),
    )
    assert resp_cabut.status_code == 200, resp_cabut.text
    assert resp_cabut.json()["dicabut"] is True


def test_cabut_izin_yang_sudah_dicabut_ditolak_409(session_factory, session):
    client = _client(session_factory)
    token = _token(client, session, username="admin", peran=PeranPengguna.ADMIN)
    izin = client.post(
        "/perangkat/izin-darurat",
        json={"station_id": "LOBI-1", "jam": 12, "alasan": "Uji"},
        headers=_header(token),
    ).json()
    client.post(f"/perangkat/izin-darurat/{izin['id']}/cabut", json={"alasan": "Selesai"}, headers=_header(token))

    resp = client.post(
        f"/perangkat/izin-darurat/{izin['id']}/cabut", json={"alasan": "Coba lagi"}, headers=_header(token)
    )
    assert resp.status_code == 409


# ---------------------------------------------------------------------------
# Cek status aktif per station - TIDAK dibatasi peran (dipakai lobi)
# ---------------------------------------------------------------------------


def test_operator_boleh_cek_status_aktif_untuk_banner_lobi(session_factory, session):
    client = _client(session_factory)
    token_admin = _token(client, session, username="admin", peran=PeranPengguna.ADMIN)
    client.post(
        "/perangkat/izin-darurat",
        json={"station_id": "LOBI-1", "jam": 24, "alasan": "Uji"},
        headers=_header(token_admin),
    )

    token_operator = _token(client, session, username="operator", peran=PeranPengguna.OPERATOR)
    resp = client.get(
        "/perangkat/izin-darurat/aktif", params={"station_id": "LOBI-1"}, headers=_header(token_operator)
    )
    assert resp.status_code == 200, resp.text
    assert resp.json()["aktif"] is True

    resp_lain = client.get(
        "/perangkat/izin-darurat/aktif", params={"station_id": "LOBI-9"}, headers=_header(token_operator)
    )
    assert resp_lain.json()["aktif"] is False


# ---------------------------------------------------------------------------
# Wizard ganti jalur
# ---------------------------------------------------------------------------


def test_wali_kelas_ditolak_ganti_jalur(session_factory, session):
    client = _client(session_factory)
    token = _token(client, session, username="wali_a", peran=PeranPengguna.WALI_KELAS)

    resp = client.post(
        "/perangkat/ganti-jalur",
        json={"jalur_baru": "SERIAL", "uid_kartu_verifikasi": ["A1", "A2", "A3"], "alasan": "Uji"},
        headers=_header(token),
    )
    assert resp.status_code == 403


def test_ganti_jalur_dengan_tiga_kartu_terdaftar_berhasil(session_factory, session):
    _buat_siswa_dengan_kartu(session, uid="AAAA1111", nisn="1000000001")
    _buat_siswa_dengan_kartu(session, uid="BBBB2222", nisn="1000000002")
    _buat_siswa_dengan_kartu(session, uid="CCCC3333", nisn="1000000003")
    client = _client(session_factory)
    token = _token(client, session, username="admin", peran=PeranPengguna.ADMIN)

    resp = client.post(
        "/perangkat/ganti-jalur",
        json={
            "jalur_baru": "SERIAL",
            "uid_kartu_verifikasi": ["AAAA1111", "BBBB2222", "CCCC3333"],
            "alasan": "Reader PC/SC diganti serial",
        },
        headers=_header(token),
    )
    assert resp.status_code == 200, resp.text
    assert resp.json() == {"jalur_lama": "PCSC", "jalur_baru": "SERIAL"}


def test_ganti_jalur_dengan_kartu_tak_terdaftar_ditolak_422(session_factory, session):
    _buat_siswa_dengan_kartu(session, uid="AAAA1111")
    client = _client(session_factory)
    token = _token(client, session, username="admin", peran=PeranPengguna.ADMIN)

    resp = client.post(
        "/perangkat/ganti-jalur",
        json={
            "jalur_baru": "SERIAL",
            "uid_kartu_verifikasi": ["AAAA1111", "TIDAK_ADA_1", "TIDAK_ADA_2"],
            "alasan": "Uji",
        },
        headers=_header(token),
    )
    assert resp.status_code == 422
    assert "TIDAK_ADA_1" in resp.text


def test_ganti_jalur_dengan_uid_duplikat_ditolak_422(session_factory, session):
    _buat_siswa_dengan_kartu(session, uid="AAAA1111")
    client = _client(session_factory)
    token = _token(client, session, username="admin", peran=PeranPengguna.ADMIN)

    resp = client.post(
        "/perangkat/ganti-jalur",
        json={
            "jalur_baru": "SERIAL",
            "uid_kartu_verifikasi": ["AAAA1111", "AAAA1111", "BBBB2222"],
            "alasan": "Uji",
        },
        headers=_header(token),
    )
    assert resp.status_code == 422


def test_ganti_jalur_jumlah_kartu_bukan_tiga_ditolak_422(session_factory, session):
    client = _client(session_factory)
    token = _token(client, session, username="admin", peran=PeranPengguna.ADMIN)

    resp = client.post(
        "/perangkat/ganti-jalur",
        json={"jalur_baru": "SERIAL", "uid_kartu_verifikasi": ["AAAA1111", "BBBB2222"], "alasan": "Uji"},
        headers=_header(token),
    )
    assert resp.status_code == 422


def test_ganti_jalur_nilai_tidak_dikenal_ditolak_422(session_factory, session):
    _buat_siswa_dengan_kartu(session, uid="AAAA1111", nisn="1000000001")
    _buat_siswa_dengan_kartu(session, uid="BBBB2222", nisn="1000000002")
    _buat_siswa_dengan_kartu(session, uid="CCCC3333", nisn="1000000003")
    client = _client(session_factory)
    token = _token(client, session, username="admin", peran=PeranPengguna.ADMIN)

    resp = client.post(
        "/perangkat/ganti-jalur",
        json={
            "jalur_baru": "BLUETOOTH",
            "uid_kartu_verifikasi": ["AAAA1111", "BBBB2222", "CCCC3333"],
            "alasan": "Uji",
        },
        headers=_header(token),
    )
    assert resp.status_code == 422


# ---------------------------------------------------------------------------
# GET /kartu/cek/{uid_kartu} (Uji Baca Kartu, §18) - kepsek/admin saja
# ---------------------------------------------------------------------------


def test_wali_kelas_ditolak_uji_baca_kartu(session_factory, session):
    client = _client(session_factory)
    token = _token(client, session, username="wali_a", peran=PeranPengguna.WALI_KELAS)

    resp = client.get("/kartu/cek/AAAA1111", headers=_header(token))
    assert resp.status_code == 403


def test_uji_baca_kartu_terdaftar(session_factory, session):
    _buat_siswa_dengan_kartu(session, uid="AAAA1111")
    client = _client(session_factory)
    token = _token(client, session, username="admin", peran=PeranPengguna.ADMIN)

    resp = client.get("/kartu/cek/AAAA1111", headers=_header(token))
    assert resp.status_code == 200, resp.text
    body = resp.json()
    assert body["terdaftar"] is True
    assert body["nama_pemilik"] == "Budi"
    assert body["status"] == "AKTIF"


def test_uji_baca_kartu_tidak_terdaftar(session_factory, session):
    client = _client(session_factory)
    token = _token(client, session, username="admin", peran=PeranPengguna.ADMIN)

    resp = client.get("/kartu/cek/TIDAK_ADA", headers=_header(token))
    assert resp.status_code == 200, resp.text
    body = resp.json()
    assert body["terdaftar"] is False
    assert body["nama_pemilik"] is None


# ---------------------------------------------------------------------------
# POST /perangkat/telemetri (PRD §14.1, status operasional Dashboard) -
# terbuka untuk seluruh peran, termasuk Operator (klien lobi).
# ---------------------------------------------------------------------------


def test_operator_boleh_lapor_telemetri(session_factory, session):
    client = _client(session_factory)
    token = _token(client, session, username="operator_lobi", peran=PeranPengguna.OPERATOR)

    resp = client.post(
        "/perangkat/telemetri", json={"station_id": "LOBI-1", "jumlah_pending": 4}, headers=_header(token)
    )
    assert resp.status_code == 201, resp.text
    assert resp.json()["station_id"] == "LOBI-1"
    assert resp.json()["jumlah_pending"] == 4


def test_lapor_telemetri_kedua_menimpa_angka_sebelumnya(session_factory, session):
    client = _client(session_factory)
    token = _token(client, session, username="operator_lobi", peran=PeranPengguna.OPERATOR)
    client.post("/perangkat/telemetri", json={"station_id": "LOBI-1", "jumlah_pending": 4}, headers=_header(token))

    resp = client.post(
        "/perangkat/telemetri", json={"station_id": "LOBI-1", "jumlah_pending": 0}, headers=_header(token)
    )

    assert resp.json()["jumlah_pending"] == 0


def test_lapor_telemetri_jumlah_negatif_ditolak_422(session_factory, session):
    client = _client(session_factory)
    token = _token(client, session, username="operator_lobi", peran=PeranPengguna.OPERATOR)

    resp = client.post(
        "/perangkat/telemetri", json={"station_id": "LOBI-1", "jumlah_pending": -1}, headers=_header(token)
    )
    assert resp.status_code == 422
