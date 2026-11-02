"""Endpoint Koreksi Satuan (PRD §9.1, §10) - koreksi status_final satu
siswa dan input manual IZIN/SAKIT/DISPENSASI. Kepala sekolah/admin bebas
semua kelas, wali kelas hanya kelasnya sendiri, operator ditolak
sepenuhnya - ditegakkan di app.server.koreksi_satuan_service (LAPISAN
DATA), bukan di sini.

TIDAK menyentuh app.core.engine/mesin_absensi. status_masuk ("kenyataan"
hasil scan) tidak pernah disentuh - hanya status_final.
"""

from datetime import date
from typing import Annotated

from fastapi import APIRouter, Depends, HTTPException, status
from sqlalchemy.orm import Session

from app.core.akses_kelas import AksesKelasDitolak
from app.core.koreksi_satuan import AksesKoreksiSatuanDitolak
from app.core.status import Peran, StatusSiswa
from app.data.models.system import User
from app.data.repositories.attendance import AttendanceRepository
from app.data.repositories.audit_log import AuditLogRepository
from app.data.repositories.koreksi_satuan import KoreksiSatuanRepository
from app.data.repositories.student import StudentRepository
from app.server.dependencies import SessionDep, wajib_peran
from app.server.koreksi_satuan_service import (
    KoreksiSatuanService,
    KoreksiSudahDibatalkan,
    KoreksiTidakDitemukan,
    SiswaTidakDitemukan,
    TidakAdaPerubahan,
)
from app.server.schemas import (
    BatalkanKoreksiSatuanRequest,
    KoreksiSatuanBuatRequest,
    KoreksiSatuanResponse,
    StatusSaatIniResponse,
)

router = APIRouter(prefix="/koreksi-satuan", tags=["koreksi-satuan"])

#: Dua lapis, sama seperti router lain (kalender/pengaturan/pengguna):
#: wajib_peran() di sini menolak operator lebih awal (di lapisan HTTP),
#: DAN app.core.koreksi_satuan.pastikan_berwenang() + pastikan_akses_kelas()
#: tetap jadi penegakan otoritatif di lapisan SERVICE (instruksi "tegakkan
#: di lapisan data") - bukan pengganti satu sama lain.
KewenanganDep = Annotated[User, Depends(wajib_peran(Peran.KEPALA_SEKOLAH, Peran.ADMIN, Peran.WALI_KELAS))]


def _buat_service(session: Session) -> KoreksiSatuanService:
    return KoreksiSatuanService(
        attendance_repo=AttendanceRepository(session),
        koreksi_repo=KoreksiSatuanRepository(session),
        student_repo=StudentRepository(session),
        audit_repo=AuditLogRepository(session),
    )


def _ke_response(koreksi) -> KoreksiSatuanResponse:
    return KoreksiSatuanResponse(
        id=koreksi.id,
        id_absensi=koreksi.id_absensi,
        id_pengguna=koreksi.id_pengguna,
        waktu=koreksi.waktu,
        nilai_sebelum=koreksi.nilai_sebelum,
        nilai_sesudah=koreksi.nilai_sesudah,
        alasan=koreksi.alasan,
        dibatalkan=koreksi.dibatalkan,
    )


@router.get("/status", response_model=StatusSaatIniResponse)
def status_saat_ini(
    id_siswa: int, tanggal: date, session: SessionDep, pengguna: KewenanganDep
) -> StatusSaatIniResponse:
    try:
        hasil = _buat_service(session).status_saat_ini(pelaku=pengguna, id_siswa=id_siswa, tanggal=tanggal)
    except SiswaTidakDitemukan as exc:
        raise HTTPException(status.HTTP_404_NOT_FOUND, "Siswa tidak ditemukan") from exc
    except AksesKoreksiSatuanDitolak as exc:
        raise HTTPException(status.HTTP_403_FORBIDDEN, str(exc)) from exc
    except AksesKelasDitolak as exc:
        raise HTTPException(status.HTTP_403_FORBIDDEN, str(exc)) from exc
    return StatusSaatIniResponse(**hasil.__dict__)


@router.post("", response_model=KoreksiSatuanResponse, status_code=status.HTTP_201_CREATED)
def buat(payload: KoreksiSatuanBuatRequest, session: SessionDep, pengguna: KewenanganDep) -> KoreksiSatuanResponse:
    try:
        koreksi = _buat_service(session).koreksi(
            pelaku=pengguna,
            id_siswa=payload.id_siswa,
            tanggal=payload.tanggal,
            status_baru=StatusSiswa(payload.status_baru),
            alasan=payload.alasan,
        )
    except SiswaTidakDitemukan as exc:
        raise HTTPException(status.HTTP_404_NOT_FOUND, "Siswa tidak ditemukan") from exc
    except AksesKoreksiSatuanDitolak as exc:
        raise HTTPException(status.HTTP_403_FORBIDDEN, str(exc)) from exc
    except AksesKelasDitolak as exc:
        raise HTTPException(status.HTTP_403_FORBIDDEN, str(exc)) from exc
    except TidakAdaPerubahan as exc:
        raise HTTPException(status.HTTP_400_BAD_REQUEST, str(exc)) from exc
    return _ke_response(koreksi)


@router.post("/{id_koreksi}/batalkan", response_model=KoreksiSatuanResponse)
def batalkan(
    id_koreksi: int, payload: BatalkanKoreksiSatuanRequest, session: SessionDep, pengguna: KewenanganDep
) -> KoreksiSatuanResponse:
    try:
        koreksi = _buat_service(session).batalkan(pelaku=pengguna, id_koreksi=id_koreksi, alasan=payload.alasan)
    except KoreksiTidakDitemukan as exc:
        raise HTTPException(status.HTTP_404_NOT_FOUND, "Koreksi tidak ditemukan") from exc
    except KoreksiSudahDibatalkan as exc:
        raise HTTPException(status.HTTP_409_CONFLICT, "Koreksi ini sudah dibatalkan sebelumnya") from exc
    except AksesKoreksiSatuanDitolak as exc:
        raise HTTPException(status.HTTP_403_FORBIDDEN, str(exc)) from exc
    except AksesKelasDitolak as exc:
        raise HTTPException(status.HTTP_403_FORBIDDEN, str(exc)) from exc
    return _ke_response(koreksi)


@router.get("", response_model=list[KoreksiSatuanResponse])
def riwayat(
    id_siswa: int, tanggal: date, session: SessionDep, pengguna: KewenanganDep
) -> list[KoreksiSatuanResponse]:
    try:
        daftar = _buat_service(session).daftar_riwayat(pelaku=pengguna, id_siswa=id_siswa, tanggal=tanggal)
    except SiswaTidakDitemukan as exc:
        raise HTTPException(status.HTTP_404_NOT_FOUND, "Siswa tidak ditemukan") from exc
    except AksesKoreksiSatuanDitolak as exc:
        raise HTTPException(status.HTTP_403_FORBIDDEN, str(exc)) from exc
    except AksesKelasDitolak as exc:
        raise HTTPException(status.HTTP_403_FORBIDDEN, str(exc)) from exc
    return [_ke_response(k) for k in daftar]
