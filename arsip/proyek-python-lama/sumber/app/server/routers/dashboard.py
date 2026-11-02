"""Endpoint Dashboard (PRD §14.1): ringkasan kehadiran hari berjalan,
grafik harian/mingguan/bulanan, perbandingan kedisiplinan antar kelas,
tren keterlambatan, daftar siswa alpa terbanyak, dan status operasional.
Kepala sekolah/admin bebas; wali kelas HANYA kelasnya sendiri; operator
TIDAK BERWENANG SAMA SEKALI (lihat docstring app.server.dashboard_service
untuk rincian lingkup peran PRD §10 - baris "Dashboard dan laporan" sama
dengan modul Laporan).
"""

from __future__ import annotations

from datetime import date
from typing import Annotated

from fastapi import APIRouter, Depends, HTTPException, status

from app.core.akses_kelas import AksesKelasDitolak
from app.core.dashboard import Granularitas
from app.core.laporan import AksesLaporanDitolak
from app.core.status import Peran
from app.data.models.system import User
from app.data.repositories.attendance_event import AttendanceEventRepository
from app.data.repositories.dashboard import DashboardRepository
from app.data.repositories.perangkat import TelemetriLobiRepository
from app.data.repositories.session import SesiKlienRepository
from app.data.repositories.user import UserRepository
from app.server.dashboard_service import DashboardService, WaliKelasBelumDiampu
from app.server.dependencies import SessionDep, wajib_peran
from app.server.schemas import (
    PerbandinganKelasResponse,
    RingkasanHariIniResponse,
    SiswaAlpaTerbanyakResponse,
    StatusAntreanLobiResponse,
    StatusJalurResponse,
    StatusKlienResponse,
    StatusOperasionalResponse,
    TitikGrafikResponse,
    TitikTrenResponse,
)

router = APIRouter(prefix="/dashboard", tags=["dashboard"])

#: PRD §10, baris "Dashboard dan laporan" - operator SENGAJA tidak
#: termasuk (sama seperti app.server.routers.laporan.KewenanganDep).
KewenanganDep = Annotated[User, Depends(wajib_peran(Peran.KEPALA_SEKOLAH, Peran.ADMIN, Peran.WALI_KELAS))]


def _service(session: SessionDep) -> DashboardService:
    return DashboardService(
        dashboard_repo=DashboardRepository(session),
        attendance_event_repo=AttendanceEventRepository(session),
        sesi_repo=SesiKlienRepository(session),
        telemetri_repo=TelemetriLobiRepository(session),
        user_repo=UserRepository(session),
    )


def _tangani_akses(exc: Exception):
    if isinstance(exc, AksesLaporanDitolak):
        raise HTTPException(status.HTTP_403_FORBIDDEN, str(exc)) from exc
    if isinstance(exc, AksesKelasDitolak):
        raise HTTPException(status.HTTP_403_FORBIDDEN, str(exc)) from exc
    if isinstance(exc, WaliKelasBelumDiampu):
        raise HTTPException(status.HTTP_409_CONFLICT, str(exc)) from exc
    raise exc


@router.get("/ringkasan", response_model=RingkasanHariIniResponse)
def ringkasan_hari_ini(
    session: SessionDep, pengguna: KewenanganDep, tanggal: date | None = None
) -> RingkasanHariIniResponse:
    try:
        hasil = _service(session).ringkasan_hari_ini(pelaku=pengguna, tanggal=tanggal)
    except (AksesLaporanDitolak, AksesKelasDitolak, WaliKelasBelumDiampu) as exc:
        _tangani_akses(exc)
    return RingkasanHariIniResponse(
        tanggal=hasil.tanggal, per_status=hasil.per_status, total_tercatat=hasil.total_tercatat
    )


@router.get("/grafik", response_model=list[TitikGrafikResponse])
def grafik(
    session: SessionDep,
    pengguna: KewenanganDep,
    tanggal_mulai: date,
    tanggal_selesai: date,
    granularitas: str = "HARIAN",
    id_kelas: int | None = None,
) -> list[TitikGrafikResponse]:
    try:
        granularitas_enum = Granularitas(granularitas)
    except ValueError as exc:
        raise HTTPException(
            status.HTTP_422_UNPROCESSABLE_ENTITY, f"granularitas tidak dikenal: {granularitas}"
        ) from exc
    try:
        hasil = _service(session).grafik(
            pelaku=pengguna,
            tanggal_mulai=tanggal_mulai,
            tanggal_selesai=tanggal_selesai,
            granularitas=granularitas_enum,
            id_kelas=id_kelas,
        )
    except (AksesLaporanDitolak, AksesKelasDitolak, WaliKelasBelumDiampu) as exc:
        _tangani_akses(exc)
    return [TitikGrafikResponse(periode=t.periode, per_status=t.per_status) for t in hasil]


