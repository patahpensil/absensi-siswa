"""
Layanan lokal server (SKILL.md "Stack teknis": FastAPI + uvicorn).

buat_app() menyuntikkan session_factory lewat app.state - setiap request
membuka satu Session lewat app.server.dependencies.get_session dan
menutupnya setelah selesai (pola yang sama dengan app/scheduler/).

``app.state.scheduler`` (opsional, diisi app/server/main.py SETELAH
buat_app() dipanggil - lihat main.py) adalah referensi ke SchedulerAbsensi
yang berjalan di proses yang sama, dipakai app.server.routers.pengaturan
untuk memanggil reload_jadwal() setelah admin mengubah jam cutoff/jadwal,
supaya scheduler yang sedang berjalan tidak perlu di-restart manual.
Tidak diisi di sini (bukan parameter buat_app()) supaya TestClient yang
sudah ada tidak perlu tahu apa-apa soal scheduler.

Layanan ini HANYA untuk jaringan lokal sekolah - tidak pernah menghadap
internet (SKILL.md). Tidak ada middleware HTTPS/CORS publik karena memang
tidak dimaksudkan diakses dari luar LAN; menjaga port ini di belakang
firewall sekolah adalah tanggung jawab operasional, bukan kode ini.
"""

from pathlib import Path

from fastapi import FastAPI
from sqlalchemy.orm import Session, sessionmaker

from app.server.routers import (
    auth,
    dashboard,
    guru,
    kalender,
    kartu,
    kelas,
    koreksi_guru,
    koreksi_massal,
    koreksi_satuan,
    laporan,
    notifikasi,
    pengaturan,
    pengguna,
    perangkat,
    riwayat_scan,
    scan,
    siswa,
)

#: Dipakai kalau buat_app() dipanggil tanpa lampiran_dir/laporan_dir
#: eksplisit (mis. TestClient yang tidak menyentuh endpoint terkait) -
#: direktori baru dibuat lazy saat berkas pertama disimpan (lihat
#: app.server.lampiran_storage/laporan_storage), bukan di sini.
_LAMPIRAN_DIR_BAWAAN = Path("lampiran")
_LAPORAN_DIR_BAWAAN = Path("laporan_arsip")


def buat_app(
    session_factory: sessionmaker[Session],
    *,
    lampiran_dir: Path | str | None = None,
    laporan_dir: Path | str | None = None,
) -> FastAPI:
    app = FastAPI(title="Absensi SMPN 1 Pancarijang - Server")
    app.state.session_factory = session_factory
    app.state.lampiran_dir = Path(lampiran_dir) if lampiran_dir is not None else _LAMPIRAN_DIR_BAWAAN
    app.state.laporan_dir = Path(laporan_dir) if laporan_dir is not None else _LAPORAN_DIR_BAWAAN

    @app.get("/health")
    def health() -> dict[str, str]:
        return {"status": "ok"}

    app.include_router(auth.router)
    app.include_router(scan.router)
    app.include_router(koreksi_massal.router)
    app.include_router(siswa.router)
    app.include_router(guru.router)
    app.include_router(kelas.router)
    app.include_router(kartu.router)
    app.include_router(kalender.router)
    app.include_router(pengaturan.router)
    app.include_router(pengguna.router)
    app.include_router(riwayat_scan.router)
    app.include_router(koreksi_satuan.router)
    app.include_router(koreksi_guru.router)
    app.include_router(notifikasi.router)
    app.include_router(laporan.router)
    app.include_router(perangkat.router)
    app.include_router(dashboard.router)
    return app
