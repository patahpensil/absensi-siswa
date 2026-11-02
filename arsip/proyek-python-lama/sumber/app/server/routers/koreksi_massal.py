from typing import Annotated

from fastapi import APIRouter, Depends, HTTPException, status
from sqlalchemy.orm import Session

from app.config.pengaturan import Pengaturan
from app.core.koreksi_massal import JenisTindakanMassal, LingkupOrang, PenyaringKoreksiMassal
from app.core.koreksi_massal_service import (
    KonfirmasiKetikDiperlukan,
    KonfirmasiKetikTidakCocok,
    KoreksiMassalService,
    SnapshotDiperlukan,
)
from app.core.status import Peran, StatusSiswa
from app.data.models.koreksi_massal import TindakanMassal
from app.data.models.system import User
from app.data.repositories.attendance import AttendanceRepository
from app.data.repositories.audit_log import AuditLogRepository
from app.data.repositories.mass_correction import (
    MassCorrectionRepository,
    TindakanMassalSudahDibatalkan,
    TindakanMassalTidakDitemukan,
)
from app.data.repositories.setting import SettingRepository
from app.server.dependencies import SessionDep, wajib_peran
from app.server.schemas import (
    BatalkanKoreksiMassalRequest,
    JalankanKoreksiMassalRequest,
    PenyaringSchema,
    PratinjauResponse,
    TindakanMassalResponse,
)

router = APIRouter(prefix="/koreksi-massal", tags=["koreksi-massal"])

#: SKILL.md "Koreksi massal": hanya admin dan kepala sekolah.
KewenanganDep = Annotated[User, Depends(wajib_peran(Peran.KEPALA_SEKOLAH, Peran.ADMIN))]


def _penyaring_dari_schema(data: PenyaringSchema) -> PenyaringKoreksiMassal:
    status_termasuk = None
    if data.status_termasuk is not None:
        status_termasuk = frozenset(StatusSiswa(s) for s in data.status_termasuk)
    return PenyaringKoreksiMassal(
        tanggal_mulai=data.tanggal_mulai,
        tanggal_selesai=data.tanggal_selesai,
        lingkup=LingkupOrang(data.lingkup),
        tingkat=data.tingkat,
        id_kelas=data.id_kelas,
        id_siswa_terpilih=frozenset(data.id_siswa_terpilih),
        status_termasuk=status_termasuk,
    )


def _buat_service(session: Session) -> KoreksiMassalService:
    return KoreksiMassalService(
        attendance_repo=AttendanceRepository(session),
        mass_correction_repo=MassCorrectionRepository(session),
        audit_repo=AuditLogRepository(session),
        pengaturan=Pengaturan(SettingRepository(session)),
    )


def _ke_response(tindakan: TindakanMassal) -> TindakanMassalResponse:
    return TindakanMassalResponse(
        id=tindakan.id,
        id_pengguna=tindakan.id_pengguna,
        jenis_tindakan=tindakan.jenis_tindakan,
        status_baru=tindakan.status_baru,
        penyaring=tindakan.penyaring,
        jumlah_baris=tindakan.jumlah_baris,
        alasan=tindakan.alasan,
        dibatalkan=tindakan.dibatalkan,
        waktu=tindakan.waktu,
        waktu_dibatalkan=tindakan.waktu_dibatalkan,
        dibatalkan_oleh=tindakan.dibatalkan_oleh,
    )


@router.post("/pratinjau", response_model=PratinjauResponse)
def pratinjau(payload: PenyaringSchema, session: SessionDep, pengguna: KewenanganDep) -> PratinjauResponse:
    hasil = _buat_service(session).pratinjau(_penyaring_dari_schema(payload), peran=Peran(pengguna.peran.value))
    return PratinjauResponse(
        jumlah_baris=hasil.jumlah_baris,
        perlu_snapshot=hasil.perlu_snapshot,
        perlu_konfirmasi_ketik=hasil.perlu_konfirmasi_ketik,
    )


@router.post("/jalankan", response_model=TindakanMassalResponse)
def jalankan(
    payload: JalankanKoreksiMassalRequest, session: SessionDep, pengguna: KewenanganDep
) -> TindakanMassalResponse:
    try:
        tindakan = _buat_service(session).jalankan(
            penyaring=_penyaring_dari_schema(payload.penyaring),
            jenis_tindakan=JenisTindakanMassal(payload.jenis_tindakan),
            status_baru=StatusSiswa(payload.status_baru) if payload.status_baru else None,
            id_pengguna=pengguna.id,
            peran=Peran(pengguna.peran.value),
            alasan=payload.alasan,
            snapshot_sudah_diambil=payload.snapshot_sudah_diambil,
            konfirmasi_ketik=payload.konfirmasi_ketik,
        )
    except SnapshotDiperlukan as exc:
        raise HTTPException(status.HTTP_412_PRECONDITION_FAILED, str(exc)) from exc
    except KonfirmasiKetikDiperlukan as exc:
        raise HTTPException(status.HTTP_412_PRECONDITION_FAILED, str(exc)) from exc
    except KonfirmasiKetikTidakCocok as exc:
        raise HTTPException(status.HTTP_400_BAD_REQUEST, str(exc)) from exc
    return _ke_response(tindakan)


@router.post("/{id_tindakan_massal}/batalkan", response_model=TindakanMassalResponse)
def batalkan(
    id_tindakan_massal: int,
    payload: BatalkanKoreksiMassalRequest,
    session: SessionDep,
    pengguna: KewenanganDep,
) -> TindakanMassalResponse:
    try:
        tindakan = _buat_service(session).batalkan(
            id_tindakan_massal,
            id_pengguna=pengguna.id,
            peran=Peran(pengguna.peran.value),
            alasan=payload.alasan,
        )
    except TindakanMassalTidakDitemukan as exc:
        raise HTTPException(status.HTTP_404_NOT_FOUND, "Tindakan massal tidak ditemukan") from exc
    except TindakanMassalSudahDibatalkan as exc:
        raise HTTPException(status.HTTP_409_CONFLICT, "Tindakan ini sudah dibatalkan sebelumnya") from exc
    return _ke_response(tindakan)


@router.get("", response_model=list[TindakanMassalResponse])
def riwayat(session: SessionDep, pengguna: KewenanganDep, limit: int = 50) -> list[TindakanMassalResponse]:
    """Untuk layar Riwayat Tindakan Massal (SKILL.md "Koreksi massal") -
    layarnya sendiri belum dibangun (app/ui/ belum ada), tapi datanya
    sudah bisa diambil lewat endpoint ini."""
    repo = MassCorrectionRepository(session)
    return [_ke_response(t) for t in repo.daftar_terbaru(limit=limit)]
