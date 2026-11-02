from sqlalchemy import func, select

from app.data.models.core import Kelas, Student
from app.data.repositories.base import BaseRepository


class KelasRepository(BaseRepository):
    def tambah(
        self,
        *,
        tingkat: int,
        nama_kelas: str,
        tahun_ajaran: str,
        wali_kelas_id: int | None = None,
    ) -> Kelas:
        kelas = Kelas(
            tingkat=tingkat,
            nama_kelas=nama_kelas,
            tahun_ajaran=tahun_ajaran,
            wali_kelas_id=wali_kelas_id,
            aktif=True,
        )
        self._session.add(kelas)
        self._commit()
        return kelas

    def ubah(
        self,
        id_kelas: int,
        *,
        tingkat: int,
        nama_kelas: str,
        tahun_ajaran: str,
        wali_kelas_id: int | None,
    ) -> Kelas | None:
        kelas = self._session.get(Kelas, id_kelas)
        if kelas is None:
            return None
        kelas.tingkat = tingkat
        kelas.nama_kelas = nama_kelas
        kelas.tahun_ajaran = tahun_ajaran
        kelas.wali_kelas_id = wali_kelas_id
        self._commit()
        return kelas

    def cari_by_id(self, id_kelas: int) -> Kelas | None:
        return self._session.get(Kelas, id_kelas)

    def daftar(self, *, hanya_aktif: bool = True) -> list[Kelas]:
        stmt = select(Kelas)
        if hanya_aktif:
            stmt = stmt.where(Kelas.aktif.is_(True))
        return list(self._session.execute(stmt).scalars())

    def hapus_atau_nonaktifkan(self, id_kelas: int) -> Kelas | None:
        """Hapus fisik hanya bila tidak ada siswa aktif di kelas itu; kalau
        ada, tandai tidak aktif. Aturan ini datang langsung dari
        data-model.md - soal integritas data kelas, bukan aturan status
        kehadiran.

        Mengembalikan Kelas yang ditandai tidak aktif, atau None bila
        baris benar-benar dihapus (atau tidak ditemukan).
        """
        kelas = self._session.get(Kelas, id_kelas)
        if kelas is None:
            return None

        jumlah_siswa_aktif = self._session.execute(
            select(func.count()).select_from(Student).where(Student.id_kelas == id_kelas, Student.aktif.is_(True))
        ).scalar_one()

        if jumlah_siswa_aktif > 0:
            kelas.aktif = False
            self._commit()
            return kelas

        self._session.delete(kelas)
        self._commit()
        return None
