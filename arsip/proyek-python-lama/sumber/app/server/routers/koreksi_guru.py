"""Endpoint Koreksi Guru (PRD §6): status manual DINAS_LUAR/IZIN/SAKIT/
CUTI + lampiran foto surat tugas/keterangan. Hanya kepala sekolah/admin
(app.core.koreksi_guru.PERAN_KOREKSI_GURU) - TIDAK ada pengecualian wali
kelas (beda dari koreksi satuan siswa).

POST /koreksi-guru memakai multipart/form-data (bukan JSON) supaya
lampiran opsional bisa ikut dalam satu permintaan, sesuai alur "input
status + alasan + lampiran sekaligus" yang diminta slice ini - bukan
alur unggah-dulu-baru-rujuk-terpisah.
"""

from __future__ import annotations

from datetime import date
from typing import Annotated

from fastapi import APIRouter, Depends, File, Form, HTTPException, UploadFile, status
from fastapi.responses import Response
from sqlalchemy.orm import Session

from app.core.koreksi_guru import AksesKoreksiGuruDitolak, StatusBukanManual
from app.core.status import Peran, StatusGuru
from app.data.models.koreksi_guru import KoreksiGuru
from app.data.models.system import User
from app.data.repositories.attendance import AttendanceRepository
from app.data.repositories.audit_log import AuditLogRepository
from app.data.repositories.koreksi_guru import KoreksiGuruRepository
from app.data.repositories.teacher import TeacherRepository
from app.server.dependencies import LampiranDirDep, SessionDep, wajib_peran
from app.server.koreksi_guru_service import (
    GuruTidakDitemukan,
    KoreksiGuruService,
    KoreksiSudahDibatalkan,
    KoreksiTidakDitemukan,
    TidakAdaPerubahan,
)
from app.server.lampiran_storage import (
    BerkasTerlaluBesar,
    JenisBerkasTidakDidukung,
    path_lampiran,
    simpan_lampiran_koreksi_guru,
)
from app.server.schemas import BatalkanKoreksiGuruRequest, KoreksiGuruResponse, StatusSaatIniGuruResponse

router = APIRouter(prefix="/koreksi-guru", tags=["koreksi-guru"])

#: SKILL.md "Absensi guru": status manual guru hanya kepala sekolah/admin.
KewenanganDep = Annotated[User, Depends(wajib_peran(Peran.KEPALA_SEKOLAH, Peran.ADMIN))]

_MEDIA_TYPE = {
    ".jpg": "image/jpeg",
    ".jpeg": "image/jpeg",
    ".png": "image/png",
    ".pdf": "application/pdf",
}


def _buat_service(session: Session) -> KoreksiGuruService:
    return KoreksiGuruService(
        attendance_repo=AttendanceRepository(session),
        koreksi_repo=KoreksiGuruRepository(session),
        teacher_repo=TeacherRepository(session),
        audit_repo=AuditLogRepository(session),
    )


def _ke_response(koreksi: KoreksiGuru) -> KoreksiGuruResponse:
    return KoreksiGuruResponse(
        id=koreksi.id,
        id_absensi=koreksi.id_absensi,
        id_pengguna=koreksi.id_pengguna,
        waktu=koreksi.waktu,
        nilai_sebelum=koreksi.nilai_sebelum,
        nilai_sesudah=koreksi.nilai_sesudah,
        alasan=koreksi.alasan,
        lampiran=koreksi.lampiran,
        dibatalkan=koreksi.dibatalkan,
    )


@router.get("/status", response_model=StatusSaatIniGuruResponse)
def status_saat_ini(id_guru: int, tanggal: date, session: SessionDep, pengguna: KewenanganDep) -> StatusSaatIniGuruResponse:
    try:
        hasil = _buat_service(session).status_saat_ini(pelaku=pengguna, id_guru=id_guru, tanggal=tanggal)
    except GuruTidakDitemukan as exc:
        raise HTTPException(status.HTTP_404_NOT_FOUND, "Guru tidak ditemukan") from exc
    return StatusSaatIniGuruResponse(
        id_guru=hasil.id_guru,
        nama_lengkap=hasil.nama_lengkap,
        id_absensi=hasil.id_absensi,
        status_masuk_sekarang=hasil.status_masuk_sekarang,
        status_final_sekarang=hasil.status_final_sekarang,
    )


