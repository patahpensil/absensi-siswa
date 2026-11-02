"""Entrypoint server.

    python -m app.server.main sqlite:///absensi.db

Jalankan setelah `alembic upgrade head`. Server hanya boleh terbuka di
jaringan lokal sekolah - jangan buka port ini ke internet (SKILL.md
"Stack teknis"). --host 0.0.0.0 (bawaan) supaya PC lain di LAN bisa
mengakses; batasi lewat firewall, bukan lewat kode ini.

Menyalakan app/scheduler/ (cutoff ALPA, notifikasi, cutoff pulang) di
proses yang sama - server adalah satu-satunya proses berumur panjang yang
memegang basis data, jadi di sinilah pemicu waktu itu seharusnya hidup.
Kalau parameter waktu belum di-seed (scripts/seed_jadwal.py belum
dijalankan), server tetap menyala untuk login/scan/koreksi massal, hanya
scheduler-nya yang menunggu sampai di-restart setelah di-seed.
"""

import argparse

import uvicorn

from app.core.exceptions import KonfigurasiBelumLengkap
from app.data.db import buat_engine, buat_session_factory
from app.scheduler.scheduler import SchedulerAbsensi
from app.server.app import buat_app


def main() -> None:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("database_url", help="URL SQLAlchemy, mis. sqlite:///absensi.db")
    parser.add_argument("--host", default="0.0.0.0")
    parser.add_argument("--port", type=int, default=8420)
    parser.add_argument(
        "--lampiran-dir",
        default="lampiran",
        help="Direktori penyimpanan lampiran koreksi guru (PRD §6, default: ./lampiran)",
    )
    parser.add_argument(
        "--laporan-dir",
        default="laporan_arsip",
        help="Direktori penyimpanan arsip PDF laporan (PRD §9.3, default: ./laporan_arsip)",
    )
    parser.add_argument(
        "--tanpa-scheduler", action="store_true", help="Jangan jalankan cutoff/notifikasi terjadwal di proses ini"
    )
    args = parser.parse_args()

    engine = buat_engine(args.database_url, connect_args={"check_same_thread": False})
    session_factory = buat_session_factory(engine)
    app = buat_app(session_factory, lampiran_dir=args.lampiran_dir, laporan_dir=args.laporan_dir)

    scheduler = SchedulerAbsensi(session_factory=session_factory)
    # Dipakai app.server.routers.pengaturan untuk memanggil reload_jadwal()
    # setelah admin mengubah jam - lihat catatan di app/server/app.py.
    app.state.scheduler = scheduler
    if not args.tanpa_scheduler:
        try:
            scheduler.start()
        except KonfigurasiBelumLengkap as exc:
            print(
                f"Peringatan: scheduler tidak dijalankan - {exc}. "
                "Jalankan scripts/seed_jadwal.py lalu restart server."
            )

    try:
        uvicorn.run(app, host=args.host, port=args.port)
    finally:
        if scheduler.is_running:
            scheduler.stop()


if __name__ == "__main__":
    main()
