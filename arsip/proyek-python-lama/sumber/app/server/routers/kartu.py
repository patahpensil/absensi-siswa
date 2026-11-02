"""Endpoint siklus hidup kartu RFID: terbitkan, nonaktifkan, cetak ulang
(PRD §11.4, §12.6). Akses kepala sekolah + admin saja.

Pendaftaran murni mengikat UID<->pemilik - TIDAK PERNAH memanggil
app.core.engine.MesinAbsensi.proses_scan maupun klasifikasi kehadiran
apa pun. Klien menormalisasi UID sendiri (app/rfid/) sebelum mengirim,
persis seperti /scan - server tidak pernah menormalisasi ulang.
"""

from typing import Annotated

from fastapi import APIRouter, Depends, HTTPException, status
from sqlalchemy.orm import Session

from app.core.status import Peran
from app.data.models.enums import JenisPemilik
from app.data.models.system import User
from app.data.repositories.audit_log import AuditLogRepository
from app.data.repositories.rfid_card import RfidCardRepository
from app.data.repositories.student import StudentRepository
from app.data.repositories.teacher import TeacherRepository
from app.server.data_master_service import (
    KartuService,
    KartuTidakDitemukan,
    PemilikTidakDitemukan,
    UidKartuSudahDipakai,
)
from app.server.dependencies import SessionDep, wajib_peran
from app.server.schemas import (
    KartuCekResponse,
    KartuCetakUlangRequest,
    KartuNonaktifkanRequest,
    KartuResponse,
    KartuTerbitkanRequest,
)

router = APIRouter(prefix="/kartu", tags=["data-master-kartu"])

KewenanganDep = Annotated[User, Depends(wajib_peran(Peran.KEPALA_SEKOLAH, Peran.ADMIN))]


def _buat_service(session: Session) -> KartuService:
    return KartuService(
        rfid_card_repo=RfidCardRepository(session),
        student_repo=StudentRepository(session),
        teacher_repo=TeacherRepository(session),
        audit_repo=AuditLogRepository(session),
    )


def _ke_response(kartu) -> KartuResponse:
    return KartuResponse(
        id=kartu.id,
        uid_kartu=kartu.uid_kartu,
        id_pemilik=kartu.id_pemilik,
        jenis_pemilik=kartu.jenis_pemilik.value,
        status=kartu.status.value,
        tanggal_terbit=kartu.tanggal_terbit,
    )


@router.post("", response_model=KartuResponse, status_code=status.HTTP_201_CREATED)
def terbitkan(payload: KartuTerbitkanRequest, session: SessionDep, pengguna: KewenanganDep) -> KartuResponse:
    try:
        kartu = _buat_service(session).terbitkan(data=payload, id_pengguna=pengguna.id)
    except PemilikTidakDitemukan as exc:
        raise HTTPException(status.HTTP_404_NOT_FOUND, "Pemilik (siswa/guru) tidak ditemukan") from exc
    except UidKartuSudahDipakai as exc:
        raise HTTPException(status.HTTP_409_CONFLICT, f"UID kartu {exc} sudah terdaftar") from exc
    return _ke_response(kartu)


@router.post("/{id_kartu}/nonaktifkan", response_model=KartuResponse)
def nonaktifkan(
    id_kartu: int, payload: KartuNonaktifkanRequest, session: SessionDep, pengguna: KewenanganDep
) -> KartuResponse:
    try:
        kartu = _buat_service(session).nonaktifkan(id_kartu, alasan=payload.alasan, id_pengguna=pengguna.id)
    except KartuTidakDitemukan as exc:
        raise HTTPException(status.HTTP_404_NOT_FOUND, "Kartu tidak ditemukan") from exc
    return _ke_response(kartu)


@router.post("/{id_kartu}/cetak-ulang", response_model=KartuResponse)
def cetak_ulang(
    id_kartu: int, payload: KartuCetakUlangRequest, session: SessionDep, pengguna: KewenanganDep
) -> KartuResponse:
    try:
        kartu_baru = _buat_service(session).cetak_ulang(id_kartu, data=payload, id_pengguna=pengguna.id)
    except KartuTidakDitemukan as exc:
        raise HTTPException(status.HTTP_404_NOT_FOUND, "Kartu tidak ditemukan") from exc
    except UidKartuSudahDipakai as exc:
        raise HTTPException(status.HTTP_409_CONFLICT, f"UID kartu {exc} sudah terdaftar") from exc
    return _ke_response(kartu_baru)


@router.get("/cek/{uid_kartu}", response_model=KartuCekResponse)
def cek_uid(uid_kartu: str, session: SessionDep, pengguna: KewenanganDep) -> KartuCekResponse:
    """Layar Uji Baca Kartu (§18) dan verifikasi Wizard Ganti Jalur
    (§12.4) - tempel kartu, lihat UID + status terdaftar/tidak, TIDAK
    PERNAH membentuk absensi (beda dari POST /scan)."""
    hasil = _buat_service(session).cek_uid(uid_kartu)
    return KartuCekResponse(
        uid=hasil.uid, terdaftar=hasil.terdaftar, status=hasil.status, nama_pemilik=hasil.nama_pemilik
    )


@router.get("/pemilik/{jenis_pemilik}/{id_pemilik}", response_model=list[KartuResponse])
def daftar_by_pemilik(
    jenis_pemilik: str, id_pemilik: int, session: SessionDep, pengguna: KewenanganDep
) -> list[KartuResponse]:
    try:
        jenis = JenisPemilik(jenis_pemilik)
    except ValueError as exc:
        raise HTTPException(status.HTTP_422_UNPROCESSABLE_ENTITY, "jenis_pemilik harus SISWA atau GURU") from exc
    kartu_list = _buat_service(session).daftar_by_pemilik(id_pemilik=id_pemilik, jenis_pemilik=jenis)
    return [_ke_response(k) for k in kartu_list]