@router.post("", response_model=KoreksiGuruResponse, status_code=status.HTTP_201_CREATED)
def buat_koreksi(
    session: SessionDep,
    pengguna: KewenanganDep,
    lampiran_dir: LampiranDirDep,
    id_guru: Annotated[int, Form()],
    tanggal: Annotated[date, Form()],
    status_baru: Annotated[str, Form()],
    alasan: Annotated[str, Form(min_length=1)],
    lampiran: Annotated[UploadFile | None, File()] = None,
) -> KoreksiGuruResponse:
    try:
        status_baru_enum = StatusGuru(status_baru)
    except ValueError as exc:
        raise HTTPException(status.HTTP_422_UNPROCESSABLE_ENTITY, f"status_baru tidak dikenal: {status_baru}") from exc

    path_relatif: str | None = None
    if lampiran is not None and lampiran.filename:
        isi = lampiran.file.read()
        try:
            path_relatif = simpan_lampiran_koreksi_guru(isi, nama_asli=lampiran.filename, base_dir=lampiran_dir)
        except (JenisBerkasTidakDidukung, BerkasTerlaluBesar) as exc:
            raise HTTPException(status.HTTP_422_UNPROCESSABLE_ENTITY, str(exc)) from exc

    try:
        koreksi = _buat_service(session).koreksi(
            pelaku=pengguna,
            id_guru=id_guru,
            tanggal=tanggal,
            status_baru=status_baru_enum,
            alasan=alasan,
            lampiran=path_relatif,
        )
    except GuruTidakDitemukan as exc:
        raise HTTPException(status.HTTP_404_NOT_FOUND, "Guru tidak ditemukan") from exc
    except AksesKoreksiGuruDitolak as exc:
        raise HTTPException(status.HTTP_403_FORBIDDEN, str(exc)) from exc
    except StatusBukanManual as exc:
        raise HTTPException(status.HTTP_422_UNPROCESSABLE_ENTITY, str(exc)) from exc
    except TidakAdaPerubahan as exc:
        raise HTTPException(status.HTTP_400_BAD_REQUEST, str(exc)) from exc
    return _ke_response(koreksi)


@router.post("/{id_koreksi}/batalkan", response_model=KoreksiGuruResponse)
def batalkan(
    id_koreksi: int, payload: BatalkanKoreksiGuruRequest, session: SessionDep, pengguna: KewenanganDep
) -> KoreksiGuruResponse:
    try:
        koreksi = _buat_service(session).batalkan(pelaku=pengguna, id_koreksi=id_koreksi, alasan=payload.alasan)
    except KoreksiTidakDitemukan as exc:
        raise HTTPException(status.HTTP_404_NOT_FOUND, "Koreksi tidak ditemukan") from exc
    except KoreksiSudahDibatalkan as exc:
        raise HTTPException(status.HTTP_409_CONFLICT, "Koreksi ini sudah dibatalkan sebelumnya") from exc
    return _ke_response(koreksi)


@router.get("", response_model=list[KoreksiGuruResponse])
def daftar_riwayat(id_guru: int, tanggal: date, session: SessionDep, pengguna: KewenanganDep) -> list[KoreksiGuruResponse]:
    try:
        riwayat = _buat_service(session).daftar_riwayat(pelaku=pengguna, id_guru=id_guru, tanggal=tanggal)
    except GuruTidakDitemukan as exc:
        raise HTTPException(status.HTTP_404_NOT_FOUND, "Guru tidak ditemukan") from exc
    return [_ke_response(k) for k in riwayat]


@router.get("/{id_koreksi}/lampiran")
def unduh_lampiran(id_koreksi: int, session: SessionDep, pengguna: KewenanganDep, lampiran_dir: LampiranDirDep) -> Response:
    koreksi = KoreksiGuruRepository(session).cari_by_id(id_koreksi)
    if koreksi is None or koreksi.lampiran is None:
        raise HTTPException(status.HTTP_404_NOT_FOUND, "Lampiran tidak ditemukan")

    path = path_lampiran(koreksi.lampiran, base_dir=lampiran_dir)
    if not path.is_file():
        raise HTTPException(status.HTTP_404_NOT_FOUND, "Berkas lampiran tidak ditemukan di server")

    media_type = _MEDIA_TYPE.get(path.suffix.lower(), "application/octet-stream")
    return Response(content=path.read_bytes(), media_type=media_type)
