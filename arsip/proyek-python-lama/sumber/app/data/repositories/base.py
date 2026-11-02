from sqlalchemy.orm import Session


class BaseRepository:
    """Induk seluruh repository.

    Menyimpan Session SQLAlchemy secara internal. Session tidak pernah
    dikembalikan atau diekspos lewat method publik manapun - pemanggil di
    luar lapisan data hanya menerima objek model biasa, None, atau list,
    tidak pernah Session maupun Query.
    """

    def __init__(self, session: Session) -> None:
        self._session = session

    def _commit(self) -> None:
        try:
            self._session.commit()
        except Exception:
            self._session.rollback()
            raise
