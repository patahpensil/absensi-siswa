"""Endpoint Laporan (PRD §14.2, §9.3): rekap/detail absensi siswa,
ekspor Excel/PDF berkop sekolah, arsip PDF otomatis, dan Koreksi Setelah
Laporan Terbit. Kepala sekolah/admin bebas; wali kelas HANYA kelasnya
sendiri; operator TIDAK BERWENANG SAMA SEKALI (lihat docstring
app.server.laporan_service untuk rincian lingkup peran PRD §10).
"""

from __future__ import annotations

from datetime import date
from typing import Annotated

from fastapi import APIRouter, Depends, HTTPException, status
from fastapi.responses import Response

from app.config.pengaturan import Pengaturan
from app.core.akses_kelas import AksesKelasDitolak
from app.core.laporan import AksesLaporanDitolak
from app.core.status import Peran
from app.data.models.system import User
from app.data.repositories.kelas import KelasRepository
from app.data.repositories.koreksi_satuan import KoreksiSatuanRepository
from app.data.repositories.laporan import LaporanRepository
from app.data.repositories.mass_correction import MassCorrectionRepository
from app.data.repositories.setting import SettingRepository
from app.data.repositories.student import StudentRepository
from app.server.dependencies import LaporanDirDep, SessionDep, wajib_peran
from app.server.laporan_service import (
    ArsipTidakDitemukan,
    KelasTidakDitemukan,
    LaporanService,
    SiswaTidakDitemukan,
    WaliKelasBelumDiampu,
)
from app.server.laporan_storage import path_arsip_laporan
from app.server.schemas import (
    BarisDetailHarianResponse,
    HasilLaporanResponse,
    KoreksiSetelahTerbitResponse,
    LaporanArsipResponse,
    LaporanDetailSiswaResponse,
    RekapSiswaResponse,
)

router = APIRouter(prefix="/laporan", tags=["laporan"])

#: PRD §10, baris "Dashboard dan laporan" - operator SENGAJA tidak
#: termasuk (beda dari Riwayat Scan).
KewenanganDep = Annotated[User, Depends(wajib_peran(Peran.KEPALA_SEKOLAH, Peran.ADMIN, Peran.WALI_KELAS))]


def _buat_service(session) -> LaporanService:
    return LaporanService(
        laporan_repo=LaporanRepository(session),
        student_repo=StudentRepository(session),
        kelas_repo=KelasRepository(session),
        koreksi_satuan_repo=KoreksiSatuanRepository(session),
        mass_correction_repo=MassCorrectionRepository(session),
        pengaturan=Pengaturan(SettingRepository(session)),
    )


def _ke_hasil_response(hasil) -> HasilLaporanResponse:
    return HasilLaporanResponse(
        label=hasil.label,
        tanggal_mulai=hasil.tanggal_mulai,
        tanggal_selesai=hasil.tanggal_selesai,
        id_kelas=hasil.id_kelas,
        nama_kelas=hasil.nama_kelas,
        id_siswa=hasil.id_siswa,
        rekap=(
            [
                RekapSiswaResponse(
                    id_siswa=r.id_siswa,
                    nama_lengkap=r.nama_lengkap,
                    nama_kelas=r.nama_kelas,
                    jumlah_hari_tercatat=r.jumlah_hari_tercatat,
                    jumlah=r.jumlah,
                )
                for r in hasil.rekap
            ]
            if hasil.rekap is not None
            else None
        ),
        detail=(
            LaporanDetailSiswaResponse(
                nama_lengkap=hasil.detail.nama_lengkap,
                nisn=hasil.detail.nisn,
                nama_kelas=hasil.detail.nama_kelas,
                baris=[
                    BarisDetailHarianResponse(
                        tanggal=b.tanggal, status_masuk=b.status_masuk, status_final=b.status_final,
                        alasan_final=b.alasan_final,
                    )
                    for b in hasil.detail.baris
                ],
            )
            if hasil.detail is not None
            else None
        ),
    )


@router.get("", response_model=HasilLaporanResponse)
def lihat_laporan(
    session: SessionDep,
    pengguna: KewenanganDep,
    tanggal_mulai: date,
    tanggal_selesai: date,
    id_kelas: int | None = None,
    id_siswa: int | None = None,
) -> HasilLaporanResponse:
    try:
        hasil = _buat_service(session).hasilkan(
            pelaku=pengguna, tanggal_mulai=tanggal_mulai, tanggal_selesai=tanggal_selesai, id_kelas=id_kelas,
            id_siswa=id_siswa,
        )
    except (AksesLaporanDitolak, AksesKelasDitolak, WaliKelasBelumDiampu) as exc:
        raise HTTPException(status.HTTP_403_FORBIDDEN, str(exc)) from exc
    except (SiswaTidakDitemukan, KelasTidakDitemukan) as exc:
        raise HTTPException(status.HTTP_404_NOT_FOUND, str(exc)) from exc
    return _ke_hasil_response(hasil)


