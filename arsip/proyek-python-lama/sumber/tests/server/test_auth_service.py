"""Tes AuthService: login (termasuk kunci akun setelah 5 kali gagal),
verifikasi token, logout, dan ganti password (mencabut seluruh sesi)."""

import pytest

from app.data.models.system import PeranPengguna
from app.data.repositories.audit_log import AuditLogRepository
from app.data.repositories.session import SesiKlienRepository
from app.data.repositories.user import UserRepository
from app.server.auth_service import AkunTerkunci, AuthService, LoginGagal, TokenTidakValid
from app.server.security import hash_password


def _buat_service(session) -> AuthService:
    return AuthService(
        user_repo=UserRepository(session),
        sesi_repo=SesiKlienRepository(session),
        audit_repo=AuditLogRepository(session),
    )


def _buat_user(session, *, username="admin_uji", password="rahasia123", aktif=True) -> int:
    pengguna = UserRepository(session).tambah(
        username=username, password_hash=hash_password(password), peran=PeranPengguna.ADMIN
    )
    if not aktif:
        pengguna.aktif = False
        session.commit()
    return pengguna.id


def test_login_berhasil_mengembalikan_token(session):
    _buat_user(session, username="admin1", password="rahasia123")
    service = _buat_service(session)

    hasil = service.login(username="admin1", password="rahasia123")

    assert hasil.token
    assert hasil.pengguna.username == "admin1"
    assert hasil.wajib_ganti_password is True  # bawaan akun baru


def test_login_password_salah_gagal(session):
    _buat_user(session, username="admin2", password="rahasia123")
    service = _buat_service(session)

    with pytest.raises(LoginGagal):
        service.login(username="admin2", password="salah")


def test_login_username_tidak_ada_gagal(session):
    service = _buat_service(session)
    with pytest.raises(LoginGagal):
        service.login(username="tidak-ada", password="apapun")


def test_login_akun_nonaktif_gagal(session):
    _buat_user(session, username="admin3", password="rahasia123", aktif=False)
    service = _buat_service(session)
    with pytest.raises(LoginGagal):
        service.login(username="admin3", password="rahasia123")


def test_login_terkunci_setelah_lima_kali_gagal(session):
    _buat_user(session, username="admin4", password="rahasia123")
    service = _buat_service(session)

    for _ in range(5):
        with pytest.raises(LoginGagal):
            service.login(username="admin4", password="salah")

    with pytest.raises(AkunTerkunci):
        service.login(username="admin4", password="rahasia123")  # password benar pun ditolak selagi terkunci


def test_verifikasi_token_berhasil(session):
    _buat_user(session, username="admin5", password="rahasia123")
    service = _buat_service(session)
    hasil = service.login(username="admin5", password="rahasia123")

    pengguna = service.verifikasi_token(hasil.token)

    assert pengguna.username == "admin5"


def test_verifikasi_token_tidak_valid(session):
    service = _buat_service(session)
    with pytest.raises(TokenTidakValid):
        service.verifikasi_token("token-asal-asalan")


def test_logout_mencabut_token(session):
    _buat_user(session, username="admin6", password="rahasia123")
    service = _buat_service(session)
    hasil = service.login(username="admin6", password="rahasia123")

    service.logout(hasil.token)

    with pytest.raises(TokenTidakValid):
        service.verifikasi_token(hasil.token)


def test_ganti_password_berhasil_dan_mencabut_semua_sesi(session):
    _buat_user(session, username="admin7", password="rahasia123")
    service = _buat_service(session)
    hasil = service.login(username="admin7", password="rahasia123")

    service.ganti_password(hasil.pengguna.id, password_lama="rahasia123", password_baru="rahasiabaru456")

    # Sesi lama (termasuk yang dipakai memanggil ganti_password) dicabut.
    with pytest.raises(TokenTidakValid):
        service.verifikasi_token(hasil.token)

    # Password baru berlaku, password lama sudah tidak.
    with pytest.raises(LoginGagal):
        service.login(username="admin7", password="rahasia123")
    hasil_baru = service.login(username="admin7", password="rahasiabaru456")
    assert hasil_baru.wajib_ganti_password is False


def test_ganti_password_lama_salah_ditolak(session):
    _buat_user(session, username="admin8", password="rahasia123")
    service = _buat_service(session)
    hasil = service.login(username="admin8", password="rahasia123")

    with pytest.raises(LoginGagal):
        service.ganti_password(hasil.pengguna.id, password_lama="salah", password_baru="rahasiabaru456")
