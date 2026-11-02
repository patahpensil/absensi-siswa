"""Endpoint Notifikasi (PRD §13, menu §18 NOTIFIKASI): empat template
pesan terpisah, telusuri antrean, dan mode uji. Hanya kepala
sekolah/admin (app.core.notifikasi.PERAN_NOTIFIKASI) - sama seperti
Koreksi Massal dan Koreksi Guru.

KONEKSI WHATSAPP NYATA TETAP MATI (Fase 2) - lihat docstring
app.server.notifikasi_service untuk batas cakupan slice ini (struktur
saja, pemicu nyata dari absensi belum disambungkan, dicatat sebagai
utang di README).
"""

from __future__ import annotations

from typing import Annotated

from fastapi import APIRouter, Depends, HTTPException, status

from app.core.status import Peran
from app.data.models.notification import JenisNotifikasi, StatusNotifikasi
from app.data.models.system import User
from app.data.repositories.audit_log import AuditLogRepository
from app.data.repositories.message_template import MessageTemplateRepository
from app.data.repositories.notification import NotificationRepository
from app.data.repositories.setting import SettingRepository
from app.config.pengaturan import Pengaturan
from app.server.dependencies import SessionDep, wajib_peran
from app.server.notifikasi_service import (
    ModeUjiTanpaNomorAdminTersimpan,
    NotifikasiService,
    TemplateTidakValid,
)
from app.server.schemas import (
    AntreanNotifikasiResponse,
    ModeUjiResponse,
    ModeUjiUbahRequest,
    TemplateNotifikasiResponse,
    TemplateNotifikasiUbahRequest,
)

router = APIRouter(prefix="/notifikasi", tags=["notifikasi"])

KewenanganDep = Annotated[User, Depends(wajib_peran(Peran.KEPALA_SEKOLAH, Peran.ADMIN))]


def _buat_service(session) -> NotifikasiService:
    return NotifikasiService(
        template_repo=MessageTemplateRepository(session),
        notification_repo=NotificationRepository(session),
        audit_repo=AuditLogRepository(session),
        pengaturan=Pengaturan(SettingRepository(session)),
    )


@router.get("/template", response_model=list[TemplateNotifikasiResponse])
def daftar_template(session: SessionDep, pengguna: KewenanganDep) -> list[TemplateNotifikasiResponse]:
    hasil = _buat_service(session).daftar_template(pelaku=pengguna)
    return [
        TemplateNotifikasiResponse(jenis=t.jenis.value, konten=t.konten, versi=t.versi, aktif=t.aktif) for t in hasil
    ]


@router.put("/template/{jenis}", response_model=TemplateNotifikasiResponse)
def ubah_template(
    jenis: str, payload: TemplateNotifikasiUbahRequest, session: SessionDep, pengguna: KewenanganDep
) -> TemplateNotifikasiResponse:
    try:
        jenis_enum = JenisNotifikasi(jenis)
    except ValueError as exc:
        raise HTTPException(status.HTTP_422_UNPROCESSABLE_ENTITY, f"jenis notifikasi tidak dikenal: {jenis}") from exc

    try:
        template = _buat_service(session).ubah_template(
            pelaku=pengguna, jenis=jenis_enum, konten=payload.konten, alasan=payload.alasan
        )
    except TemplateTidakValid as exc:
        raise HTTPException(status.HTTP_422_UNPROCESSABLE_ENTITY, str(exc)) from exc

    return TemplateNotifikasiResponse(
        jenis=template.jenis.value, konten=template.konten, versi=template.versi, aktif=template.aktif
    )


@router.get("/antrean", response_model=list[AntreanNotifikasiResponse])
def daftar_antrean(
    session: SessionDep, pengguna: KewenanganDep, status_filter: str | None = None
) -> list[AntreanNotifikasiResponse]:
    status_enum: StatusNotifikasi | None = None
    if status_filter is not None:
        try:
            status_enum = StatusNotifikasi(status_filter)
        except ValueError as exc:
            raise HTTPException(
                status.HTTP_422_UNPROCESSABLE_ENTITY, f"status tidak dikenal: {status_filter}"
            ) from exc

    hasil = _buat_service(session).daftar_antrean(pelaku=pengguna, status=status_enum)
    return [
        AntreanNotifikasiResponse(
            id=n.id,
            id_absensi=n.id_absensi,
            nomor_tujuan=n.nomor_tujuan,
            isi_pesan=n.isi_pesan,
            jenis=n.jenis.value,
            status=n.status.value,
            kunci_idempotensi=n.kunci_idempotensi,
            waktu_kejadian=n.waktu_kejadian,
            waktu_dijadwalkan=n.waktu_dijadwalkan,
            waktu_kirim=n.waktu_kirim,
            jumlah_percobaan=n.jumlah_percobaan,
            pesan_galat=n.pesan_galat,
        )
        for n in hasil
    ]


@router.get("/mode-uji", response_model=ModeUjiResponse)
def ambil_mode_uji(session: SessionDep, pengguna: KewenanganDep) -> ModeUjiResponse:
    info = _buat_service(session).ambil_mode_uji(pelaku=pengguna)
    return ModeUjiResponse(aktif=info.aktif, nomor_admin=info.nomor_admin)


@router.put("/mode-uji", response_model=ModeUjiResponse)
def ubah_mode_uji(payload: ModeUjiUbahRequest, session: SessionDep, pengguna: KewenanganDep) -> ModeUjiResponse:
    try:
        info = _buat_service(session).ubah_mode_uji(
            pelaku=pengguna, aktif=payload.aktif, nomor_admin=payload.nomor_admin, alasan=payload.alasan
        )
    except ModeUjiTanpaNomorAdminTersimpan as exc:
        raise HTTPException(
            status.HTTP_422_UNPROCESSABLE_ENTITY,
            "Mode uji tidak bisa diaktifkan tanpa nomor admin - isi nomor admin terlebih dahulu",
        ) from exc
    return ModeUjiResponse(aktif=info.aktif, nomor_admin=info.nomor_admin)
