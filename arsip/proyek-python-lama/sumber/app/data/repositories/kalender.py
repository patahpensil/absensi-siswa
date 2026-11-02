from datetime import date

from sqlalchemy import select

from app.core.timeutil import now_wita
from app.data.models.kalender import EntriKalender
from app.data.repositories.base import BaseRepository


class KalenderRepository(BaseRepository):
    """Akses data untuk kalender (entri kalender akademik dua jalur)."""

    def tambah(
        self,
        *,
        mulai: date,
        selesai: date,
        keterangan: str,
        jenis_siswa: str,
        jenis_guru: str,
        tahun_ajaran: str,
        id_pengguna: int,
        alasan: str,
    ) -> EntriKalender:
        entri = EntriKalender(
            mulai=mulai,
            selesai=selesai,
            keterangan=keterangan,
            jenis_siswa=jenis_siswa,
            jenis_guru=jenis_guru,
            tahun_ajaran=tahun_ajaran,
            id_pengguna=id_pengguna,
            alasan=alasan,
        )
        self._session.add(entri)
        self._commit()
        return entri

    def cari_by_id(self, id_entri: int) -> EntriKalender | None:
        return self._session.get(EntriKalender, id_entri)

    def daftar_aktif(self, *, tahun_ajaran: str | None = None) -> list[EntriKalender]:
        """Entri yang belum dibatalkan - inilah yang dipakai membentuk
        app.core.kalender.Kalender untuk penentuan jenis hari sungguhan.
        Beberapa tahun ajaran boleh aktif bersamaan (SKILL.md "Kalender
        akademik") - tahun_ajaran hanya menyaring kalau eksplisit diminta.
        """
        stmt = select(EntriKalender).where(EntriKalender.dibatalkan.is_(False))
        if tahun_ajaran is not None:
            stmt = stmt.where(EntriKalender.tahun_ajaran == tahun_ajaran)
        return list(self._session.execute(stmt).scalars())

    def daftar_terbaru(self, *, limit: int = 100) -> list[EntriKalender]:
        stmt = select(EntriKalender).order_by(EntriKalender.waktu_dibuat.desc()).limit(limit)
        return list(self._session.execute(stmt).scalars())

    def batalkan(self, entri: EntriKalender, *, id_pengguna: int) -> EntriKalender:
        entri.dibatalkan = True
        entri.waktu_dibatalkan = now_wita()
        entri.dibatalkan_oleh = id_pengguna
        self._commit()
        return entri
