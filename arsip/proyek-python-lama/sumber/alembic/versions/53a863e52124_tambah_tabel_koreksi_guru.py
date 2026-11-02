"""tambah tabel koreksi guru

Revision ID: 53a863e52124
Revises: 78beaa10fbb1
Create Date: 2026-09-10 16:36:27.832702

Slice "Koreksi Guru" (PRD §6): input status manual DINAS_LUAR/IZIN/
SAKIT/CUTI untuk guru, dengan nilai lama/baru tersimpan permanen dan
bisa dibatalkan (undo) - setara "koreksi_satuan" (siswa) tapi tabel
terpisah karena kewenangan dan target status berbeda (lihat
app/data/models/koreksi_guru.py). ``lampiran`` menyimpan path relatif ke
foto surat tugas/keterangan (app.server.lampiran_storage), bukan isi
berkas.
"""
from typing import Sequence, Union

from alembic import op
import sqlalchemy as sa


# revision identifiers, used by Alembic.
revision: str = '53a863e52124'
down_revision: Union[str, None] = '78beaa10fbb1'
branch_labels: Union[str, Sequence[str], None] = None
depends_on: Union[str, Sequence[str], None] = None


def upgrade() -> None:
    op.create_table(
        'koreksi_guru',
        sa.Column('id', sa.Integer(), nullable=False),
        sa.Column('id_absensi', sa.Integer(), nullable=False),
        sa.Column('id_pengguna', sa.Integer(), nullable=False),
        sa.Column('waktu', sa.DateTime(), nullable=False),
        sa.Column('nilai_sebelum', sa.JSON(), nullable=False),
        sa.Column('nilai_sesudah', sa.JSON(), nullable=False),
        sa.Column('alasan', sa.Text(), nullable=False),
        sa.Column('lampiran', sa.String(length=500), nullable=True),
        sa.Column('dibatalkan', sa.Boolean(), nullable=False),
        sa.Column('waktu_dibatalkan', sa.DateTime(), nullable=True),
        sa.Column('dibatalkan_oleh', sa.Integer(), nullable=True),
        sa.ForeignKeyConstraint(['id_absensi'], ['attendance.id']),
        sa.ForeignKeyConstraint(['id_pengguna'], ['users.id']),
        sa.ForeignKeyConstraint(['dibatalkan_oleh'], ['users.id']),
        sa.PrimaryKeyConstraint('id'),
    )
    op.create_index('ix_koreksi_guru_id_absensi', 'koreksi_guru', ['id_absensi'])


def downgrade() -> None:
    op.drop_index('ix_koreksi_guru_id_absensi', table_name='koreksi_guru')
    op.drop_table('koreksi_guru')
