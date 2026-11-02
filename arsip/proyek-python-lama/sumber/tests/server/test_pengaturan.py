"""Tes integrasi endpoint Pengaturan (PRD §4, §14.2): jadwal per hari
(versi berlaku per tanggal, tidak menimpa data lama), ambang, saklar
modul, identitas sekolah - hanya kepala sekolah/admin. Tidak menyentuh
app.core.engine/mesin_absensi maupun app.core.kalender.
"""

from datetime import date, time

from fastapi.testclient import TestClient

from app.data.models.enums import JenisPemilik
from app.data.models.system import PeranPengguna
from app.data.repositories.schedule import ScheduleRepository
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


JADWAL_SISWA_SENIN = {
    "jenis_pemilik": "SISWA",
    "hari": 1,
    "jam_masuk": "07:15:00",
    "batas_terlambat": "07:20:59",
    "cutoff_alpa": "07:30:00",
    "pulang_mulai": "13:20:00",
    "jam_pulang": "15:00:00",
    "berlaku_mulai": "2026-01-01",
}


# ---------------------------------------------------------------------------
# Hak akses - kepala sekolah/admin SAJA, tidak ada "lihat" untuk peran lain
# ---------------------------------------------------------------------------


def test_wali_kelas_ditolak_melihat_jadwal(session_factory, session):
    _buat_user(session, username="wali", password="rahasia123", peran=PeranPengguna.WALI_KELAS)
    client = _client(session_factory)
    token = _login(client, username="wali", password="rahasia123")

    resp = client.get("/pengaturan/jadwal", params={"jenis_pemilik": "SISWA"}, headers=_header(token))
    assert resp.status_code == 403


def test_operator_ditolak_melihat_ambang(session_factory, session):
    _buat_user(session, username="operator", password="rahasia123", peran=PeranPengguna.OPERATOR)
    client = _client(session_factory)
    token = _login(client, username="operator", password="rahasia123")

    resp = client.get("/pengaturan/ambang", headers=_header(token))
    assert resp.status_code == 403


def test_wali_kelas_ditolak_mengubah_identitas_sekolah(session_factory, session):
    _buat_user(session, username="wali", password="rahasia123", peran=PeranPengguna.WALI_KELAS)
    client = _client(session_factory)
    token = _login(client, username="wali", password="rahasia123")

    resp = client.put(
        "/pengaturan/identitas-sekolah", json={"nama": "Coba", "alasan": "Uji"}, headers=_header(token)
    )
    assert resp.status_code == 403


# ---------------------------------------------------------------------------
# Jadwal - tambah versi baru, tanggal-berlaku, historis tak bergeser
# ---------------------------------------------------------------------------


def test_tambah_jadwal_baru_berhasil(session_factory, session):
    client = _client(session_factory)
    token = _token_admin(client, session)

    resp = client.post("/pengaturan/jadwal", json=JADWAL_SISWA_SENIN, headers=_header(token))
    assert resp.status_code == 201, resp.text
    body = resp.json()
    assert body["jam_masuk"] == "07:15:00"
    assert body["berlaku_mulai"] == "2026-01-01"


def test_jadwal_berlaku_per_tanggal_historis_tidak_bergeser(session_factory, session):
    """Bukti utama slice ini: menambah versi jadwal baru TIDAK mengubah
    versi lama - tanggal sebelum versi baru mulai berlaku tetap memakai
    jam lama (PRD §4)."""
    client = _client(session_factory)
    token = _token_admin(client, session)

    client.post("/pengaturan/jadwal", json=JADWAL_SISWA_SENIN, headers=_header(token))
    jadwal_baru = dict(JADWAL_SISWA_SENIN, jam_masuk="07:00:00", batas_terlambat="07:10:00", berlaku_mulai="2026-07-01")
    resp = client.post("/pengaturan/jadwal", json=jadwal_baru, headers=_header(token))
    assert resp.status_code == 201, resp.text

    resp_daftar = client.get("/pengaturan/jadwal", params={"jenis_pemilik": "SISWA"}, headers=_header(token))
    assert resp_daftar.status_code == 200
    daftar = resp_daftar.json()
    assert len(daftar) == 2  # baris lama TETAP ADA, tidak ditimpa

    # Buktikan lewat repository yang sesungguhnya dipakai mesin absensi:
    # tanggal sebelum 2026-07-01 masih resolve ke jadwal LAMA.
    repo = ScheduleRepository(session)
    jadwal_lama_berlaku = repo.jadwal_berlaku(jenis_pemilik=JenisPemilik.SISWA, hari=1, tanggal=date(2026, 3, 1))
    jadwal_baru_berlaku = repo.jadwal_berlaku(jenis_pemilik=JenisPemilik.SISWA, hari=1, tanggal=date(2026, 8, 1))
    assert jadwal_lama_berlaku.jam_masuk == time(7, 15)
    assert jadwal_baru_berlaku.jam_masuk == time(7, 0)


