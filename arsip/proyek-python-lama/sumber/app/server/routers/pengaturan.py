"""Endpoint Pengaturan (PRD §4, §14.2): jadwal per hari (versi berlaku
per tanggal), ambang, saklar modul, identitas sekolah. Akses kepala
sekolah + admin SAJA - termasuk melihat (beda dari Kalender yang membuka
GET untuk seluruh peran; PRD §10 tidak memberi wali kelas/operator hak
lihat Pengaturan sama sekali).

TIDAK mengubah cara app.core.engine/mesin_absensi maupun
app.scheduler.scheduler membaca konfigurasi - endpoint ini murni
menyediakan UI di atas ScheduleRepository dan Pengaturan yang sudah ada.
Satu-satunya wiring baru: memanggil scheduler.reload_jadwal() (method
yang SUDAH ADA, didokumentasikan sebagai "dipanggil setelah admin
mengubah pengaturan jam") lewat referensi opsional di app.state.scheduler
(lihat app/server/main.py) supaya scheduler yang sedang berjalan tidak
perlu di-restart manual setelah jam diubah.
"""

from typing import Annotated

from fastapi import APIRouter, Depends, Request

from app.config.pengaturan import IdentitasSekolah, Pengaturan
from app.core.exceptions import KonfigurasiBelumLengkap
from app.core.status import Peran
from app.data.models.enums import JenisPemilik
from app.data.models.system import User
from app.data.repositories.schedule import ScheduleRepository
from app.data.repositories.setting import SettingRepository
from app.server.dependencies import SessionDep, wajib_peran
from app.server.schemas import (
    AmbangResponse,
    AmbangUbahRequest,
    IdentitasSekolahResponse,
    IdentitasSekolahUbahRequest,
    JadwalBaruRequest,
    JadwalResponse,
    SaklarResponse,
    SaklarUbahRequest,
)

router = APIRouter(prefix="/pengaturan", tags=["pengaturan"])

KewenanganDep = Annotated[User, Depends(wajib_peran(Peran.KEPALA_SEKOLAH, Peran.ADMIN))]


def _muat_ulang_scheduler(request: Request) -> None:
    """Lihat docstring modul - tidak fatal bila scheduler belum
    dipasang (mis. TestClient tanpa app.state.scheduler) atau belum bisa
    dijadwalkan (jam belum lengkap)."""
    scheduler = getattr(request.app.state, "scheduler", None)
    if scheduler is None:
        return
    try:
        scheduler.reload_jadwal()
    except KonfigurasiBelumLengkap:
        pass


def _ke_jadwal_response(jenis_pemilik: JenisPemilik, baris) -> JadwalResponse:
    return JadwalResponse(
        id=baris.id,
        jenis_pemilik=jenis_pemilik.value,
        hari=baris.hari,
        jam_masuk=baris.jam_masuk,
        batas_terlambat=baris.batas_terlambat,
        jam_pulang=baris.jam_pulang,
        cutoff_alpa=getattr(baris, "cutoff_alpa", None),
        pulang_mulai=getattr(baris, "pulang_mulai", None),
        berlaku_mulai=baris.berlaku_mulai,
        berlaku_sampai=baris.berlaku_sampai,
    )


@router.get("/jadwal", response_model=list[JadwalResponse])
def daftar_jadwal(jenis_pemilik: str, session: SessionDep, pengguna: KewenanganDep) -> list[JadwalResponse]:
    jenis = JenisPemilik(jenis_pemilik)
    baris = ScheduleRepository(session).daftar(jenis_pemilik=jenis)
    return [_ke_jadwal_response(jenis, b) for b in baris]


@router.post("/jadwal", response_model=JadwalResponse, status_code=201)
def tambah_jadwal(
    payload: JadwalBaruRequest, request: Request, session: SessionDep, pengguna: KewenanganDep
) -> JadwalResponse:
    """Selalu INSERT baris baru - tidak pernah menimpa baris lama.
    ScheduleRepository.jadwal_berlaku() memilih baris ber-berlaku_mulai
    TERBARU yang <= tanggal absensi (lihat docstring method itu), jadi
    baris lama tetap resolve benar untuk tanggal sebelum berlaku_mulai
    baris baru ini - historis tidak bergeser tanpa perlu mengubah
    berlaku_sampai baris lama sama sekali."""
    jenis = JenisPemilik(payload.jenis_pemilik)
    baris = ScheduleRepository(session).tambah(
        jenis_pemilik=jenis,
        hari=payload.hari,
        jam_masuk=payload.jam_masuk,
        batas_terlambat=payload.batas_terlambat,
        jam_pulang=payload.jam_pulang,
        berlaku_mulai=payload.berlaku_mulai,
        cutoff_alpa=payload.cutoff_alpa,
        pulang_mulai=payload.pulang_mulai,
        berlaku_sampai=payload.berlaku_sampai,
    )
    if jenis is JenisPemilik.SISWA:
        # Hanya jadwal siswa yang memengaruhi cron cutoff pulang/notifikasi
        # ALPA pulang scheduler (lihat SchedulerAbsensi._baca_jam_dari_config).
        _muat_ulang_scheduler(request)
    return _ke_jadwal_response(jenis, baris)


