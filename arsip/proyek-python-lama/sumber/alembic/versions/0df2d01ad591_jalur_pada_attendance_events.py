"""jalur pada attendance_events

Revision ID: 0df2d01ad591
Revises: d78c3682db53
Create Date: 2026-09-13 00:00:00.000000

"""
from typing import Sequence, Union

from alembic import op
import sqlalchemy as sa


# revision identifiers, used by Alembic.
revision: str = '0df2d01ad591'
down_revision: Union[str, None] = 'd78c3682db53'
branch_labels: Union[str, Sequence[str], None] = None
depends_on: Union[str, Sequence[str], None] = None


def upgrade() -> None:
    # PRD §17: peristiwa_scan wajib menyimpan "UID mentah, UID baku, jalur,
    # waktu, hasil" - attendance_events tidak pernah punya kolom jalur.
    # Nullable karena baris lama tidak punya nilai jujur untuk diisi (server
    # tidak pernah menerima jalur sebelum slice ini) - dibiarkan None, tidak
    # dikarang (pola sama migrasi f6b69a2af59c, uid_mentah). Baris baru
    # selalu terisi lewat ScanRequest.jalur (wajib di kontrak HTTP sejak
    # slice ini).
    op.add_column(
        'attendance_events',
        sa.Column('jalur', sa.Enum('PCSC', 'SERIAL', 'HID', 'SIMULATOR', name='jaluralat', native_enum=False), nullable=True),
    )


def downgrade() -> None:
    op.drop_column('attendance_events', 'jalur')
