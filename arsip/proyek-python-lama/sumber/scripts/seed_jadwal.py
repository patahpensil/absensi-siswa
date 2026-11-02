"""Seed jadwal siswa dan parameter waktu ke basis data sekolah.

Jalankan sekali saat menyiapkan basis data baru (setelah alembic upgrade
head):

    python scripts/seed_jadwal.py sqlite:///absensi.db --id-pengguna 1

Aman dipanggil ulang untuk parameter waktu di settings (menimpa nilai
lama, tercatat di audit log). TIDAK aman dipanggil ulang untuk jadwal
siswa dengan tanggal mulai berlaku yang sama - itu akan membuat baris
duplikat, bukan menimpa.
"""

import argparse
import sys
from datetime import date
from pathlib import Path

sys.path.insert(0, str(Path(__file__).resolve().parent.parent))

from app.config.pengaturan import Pengaturan
from app.config.seed import seed_jadwal_siswa, seed_pengaturan
from app.data.db import buat_engine, buat_session_factory
from app.data.repositories.schedule import ScheduleRepository
from app.data.repositories.setting import SettingRepository


def main() -> None:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("database_url", help="URL SQLAlchemy, mis. sqlite:///absensi.db")
    parser.add_argument("--id-pengguna", type=int, required=True, help="id pengguna admin yang menjalankan seed")
    parser.add_argument("--alasan", default="Seed jadwal dan parameter waktu awal")
    parser.add_argument("--berlaku-mulai", default=None, help="YYYY-MM-DD, bawaan hari ini")
    args = parser.parse_args()

    berlaku_mulai = date.fromisoformat(args.berlaku_mulai) if args.berlaku_mulai else date.today()

    engine = buat_engine(args.database_url)
    session = buat_session_factory(engine)()

    seed_jadwal_siswa(ScheduleRepository(session), berlaku_mulai=berlaku_mulai)
    seed_pengaturan(Pengaturan(SettingRepository(session)), id_pengguna=args.id_pengguna, alasan=args.alasan)

    print(f"Jadwal siswa dan parameter waktu berhasil di-seed, berlaku mulai {berlaku_mulai}.")


if __name__ == "__main__":
    main()