@router.get("/ambang", response_model=AmbangResponse)
def ambil_ambang(session: SessionDep, pengguna: KewenanganDep) -> AmbangResponse:
    p = Pengaturan(SettingRepository(session))
    return AmbangResponse(
        jendela_scan_berulang_detik=p.jendela_scan_berulang_detik(),
        batas_terlambat_guru=p.batas_terlambat_guru(),
        ambang_tahan_konversi_pulang=p.ambang_tahan_konversi_pulang,
        cutoff_alpa=p.cutoff_alpa(),
        waktu_kirim_notif_alpa=p.waktu_kirim_notif_alpa(),
    )


@router.put("/ambang", response_model=AmbangResponse)
def ubah_ambang(
    payload: AmbangUbahRequest, request: Request, session: SessionDep, pengguna: KewenanganDep
) -> AmbangResponse:
    p = Pengaturan(SettingRepository(session))
    perlu_reload_scheduler = False

    if payload.jendela_scan_berulang_detik is not None:
        p.atur_jendela_scan_berulang_detik(
            payload.jendela_scan_berulang_detik, id_pengguna=pengguna.id, alasan=payload.alasan
        )
    if payload.batas_terlambat_guru is not None:
        p.atur_batas_terlambat_guru(payload.batas_terlambat_guru, id_pengguna=pengguna.id, alasan=payload.alasan)
    if payload.ambang_tahan_konversi_pulang is not None:
        p.atur_ambang_tahan_konversi_pulang(
            payload.ambang_tahan_konversi_pulang, id_pengguna=pengguna.id, alasan=payload.alasan
        )
    if payload.cutoff_alpa is not None:
        p.atur_cutoff_alpa(payload.cutoff_alpa, id_pengguna=pengguna.id, alasan=payload.alasan)
        perlu_reload_scheduler = True
    if payload.waktu_kirim_notif_alpa is not None:
        p.atur_waktu_kirim_notif_alpa(payload.waktu_kirim_notif_alpa, id_pengguna=pengguna.id, alasan=payload.alasan)
        perlu_reload_scheduler = True

    if perlu_reload_scheduler:
        _muat_ulang_scheduler(request)

    return AmbangResponse(
        jendela_scan_berulang_detik=p.jendela_scan_berulang_detik(),
        batas_terlambat_guru=p.batas_terlambat_guru(),
        ambang_tahan_konversi_pulang=p.ambang_tahan_konversi_pulang,
        cutoff_alpa=p.cutoff_alpa(),
        waktu_kirim_notif_alpa=p.waktu_kirim_notif_alpa(),
    )


@router.get("/saklar", response_model=SaklarResponse)
def ambil_saklar(session: SessionDep, pengguna: KewenanganDep) -> SaklarResponse:
    p = Pengaturan(SettingRepository(session))
    return SaklarResponse(modul_whatsapp_aktif=p.modul_whatsapp_aktif(), modul_absen_pulang=p.modul_absen_pulang)


@router.put("/saklar", response_model=SaklarResponse)
def ubah_saklar(payload: SaklarUbahRequest, session: SessionDep, pengguna: KewenanganDep) -> SaklarResponse:
    p = Pengaturan(SettingRepository(session))
    if payload.modul_whatsapp_aktif is not None:
        p.atur_modul_whatsapp_aktif(payload.modul_whatsapp_aktif, id_pengguna=pengguna.id, alasan=payload.alasan)
    if payload.modul_absen_pulang is not None:
        p.atur_modul_absen_pulang(payload.modul_absen_pulang, id_pengguna=pengguna.id, alasan=payload.alasan)
    return SaklarResponse(modul_whatsapp_aktif=p.modul_whatsapp_aktif(), modul_absen_pulang=p.modul_absen_pulang)


@router.get("/identitas-sekolah", response_model=IdentitasSekolahResponse)
def ambil_identitas_sekolah(session: SessionDep, pengguna: KewenanganDep) -> IdentitasSekolahResponse:
    identitas = Pengaturan(SettingRepository(session)).identitas_sekolah()
    return IdentitasSekolahResponse(**identitas.__dict__)


@router.put("/identitas-sekolah", response_model=IdentitasSekolahResponse)
def ubah_identitas_sekolah(
    payload: IdentitasSekolahUbahRequest, session: SessionDep, pengguna: KewenanganDep
) -> IdentitasSekolahResponse:
    p = Pengaturan(SettingRepository(session))
    p.atur_identitas_sekolah(
        IdentitasSekolah(
            nama=payload.nama,
            npsn=payload.npsn,
            alamat=payload.alamat,
            logo=payload.logo,
            nama_kepala_sekolah=payload.nama_kepala_sekolah,
            nip_kepala_sekolah=payload.nip_kepala_sekolah,
        ),
        id_pengguna=pengguna.id,
        alasan=payload.alasan,
    )
    return IdentitasSekolahResponse(**p.identitas_sekolah().__dict__)
