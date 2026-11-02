from datetime import date

from sqlalchemy import select

from app.data.models.core import RfidCard, StatusKartu
from app.data.models.enums import JenisPemilik
from app.data.repositories.base import BaseRepository


class RfidCardRepository(BaseRepository):
    def cari_by_id(self, id_kartu: int) -> RfidCard | None:
        return self._session.get(RfidCard, id_kartu)

    def cari_by_uid(self, uid_kartu: str) -> RfidCard | None:
        """Query terpanas di seluruh aplikasi: dipanggil setiap kali kartu
        ditempel di pembaca, ratusan kali dalam sepuluh menit setiap pagi.

        Method khusus ini, bukan filter umum, supaya query yang dieksekusi
        selalu satu pencarian tunggal lewat indeks unik rfid_cards.uid_kartu
        dan tidak diam-diam berubah jadi table scan di kemudian hari.
        """
        stmt = select(RfidCard).where(RfidCard.uid_kartu == uid_kartu)
        return self._session.execute(stmt).scalar_one_or_none()

    def terbitkan(
        self,
        *,
        uid_kartu: str,
        id_pemilik: int,
        jenis_pemilik: JenisPemilik,
        tanggal_terbit: date,
    ) -> RfidCard:
        kartu = RfidCard(
            uid_kartu=uid_kartu,
            id_pemilik=id_pemilik,
            jenis_pemilik=jenis_pemilik,
            status=StatusKartu.AKTIF,
            tanggal_terbit=tanggal_terbit,
        )
        self._session.add(kartu)
        self._commit()
        return kartu

    def ubah_status(self, id_kartu: int, status_baru: StatusKartu) -> RfidCard | None:
        kartu = self._session.get(RfidCard, id_kartu)
        if kartu is None:
            return None
        kartu.status = status_baru
        self._commit()
        return kartu

    def daftar_by_pemilik(self, id_pemilik: int, jenis_pemilik: JenisPemilik) -> list[RfidCard]:
        stmt = select(RfidCard).where(
            RfidCard.id_pemilik == id_pemilik,
            RfidCard.jenis_pemilik == jenis_pemilik,
        )
        return list(self._session.execute(stmt).scalars())
