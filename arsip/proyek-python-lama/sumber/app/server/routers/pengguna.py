"""Endpoint manajemen akun & peran (PRD §10). Akses kepala sekolah dan
admin - wewenang lebih rinci per peran TARGET ditegakkan di
app.server.pengguna_service.PenggunaService (kepala sekolah berwenang
atas keempat peran, admin hanya wali kelas & operator - lihat docstring
modul itu untuk rujukan PRD persisnya).

TIDAK menyentuh app.core.engine/mesin_absensi. Pembatasan kelas wali
kelas (app.core.akses_kelas.pastikan_akses_kelas) belum dipanggil dari
router mana pun di sini - PENETAPAN wali kelas ke kelas (siapa mengampu
kelas apa) adalah tanggung jawab slice ini; PENEGAKAN batasan itu saat
wali kelas menyentuh data (mis. koreksi satuan) adalah tanggung jawab
endpoint tersebut kelak, belum dibangun.
"""

from typing import Annotated

from fastapi import APIRouter, Depends, HTTPException, status
from sqlalchemy.orm import Session

from app.core.status import Peran
from app.data.models.system import PeranPengguna, User
from app.data.repositories.audit_log import AuditLogRepository
from app.data.repositories.kelas import KelasRepository
from app.data.repositories.session import SesiKlienRepository
from app.data.repositories.user import UserRepository
from app.server.dependencies import CurrentUserDep, SessionDep, wajib_peran
from app.server.pengguna_service import (
    IdKelasDiampuHanyaUntukWaliKelas,
    KelasTidakDitemukan,
    PenggunaService,
    PenggunaTidakDitemukan,
    PeranTidakBerwenang,
    UsernameSudahDipakai,
)
from app.server.schemas import (
    AturKelasDiampuRequest,
    PenggunaBuatRequest,
    PenggunaResponse,
    ResetPasswordAkunRequest,
)

router = APIRouter(prefix="/pengguna", tags=["pengguna"])

KewenanganDep = Annotated[User, Depends(wajib_peran(Peran.KEPALA_SEKOLAH, Peran.ADMIN))]


def _buat_service(session: Session) -> PenggunaService:
    return PenggunaService(
        user_repo=UserRepository(session),
        kelas_repo=KelasRepository(session),
        sesi_repo=SesiKlienRepository(session),
        audit_repo=AuditLogRepository(session),
    )


def _ke_response(pengguna: User) -> PenggunaResponse:
    return PenggunaResponse(
        id=pengguna.id,
        username=pengguna.username,
        peran=pengguna.peran.value,
        id_kelas_diampu=pengguna.id_kelas_diampu,
        aktif=pengguna.aktif,
        wajib_ganti_password=pengguna.wajib_ganti_password,
        waktu_login_terakhir=pengguna.waktu_login_terakhir,
    )


@router.post("", response_model=PenggunaResponse, status_code=status.HTTP_201_CREATED)
def buat_akun(payload: PenggunaBuatRequest, session: SessionDep, pengguna: KewenanganDep) -> PenggunaResponse:
    try:
        baru = _buat_service(session).buat_akun(
            pembuat=pengguna,
            username=payload.username,
            password_awal=payload.password_awal,
            peran=PeranPengguna(payload.peran),
            id_kelas_diampu=payload.id_kelas_diampu,
        )
    except PeranTidakBerwenang as exc:
        raise HTTPException(status.HTTP_403_FORBIDDEN, str(exc)) from exc
    except UsernameSudahDipakai as exc:
        raise HTTPException(status.HTTP_409_CONFLICT, f"Username {exc} sudah dipakai") from exc
    except KelasTidakDitemukan as exc:
        raise HTTPException(status.HTTP_404_NOT_FOUND, "Kelas tidak ditemukan") from exc
    except IdKelasDiampuHanyaUntukWaliKelas as exc:
        raise HTTPException(
            status.HTTP_422_UNPROCESSABLE_ENTITY, "id_kelas_diampu hanya berlaku untuk peran WALI_KELAS"
        ) from exc
    return _ke_response(baru)


@router.get("", response_model=list[PenggunaResponse])
def daftar_akun(session: SessionDep, pengguna: KewenanganDep) -> list[PenggunaResponse]:
    return [_ke_response(p) for p in _buat_service(session).daftar()]


@router.post("/{id_target}/reset-password", response_model=PenggunaResponse)
def reset_password(
    id_target: int, payload: ResetPasswordAkunRequest, session: SessionDep, pengguna: KewenanganDep
) -> PenggunaResponse:
    try:
        diperbarui = _buat_service(session).reset_password(
            pelaku=pengguna, id_target=id_target, password_baru=payload.password_baru
        )
    except PenggunaTidakDitemukan as exc:
        raise HTTPException(status.HTTP_404_NOT_FOUND, "Akun tidak ditemukan") from exc
    except PeranTidakBerwenang as exc:
        raise HTTPException(status.HTTP_403_FORBIDDEN, str(exc)) from exc
    return _ke_response(diperbarui)


@router.put("/{id_target}/kelas-diampu", response_model=PenggunaResponse)
def atur_kelas_diampu(
    id_target: int, payload: AturKelasDiampuRequest, session: SessionDep, pengguna: KewenanganDep
) -> PenggunaResponse:
    try:
        diperbarui = _buat_service(session).atur_kelas_diampu(
            pelaku=pengguna, id_target=id_target, id_kelas_diampu=payload.id_kelas_diampu
        )
    except PenggunaTidakDitemukan as exc:
        raise HTTPException(status.HTTP_404_NOT_FOUND, "Akun tidak ditemukan") from exc
    except IdKelasDiampuHanyaUntukWaliKelas as exc:
        raise HTTPException(
            status.HTTP_422_UNPROCESSABLE_ENTITY, "Hanya akun berperan WALI_KELAS yang punya kelas diampu"
        ) from exc
    except KelasTidakDitemukan as exc:
        raise HTTPException(status.HTTP_404_NOT_FOUND, "Kelas tidak ditemukan") from exc
    return _ke_response(diperbarui)
