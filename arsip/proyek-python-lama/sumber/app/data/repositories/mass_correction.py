from datetime import date, datetime
from typing import Any

from sqlalchemy import select

from app.core.timeutil import now_wita
from app.data.models.attendance import Attendance
from app.data.models.core import Kelas, Student
from app.data.models.enums import JenisPemilik
from app.data.models.koreksi_massal import TindakanMassal, TindakanMassalBaris
from app.data.repositories.base import BaseRepository


class TindakanMassalTidakDitemukan(LookupError):
    pass


class TindakanMassalSudahDibatalkan(Exception):
    pass


class MassCorrectionRepository(BaseRepository):
    """Akses data untuk tindakan_massal dan tindakan_massal_baris.

    Method yang menulis di sini SENGAJA tidak commit sendiri (kecuali
    commit() eksplisit) - satu tindakan massal menyentuh banyak baris
    attendance sekaligus, dan semuanya harus tersimpan dalam satu
    transaksi lewat app.core.koreksi_massal_service, bukan tercecer per
    baris seperti koreksi satuan.
    """

    def cari_baris_terdampak(
        self,
        *,
        tanggal_mulai: date,
        tanggal_selesai: date,
        lingkup: str,
        tingkat: int | None,
        id_kelas: int | None,
        id_siswa_terpilih: frozenset[int],
        status_termasuk: frozenset[str],
    ) -> list[Attendance]:
        """Koreksi massal hanya untuk siswa (lihat SKILL.md "Koreksi
        massal" - "Orang" berbasis tingkat/kelas, konsep yang tidak
        berlaku untuk guru)."""
        stmt = (
            select(Attendance)
            .join(Student, Student.id == Attendance.id_pemilik)
            .where(
                Attendance.jenis_pemilik == JenisPemilik.SISWA,
                Attendance.tanggal >= tanggal_mulai,
                Attendance.tanggal <= tanggal_selesai,
                Attendance.status_final.in_(status_termasuk),
            )
        )
        if lingkup == "TINGKAT":
            stmt = stmt.join(Kelas, Kelas.id == Student.id_kelas).where(Kelas.tingkat == tingkat)
        elif lingkup == "KELAS":
            stmt = stmt.where(Student.id_kelas == id_kelas)
        elif lingkup == "SISWA_TERPILIH":
            stmt = stmt.where(Student.id.in_(id_siswa_terpilih))
        # SEKOLAH: tidak ada penyaring orang tambahan.
        return list(self._session.execute(stmt).scalars())

    def catat(
        self,
        *,
        id_pengguna: int,
        jenis_tindakan: str,
        status_baru: str | None,
        penyaring: dict[str, Any],
        jumlah_baris: int,
        alasan: str,
    ) -> TindakanMassal:
        tindakan = TindakanMassal(
            id_pengguna=id_pengguna,
            jenis_tindakan=jenis_tindakan,
            status_baru=status_baru,
            penyaring=penyaring,
            jumlah_baris=jumlah_baris,
            alasan=alasan,
        )
        self._session.add(tindakan)
        self._session.flush()  # supaya tindakan.id terisi sebelum dipakai TindakanMassalBaris
        return tindakan

    def catat_baris(
        self, *, id_tindakan_massal: int, id_absensi: int, nilai_sebelum: dict, nilai_sesudah: dict
    ) -> TindakanMassalBaris:
        baris = TindakanMassalBaris(
            id_tindakan_massal=id_tindakan_massal,
            id_absensi=id_absensi,
            nilai_sebelum=nilai_sebelum,
            nilai_sesudah=nilai_sesudah,
        )
        self._session.add(baris)
        return baris

    def cari_by_id(self, id_tindakan_massal: int) -> TindakanMassal | None:
        return self._session.get(TindakanMassal, id_tindakan_massal)

    def daftar_terbaru(self, *, limit: int = 50) -> list[TindakanMassal]:
        """Untuk layar Riwayat Tindakan Massal (SKILL.md "Koreksi massal")."""
        stmt = select(TindakanMassal).order_by(TindakanMassal.waktu.desc()).limit(limit)
        return list(self._session.execute(stmt).scalars())

    def daftar_baris(self, id_tindakan_massal: int) -> list[TindakanMassalBaris]:
        stmt = select(TindakanMassalBaris).where(TindakanMassalBaris.id_tindakan_massal == id_tindakan_massal)
        return list(self._session.execute(stmt).scalars())

    def daftar_baris_setelah(
        self, *, sejak: datetime, ids_absensi: list[int]
    ) -> list[tuple[TindakanMassalBaris, TindakanMassal]]:
        """Pasangan (baris, tindakan induknya) dari tindakan yang TIDAK
        dibatalkan dengan TindakanMassal.waktu SETELAH ``sejak``, untuk
        baris attendance dalam ``ids_absensi`` - pasangan
        app.data.repositories.koreksi_satuan.KoreksiSatuanRepository.daftar_setelah()
        untuk layar Koreksi Setelah Laporan Terbit
        (app.server.laporan_service). Induknya ikut dikembalikan (bukan
        cuma TindakanMassalBaris) karena pelaku/alasan hanya ada di
        TindakanMassal, tidak diduplikasi ke tiap baris - lihat
        docstring TindakanMassal.
        """
        if not ids_absensi:
            return []
        stmt = (
            select(TindakanMassalBaris, TindakanMassal)
            .join(TindakanMassal, TindakanMassal.id == TindakanMassalBaris.id_tindakan_massal)
            .where(
                TindakanMassal.waktu > sejak,
                TindakanMassal.dibatalkan.is_(False),
                TindakanMassalBaris.id_absensi.in_(ids_absensi),
            )
            .order_by(TindakanMassal.waktu.desc())
        )
        return [(baris, tindakan) for baris, tindakan in self._session.execute(stmt).all()]

    def tandai_dibatalkan(self, tindakan: TindakanMassal, *, id_pengguna: int) -> TindakanMassal:
        tindakan.dibatalkan = True
        tindakan.waktu_dibatalkan = now_wita()
        tindakan.dibatalkan_oleh = id_pengguna
        return tindakan

    def commit(self) -> None:
        self._commit()
