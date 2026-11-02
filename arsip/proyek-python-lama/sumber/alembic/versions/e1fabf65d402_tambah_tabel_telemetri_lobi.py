"""tambah tabel telemetri lobi

Revision ID: e1fabf65d402
Revises: cbdfe4144225
Create Date: 2026-09-11 00:00:00.000000

"""
from typing import Sequence, Union

from alembic import op
import sqlalchemy as sa


# revision identifiers, used by Alembic.
revision: str = 'e1fabf65d402'
down_revision: Union[str, None] = 'cbdfe4144225'
branch_labels: Union[str, Sequence[str], None] = None
depends_on: Union[str, Sequence[str], None] = None


def upgrade() -> None:
    # PRD §14.1: status operasional Dashboard - jumlah scan yang masih
    # mengantre di PC lobi. SATU baris per station_id, ditimpa tiap
    # laporan (bukan log historis) - lihat app.data.models.perangkat.TelemetriLobi.
    op.create_table(
        'telemetri_lobi',
        sa.Column('station_id', sa.String(length=100), nullable=False),
        sa.Column('jumlah_pending', sa.Integer(), nullable=False),
        sa.Column('waktu_lapor', sa.DateTime(), nullable=False),
        sa.Column('id_pengguna', sa.Integer(), nullable=False),
        sa.ForeignKeyConstraint(['id_pengguna'], ['users.id']),
        sa.PrimaryKeyConstraint('station_id'),
    )


def downgrade() -> None:
    op.drop_table('telemetri_lobi')