def test_tambah_jadwal_guru_tidak_membawa_cutoff_alpa(session_factory, session):
    client = _client(session_factory)
    token = _token_admin(client, session)

    payload = {
        "jenis_pemilik": "GURU",
        "hari": 1,
        "jam_masuk": "07:15:00",
        "batas_terlambat": "09:30:00",
        "jam_pulang": "15:00:00",
        "berlaku_mulai": "2026-01-01",
    }
    resp = client.post("/pengaturan/jadwal", json=payload, headers=_header(token))
    assert resp.status_code == 201, resp.text
    assert resp.json()["cutoff_alpa"] is None
    assert resp.json()["pulang_mulai"] is None


def test_jenis_pemilik_tidak_dikenal_422(session_factory, session):
    client = _client(session_factory)
    token = _token_admin(client, session)

    payload = dict(JADWAL_SISWA_SENIN, jenis_pemilik="TIDAK_ADA")
    resp = client.post("/pengaturan/jadwal", json=payload, headers=_header(token))
    assert resp.status_code == 422


class _SchedulerPalsu:
    def __init__(self):
        self.jumlah_reload = 0

    def reload_jadwal(self):
        self.jumlah_reload += 1


def test_tambah_jadwal_siswa_memuat_ulang_scheduler_bila_terpasang(session_factory, session):
    client = _client(session_factory)
    scheduler_palsu = _SchedulerPalsu()
    client.app.state.scheduler = scheduler_palsu
    token = _token_admin(client, session)

    client.post("/pengaturan/jadwal", json=JADWAL_SISWA_SENIN, headers=_header(token))

    assert scheduler_palsu.jumlah_reload == 1


def test_tambah_jadwal_guru_tidak_memuat_ulang_scheduler(session_factory, session):
    """Jadwal guru tidak memengaruhi cron cutoff pulang/notifikasi (yang
    hanya berbasis jalur siswa) - lihat SchedulerAbsensi._baca_jam_dari_config."""
    client = _client(session_factory)
    scheduler_palsu = _SchedulerPalsu()
    client.app.state.scheduler = scheduler_palsu
    token = _token_admin(client, session)

    payload = {
        "jenis_pemilik": "GURU", "hari": 1, "jam_masuk": "07:15:00", "batas_terlambat": "09:30:00",
        "jam_pulang": "15:00:00", "berlaku_mulai": "2026-01-01",
    }
    client.post("/pengaturan/jadwal", json=payload, headers=_header(token))

    assert scheduler_palsu.jumlah_reload == 0


def test_ubah_ambang_cutoff_alpa_memuat_ulang_scheduler(session_factory, session):
    client = _client(session_factory)
    scheduler_palsu = _SchedulerPalsu()
    client.app.state.scheduler = scheduler_palsu
    token = _token_admin(client, session)

    client.put(
        "/pengaturan/ambang",
        json={"cutoff_alpa": "07:30:00", "alasan": "Uji reload scheduler"},
        headers=_header(token),
    )

    assert scheduler_palsu.jumlah_reload == 1


# ---------------------------------------------------------------------------
# Ambang
# ---------------------------------------------------------------------------


def test_ambang_default_sebelum_diubah(session_factory, session):
    client = _client(session_factory)
    token = _token_admin(client, session)

    resp = client.get("/pengaturan/ambang", headers=_header(token))
    assert resp.status_code == 200
    body = resp.json()
    assert body["jendela_scan_berulang_detik"] == 10.0
    assert body["ambang_tahan_konversi_pulang"] == 0.30
    assert body["cutoff_alpa"] is None


