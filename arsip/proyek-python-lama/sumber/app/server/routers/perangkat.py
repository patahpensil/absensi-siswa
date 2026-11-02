"""Endpoint Perangkat (PRD §12.4): izin darurat alat RFID dan wizard
ganti jalur. Lingkup peran mengikuti app.core.perangkat/app.core.status:
memberi izin darurat = kepsek/admin/wali kelas; memperpanjang, mencabut,
dan ganti jalur = kepsek/admin saja. Cek status aktif per station TIDAK
dibatasi peran - dipakai klien lobi (peran Operator) untuk menyalakan
banner MODE DARURAT.
"""

from __future__ import annotations

from typing import Annotated

from fastapi import APIRouter, Depends, HTTPException, status
from sqlalchemy.orm import Session

from app.config.pengaturan import Pengaturan
from app.core.perangkat import (
    AksesGantiJalurDitolak,
    AksesIzinDaruratDitolak,
    AksesPerpanjangIzinDaruratDitolak,
    DurasiIzinMelebihiBatas,
    JumlahKartuVerifikasiTidakCukup,
    KartuTidakTerdaftarSaatVerifikasi,
    KartuVerifikasiTidakUnik,
)
from app.core.status import Peran
from app.data.models.perangkat import IzinDaruratAlat
from app.rfid.base import JalurAlat
from app.data.models.perangkat import TelemetriLobi
from app.data.models.system import User
from app.data.repositories.perangkat import IzinDaruratAlatRepository, TelemetriLobiRepository
from app.data.repositories.rfid_card import RfidCardRepository
from app.data.repositories.setting import SettingRepository
from app.server.dependencies import CurrentUserDep, SessionDep, wajib_peran
from app.server.perangkat_service import (
    GantiJalurService,
    IzinDaruratService,
    IzinSudahTidakAktif,
    IzinTidakDitemukan,
    SudahAdaIzinAktif,
)
from app.server.schemas import (
    GantiJalurRequest,
    GantiJalurResponse,
    IzinDaruratAktifResponse,
    IzinDaruratBuatRequest,
    IzinDaruratCabutRequest,
    IzinDaruratPerpanjangRequest,
    IzinDaruratResponse,
    TelemetriLobiRequest,
    TelemetriLobiResponse,
)

router = APIRouter(prefix="/perangkat", tags=["perangkat"])

#: PRD §12.4: kepsek, admin, atau wali kelas boleh MEMBERI izin darurat.
KewenanganBeriIzinDep = Annotated[
    User, Depends(wajib_peran(Peran.KEPALA_SEKOLAH, Peran.ADMIN, Peran.WALI_KELAS))
]
#: SKILL.md "Kunci jalur alat": perpanjangan/pencabutan HANYA kepsek/admin.
#: Wizard ganti jalur juga eksplisit kepsek/admin.
KewenanganAdminDep = Annotated[User, Depends(wajib_peran(Peran.KEPALA_SEKOLAH, Peran.ADMIN))]


def _izin_service(session: Session) -> IzinDaruratService:
    return IzinDaruratService(
        izin_repo=IzinDaruratAlatRepository(session), pengaturan=Pengaturan(SettingRepository(session))
    )


def _ganti_jalur_service(session: Session) -> GantiJalurService:
    return GantiJalurService(
        rfid_card_repo=RfidCardRepository(session), pengaturan=Pengaturan(SettingRepository(session))
    )


def _ke_response(izin: IzinDaruratAlat) -> IzinDaruratResponse:
    return IzinDaruratResponse(
        id=izin.id,
        station_id=izin.station_id,
        id_pengguna_pemberi=izin.id_pengguna_pemberi,
        waktu_mulai=izin.waktu_mulai,
        waktu_berakhir=izin.waktu_berakhir,
        alasan=izin.alasan,
        dicabut=izin.dicabut,
    )


@router.post("/izin-darurat", response_model=IzinDaruratResponse, status_code=status.HTTP_201_CREATED)
def beri_izin_darurat(
    payload: IzinDaruratBuatRequest, session: SessionDep, pengguna: KewenanganBeriIzinDep
) -> IzinDaruratResponse:
    try:
        izin = _izin_service(session).beri_izin(
            pelaku=pengguna, station_id=payload.station_id, jam=payload.jam, alasan=payload.alasan
        )
    except AksesIzinDaruratDitolak as exc:
        raise HTTPException(status.HTTP_403_FORBIDDEN, str(exc)) from exc
    except DurasiIzinMelebihiBatas as exc:
        raise HTTPException(status.HTTP_422_UNPROCESSABLE_ENTITY, str(exc)) from exc
    except SudahAdaIzinAktif as exc:
        raise HTTPException(status.HTTP_409_CONFLICT, str(exc)) from exc
    return _ke_response(izin)


