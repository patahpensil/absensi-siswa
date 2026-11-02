from sqlalchemy import select

from app.data.models.core import Teacher
from app.data.repositories.base import BaseRepository


class TeacherRepository(BaseRepository):
    def tambah(
        self,
        *,
        nama_lengkap: str,
        nip: str | None = None,
        pangkat_jabatan: str | None = None,
        status_kepegawaian: str | None = None,
    ) -> Teacher:
        guru = Teacher(
            nama_lengkap=nama_lengkap,
            nip=nip,
            pangkat_jabatan=pangkat_jabatan,
            status_kepegawaian=status_kepegawaian,
            aktif=True,
        )
        self._session.add(guru)
        self._commit()
        return guru

    def ubah(
        self,
        id_guru: int,
        *,
        nama_lengkap: str,
        nip: str | None,
        pangkat_jabatan: str | None,
    ) -> Teacher | None:
        """status_kepegawaian sengaja tidak ikut diubah di sini - topik
        terpisah (status kehadiran guru, belum ditetapkan sekolah)."""
        guru = self._session.get(Teacher, id_guru)
        if guru is None:
            return None
        guru.nama_lengkap = nama_lengkap
        guru.nip = nip
        guru.pangkat_jabatan = pangkat_jabatan
        self._commit()
        return guru

    def cari_by_id(self, id_guru: int) -> Teacher | None:
        return self._session.get(Teacher, id_guru)

    def cari_by_nip(self, nip: str) -> Teacher | None:
        stmt = select(Teacher).where(Teacher.nip == nip)
        return self._session.execute(stmt).scalar_one_or_none()

    def daftar(self, *, hanya_aktif: bool = True) -> list[Teacher]:
        stmt = select(Teacher)
        if hanya_aktif:
            stmt = stmt.where(Teacher.aktif.is_(True))
        return list(self._session.execute(stmt).scalars())