def test_ubah_ambang_sebagian_field_saja(session_factory, session):
    client = _client(session_factory)
    token = _token_admin(client, session)

    resp = client.put(
        "/pengaturan/ambang",
        json={"jendela_scan_berulang_detik": 15.0, "alasan": "Uji override jendela scan"},
        headers=_header(token),
    )
    assert resp.status_code == 200, resp.text
    body = resp.json()
    assert body["jendela_scan_berulang_detik"] == 15.0
    # Field lain yang tidak dikirim tetap default, tidak ikut berubah.
    assert body["ambang_tahan_konversi_pulang"] == 0.30


def test_ubah_ambang_tanpa_alasan_422(session_factory, session):
    client = _client(session_factory)
    token = _token_admin(client, session)

    resp = client.put("/pengaturan/ambang", json={"jendela_scan_berulang_detik": 15.0}, headers=_header(token))
    assert resp.status_code == 422


# ---------------------------------------------------------------------------
# Saklar modul
# ---------------------------------------------------------------------------


def test_saklar_whatsapp_default_mati(session_factory, session):
    client = _client(session_factory)
    token = _token_admin(client, session)

    resp = client.get("/pengaturan/saklar", headers=_header(token))
    assert resp.status_code == 200
    assert resp.json()["modul_whatsapp_aktif"] is False


def test_ubah_saklar_whatsapp(session_factory, session):
    client = _client(session_factory)
    token = _token_admin(client, session)

    resp = client.put(
        "/pengaturan/saklar", json={"modul_whatsapp_aktif": True, "alasan": "Uji aktifkan"}, headers=_header(token)
    )
    assert resp.status_code == 200
    assert resp.json()["modul_whatsapp_aktif"] is True
    assert resp.json()["modul_absen_pulang"] is True  # tidak ikut berubah


# ---------------------------------------------------------------------------
# Identitas sekolah
# ---------------------------------------------------------------------------


def test_identitas_sekolah_kosong_sebelum_diisi(session_factory, session):
    client = _client(session_factory)
    token = _token_admin(client, session)

    resp = client.get("/pengaturan/identitas-sekolah", headers=_header(token))
    assert resp.status_code == 200
    assert resp.json() == {
        "nama": None, "npsn": None, "alamat": None, "logo": None,
        "nama_kepala_sekolah": None, "nip_kepala_sekolah": None,
    }


def test_ubah_identitas_sekolah_lalu_terbaca_kembali(session_factory, session):
    client = _client(session_factory)
    token = _token_admin(client, session)

    resp = client.put(
        "/pengaturan/identitas-sekolah",
        json={
            "nama": "UPT SMP Negeri 1 Pancarijang",
            "npsn": "12345678",
            "alamat": "Jl. Pendidikan No. 1",
            "nama_kepala_sekolah": "Rahmat Perdana, S.Kom.",
            "nip_kepala_sekolah": "19830121 200901 1 007",
            "alasan": "Isi identitas sekolah",
        },
        headers=_header(token),
    )
    assert resp.status_code == 200, resp.text
    assert resp.json()["nama"] == "UPT SMP Negeri 1 Pancarijang"

    resp_get = client.get("/pengaturan/identitas-sekolah", headers=_header(token))
    assert resp_get.json()["npsn"] == "12345678"


def test_ubah_identitas_sekolah_parsial_tidak_menghapus_field_lain(session_factory, session):
    client = _client(session_factory)
    token = _token_admin(client, session)
    client.put(
        "/pengaturan/identitas-sekolah",
        json={"nama": "UPT SMP Negeri 1 Pancarijang", "alasan": "Isi nama dulu"},
        headers=_header(token),
    )

    client.put("/pengaturan/identitas-sekolah", json={"npsn": "12345678", "alasan": "Tambah NPSN"}, headers=_header(token))

    resp = client.get("/pengaturan/identitas-sekolah", headers=_header(token))
    assert resp.json()["nama"] == "UPT SMP Negeri 1 Pancarijang"
    assert resp.json()["npsn"] == "12345678"
