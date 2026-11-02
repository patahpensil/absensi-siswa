from datetime import date, datetime
from typing import Any

from sqlalchemy import select

from app.data.models.attendance import Attendance
from app.data.models.core import Student
from app.data.models.enums import JenisPemilik
from app.data.models.laporan import LaporanArsip
from app.data.repositories.base import BaseRepository


class LaporanRepository(BaseRepository):
    """Akses data untuk laporan (PRD §14.2) - siswa saja, sama seperti
    koreksi massal (app.data.repositories.mass_correction) - "per kelas"/
    "per siswa" konsep yang tidak berlaku untuk guru.
    """

    def cari_absensi_untuk_laporan(
        self, *, tanggal_mulai: date, tanggal_selesai: date, id_kelas: int | None = None, id_siswa: int | None = None
    ) -> list[Attendance]:
        """Baris attendance siswa dalam rentang tanggal, opsional disaring
        satu kelas atau satu siswa. Diurutkan per siswa lalu tanggal supaya
        pemanggil (LaporanService) bisa mengelompokkan tanpa query tambahan.
        """
        stmt = (
            select(Attendance)
            .join(Student, Student.id == Attendance.id_pemilik)
            .where(
                Attendance.jenis_pemilik == JenisPemilik.SISWA,
                Attendance.tanggal >= tanggal_mulai,
                Attendance.tanggal <= tanggal_selesai,
            )
            .order_by(Attendance.id_pemilik, Attendance.tanggal)
        )
        if id_siswa is not None:
            stmt = stmt.where(Student.id == id_siswa)
        elif id_kelas is not None:
            stmt = stmt.where(Student.id_kelas == id_kelas)
        return list(self._session.execute(stmt).scalars())

    def cari_nama_siswa_untuk_absensi(self, id_absensi: int) -> str | None:
        """Nama siswa pemilik satu baris attendance - dipakai layar
        Koreksi Setelah Laporan Terbit (koreksi_satuan/tindakan_massal_baris
        hanya menyimpan id_absensi, bukan nama)."""
        stmt = select(Student.nama_lengkap).join(Attendance, Attendance.id_pemilik == Student.id).where(
            Attendance.id == id_absensi, Attendance.jenis_pemilik == JenisPemilik.SISWA
        )
        return self._session.execute(stmt).scalar_one_or_none()

    def catat_arsip(
        self,
        *,
        id_pengguna: int,
        label: str,
        tanggal_mulai: date,
        tanggal_selesai: date,
        id_kelas: int | None,
        id_siswa: int | None,
        penyaring: dict[str, Any],
        path_pdf: str,
        waktu_cetak: datetime,
    ) -> LaporanArsip:
        arsip = LaporanArsip(
            id_pengguna=id_pengguna,
            label=label,
            tanggal_mulai=tanggal_mulai,
            tanggal_selesai=tanggal_selesai,
            id_kelas=id_kelas,
            id_siswa=id_siswa,
            penyaring=penyaring,
            path_pdf=path_pdf,
            waktu_cetak=waktu_cetak,
        )
        self._session.add(arsip)
        self._commit()
        return arsip

    def cari_arsip_by_id(self, id_arsip: int) -> LaporanArsip | None:
        return self._session.get(LaporanArsip, id_arsip)

    def daftar_arsip(self, *, id_kelas: int | None = None, limit: int = 200) -> list[LaporanArsip]:
        """Riwayat laporan yang pernah dicetak, terbaru dulu.

        ``id_kelas`` dipakai wali kelas (satu wali kelas selalu tepat satu
        kelas diampu) - hanya arsip dengan id_kelas itu persis yang
        muncul; arsip seluruh-sekolah (id_kelas None di baris arsip) atau
        kelas lain TIDAK ikut tampil, supaya tidak bocor cakupan laporan
        lain. Kepsek/admin memanggil tanpa id_kelas untuk melihat semua.
        """
        stmt = select(LaporanArsip).order_by(LaporanArsip.waktu_cetak.desc()).limit(limit)
        if id_kelas is not None:
            stmt = stmt.where(LaporanArsip.id_kelas == id_kelas)
        return list(self._session.execute(stmt).scalars())
