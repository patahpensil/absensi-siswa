"""tambah tabel laporan_arsip (PRD 14.2, 9.3)

Revision ID: d78c3682db53
Revises: 1cdfb1a07236
Create Date: 2026-09-12 00:00:00.000000

"""
from typing import Sequence, Union

from alembic import op
import sqlalchemy as sa


# revision identifiers, used by Alembic.
revision: str = 'd78c3682db53'
down_revision: Union[str, None] = '1cdfb1a07236'
branch_labels: Union[str, Sequence[str], None] = None
depends_on: Union[str, Sequence[str], None] = None


def upgrade() -> None:
    op.create_table(
        'laporan_arsip',
        sa.Column('id', sa.Integer(), nullable=False),
        sa.Column('id_pengguna', sa.Integer(), nullable=False),
        sa.Column('label', sa.String(length=120), nullable=False),
        sa.Column('tanggal_mulai', sa.Date(), nullable=False),
        sa.Column('tanggal_selesai', sa.Date(), nullable=False),
        sa.Column('id_kelas', sa.Integer(), nullable=True),
        sa.Column('id_siswa', sa.Integer(), nullable=True),
        sa.Column('penyaring', sa.JSON(), nullable=False),
        sa.Column('path_pdf', sa.String(length=255), nullable=False),
        sa.Column('waktu_cetak', sa.DateTime(), nullable=False),
        sa.ForeignKeyConstraint(['id_kelas'], ['classes.id'], ),
        sa.ForeignKeyConstraint(['id_pengguna'], ['users.id'], ),
        sa.ForeignKeyConstraint(['id_siswa'], ['students.id'], ),
        sa.PrimaryKeyConstraint('id'),
    )
    op.create_index('ix_laporan_arsip_waktu_cetak', 'laporan_arsip', ['waktu_cetak'], unique=False)


def downgrade() -> None:
    op.drop_index('ix_laporan_arsip_waktu_cetak', table_name='laporan_arsip')
    op.drop_table('laporan_arsip')