@router.get("/tren-keterlambatan", response_model=list[TitikTrenResponse])
def tren_keterlambatan(
    session: SessionDep,
    pengguna: KewenanganDep,
    tanggal_mulai: date,
    tanggal_selesai: date,
    id_kelas: int | None = None,
) -> list[TitikTrenResponse]:
    try:
        hasil = _service(session).tren_keterlambatan(
            pelaku=pengguna, tanggal_mulai=tanggal_mulai, tanggal_selesai=tanggal_selesai, id_kelas=id_kelas
        )
    except (AksesLaporanDitolak, AksesKelasDitolak, WaliKelasBelumDiampu) as exc:
        _tangani_akses(exc)
    return [TitikTrenResponse(tanggal=tanggal, jumlah=jumlah) for tanggal, jumlah in hasil]


@router.get("/perbandingan-kelas", response_model=list[PerbandinganKelasResponse])
def perbandingan_kelas(
    session: SessionDep, pengguna: KewenanganDep, tanggal_mulai: date, tanggal_selesai: date
) -> list[PerbandinganKelasResponse]:
    try:
        hasil = _service(session).perbandingan_kelas(
            pelaku=pengguna, tanggal_mulai=tanggal_mulai, tanggal_selesai=tanggal_selesai
        )
    except (AksesLaporanDitolak, AksesKelasDitolak, WaliKelasBelumDiampu) as exc:
        _tangani_akses(exc)
    return [
        PerbandinganKelasResponse(
            id_kelas=p.id_kelas, nama_kelas=p.nama_kelas, per_status=p.per_status, total_tercatat=p.total_tercatat
        )
        for p in hasil
    ]


@router.get("/alpa-terbanyak", response_model=list[SiswaAlpaTerbanyakResponse])
def alpa_terbanyak(
    session: SessionDep,
    pengguna: KewenanganDep,
    tanggal_mulai: date,
    tanggal_selesai: date,
    id_kelas: int | None = None,
    limit: int = 10,
) -> list[SiswaAlpaTerbanyakResponse]:
    try:
        hasil = _service(session).alpa_terbanyak(
            pelaku=pengguna,
            tanggal_mulai=tanggal_mulai,
            tanggal_selesai=tanggal_selesai,
            id_kelas=id_kelas,
            limit=limit,
        )
    except (AksesLaporanDitolak, AksesKelasDitolak, WaliKelasBelumDiampu) as exc:
        _tangani_akses(exc)
    return [
        SiswaAlpaTerbanyakResponse(
            id_siswa=s.id_siswa, nama_lengkap=s.nama_lengkap, nama_kelas=s.nama_kelas, jumlah_alpa=s.jumlah_alpa
        )
        for s in hasil
    ]


@router.get("/status-operasional", response_model=StatusOperasionalResponse)
def status_operasional(session: SessionDep, pengguna: KewenanganDep) -> StatusOperasionalResponse:
    try:
        hasil = _service(session).status_operasional(pelaku=pengguna)
    except (AksesLaporanDitolak, AksesKelasDitolak, WaliKelasBelumDiampu) as exc:
        _tangani_akses(exc)
    return StatusOperasionalResponse(
        jalur=[
            StatusJalurResponse(jalur=j.jalur, waktu_terakhir_baca=j.waktu_terakhir_baca, diam=j.diam)
            for j in hasil.jalur
        ],
        klien=[
            StatusKlienResponse(username=k.username, peran=k.peran, terakhir_aktif=k.terakhir_aktif, diam=k.diam)
            for k in hasil.klien
        ],
        antrean_lobi=[
            StatusAntreanLobiResponse(
                station_id=a.station_id, jumlah_pending=a.jumlah_pending, waktu_lapor=a.waktu_lapor,
                kedaluwarsa=a.kedaluwarsa,
            )
            for a in hasil.antrean_lobi
        ],
        pencadangan_terakhir=hasil.pencadangan_terakhir,
    )
