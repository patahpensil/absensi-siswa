"""pecah JenisNotifikasi jadi empat template terpisah (PRD 13.1)

Revision ID: 1cdfb1a07236
Revises: 53a863e52124
Create Date: 2026-09-11 00:00:00.000000

"""
from typing import Sequence, Union

from alembic import op
import sqlalchemy as sa


# revision identifiers, used by Alembic.
revision: str = '1cdfb1a07236'
down_revision: Union[str, None] = '53a863e52124'
branch_labels: Union[str, Sequence[str], None] = None
depends_on: Union[str, Sequence[str], None] = None


def upgrade() -> None:
    # MASUK/PULANG/ALPA generik -> TERLAMBAT/ALPA_PAGI/ALPA_PULANG/REKAP
    # (PRD SS13.1, empat template terpisah). Tidak ada baris data lama
    # untuk dipetakan - notification_queue dan message_templates masih
    # kosong (belum ada pemanggil produksi untuk antrekan(), lihat
    # README "Utang aktif") sehingga tidak ada UPDATE data yang diperlukan
    # di sini, murni perubahan bentuk kolom. SQLite tidak punya ALTER
    # COLUMN TYPE - lewat batch_alter_table (pola sama dengan migrasi
    # 41cd37afef4c, peran KEPALA_SEKOLAH).
    with op.batch_alter_table('message_templates') as batch_op:
        batch_op.alter_column(
            'jenis',
            existing_type=sa.VARCHAR(length=6),
            type_=sa.Enum('TERLAMBAT', 'ALPA_PAGI', 'ALPA_PULANG', 'REKAP', name='jenisnotifikasi', native_enum=False),
            existing_nullable=False,
        )
    with op.batch_alter_table('notification_queue') as batch_op:
        batch_op.alter_column(
            'jenis',
            existing_type=sa.VARCHAR(length=6),
            type_=sa.Enum('TERLAMBAT', 'ALPA_PAGI', 'ALPA_PULANG', 'REKAP', name='jenisnotifikasi', native_enum=False),
            existing_nullable=False,
        )


def downgrade() -> None:
    with op.batch_alter_table('notification_queue') as batch_op:
        batch_op.alter_column(
            'jenis',
            existing_type=sa.Enum('TERLAMBAT', 'ALPA_PAGI', 'ALPA_PULANG', 'REKAP', name='jenisnotifikasi', native_enum=False),
            type_=sa.VARCHAR(length=6),
            existing_nullable=False,
        )
    with op.batch_alter_table('message_templates') as batch_op:
        batch_op.alter_column(
            'jenis',
            existing_type=sa.Enum('TERLAMBAT', 'ALPA_PAGI', 'ALPA_PULANG', 'REKAP', name='jenisnotifikasi', native_enum=False),
            type_=sa.VARCHAR(length=6),
            existing_nullable=False,
        )
