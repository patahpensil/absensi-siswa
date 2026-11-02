"""Dependency FastAPI bersama: Session per-request, AuthService, dan
pengguna saat ini lewat token Bearer.
"""

from __future__ import annotations

from collections.abc import Generator
from pathlib import Path
from typing import Annotated

from fastapi import Depends, HTTPException, Request, status
from fastapi.security import HTTPAuthorizationCredentials, HTTPBearer
from sqlalchemy.orm import Session

from app.core.status import Peran
from app.data.models.system import User
from app.data.repositories.audit_log import AuditLogRepository
from app.data.repositories.session import SesiKlienRepository
from app.data.repositories.user import UserRepository
from app.server.auth_service import AuthService, TokenTidakValid

_bearer = HTTPBearer(auto_error=False)


def get_session(request: Request) -> Generator[Session, None, None]:
    session_factory = request.app.state.session_factory
    session = session_factory()
    try:
        yield session
    finally:
        session.close()


SessionDep = Annotated[Session, Depends(get_session)]


def get_lampiran_dir(request: Request) -> Path:
    """Direktori dasar tempat lampiran koreksi guru disimpan (lihat
    app.server.app.buat_app() dan app.server.lampiran_storage)."""
    return request.app.state.lampiran_dir


LampiranDirDep = Annotated[Path, Depends(get_lampiran_dir)]


def get_laporan_dir(request: Request) -> Path:
    """Direktori dasar tempat arsip PDF laporan disimpan (lihat
    app.server.app.buat_app() dan app.server.laporan_storage)."""
    return request.app.state.laporan_dir


LaporanDirDep = Annotated[Path, Depends(get_laporan_dir)]


def get_auth_service(session: SessionDep) -> AuthService:
    return AuthService(
        user_repo=UserRepository(session),
        sesi_repo=SesiKlienRepository(session),
        audit_repo=AuditLogRepository(session),
    )


AuthServiceDep = Annotated[AuthService, Depends(get_auth_service)]


def get_bearer_token(
    credentials: Annotated[HTTPAuthorizationCredentials | None, Depends(_bearer)],
) -> str:
    if credentials is None:
        raise HTTPException(status.HTTP_401_UNAUTHORIZED, "Token tidak disertakan")
    return credentials.credentials


BearerTokenDep = Annotated[str, Depends(get_bearer_token)]


def get_current_user(token: BearerTokenDep, auth_service: AuthServiceDep) -> User:
    try:
        return auth_service.verifikasi_token(token)
    except TokenTidakValid as exc:
        raise HTTPException(status.HTTP_401_UNAUTHORIZED, "Token tidak valid atau kadaluarsa") from exc


CurrentUserDep = Annotated[User, Depends(get_current_user)]


def wajib_peran(*peran_diizinkan: Peran):
    """Dependency factory: tolak dengan 403 kalau peran pengguna yang
    sedang login tidak termasuk peran_diizinkan. Dipakai lewat
    Depends(wajib_peran(Peran.KEPALA_SEKOLAH, Peran.ADMIN))."""

    def _cek(pengguna: CurrentUserDep) -> User:
        if Peran(pengguna.peran.value) not in peran_diizinkan:
            raise HTTPException(status.HTTP_403_FORBIDDEN, "Peran tidak berwenang untuk aksi ini")
        return pengguna

    return _cek
