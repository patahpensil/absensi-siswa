"""Endpoint Riwayat Scan (PRD §7.4, §10) - baca-saja. Seluruh peran yang
login boleh memanggil endpoint ini; lingkup datanya yang berbeda per
peran (admin/kepala sekolah/operator bebas, wali kelas hanya kelasnya
sendiri) - ditegakkan di app.server.riwayat_scan_service.RiwayatScanService,
BUKAN di sini.

TIDAK menyentuh app.core.engine/mesin_absensi. Tidak ada endpoint
koreksi di sini (itu slice terpisah) - murni GET.
"""

from datetime import date, datetime, time

from fastapi import APIRouter, HTTPException, status

from app.core.akses_kelas import AksesKelasDitolak
from app.core.timeutil import now_wita
from app.data.models.enums import JenisPemilik
from app.data.repositories.attendance_event import AttendanceEventRepository
from app.data.repositories.kelas import KelasRepository
from app.data.repositories.student import StudentRepository
from app.data.repositories.teacher import TeacherRepository
from app.server.dependencies import CurrentUserDep, SessionDep
from app.server.riwayat_scan_service import FilterRiwayatScan, KelasTidakDitemukan, RiwayatScanService
from app.server.schemas import RiwayatScanResponse

router = APIRouter(prefix="/riwayat-scan", tags=["riwayat-scan"])


@router.get("", response_model=list[RiwayatScanResponse])
def daftar(
    session: SessionDep,
    pengguna: CurrentUserDep,
    tanggal_mulai: date | None = None,
    tanggal_selesai: date | None = None,
    id_kelas: int | None = None,
    id_pemilik: int | None = None,
    jenis_pemilik: str | None = None,
    limit: int = 200,
) -> list[RiwayatScanResponse]:
    if tanggal_mulai is None and tanggal_selesai is None:
        tanggal_mulai = tanggal_selesai = now_wita().date()
    elif tanggal_mulai is None:
        tanggal_mulai = tanggal_selesai
    elif tanggal_selesai is None:
        tanggal_selesai = tanggal_mulai

    try:
        jenis_pemilik_enum = JenisPemilik(jenis_pemilik) if jenis_pemilik is not None else None
    except ValueError as exc:
        raise HTTPException(
            status.HTTP_422_UNPROCESSABLE_ENTITY, f"jenis_pemilik tidak dikenal: {jenis_pemilik!r}"
        ) from exc

    service = RiwayatScanService(
        event_repo=AttendanceEventRepository(session),
        student_repo=StudentRepository(session),
        teacher_repo=TeacherRepository(session),
        kelas_repo=KelasRepository(session),
    )
    try:
        baris = service.cari(
            pengguna=pengguna,
            filter=FilterRiwayatScan(
                sejak=datetime.combine(tanggal_mulai, time.min),
                sampai=datetime.combine(tanggal_selesai, time.max),
                id_kelas=id_kelas,
                id_pemilik=id_pemilik,
                jenis_pemilik=jenis_pemilik_enum,
            ),
            limit=limit,
        )
    except KelasTidakDitemukan as exc:
        raise HTTPException(status.HTTP_404_NOT_FOUND, "Kelas tidak ditemukan") from exc
    except AksesKelasDitolak as exc:
        raise HTTPException(status.HTTP_403_FORBIDDEN, str(exc)) from exc

    return [
        RiwayatScanResponse(
            id=b.event.id,
            waktu_scan=b.event.waktu_scan,
            uid_kartu=b.event.uid_kartu,
            jenis_event=b.event.jenis_event.value,
            label=b.label,
            id_pemilik=b.event.id_pemilik,
            jenis_pemilik=b.event.jenis_pemilik.value if b.event.jenis_pemilik is not None else None,
            nama=b.nama,
            jalur=b.event.jalur.value if b.event.jalur is not None else None,
        )
        for b in baris
    ]
