"""Endpoint data master siswa (PRD §11.1). Akses kepala sekolah + admin
saja (SKILL.md "Peran dan hak akses": data master = Kepsek/Admin).
"""

from typing import Annotated

from fastapi import APIRouter, Depends, HTTPException, status
from sqlalchemy.orm import Session

from app.core.status import Peran
from app.data.models.system import User
from app.data.repositories.audit_log import AuditLogRepository
from app.data.repositories.guardian import GuardianRepository
from app.data.repositories.kelas import KelasRepository
from app.data.repositories.student import StudentRepository
from app.server.data_master_service import (
    KelasTidakDitemukan,
    NikSudahDipakai,
    NisnSudahDipakai,
    SiswaService,
    SiswaTidakDitemukan,
)
from app.server.dependencies import SessionDep, wajib_peran
from app.server.schemas import SiswaBuatRequest, SiswaDetailResponse, SiswaRingkasResponse, SiswaUbahRequest
from app.core.validasi_data_master import samarkan_nik

router = APIRouter(prefix="/siswa", tags=["data-master-siswa"])

KewenanganDep = Annotated[User, Depends(wajib_peran(Peran.KEPALA_SEKOLAH, Peran.ADMIN))]


def _buat_service(session: Session) -> SiswaService:
    return SiswaService(
        student_repo=StudentRepository(session),
        guardian_repo=GuardianRepository(session),
        kelas_repo=KelasRepository(session),
        audit_repo=AuditLogRepository(session),
    )


def _ke_ringkas(session: Session, siswa) -> SiswaRingkasResponse:
    nama_kelas = None
    if siswa.id_kelas is not None:
        kelas = KelasRepository(session).cari_by_id(siswa.id_kelas)
        nama_kelas = kelas.nama_kelas if kelas is not None else None
    nama_ortu, kontak_ortu = _buat_service(session).nama_ortu_dan_kontak(siswa.id)
    return SiswaRingkasResponse(
        id=siswa.id,
        nisn=siswa.nisn,
        nipd=siswa.nipd,
        nama_lengkap=siswa.nama_lengkap,
        jenis_kelamin=siswa.jenis_kelamin.value,
        tempat_lahir=siswa.tempat_lahir,
        tanggal_lahir=siswa.tanggal_lahir,
        nik_tersamar=samarkan_nik(siswa.nik),
        alamat=siswa.alamat,
        id_kelas=siswa.id_kelas,
        nama_kelas=nama_kelas,
        status_siswa=siswa.status_siswa.value,
        aktif=siswa.aktif,
        nama_ortu=nama_ortu,
        nomor_whatsapp_ortu=kontak_ortu,
    )


def _ke_detail(session: Session, siswa) -> SiswaDetailResponse:
    ringkas = _ke_ringkas(session, siswa)
    return SiswaDetailResponse(**ringkas.model_dump(), nik=siswa.nik)


@router.post("", response_model=SiswaDetailResponse, status_code=status.HTTP_201_CREATED)
def buat(payload: SiswaBuatRequest, session: SessionDep, pengguna: KewenanganDep) -> SiswaDetailResponse:
    try:
        siswa = _buat_service(session).buat(data=payload, id_pengguna=pengguna.id)
    except NisnSudahDipakai as exc:
        raise HTTPException(status.HTTP_409_CONFLICT, f"NISN {exc} sudah dipakai siswa lain") from exc
    except NikSudahDipakai as exc:
        raise HTTPException(status.HTTP_409_CONFLICT, f"NIK {exc} sudah dipakai siswa lain") from exc
    except KelasTidakDitemukan as exc:
        raise HTTPException(status.HTTP_404_NOT_FOUND, "Kelas tidak ditemukan") from exc
    return _ke_detail(session, siswa)


@router.put("/{id_siswa}", response_model=SiswaDetailResponse)
def ubah(
    id_siswa: int, payload: SiswaUbahRequest, session: SessionDep, pengguna: KewenanganDep
) -> SiswaDetailResponse:
    try:
        siswa = _buat_service(session).ubah(id_siswa, data=payload, id_pengguna=pengguna.id)
    except SiswaTidakDitemukan as exc:
        raise HTTPException(status.HTTP_404_NOT_FOUND, "Siswa tidak ditemukan") from exc
    except NikSudahDipakai as exc:
        raise HTTPException(status.HTTP_409_CONFLICT, f"NIK {exc} sudah dipakai siswa lain") from exc
    except KelasTidakDitemukan as exc:
        raise HTTPException(status.HTTP_404_NOT_FOUND, "Kelas tidak ditemukan") from exc
    return _ke_detail(session, siswa)


@router.get("/{id_siswa}", response_model=SiswaDetailResponse)
def detail(id_siswa: int, session: SessionDep, pengguna: KewenanganDep) -> SiswaDetailResponse:
    siswa = _buat_service(session).cari_by_id(id_siswa)
    if siswa is None:
        raise HTTPException(status.HTTP_404_NOT_FOUND, "Siswa tidak ditemukan")
    return _ke_detail(session, siswa)


@router.get("", response_model=list[SiswaRingkasResponse])
def daftar(session: SessionDep, pengguna: KewenanganDep) -> list[SiswaRingkasResponse]:
    siswa_list = _buat_service(session).daftar_aktif()
    return [_ke_ringkas(session, s) for s in siswa_list]