@router.post("/izin-darurat/{id_izin}/perpanjang", response_model=IzinDaruratResponse)
def perpanjang_izin_darurat(
    id_izin: int, payload: IzinDaruratPerpanjangRequest, session: SessionDep, pengguna: KewenanganAdminDep
) -> IzinDaruratResponse:
    try:
        izin = _izin_service(session).perpanjang(
            pelaku=pengguna, id_izin=id_izin, jam=payload.jam, alasan=payload.alasan
        )
    except AksesPerpanjangIzinDaruratDitolak as exc:
        raise HTTPException(status.HTTP_403_FORBIDDEN, str(exc)) from exc
    except IzinTidakDitemukan as exc:
        raise HTTPException(status.HTTP_404_NOT_FOUND, "Izin darurat tidak ditemukan") from exc
    except IzinSudahTidakAktif as exc:
        raise HTTPException(status.HTTP_409_CONFLICT, "Izin ini sudah dicabut atau sudah kedaluwarsa") from exc
    except DurasiIzinMelebihiBatas as exc:
        raise HTTPException(status.HTTP_422_UNPROCESSABLE_ENTITY, str(exc)) from exc
    return _ke_response(izin)


@router.post("/izin-darurat/{id_izin}/cabut", response_model=IzinDaruratResponse)
def cabut_izin_darurat(
    id_izin: int, payload: IzinDaruratCabutRequest, session: SessionDep, pengguna: KewenanganAdminDep
) -> IzinDaruratResponse:
    try:
        izin = _izin_service(session).cabut(pelaku=pengguna, id_izin=id_izin, alasan=payload.alasan)
    except AksesPerpanjangIzinDaruratDitolak as exc:
        raise HTTPException(status.HTTP_403_FORBIDDEN, str(exc)) from exc
    except IzinTidakDitemukan as exc:
        raise HTTPException(status.HTTP_404_NOT_FOUND, "Izin darurat tidak ditemukan") from exc
    except IzinSudahTidakAktif as exc:
        raise HTTPException(status.HTTP_409_CONFLICT, "Izin ini sudah dicabut atau sudah kedaluwarsa") from exc
    return _ke_response(izin)


@router.get("/izin-darurat", response_model=list[IzinDaruratResponse])
def daftar_izin_darurat_aktif(session: SessionDep, pengguna: KewenanganBeriIzinDep) -> list[IzinDaruratResponse]:
    return [_ke_response(izin) for izin in _izin_service(session).daftar_aktif()]


@router.get("/izin-darurat/aktif", response_model=IzinDaruratAktifResponse)
def cek_izin_darurat_aktif(
    station_id: str, session: SessionDep, pengguna: CurrentUserDep
) -> IzinDaruratAktifResponse:
    """TIDAK dibatasi peran (beda dari endpoint lain di router ini) -
    layar lobi (peran Operator) memanggil ini untuk menyalakan banner
    MODE DARURAT, lihat app.ui.lobi.layar.LayarAbsensiLobi.set_mode_darurat."""
    izin = _izin_service(session).cek_aktif_by_station(station_id)
    if izin is None:
        return IzinDaruratAktifResponse(aktif=False, station_id=station_id)
    return IzinDaruratAktifResponse(aktif=True, station_id=station_id, waktu_berakhir=izin.waktu_berakhir)


@router.post("/ganti-jalur", response_model=GantiJalurResponse)
def ganti_jalur(
    payload: GantiJalurRequest, session: SessionDep, pengguna: KewenanganAdminDep
) -> GantiJalurResponse:
    try:
        JalurAlat(payload.jalur_baru)
    except ValueError as exc:
        raise HTTPException(
            status.HTTP_422_UNPROCESSABLE_ENTITY, f"jalur_baru tidak dikenal: {payload.jalur_baru}"
        ) from exc
    try:
        hasil = _ganti_jalur_service(session).jalankan(
            pelaku=pengguna,
            jalur_baru=payload.jalur_baru,
            uid_kartu_verifikasi=payload.uid_kartu_verifikasi,
            alasan=payload.alasan,
        )
    except AksesGantiJalurDitolak as exc:
        raise HTTPException(status.HTTP_403_FORBIDDEN, str(exc)) from exc
    except (JumlahKartuVerifikasiTidakCukup, KartuVerifikasiTidakUnik, KartuTidakTerdaftarSaatVerifikasi) as exc:
        raise HTTPException(status.HTTP_422_UNPROCESSABLE_ENTITY, str(exc)) from exc
    return GantiJalurResponse(jalur_lama=hasil.jalur_lama, jalur_baru=hasil.jalur_baru)


@router.post("/telemetri", response_model=TelemetriLobiResponse, status_code=status.HTTP_201_CREATED)
def lapor_telemetri(
    payload: TelemetriLobiRequest, session: SessionDep, pengguna: CurrentUserDep
) -> TelemetriLobiResponse:
    """Dipanggil berkala oleh app.ui.lobi.klien_scan.KlienScan.laporkan_telemetri
    (PRD §14.1: "jumlah scan yang masih mengantre di PC lobi") - TIDAK
    dibatasi peran (klien lobi login sebagai Operator, yang tidak
    berwenang atas Dashboard sendiri, tapi tetap boleh MELAPOR angka
    murni operasional miliknya sendiri)."""
    baris = TelemetriLobiRepository(session).lapor(
        station_id=payload.station_id, jumlah_pending=payload.jumlah_pending, id_pengguna=pengguna.id
    )
    return _ke_response_telemetri(baris)


def _ke_response_telemetri(baris: TelemetriLobi) -> TelemetriLobiResponse:
    return TelemetriLobiResponse(
        station_id=baris.station_id, jumlah_pending=baris.jumlah_pending, waktu_lapor=baris.waktu_lapor
    )
