"""Seed kalender akademik semester ganjil 2026/2027 ke basis data sekolah.

Jalankan sekali saat menyiapkan basis data baru (setelah alembic upgrade
head dan scripts/buat_admin_awal.py):

    python scripts/seed_kalender.py sqlite:///absensi.db --id-pengguna 1

Sumber datanya app.core.kalender.kalender_ganjil_2026_2027() - entri yang
sama yang diuji tests/test_mesin_absensi.py. Aman dipanggil ulang kalau
belum ada entri kalender sama sekali; TIDAK aman dipanggil dua kali pada
basis data yang sudah di-seed - itu akan membuat entri duplikat (baris
tanggal yang sama muncul dua kali, keduanya aktif). Periksa dulu lewat
`KalenderRepository(session).daftar_aktif()` kalau ragu.
"""

import argparse
import sys
from pathlib import Path

sys.path.insert(0, str(Path(__file__).resolve().parent.parent))

from app.core.kalender import kalender_ganjil_2026_2027
from app.core.kalender_service import KalenderService
from app.core.status import Peran
from app.data.db import buat_engine, buat_session_factory
from app.data.repositories.audit_log import AuditLogRepository
from app.data.repositories.kalender import KalenderRepository


def main() -> None:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("database_url", help="URL SQLAlchemy, mis. sqlite:///absensi.db")
    parser.add_argument("--id-pengguna", type=int, required=True, help="id pengguna admin yang menjalankan seed")
    parser.add_argument("--alasan", default="Seed kalender akademik semester ganjil 2026/2027")
    args = parser.parse_args()

    engine = buat_engine(args.database_url)
    session = buat_session_factory(engine)()
    service = KalenderService(kalender_repo=KalenderRepository(session), audit_repo=AuditLogRepository(session))

    kalender = kalender_ganjil_2026_2027()
    for entri in kalender.entri:
        service.tambah(
            mulai=entri.mulai,
            selesai=entri.selesai,
            keterangan=entri.keterangan,
            jenis_siswa=entri.jenis_siswa,
            jenis_guru=entri.jenis_guru,
            tahun_ajaran=entri.tahun_ajaran,
            id_pengguna=args.id_pengguna,
            peran=Peran.ADMIN,
            alasan=args.alasan,
        )

    print(f"{len(kalender.entri)} entri kalender akademik berhasil di-seed.")
    print(
        "Catatan: 23-25 Desember 2026 SENGAJA tidak diisi - belum tercantum di "
        "agenda resmi sekolah (lihat SKILL.md 'Hal yang belum diputuskan')."
    )


if __name__ == "__main__":
    main()
