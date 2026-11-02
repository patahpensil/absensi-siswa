from sqlalchemy import select

from app.data.models.core import Guardian
from app.data.repositories.base import BaseRepository


class GuardianRepository(BaseRepository):
    def tambah(
        self,
        *,
        id_siswa: int,
        nama: str,
        hubungan: str,
        nomor_whatsapp: str | None = None,
        menerima_notifikasi: bool = True,
    ) -> Guardian:
        wali = Guardian(
            id_siswa=id_siswa,
            nama=nama,
            hubungan=hubungan,
            nomor_whatsapp=nomor_whatsapp,
            menerima_notifikasi=menerima_notifikasi,
        )
        self._session.add(wali)
        self._commit()
        return wali

    def ubah(
        self,
        id_wali: int,
        *,
        nama: str,
        hubungan: str,
        nomor_whatsapp: str | None,
    ) -> Guardian | None:
        wali = self._session.get(Guardian, id_wali)
        if wali is None:
            return None
        wali.nama = nama
        wali.hubungan = hubungan
        wali.nomor_whatsapp = nomor_whatsapp
        self._commit()
        return wali

    def cari_by_id(self, id_wali: int) -> Guardian | None:
        return self._session.get(Guardian, id_wali)

    def daftar_by_siswa(self, id_siswa: int) -> list[Guardian]:
        stmt = select(Guardian).where(Guardian.id_siswa == id_siswa)
        return list(self._session.execute(stmt).scalars())

    def daftar_penerima_notifikasi(self, id_siswa: int) -> list[Guardian]:
        stmt = select(Guardian).where(
            Guardian.id_siswa == id_siswa,
            Guardian.menerima_notifikasi.is_(True),
        )
        return list(self._session.execute(stmt).scalars())
