"""tambah tabel izin darurat alat

Revision ID: cbdfe4144225
Revises: 0df2d01ad591
Create Date: 2026-09-11 00:00:00.000000

"""
from typing import Sequence, Union

from alembic import op
import sqlalchemy as sa


# revision identifiers, used by Alembic.
revision: str = 'cbdfe4144225'
down_revision: Union[str, None] = '0df2d01ad591'
branch_labels: Union[str, Sequence[str], None] = None
depends_on: Union[str, Sequence[str], None] = None


def upgrade() -> None:
    # PRD §12.4: izin darurat alat - satu baris per pemberian, station_id
    # bebas (cocok dengan app.rfid.base.PembacaKartuBase.station_id, tidak
    # ada registry perangkat terpusat).
    op.create_table(
        'izin_darurat_alat',
        sa.Column('id', sa.Integer(), nullable=False),
        sa.Column('station_id', sa.String(length=100), nullable=False),
        sa.Column('id_pengguna_pemberi', sa.Integer(), nullable=False),
        sa.Column('waktu_mulai', sa.DateTime(), nullable=False),
        sa.Column('waktu_berakhir', sa.DateTime(), nullable=False),
        sa.Column('alasan', sa.Text(), nullable=False),
        sa.Column('dicabut', sa.Boolean(), nullable=False),
        sa.Column('id_pengguna_pencabut', sa.Integer(), nullable=True),
        sa.Column('waktu_dicabut', sa.DateTime(), nullable=True),
        sa.ForeignKeyConstraint(['id_pengguna_pemberi'], ['users.id']),
        sa.ForeignKeyConstraint(['id_pengguna_pencabut'], ['users.id']),
        sa.PrimaryKeyConstraint('id'),
    )
    op.create_index(
        op.f('ix_izin_darurat_alat_station_id'), 'izin_darurat_alat', ['station_id'], unique=False
    )


def downgrade() -> None:
    op.drop_index(op.f('ix_izin_darurat_alat_station_id'), table_name='izin_darurat_alat')
    op.drop_table('izin_darurat_alat')
