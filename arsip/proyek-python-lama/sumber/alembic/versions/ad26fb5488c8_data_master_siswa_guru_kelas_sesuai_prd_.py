"""data master siswa guru kelas sesuai PRD 11

Revision ID: ad26fb5488c8
Revises: f6b69a2af59c
Create Date: 2026-09-10 08:02:44.006366

Slice "Data Master": endpoint + layar input siswa/guru/kelas/kartu (PRD
§11). Belum ada layar input data sebelum slice ini (README "Yang belum
dikerjakan"), jadi diasumsikan tabel students/teachers/classes/guardians
belum berisi data sungguhan dari pengguna - server_default sementara di
bawah hanya jaring pengaman skema, bukan nilai yang pernah dimaksudkan
dipakai.

Perubahan:
- students: hapus kolom `nis` (peninggalan draf data-model.md pra-PRD v2.0,
  tidak dipakai modul lain manapun - dikonfirmasi lewat pencarian kode).
  `nisn` jadi wajib+kunci utama sesuai PRD §11.1 (PRD tidak menyebut "NIS"
  sama sekali). Tambah nipd, tempat_lahir, tanggal_lahir, nik, alamat.
  `id_kelas` jadi nullable - siswa tanpa kelas masuk "Belum ditempatkan"
  (§11.3), absensi tetap normal.
- teachers: tambah `pangkat_jabatan` (PRD §11.2). `status_kepegawaian`
  SENGAJA TIDAK DISENTUH - topik lain (lihat TODO di model).
- classes: tambah `wali_kelas_id` (FK teachers.id, identitas guru untuk
  data master - terpisah dari User.id_kelas_diampu yang menegakkan hak
  akses login) dan `tahun_ajaran` (§11.3).
- guardians: `nomor_whatsapp` jadi nullable - PRD §11.1 hanya mewajibkan
  nama salah satu orang tua, nomornya boleh kosong.
"""
from typing import Sequence, Union

from alembic import op
import sqlalchemy as sa


# revision identifiers, used by Alembic.
revision: str = 'ad26fb5488c8'
down_revision: Union[str, None] = 'f6b69a2af59c'
branch_labels: Union[str, Sequence[str], None] = None
depends_on: Union[str, Sequence[str], None] = None


def upgrade() -> None:
    with op.batch_alter_table('teachers', schema=None) as batch_op:
        batch_op.add_column(sa.Column('pangkat_jabatan', sa.String(length=80), nullable=True))

    with op.batch_alter_table('classes', schema=None) as batch_op:
        batch_op.add_column(sa.Column('wali_kelas_id', sa.Integer(), nullable=True))
        batch_op.add_column(
            sa.Column('tahun_ajaran', sa.String(length=20), nullable=False, server_default='2026/2027')
        )
        batch_op.create_foreign_key('fk_classes_wali_kelas_id_teachers', 'teachers', ['wali_kelas_id'], ['id'])
    with op.batch_alter_table('classes', schema=None) as batch_op:
        batch_op.alter_column('tahun_ajaran', server_default=None)

    with op.batch_alter_table('guardians', schema=None) as batch_op:
        batch_op.alter_column('nomor_whatsapp', existing_type=sa.String(length=20), nullable=True)

    with op.batch_alter_table('students', schema=None) as batch_op:
        batch_op.add_column(sa.Column('nipd', sa.String(length=20), nullable=True))
        batch_op.add_column(
            sa.Column('tempat_lahir', sa.String(length=80), nullable=False, server_default='')
        )
        batch_op.add_column(
            sa.Column('tanggal_lahir', sa.Date(), nullable=False, server_default='2000-01-01')
        )
        batch_op.add_column(sa.Column('nik', sa.String(length=16), nullable=True))
        batch_op.add_column(sa.Column('alamat', sa.String(length=255), nullable=False, server_default=''))
        batch_op.alter_column('nisn', existing_type=sa.String(length=20), type_=sa.String(length=10), nullable=False)
        batch_op.alter_column('id_kelas', existing_type=sa.Integer(), nullable=True)
        batch_op.create_unique_constraint('uq_students_nik', ['nik'])
        batch_op.drop_column('nis')
    with op.batch_alter_table('students', schema=None) as batch_op:
        batch_op.alter_column('tempat_lahir', server_default=None)
        batch_op.alter_column('tanggal_lahir', server_default=None)
        batch_op.alter_column('alamat', server_default=None)


def downgrade() -> None:
    with op.batch_alter_table('students', schema=None) as batch_op:
        batch_op.add_column(sa.Column('nis', sa.String(length=20), nullable=True))
        batch_op.alter_column('id_kelas', existing_type=sa.Integer(), nullable=False)
        batch_op.alter_column('nisn', existing_type=sa.String(length=10), type_=sa.String(length=20), nullable=True)
        batch_op.drop_constraint('uq_students_nik', type_='unique')
        batch_op.drop_column('alamat')
        batch_op.drop_column('nik')
        batch_op.drop_column('tanggal_lahir')
        batch_op.drop_column('tempat_lahir')
        batch_op.drop_column('nipd')

    with op.batch_alter_table('guardians', schema=None) as batch_op:
        batch_op.alter_column('nomor_whatsapp', existing_type=sa.String(length=20), nullable=False)

    with op.batch_alter_table('classes', schema=None) as batch_op:
        batch_op.drop_constraint('fk_classes_wali_kelas_id_teachers', type_='foreignkey')
        batch_op.drop_column('tahun_ajaran')
        batch_op.drop_column('wali_kelas_id')

    with op.batch_alter_table('teachers', schema=None) as batch_op:
        batch_op.drop_column('pangkat_jabatan')
