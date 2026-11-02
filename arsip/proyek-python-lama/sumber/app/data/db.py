from sqlalchemy import Engine, create_engine, event
from sqlalchemy.orm import Session, sessionmaker


@event.listens_for(Engine, "connect")
def _terapkan_pragma_sqlite(dbapi_connection, connection_record) -> None:
    """Aktifkan foreign key enforcement dan mode WAL di setiap koneksi SQLite.

    SQLite mematikan penegakan foreign key secara bawaan. Tanpa mode WAL,
    pembacaan dashboard bisa mengunci penulisan absensi.
    """
    if type(dbapi_connection).__module__ != "sqlite3":
        return
    cursor = dbapi_connection.cursor()
    cursor.execute("PRAGMA foreign_keys=ON")
    cursor.execute("PRAGMA journal_mode=WAL")
    cursor.close()


def buat_engine(url: str, **kwargs) -> Engine:
    return create_engine(url, **kwargs)


def buat_session_factory(engine: Engine) -> sessionmaker[Session]:
    """Session factory bersama untuk seluruh repository.

    expire_on_commit=False supaya objek yang dikembalikan repository tetap
    bisa dibaca pemanggil setelah commit, tanpa pemanggil perlu memegang
    Session itu sendiri.
    """
    return sessionmaker(bind=engine, expire_on_commit=False)
