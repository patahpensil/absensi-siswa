"""Endpoint scan RFID: klien (PC lobi) sudah membaca dan menormalisasi UID
sendiri lewat app/rfid/ - endpoint ini hanya menyambungkannya ke
MesinAbsensi lewat basis data server.
"""

from fastapi import APIRouter, HTTPException, status

from app.config.pengaturan import Pengaturan
from app.core.engine import MesinAbsensi
from app.data.models.core import StatusKartu
from app.data.models.enums import JenisPemilik
from app.data.repositories.attendance import AttendanceRepository
from app.data.repositories.attendance_event import AttendanceEventRepository
from app.data.repositories.audit_log import AuditLogRepository
from app.data.repositories.kalender import KalenderRepository
from app.data.repositories.rfid_card import RfidCardRepository
from app.data.repositories.schedule import ScheduleRepository
from app.data.repositories.setting import SettingRepository
from app.data.repositories.student import StudentRepository
from app.data.repositories.teacher import TeacherRepository
from app.rfid.base import JalurAlat
from app.server.dependencies import CurrentUserDep, SessionDep
from app.server.schemas import ScanRequest, ScanResponse

router = APIRouter(prefix="/scan", tags=["scan"])


def _cari_nama(session, *, id_pemilik: int, jenis_pemilik: JenisPemilik) -> str | None:
    """Nama untuk tampilan layar absensi (PRD §12.5) - klien tidak boleh
    membaca app/data langsung (SKILL.md, prinsip arsitektur), jadi lookup
    ini sengaja dilakukan di sini, bukan di klien."""
    if jenis_pemilik == JenisPemilik.SISWA:
        orang = StudentRepository(session).cari_by_id(id_pemilik)
    else:
        orang = TeacherRepository(session).cari_by_id(id_pemilik)
    return orang.nama_lengkap if orang is not None else None


@router.post("", response_model=ScanResponse)
def proses_scan(payload: ScanRequest, session: SessionDep, _pengguna: CurrentUserDep) -> ScanResponse:
    """station_id di payload murni informasional untuk sekarang - belum
    dipetakan ke tabel devices (manajemen alat RFID belum dibangun, lihat
    README "Yang belum dikerjakan"), jadi id_perangkat pada
    attendance_events tetap kosong.
    """
    try:
        jalur = JalurAlat(payload.jalur)
    except ValueError as exc:
        raise HTTPException(status.HTTP_422_UNPROCESSABLE_ENTITY, f"jalur tidak dikenal: {payload.jalur}") from exc

    kartu = RfidCardRepository(session).cari_by_uid(payload.uid_kartu)
    if kartu is not None and kartu.status == StatusKartu.AKTIF:
        id_pemilik, jenis_pemilik = kartu.id_pemilik, kartu.jenis_pemilik
    else:
        id_pemilik, jenis_pemilik = None, None

    nama = (
        _cari_nama(session, id_pemilik=id_pemilik, jenis_pemilik=jenis_pemilik)
        if id_pemilik is not None and jenis_pemilik is not None
        else None
    )

    mesin = MesinAbsensi(
        attendance_repo=AttendanceRepository(session),
        attendance_event_repo=AttendanceEventRepository(session),
        schedule_repo=ScheduleRepository(session),
        kalender_repo=KalenderRepository(session),
        student_repo=StudentRepository(session),
        teacher_repo=TeacherRepository(session),
        audit_repo=AuditLogRepository(session),
        pengaturan=Pengaturan(SettingRepository(session)),
    )
    hasil = mesin.proses_scan(
        uid_kartu=payload.uid_kartu,
        id_pemilik=id_pemilik,
        jenis_pemilik=jenis_pemilik,
        waktu_scan=payload.waktu_scan,
        uid_mentah=payload.uid_mentah,
        jalur=jalur,
    )

    absensi = hasil.absensi
    return ScanResponse(
        jenis_event=hasil.event.jenis_event.value,
        uid_kartu=hasil.event.uid_kartu,
        id_absensi=absensi.id if absensi is not None else None,
        status_masuk=absensi.status_masuk if absensi is not None else None,
        status_final=absensi.status_final if absensi is not None else None,
        jam_masuk=absensi.jam_masuk if absensi is not None else None,
        jam_pulang=absensi.jam_pulang if absensi is not None else None,
        id_pemilik=id_pemilik,
        jenis_pemilik=jenis_pemilik.value if jenis_pemilik is not None else None,
        nama=nama,
    )
