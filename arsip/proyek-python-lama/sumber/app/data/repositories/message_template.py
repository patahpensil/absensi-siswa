from sqlalchemy import select

from app.data.models.notification import JenisNotifikasi, MessageTemplate
from app.data.repositories.base import BaseRepository


class MessageTemplateRepository(BaseRepository):
    """Akses data untuk message_templates.

    Hanya satu template boleh aktif per jenis pada satu waktu. tambah()
    dengan aktif=True menonaktifkan versi aktif sebelumnya untuk jenis
    yang sama - versi lama tetap ada di tabel (aktif=False), tidak
    dihapus, supaya masih terlihat template mana yang dipakai pada pesan
    lama.
    """

    def tambah(self, *, jenis: JenisNotifikasi, versi: int, konten: str, aktif: bool = True) -> MessageTemplate:
        if aktif:
            aktif_lama = self.cari_aktif(jenis)
            if aktif_lama is not None:
                aktif_lama.aktif = False

        template = MessageTemplate(jenis=jenis, versi=versi, konten=konten, aktif=aktif)
        self._session.add(template)
        self._commit()
        return template

    def cari_by_id(self, id_template: int) -> MessageTemplate | None:
        return self._session.get(MessageTemplate, id_template)

    def cari_aktif(self, jenis: JenisNotifikasi) -> MessageTemplate | None:
        stmt = select(MessageTemplate).where(
            MessageTemplate.jenis == jenis,
            MessageTemplate.aktif.is_(True),
        )
        return self._session.execute(stmt).scalar_one_or_none()

    def daftar_versi(self, jenis: JenisNotifikasi) -> list[MessageTemplate]:
        stmt = select(MessageTemplate).where(MessageTemplate.jenis == jenis).order_by(MessageTemplate.versi)
        return list(self._session.execute(stmt).scalars())
