import pytest
from sqlalchemy.pool import StaticPool

import app.data.db as db
from app.data.models import Base


@pytest.fixture
def session_factory():
    """Session factory SQLite in-memory dengan seluruh tabel dari
    Base.metadata.

    StaticPool supaya satu koneksi in-memory dipakai bersama sepanjang
    hidup engine - tanpa itu setiap checkout koneksi baru akan melihat
    basis data kosong yang berbeda. check_same_thread=False supaya sesi
    yang dibuat dari thread lain (mis. thread background scheduler) tetap
    bisa memakai koneksi yang sama.
    """
    engine = db.buat_engine(
        "sqlite://",
        connect_args={"check_same_thread": False},
        poolclass=StaticPool,
    )
    Base.metadata.create_all(engine)
    factory = db.buat_session_factory(engine)
    yield factory
    engine.dispose()


@pytest.fixture
def session(session_factory):
    sesi = session_factory()
    try:
        yield sesi
    finally:
        sesi.close()
