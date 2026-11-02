"""Endpoint data master guru (PRD §11.2). Akses kepala sekolah + admin saja."""

from typing import Annotated

from fastapi import APIRouter, Depends, HTTPException, status
from sqlalchemy.orm import Session

from app.core.status import Peran
from app.data.models.system import User
from app.data.repositories.audit_log import AuditLogRepository
from app.data.repositories.teacher import TeacherRepository
from app.server.data_master_service import GuruService, GuruTidakDitemukan, NipSudahDipakai
from app.server.dependencies import SessionDep, wajib_peran
from app.server.schemas import GuruBuatRequest, GuruResponse, GuruUbahRequest

router = APIRouter(prefix="/guru", tags=["data-master-guru"])

KewenanganDep = Annotated[User, Depends(wajib_peran(Peran.KEPALA_SEKOLAH, Peran.ADMIN))]


def _buat_service(session: Session) -> GuruService:
    return GuruService(teacher_repo=TeacherRepository(session), audit_repo=AuditLogRepository(session))


def _ke_response(guru) -> GuruResponse:
    return GuruResponse(
        id=guru.id, nama_lengkap=guru.nama_lengkap, nip=guru.nip, pangkat_jabatan=guru.pangkat_jabatan, aktif=guru.aktif
    )


@router.post("", response_model=GuruResponse, status_code=status.HTTP_201_CREATED)
def buat(payload: GuruBuatRequest, session: SessionDep, pengguna: KewenanganDep) -> GuruResponse:
    try:
        guru = _buat_service(session).buat(data=payload, id_pengguna=pengguna.id)
    except NipSudahDipakai as exc:
        raise HTTPException(status.HTTP_409_CONFLICT, f"NIP {exc} sudah dipakai guru lain") from exc
    return _ke_response(guru)


@router.put("/{id_guru}", response_model=GuruResponse)
def ubah(id_guru: int, payload: GuruUbahRequest, session: SessionDep, pengguna: KewenanganDep) -> GuruResponse:
    try:
        guru = _buat_service(session).ubah(id_guru, data=payload, id_pengguna=pengguna.id)
    except GuruTidakDitemukan as exc:
        raise HTTPException(status.HTTP_404_NOT_FOUND, "Guru tidak ditemukan") from exc
    except NipSudahDipakai as exc:
        raise HTTPException(status.HTTP_409_CONFLICT, f"NIP {exc} sudah dipakai guru lain") from exc
    return _ke_response(guru)


@router.get("/{id_guru}", response_model=GuruResponse)
def detail(id_guru: int, session: SessionDep, pengguna: KewenanganDep) -> GuruResponse:
    guru = _buat_service(session).cari_by_id(id_guru)
    if guru is None:
        raise HTTPException(status.HTTP_404_NOT_FOUND, "Guru tidak ditemukan")
    return _ke_response(guru)


@router.get("", response_model=list[GuruResponse])
def daftar(session: SessionDep, pengguna: KewenanganDep) -> list[GuruResponse]:
    return [_ke_response(g) for g in _buat_service(session).daftar()]
