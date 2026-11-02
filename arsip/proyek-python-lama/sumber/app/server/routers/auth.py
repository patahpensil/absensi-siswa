from fastapi import APIRouter, HTTPException, status

from app.server.auth_service import AkunTerkunci, LoginGagal
from app.server.dependencies import AuthServiceDep, BearerTokenDep, CurrentUserDep
from app.server.schemas import GantiPasswordRequest, LoginRequest, LoginResponse

router = APIRouter(prefix="/auth", tags=["auth"])


@router.post("/login", response_model=LoginResponse)
def login(payload: LoginRequest, auth_service: AuthServiceDep) -> LoginResponse:
    try:
        hasil = auth_service.login(username=payload.username, password=payload.password)
    except AkunTerkunci as exc:
        raise HTTPException(status.HTTP_423_LOCKED, str(exc)) from exc
    except LoginGagal as exc:
        raise HTTPException(status.HTTP_401_UNAUTHORIZED, "Username atau password salah") from exc

    return LoginResponse(
        token=hasil.token,
        peran=hasil.pengguna.peran.value,
        wajib_ganti_password=hasil.wajib_ganti_password,
        kadaluarsa=hasil.kadaluarsa,
    )


@router.post("/logout", status_code=status.HTTP_204_NO_CONTENT)
def logout(token: BearerTokenDep, auth_service: AuthServiceDep) -> None:
    auth_service.logout(token)


@router.post("/ganti-password", status_code=status.HTTP_204_NO_CONTENT)
def ganti_password(
    payload: GantiPasswordRequest,
    pengguna: CurrentUserDep,
    auth_service: AuthServiceDep,
) -> None:
    try:
        auth_service.ganti_password(
            pengguna.id, password_lama=payload.password_lama, password_baru=payload.password_baru
        )
    except LoginGagal as exc:
        raise HTTPException(status.HTTP_401_UNAUTHORIZED, "Password lama salah") from exc
