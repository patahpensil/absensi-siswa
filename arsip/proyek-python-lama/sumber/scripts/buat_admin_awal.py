"""Buat akun Admin pertama di basis data baru.

Jalankan sekali saat menyiapkan basis data baru (setelah alembic upgrade
head), SEBELUM scripts/seed_jadwal.py - seed itu butuh --id-pengguna yang
sudah ada:

    python scripts/buat_admin_awal.py sqlite:///absensi.db --username admin --password <sandi-awal>

Tanpa skrip ini tidak ada jalan masuk sama sekali ke app/server/ - login
butuh User yang sudah ada, dan tidak ada endpoint untuk membuat User
pertama (membuat pengguna lain memang wewenang Admin/Kepsek yang sudah
login, lihat SKILL.md "Peran dan hak akses"). Akun yang dibuat di sini
wajib_ganti_password=True secara bawaan - pengguna diminta ganti sandi
saat login pertama.
"""

import argparse
import sys
from pathlib import Path

sys.path.insert(0, str(Path(__file__).resolve().parent.parent))

from app.data.db import buat_engine, buat_session_factory
from app.data.models.system import PeranPengguna
from app.data.repositories.user import UserRepository
from app.server.security import hash_password


def main() -> None:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("database_url", help="URL SQLAlchemy, mis. sqlite:///absensi.db")
    parser.add_argument("--username", required=True)
    parser.add_argument("--password", required=True, help="Sandi awal - wajib diganti saat login pertama")
    parser.add_argument(
        "--peran",
        default=PeranPengguna.ADMIN.value,
        choices=[p.value for p in PeranPengguna],
    )
    args = parser.parse_args()

    engine = buat_engine(args.database_url)
    session = buat_session_factory(engine)()
    repo = UserRepository(session)

    if repo.cari_by_username(args.username) is not None:
        print(f"Username '{args.username}' sudah ada - tidak dibuat ulang.")
        return

    pengguna = repo.tambah(
        username=args.username,
        password_hash=hash_password(args.password),
        peran=PeranPengguna(args.peran),
    )
    print(f"Akun '{pengguna.username}' ({pengguna.peran.value}) berhasil dibuat dengan id={pengguna.id}.")


if __name__ == "__main__":
    main()
