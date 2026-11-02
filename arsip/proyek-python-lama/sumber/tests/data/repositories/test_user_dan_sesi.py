"""Tes UserRepository (kunci akun setelah 5 kali gagal, wajib ganti
password) dan SesiKlienRepository (token, kadaluarsa, pencabutan)."""

from datetime import timedelta

from app.core.timeutil import now_wita
from app.data.models.system import PeranPengguna
from app.data.repositories.session import SesiKlienRepository
from app.data.repositories.user import BATAS_PERCOBAAN_GAGAL, UserRepository


def _buat_user(session) -> int:
    return UserRepository(session).tambah(username="admin_uji", password_hash="hash-palsu", peran=PeranPengguna.ADMIN).id


# ---------------------------------------------------------------------------
# UserRepository - wajib_ganti_password dan kunci akun
# ---------------------------------------------------------------------------


def test_user_baru_wajib_ganti_password_secara_bawaan(session):
    id_user = _buat_user(session)
    pengguna = UserRepository(session).cari_by_id(id_user)
    assert pengguna.wajib_ganti_password is True


def test_percobaan_gagal_dihitung_dan_belum_terkunci_di_bawah_batas(session):
    id_user = _buat_user(session)
    repo = UserRepository(session)

    for _ in range(BATAS_PERCOBAAN_GAGAL - 1):
        repo.catat_login_gagal(id_user)

    pengguna = repo.cari_by_id(id_user)
    assert pengguna.percobaan_gagal_berturut == BATAS_PERCOBAAN_GAGAL - 1
    assert pengguna.terkunci_sampai is None


def test_percobaan_gagal_kelima_mengunci_akun(session):
    id_user = _buat_user(session)
    repo = UserRepository(session)

    for _ in range(BATAS_PERCOBAAN_GAGAL):
        repo.catat_login_gagal(id_user)

    pengguna = repo.cari_by_id(id_user)
    assert pengguna.percobaan_gagal_berturut == BATAS_PERCOBAAN_GAGAL
    assert pengguna.terkunci_sampai is not None
    assert pengguna.terkunci_sampai > now_wita()


def test_login_berhasil_mereset_percobaan_gagal(session):
    id_user = _buat_user(session)
    repo = UserRepository(session)
    repo.catat_login_gagal(id_user)
    repo.catat_login_gagal(id_user)

    repo.catat_login(id_user)

    pengguna = repo.cari_by_id(id_user)
    assert pengguna.percobaan_gagal_berturut == 0
    assert pengguna.terkunci_sampai is None
    assert pengguna.waktu_login_terakhir is not None


def test_atur_password_mengubah_hash_dan_wajib_ganti(session):
    id_user = _buat_user(session)
    repo = UserRepository(session)

    repo.atur_password(id_user, password_hash="hash-baru", wajib_ganti_password=False)

    pengguna = repo.cari_by_id(id_user)
    assert pengguna.password_hash == "hash-baru"
    assert pengguna.wajib_ganti_password is False


# ---------------------------------------------------------------------------
# SesiKlienRepository
# ---------------------------------------------------------------------------


def test_sesi_baru_aktif(session):
    id_user = _buat_user(session)
    repo = SesiKlienRepository(session)

    sesi = repo.buat(id_pengguna=id_user, umur=timedelta(hours=1))

    assert repo.sesi_aktif(sesi.token) is not None


def test_sesi_kadaluarsa_tidak_aktif(session):
    id_user = _buat_user(session)
    repo = SesiKlienRepository(session)
    sesi = repo.buat(id_pengguna=id_user, umur=timedelta(seconds=-1))

    assert repo.sesi_aktif(sesi.token) is None


def test_sesi_yang_dicabut_tidak_aktif(session):
    id_user = _buat_user(session)
    repo = SesiKlienRepository(session)
    sesi = repo.buat(id_pengguna=id_user, umur=timedelta(hours=1))

    repo.cabut(sesi.token)

    assert repo.sesi_aktif(sesi.token) is None


def test_token_yang_tidak_ada_tidak_aktif(session):
    repo = SesiKlienRepository(session)
    assert repo.sesi_aktif("token-yang-tidak-pernah-ada") is None


def test_cabut_semua_milik_pengguna(session):
    id_user = _buat_user(session)
    repo = SesiKlienRepository(session)
    sesi1 = repo.buat(id_pengguna=id_user, umur=timedelta(hours=1))
    sesi2 = repo.buat(id_pengguna=id_user, umur=timedelta(hours=1))

    jumlah = repo.cabut_semua_milik_pengguna(id_user)

    assert jumlah == 2
    assert repo.sesi_aktif(sesi1.token) is None
    assert repo.sesi_aktif(sesi2.token) is None


def test_perbarui_terakhir_dipakai(session):
    id_user = _buat_user(session)
    repo = SesiKlienRepository(session)
    sesi = repo.buat(id_pengguna=id_user, umur=timedelta(hours=1))
    waktu_awal = sesi.terakhir_dipakai

    repo.perbarui_terakhir_dipakai(sesi)

    assert sesi.terakhir_dipakai >= waktu_awal


def test_daftar_aktif_mengecualikan_kadaluarsa_dan_dicabut(session):
    id_user = _buat_user(session)
    repo = SesiKlienRepository(session)
    sesi_aktif = repo.buat(id_pengguna=id_user, umur=timedelta(hours=1))
    sesi_kadaluarsa = repo.buat(id_pengguna=id_user, umur=timedelta(seconds=-1))
    sesi_dicabut = repo.buat(id_pengguna=id_user, umur=timedelta(hours=1))
    repo.cabut(sesi_dicabut.token)

    aktif = repo.daftar_aktif()

    assert [s.id for s in aktif] == [sesi_aktif.id]
    assert sesi_kadaluarsa.id not in [s.id for s in aktif]
