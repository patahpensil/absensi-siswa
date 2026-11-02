from logging.config import fileConfig

from sqlalchemy import engine_from_config
from sqlalchemy import pool

from alembic import context

# this is the Alembic Config object, which provides
# access to the values within the .ini file in use.
config = context.config

# Interpret the config file for Python logging.
# This line sets up loggers basically.
if config.config_file_name is not None:
    fileConfig(config.config_file_name)

import app.data.db  # noqa: E402  (registers PRAGMA listener sebelum Engine dibuat)
from app.data.models import Base

target_metadata = Base.metadata

# other values from the config, defined by the needs of env.py,
# can be acquired:
# my_important_option = config.get_main_option("my_important_option")
# ... etc.


def _resolve_url() -> str:
    """URL basis data efektif untuk migrasi ini.

    ``-x db_url=...`` (mis. ``alembic -x db_url=sqlite:///salinan.db
    upgrade head``) MENGALAHKAN ``sqlalchemy.url`` di alembic.ini kalau
    diberikan - dipakai untuk menguji migrasi terhadap salinan/basis data
    sementara tanpa menyentuh basis data pengembangan sungguhan
    (absensi.db). Sebelum perbaikan ini, ``-x`` diterima CLI tapi tidak
    pernah dibaca di sini, jadi tetap diam-diam memakai sqlalchemy.url -
    itulah sebab dry-run migrasi izin_darurat_alat sempat mendarat di
    absensi.db asli (lihat README, catatan verifikasi keamanannya)."""
    x_args = context.get_x_argument(as_dictionary=True)
    return x_args.get("db_url") or config.get_main_option("sqlalchemy.url")


def run_migrations_offline() -> None:
    """Run migrations in 'offline' mode.

    This configures the context with just a URL
    and not an Engine, though an Engine is acceptable
    here as well.  By skipping the Engine creation
    we don't even need a DBAPI to be available.

    Calls to context.execute() here emit the given string to the
    script output.

    """
    url = _resolve_url()
    context.configure(
        url=url,
        target_metadata=target_metadata,
        literal_binds=True,
        dialect_opts={"paramstyle": "named"},
    )

    with context.begin_transaction():
        context.run_migrations()


def run_migrations_online() -> None:
    """Run migrations in 'online' mode.

    In this scenario we need to create an Engine
    and associate a connection with the context.

    """
    configuration = config.get_section(config.config_ini_section, {})
    configuration["sqlalchemy.url"] = _resolve_url()
    connectable = engine_from_config(
        configuration,
        prefix="sqlalchemy.",
        poolclass=pool.NullPool,
    )

    with connectable.connect() as connection:
        context.configure(
            connection=connection, target_metadata=target_metadata
        )

        with context.begin_transaction():
            context.run_migrations()


if context.is_offline_mode():
    run_migrations_offline()
else:
    run_migrations_online()
