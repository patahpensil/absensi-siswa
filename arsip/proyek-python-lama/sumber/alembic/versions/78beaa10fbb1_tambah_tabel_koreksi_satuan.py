"""tambah tabel koreksi satuan

Revision ID: 78beaa10fbb1
Revises: ad26fb5488c8
Create Date: 2026-09-10 13:15:46.399722

Slice "Koreksi Satuan" (PRD §9.1, §10): koreksi status_final SATU baris
attendance siswa, dengan nilai lama/baru tersimpan permanen dan bisa
dibatalkan (undo, §20). Setara "tindakan_massal" tapi untuk satu baris -
tidak perlu tabel "baris" terpisah, lihat app/data/models/koreksi_satuan.py.
"""
from typing import Sequence, Union

from alembic import op
import sqlalchemy as sa


# revision identifiers, used by Alembic.
revision: str = '78beaa10fbb1'
down_revision: Union[str, None] = 'ad26fb5488c8'
branch_labels: Union[str, Sequence[str], None] = None
depends_on: Union[str, Sequence[str], None] = None


def upgrade() -> None:
    op.create_table(
        'koreksi_satuan',
        sa.Column('id', sa.Integer(), nullable=False),
        sa.Column('id_absensi', sa.Integer(), nullable=False),
        sa.Column('id_pengguna', sa.Integer(), nullable=False),
        sa.Column('waktu', sa.DateTime(), nullable=False),
        sa.Column('nilai_sebelum', sa.JSON(), nullable=False),
        sa.Column('nilai_sesudah', sa.JSON(), nullable=False),
        sa.Column('alasan', sa.Text(), nullable=False),
        sa.Column('dibatalkan', sa.Boolean(), nullable=False),
        sa.Column('waktu_dibatalkan', sa.DateTime(), nullable=True),
        sa.Column('dibatalkan_oleh', sa.Integer(), nullable=True),
        sa.ForeignKeyConstraint(['id_absensi'], ['attendance.id']),
        sa.ForeignKeyConstraint(['id_pengguna'], ['users.id']),
        sa.ForeignKeyConstraint(['dibatalkan_oleh'], ['users.id']),
        sa.PrimaryKeyConstraint('id'),
    )
    op.create_index('ix_koreksi_satuan_id_absensi', 'koreksi_satuan', ['id_absensi'])


def downgrade() -> None:
    op.drop_index('ix_koreksi_satuan_id_absensi', table_name='koreksi_satuan')
    op.drop_table('koreksi_satuan')
