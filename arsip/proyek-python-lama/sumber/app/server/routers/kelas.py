"""Endpoint data master kelas (PRD §11.3). Akses kepala sekolah + admin saja."""

from typing import Annotated

from fastapi import APIRouter, Depends, HTTPException, status
from sqlalchemy.orm import Session

from app.core.status import Peran
from app.data.models.system import User
from app.data.repositories.audit_log import AuditLogRepository
from app.data.repositories.kelas import KelasRepository
from app.data.repositories.teacher import TeacherRepository
from app.server.data_master_service import GuruTidakDitemukan, KelasService, KelasTidakDitemukan
from app.server.dependencies import SessionDep, wajib_peran
from app.server.schemas import KelasBuatRequest, KelasResponse, KelasUbahRequest

router = APIRouter(prefix="/kelas", tags=["data-master-kelas"])

KewenanganDep = Annotated[User, Depends(wajib_peran(Peran.KEPALA_SEKOLAH, Peran.ADMIN))]


def _buat_service(session: Session) -> KelasService:
    return KelasService(
        kelas_repo=KelasRepository(session),
        teacher_repo=TeacherRepository(session),
        audit_repo=AuditLogRepository(session),
    )


def _ke_response(session: Session, kelas) -> KelasResponse:
    nama_wali = None
    if kelas.wali_kelas_id is not None:
        wali = TeacherRepository(session).cari_by_id(kelas.wali_kelas_id)
        nama_wali = wali.nama_lengkap if wali is not None else None
    return KelasResponse(
        id=kelas.id,
        tingkat=kelas.tingkat,
        nama_kelas=kelas.nama_kelas,
        tahun_ajaran=kelas.tahun_ajaran,
        wali_kelas_id=kelas.wali_kelas_id,
        nama_wali_kelas=nama_wali,
        aktif=kelas.aktif,
    )


@router.post("", response_model=KelasResponse, status_code=status.HTTP_201_CREATED)
def buat(payload: KelasBuatRequest, session: SessionDep, pengguna: KewenanganDep) -> KelasResponse:
    try:
        kelas = _buat_service(session).buat(data=payload, id_pengguna=pengguna.id)
    except GuruTidakDitemukan as exc:
        raise HTTPException(status.HTTP_404_NOT_FOUND, "Wali kelas (guru) tidak ditemukan") from exc
    return _ke_response(session, kelas)


@router.put("/{id_kelas}", response_model=KelasResponse)
def ubah(id_kelas: int, payload: KelasUbahRequest, session: SessionDep, pengguna: KewenanganDep) -> KelasResponse:
    try:
        kelas = _buat_service(session).ubah(id_kelas, data=payload, id_pengguna=pengguna.id)
    except KelasTidakDitemukan as exc:
        raise HTTPException(status.HTTP_404_NOT_FOUND, "Kelas tidak ditemukan") from exc
    except GuruTidakDitemukan as exc:
        raise HTTPException(status.HTTP_404_NOT_FOUND, "Wali kelas (guru) tidak ditemukan") from exc
    return _ke_response(session, kelas)


@router.delete("/{id_kelas}", status_code=status.HTTP_204_NO_CONTENT)
def arsipkan(id_kelas: int, session: SessionDep, pengguna: KewenanganDep) -> None:
    """Hapus fisik bila kelas kosong, arsipkan (aktif=False) bila masih
    ada siswa aktif - kelas ber-riwayat tak pernah dihapus (PRD §11.3)."""
    try:
        _buat_service(session).arsipkan(id_kelas, id_pengguna=pengguna.id)
    except KelasTidakDitemukan as exc:
        raise HTTPException(status.HTTP_404_NOT_FOUND, "Kelas tidak ditemukan") from exc


@router.get("/{id_kelas}", response_model=KelasResponse)
def detail(id_kelas: int, session: SessionDep, pengguna: KewenanganDep) -> KelasResponse:
    kelas = _buat_service(session).cari_by_id(id_kelas)
    if kelas is None:
        raise HTTPException(status.HTTP_404_NOT_FOUND, "Kelas tidak ditemukan")
    return _ke_response(session, kelas)


@router.get("", response_model=list[KelasResponse])
def daftar(session: SessionDep, pengguna: KewenanganDep) -> list[KelasResponse]:
    return [_ke_response(session, k) for k in _buat_service(session).daftar()]
