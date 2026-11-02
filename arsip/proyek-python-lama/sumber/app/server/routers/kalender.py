"""Endpoint kalender akademik dua jalur (PRD §8). Melihat = seluruh peran
yang login (wali kelas/operator "lihat" saja - SKILL.md "Peran dan hak
akses"); menambah/membatalkan/menyalin tahun = kepala sekolah dan admin
saja, ditegakkan dua lapis: KewenanganDep di sini DAN
KalenderService.pastikan_berwenang() (app/core/koreksi_massal.py) di
app/core/kalender_service.py.

TIDAK menyentuh app.core.engine/mesin_absensi maupun app.core.kalender
(mesin klasifikasi kalender) - router ini murni memanggil KalenderService
yang sudah ada.
"""

from datetime import date
from typing import Annotated

from fastapi import APIRouter, Depends, HTTPException, status
from sqlalchemy.orm import Session

from app.core.kalender import EntriKalender as EntriKalenderMurni
from app.core.kalender_service import (
    EntriKalenderSudahDibatalkan,
    EntriKalenderTidakDitemukan,
    KalenderService,
)
from app.core.status import JenisHari, Peran
from app.data.models.kalender import EntriKalender
from app.data.models.system import User
from app.data.repositories.audit_log import AuditLogRepository
from app.data.repositories.kalender import KalenderRepository
from app.data.repositories.student import StudentRepository
from app.data.repositories.teacher import TeacherRepository
from app.server.dependencies import CurrentUserDep, SessionDep, wajib_peran
from app.server.schemas import (
    BatalkanKalenderRequest,
    EntriKalenderResponse,
    JenisHariResponse,
    PratinjauDampakKalenderRequest,
    PratinjauDampakKalenderResponse,
    SalinTahunKalenderRequest,
    TambahKalenderRequest,
)

router = APIRouter(prefix="/kalender", tags=["kalender"])

#: SKILL.md "Kalender akademik": kewenangan sama dengan koreksi massal -
#: hanya admin dan kepala sekolah.
KewenanganDep = Annotated[User, Depends(wajib_peran(Peran.KEPALA_SEKOLAH, Peran.ADMIN))]


def _buat_service(session: Session) -> KalenderService:
    return KalenderService(kalender_repo=KalenderRepository(session), audit_repo=AuditLogRepository(session))


def _jenis_hari(nilai: str, *, nama_field: str) -> JenisHari:
    try:
        return JenisHari(nilai)
    except ValueError as exc:
        raise HTTPException(
            status.HTTP_422_UNPROCESSABLE_ENTITY, f"{nama_field} tidak dikenal: {nilai!r}"
        ) from exc


def _jumlah_orang_terdampak(session: Session) -> int:
    """Perkiraan populasi yang absensinya bisa terpengaruh perubahan
    kalender - siswa dan guru aktif digabung, karena tabel attendance
    memuat keduanya (jenis_pemilik SISWA/GURU)."""
    return len(StudentRepository(session).daftar_aktif()) + len(TeacherRepository(session).daftar())


def _ke_response(entri: EntriKalender) -> EntriKalenderResponse:
    return EntriKalenderResponse(
        id=entri.id,
        mulai=entri.mulai,
        selesai=entri.selesai,
        keterangan=entri.keterangan,
        jenis_siswa=entri.jenis_siswa,
        jenis_guru=entri.jenis_guru,
        tahun_ajaran=entri.tahun_ajaran,
        alasan=entri.alasan,
        dibatalkan=entri.dibatalkan,
        waktu_dibuat=entri.waktu_dibuat,
    )


@router.get("", response_model=list[EntriKalenderResponse])
def daftar(
    session: SessionDep, pengguna: CurrentUserDep, tahun_ajaran: str | None = None
) -> list[EntriKalenderResponse]:
    entri = _buat_service(session).daftar(tahun_ajaran=tahun_ajaran)
    return [_ke_response(e) for e in entri]


@router.get("/hari", response_model=JenisHariResponse)
def jenis_hari(tanggal: date, session: SessionDep, pengguna: CurrentUserDep) -> JenisHariResponse:
    service = _buat_service(session)
    return JenisHariResponse(
        tanggal=tanggal,
        jenis_siswa=service.jenis_hari_siswa(tanggal).value,
        jenis_guru=service.jenis_hari_guru(tanggal).value,
    )


@router.post("/pratinjau", response_model=PratinjauDampakKalenderResponse)
def pratinjau(
    payload: PratinjauDampakKalenderRequest, session: SessionDep, pengguna: KewenanganDep
) -> PratinjauDampakKalenderResponse:
    entri_murni = EntriKalenderMurni(
        mulai=payload.mulai,
        selesai=payload.selesai,
        keterangan="",
        jenis_siswa=_jenis_hari(payload.jenis_siswa, nama_field="jenis_siswa"),
        jenis_guru=_jenis_hari(payload.jenis_guru, nama_field="jenis_guru"),
    )
    dampak = _buat_service(session).pratinjau_dampak(entri_murni, jumlah_orang=_jumlah_orang_terdampak(session))
    return PratinjauDampakKalenderResponse(
        jumlah_hari=dampak["jumlah_hari"], perkiraan_baris=dampak["perkiraan_baris"]
    )


@router.post("", response_model=EntriKalenderResponse, status_code=status.HTTP_201_CREATED)
def tambah(payload: TambahKalenderRequest, session: SessionDep, pengguna: KewenanganDep) -> EntriKalenderResponse:
    entri = _buat_service(session).tambah(
        mulai=payload.mulai,
        selesai=payload.selesai,
        keterangan=payload.keterangan,
        jenis_siswa=_jenis_hari(payload.jenis_siswa, nama_field="jenis_siswa"),
        jenis_guru=_jenis_hari(payload.jenis_guru, nama_field="jenis_guru"),
        tahun_ajaran=payload.tahun_ajaran,
        id_pengguna=pengguna.id,
        peran=Peran(pengguna.peran.value),
        alasan=payload.alasan,
    )
    return _ke_response(entri)


@router.post("/{id_entri}/batalkan", response_model=EntriKalenderResponse)
def batalkan(
    id_entri: int, payload: BatalkanKalenderRequest, session: SessionDep, pengguna: KewenanganDep
) -> EntriKalenderResponse:
    try:
        entri = _buat_service(session).batalkan(
            id_entri, id_pengguna=pengguna.id, peran=Peran(pengguna.peran.value), alasan=payload.alasan
        )
    except EntriKalenderTidakDitemukan as exc:
        raise HTTPException(status.HTTP_404_NOT_FOUND, "Entri kalender tidak ditemukan") from exc
    except EntriKalenderSudahDibatalkan as exc:
        raise HTTPException(status.HTTP_409_CONFLICT, "Entri ini sudah dibatalkan sebelumnya") from exc
    return _ke_response(entri)


@router.post("/salin-tahun", response_model=list[EntriKalenderResponse])
def salin_tahun(
    payload: SalinTahunKalenderRequest, session: SessionDep, pengguna: KewenanganDep
) -> list[EntriKalenderResponse]:
    disalin = _buat_service(session).salin_dari_tahun_sebelumnya(
        tahun_asal=payload.tahun_asal,
        tahun_tujuan=payload.tahun_tujuan,
        id_pengguna=pengguna.id,
        peran=Peran(pengguna.peran.value),
        alasan=payload.alasan,
    )
    return [_ke_response(e) for e in disalin]