@router.get("/ekspor.xlsx")
def ekspor_excel(
    session: SessionDep,
    pengguna: KewenanganDep,
    tanggal_mulai: date,
    tanggal_selesai: date,
    id_kelas: int | None = None,
    id_siswa: int | None = None,
) -> Response:
    try:
        isi = _buat_service(session).ekspor_excel(
            pelaku=pengguna, tanggal_mulai=tanggal_mulai, tanggal_selesai=tanggal_selesai, id_kelas=id_kelas,
            id_siswa=id_siswa,
        )
    except (AksesLaporanDitolak, AksesKelasDitolak, WaliKelasBelumDiampu) as exc:
        raise HTTPException(status.HTTP_403_FORBIDDEN, str(exc)) from exc
    except (SiswaTidakDitemukan, KelasTidakDitemukan) as exc:
        raise HTTPException(status.HTTP_404_NOT_FOUND, str(exc)) from exc
    return Response(
        content=isi,
        media_type="application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
        headers={"Content-Disposition": "attachment; filename=laporan.xlsx"},
    )


@router.get("/ekspor.pdf")
def ekspor_pdf(
    session: SessionDep,
    pengguna: KewenanganDep,
    laporan_dir: LaporanDirDep,
    tanggal_mulai: date,
    tanggal_selesai: date,
    id_kelas: int | None = None,
    id_siswa: int | None = None,
) -> Response:
    """Menghasilkan PDF SEKALIGUS mengarsipkannya (PRD §9.3) - lihat
    docstring LaporanService.ekspor_pdf()."""
    try:
        isi, _arsip = _buat_service(session).ekspor_pdf(
            pelaku=pengguna, tanggal_mulai=tanggal_mulai, tanggal_selesai=tanggal_selesai, id_kelas=id_kelas,
            id_siswa=id_siswa, laporan_dir=laporan_dir,
        )
    except (AksesLaporanDitolak, AksesKelasDitolak, WaliKelasBelumDiampu) as exc:
        raise HTTPException(status.HTTP_403_FORBIDDEN, str(exc)) from exc
    except (SiswaTidakDitemukan, KelasTidakDitemukan) as exc:
        raise HTTPException(status.HTTP_404_NOT_FOUND, str(exc)) from exc
    return Response(
        content=isi, media_type="application/pdf", headers={"Content-Disposition": "attachment; filename=laporan.pdf"}
    )


@router.get("/arsip", response_model=list[LaporanArsipResponse])
def daftar_arsip(session: SessionDep, pengguna: KewenanganDep) -> list[LaporanArsipResponse]:
    try:
        daftar = _buat_service(session).daftar_arsip(pelaku=pengguna)
    except AksesLaporanDitolak as exc:
        raise HTTPException(status.HTTP_403_FORBIDDEN, str(exc)) from exc
    return [
        LaporanArsipResponse(
            id=a.id, id_pengguna=a.id_pengguna, label=a.label, tanggal_mulai=a.tanggal_mulai,
            tanggal_selesai=a.tanggal_selesai, id_kelas=a.id_kelas, id_siswa=a.id_siswa, path_pdf=a.path_pdf,
            waktu_cetak=a.waktu_cetak,
        )
        for a in daftar
    ]


@router.get("/arsip/{id_arsip}/unduh")
def unduh_arsip(id_arsip: int, session: SessionDep, pengguna: KewenanganDep, laporan_dir: LaporanDirDep) -> Response:
    service = _buat_service(session)
    try:
        # daftar_arsip() sudah menegakkan lingkup peran - cari arsip lewat
        # situ (bukan langsung repo) supaya wali kelas tidak bisa menebak
        # id arsip kelas lain untuk mengunduhnya.
        arsip = next((a for a in service.daftar_arsip(pelaku=pengguna) if a.id == id_arsip), None)
    except AksesLaporanDitolak as exc:
        raise HTTPException(status.HTTP_403_FORBIDDEN, str(exc)) from exc
    if arsip is None:
        raise HTTPException(status.HTTP_404_NOT_FOUND, "Arsip laporan tidak ditemukan")

    path = path_arsip_laporan(arsip.path_pdf, base_dir=laporan_dir)
    if not path.is_file():
        raise HTTPException(status.HTTP_404_NOT_FOUND, "Berkas arsip tidak ditemukan di server")
    return Response(content=path.read_bytes(), media_type="application/pdf")


@router.get("/arsip/{id_arsip}/koreksi-setelah-terbit", response_model=list[KoreksiSetelahTerbitResponse])
def koreksi_setelah_terbit(
    id_arsip: int, session: SessionDep, pengguna: KewenanganDep
) -> list[KoreksiSetelahTerbitResponse]:
    try:
        daftar = _buat_service(session).koreksi_setelah_terbit(pelaku=pengguna, id_arsip=id_arsip)
    except ArsipTidakDitemukan as exc:
        raise HTTPException(status.HTTP_404_NOT_FOUND, "Arsip laporan tidak ditemukan") from exc
    except (AksesLaporanDitolak, AksesKelasDitolak, WaliKelasBelumDiampu) as exc:
        raise HTTPException(status.HTTP_403_FORBIDDEN, str(exc)) from exc
    return [
        KoreksiSetelahTerbitResponse(
            sumber=k.sumber, id_absensi=k.id_absensi, nama_siswa=k.nama_siswa, tanggal=k.tanggal,
            waktu_koreksi=k.waktu_koreksi, id_pengguna=k.id_pengguna, nilai_sebelum=k.nilai_sebelum,
            nilai_sesudah=k.nilai_sesudah, alasan=k.alasan,
        )
        for k in daftar
    ]
