"""
AuthService: login, logout, verifikasi token, dan ganti password.

Lapisan integrasi DB (sama seperti app.core.engine dan
app.core.koreksi_massal_service) - khusus untuk app/server/, bukan
dipakai app/core/ mana pun.
"""

from __future__ import annotations

from dataclasses import dataclass
from datetime import datetime, timedelta

from app.core.timeutil import now_wita
from app.data.models.system import User
from app.data.repositories.audit_log import AuditLogRepository
from app.data.repositories.session import SesiKlienRepository
from app.data.repositories.user import UserRepository
from app.server.security import hash_password, verifikasi_password

#: Sesi berlaku 12 jam - cukup untuk satu hari kerja penuh tanpa harus
#: login ulang di tengah jam sibuk pagi.
UMUR_TOKEN_BAWAAN = timedelta(hours=12)


class LoginGagal(Exception):
    """Username tidak ada, akun nonaktif, atau password salah - sengaja
    tidak dibedakan ke pemanggil supaya tidak membocorkan username mana
    yang terdaftar."""


class AkunTerkunci(Exception):
    """Salah 5 kali berturut-turut - jeda 30 detik (SKILL.md)."""

    def __init__(self, sisa_detik: float) -> None:
        self.sisa_detik = max(0.0, sisa_detik)
        super().__init__(f"Akun terkunci sementara, coba lagi {self.sisa_detik:.0f} detik lagi")


class TokenTidakValid(Exception):
    """Token tidak ada, sudah dicabut, atau sudah kadaluarsa - klien harus login ulang."""


@dataclass(frozen=True)
class HasilLogin:
    token: str
    pengguna: User
    wajib_ganti_password: bool
    kadaluarsa: datetime


class AuthService:
    def __init__(
        self,
        *,
        user_repo: UserRepository,
        sesi_repo: SesiKlienRepository,
        audit_repo: AuditLogRepository,
        umur_token: timedelta = UMUR_TOKEN_BAWAAN,
    ) -> None:
        self._user_repo = user_repo
        self._sesi_repo = sesi_repo
        self._audit_repo = audit_repo
        self._umur_token = umur_token

    def login(self, *, username: str, password: str) -> HasilLogin:
        pengguna = self._user_repo.cari_by_username(username)
        if pengguna is None or not pengguna.aktif:
            raise LoginGagal()

        if pengguna.terkunci_sampai is not None and pengguna.terkunci_sampai > now_wita():
            raise AkunTerkunci((pengguna.terkunci_sampai - now_wita()).total_seconds())

        if not verifikasi_password(password, pengguna.password_hash):
            self._user_repo.catat_login_gagal(pengguna.id)
            raise LoginGagal()

        self._user_repo.catat_login(pengguna.id)
        sesi = self._sesi_repo.buat(id_pengguna=pengguna.id, umur=self._umur_token)
        self._audit_repo.catat(
            id_pengguna=pengguna.id,
            aksi="login",
            nilai_sebelum=None,
            nilai_sesudah=None,
            alasan="Login berhasil",
        )
        return HasilLogin(
            token=sesi.token,
            pengguna=pengguna,
            wajib_ganti_password=pengguna.wajib_ganti_password,
            kadaluarsa=sesi.kadaluarsa,
        )

    def logout(self, token: str) -> None:
        self._sesi_repo.cabut(token)

    def verifikasi_token(self, token: str) -> User:
        sesi = self._sesi_repo.sesi_aktif(token)
        if sesi is None:
            raise TokenTidakValid()
        pengguna = self._user_repo.cari_by_id(sesi.id_pengguna)
        if pengguna is None or not pengguna.aktif:
            raise TokenTidakValid()
        self._sesi_repo.perbarui_terakhir_dipakai(sesi)
        return pengguna

    def ganti_password(self, id_pengguna: int, *, password_lama: str, password_baru: str) -> None:
        """Mencabut SELURUH sesi milik pengguna ini setelah berhasil -
        termasuk sesi yang sedang dipakai memanggil ini sendiri. Klien
        wajib login ulang dengan password baru, di perangkat mana pun.
        """
        pengguna = self._user_repo.cari_by_id(id_pengguna)
        if pengguna is None:
            raise TokenTidakValid()
        if not verifikasi_password(password_lama, pengguna.password_hash):
            raise LoginGagal()

        self._user_repo.atur_password(
            id_pengguna, password_hash=hash_password(password_baru), wajib_ganti_password=False
        )
        self._sesi_repo.cabut_semua_milik_pengguna(id_pengguna)
        self._audit_repo.catat(
            id_pengguna=id_pengguna,
            aksi="ganti_password",
            nilai_sebelum=None,
            nilai_sesudah=None,
            alasan="Ganti password oleh pengguna sendiri",
        )
