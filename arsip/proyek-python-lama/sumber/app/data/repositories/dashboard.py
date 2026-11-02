"""Akses data untuk Dashboard (PRD §14.1): agregasi SQL langsung (GROUP
BY), bukan tarik-seluruh-baris-lalu-tally di Python seperti
LaporanRepository - bentuk yang dibutuhkan di sini per-tanggal/per-kelas,
bukan per-siswa, jadi agregasi di database lebih tepat dan lebih murah.

Siswa SAJA (sama seperti app.data.repositories.laporan) - "per kelas"/
ALPA tidak berlaku untuk guru.
"""

from __future__ import annotations

from datetime import date

from sqlalchemy import func, select

from app.data.models.attendance import Attendance
from app.data.models.core import Kelas, Student
from app.data.models.enums import JenisPemilik
from app.data.repositories.base import BaseRepository

#: Label dipakai untuk siswa tanpa kelas (Student.id_kelas None) - sama
#: istilah dengan SKILL.md ("siswa masuk 'Belum ditempatkan'").
LABEL_BELUM_DITEMPATKAN = "Belum ditempatkan"


class DashboardRepository(BaseRepository):
    def hitung_status_per_hari(
        self, *, tanggal_mulai: date, tanggal_selesai: date, id_kelas: int | None = None
    ) -> list[tuple[date, str | None, int]]:
        """(tanggal, status_final, jumlah) untuk seluruh siswa dalam
        rentang - bahan grafik harian/mingguan/bulanan DAN tren
        keterlambatan (app.core.dashboard yang mengelompokkan/menyaring
        lebih lanjut, murni fungsi, tanpa query tambahan)."""
        stmt = (
            select(Attendance.tanggal, Attendance.status_final, func.count())
            .join(Student, Student.id == Attendance.id_pemilik)
            .where(
                Attendance.jenis_pemilik == JenisPemilik.SISWA,
                Attendance.tanggal >= tanggal_mulai,
                Attendance.tanggal <= tanggal_selesai,
            )
            .group_by(Attendance.tanggal, Attendance.status_final)
        )
        if id_kelas is not None:
            stmt = stmt.where(Student.id_kelas == id_kelas)
        return list(self._session.execute(stmt).all())

    def hitung_status_per_kelas(
        self, *, tanggal_mulai: date, tanggal_selesai: date
    ) -> list[tuple[int | None, str | None, str | None, int]]:
        """(id_kelas, nama_kelas, status_final, jumlah) - bahan
        "perbandingan kedisiplinan antar kelas". LEFT JOIN ke classes
        supaya siswa tanpa kelas tetap ikut terhitung, bukan hilang diam-
        diam - app.core.dashboard yang memberi label "Belum ditempatkan"
        untuk id_kelas None."""
        stmt = (
            select(Kelas.id, Kelas.nama_kelas, Attendance.status_final, func.count())
            .select_from(Attendance)
            .join(Student, Student.id == Attendance.id_pemilik)
            .outerjoin(Kelas, Kelas.id == Student.id_kelas)
            .where(
                Attendance.jenis_pemilik == JenisPemilik.SISWA,
                Attendance.tanggal >= tanggal_mulai,
                Attendance.tanggal <= tanggal_selesai,
            )
            .group_by(Kelas.id, Kelas.nama_kelas, Attendance.status_final)
        )
        return list(self._session.execute(stmt).all())

    def daftar_alpa_terbanyak(
        self, *, tanggal_mulai: date, tanggal_selesai: date, id_kelas: int | None = None, limit: int = 10
    ) -> list[tuple[int, str, str | None, int]]:
        """(id_siswa, nama_lengkap, nama_kelas, jumlah_alpa) - siswa
        dengan status_final=ALPA terbanyak dalam rentang, untuk tindak
        lanjut wali kelas (PRD §14.1). Siswa tanpa ALPA sama sekali tidak
        muncul (bukan baris jumlah=0)."""
        stmt = (
            select(Student.id, Student.nama_lengkap, Kelas.nama_kelas, func.count())
            .select_from(Attendance)
            .join(Student, Student.id == Attendance.id_pemilik)
            .outerjoin(Kelas, Kelas.id == Student.id_kelas)
            .where(
                Attendance.jenis_pemilik == JenisPemilik.SISWA,
                Attendance.status_final == "ALPA",
                Attendance.tanggal >= tanggal_mulai,
                Attendance.tanggal <= tanggal_selesai,
            )
            .group_by(Student.id, Student.nama_lengkap, Kelas.nama_kelas)
            .order_by(func.count().desc())
            .limit(limit)
        )
        if id_kelas is not None:
            stmt = stmt.where(Student.id_kelas == id_kelas)
        return list(self._session.execute(stmt).all())
