"""id_template pada notification_queue

Revision ID: 2d4513dcfa37
Revises: 64cc4b323338
Create Date: 2026-09-09 02:33:35.832934

"""
from typing import Sequence, Union

from alembic import op
import sqlalchemy as sa


# revision identifiers, used by Alembic.
revision: str = '2d4513dcfa37'
down_revision: Union[str, None] = '64cc4b323338'
branch_labels: Union[str, Sequence[str], None] = None
depends_on: Union[str, Sequence[str], None] = None


def upgrade() -> None:
    # SQLite tidak mendukung ALTER TABLE ADD CONSTRAINT langsung - perlu
    # batch mode (salin-dan-pindah tabel), disesuaikan manual dari hasil
    # autogenerate.
    with op.batch_alter_table('notification_queue', schema=None) as batch_op:
        batch_op.add_column(sa.Column('id_template', sa.Integer(), nullable=True))
        batch_op.create_foreign_key(
            'fk_notification_queue_id_template_message_templates',
            'message_templates',
            ['id_template'],
            ['id'],
        )


def downgrade() -> None:
    with op.batch_alter_table('notification_queue', schema=None) as batch_op:
        batch_op.drop_constraint('fk_notification_queue_id_template_message_templates', type_='foreignkey')
        batch_op.drop_column('id_template')
