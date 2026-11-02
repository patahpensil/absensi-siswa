"""Konfigurasi layar Laporan: alamat server dan kredensial akun yang
login. Endpoint /laporan/* dibatasi kepala sekolah/admin/wali kelas
(operator ditolak) - lihat app.server.routers.laporan."""

from __future__ import annotations

import os
from dataclasses import dataclass

_VAR_SERVER_URL = "ABSENSI_SERVER_URL"
_VAR_USERNAME = "ABSENSI_LAPORAN_USERNAME"
_VAR_PASSWORD = "ABSENSI_LAPORAN_PASSWORD"


class KonfigurasiTidakLengkap(Exception):
    pass


@dataclass(frozen=True)
class KonfigurasiLaporan:
    server_url: str
    username: str
    password: str


def muat_dari_lingkungan() -> KonfigurasiLaporan:
    hilang = [nama for nama in (_VAR_SERVER_URL, _VAR_USERNAME, _VAR_PASSWORD) if not os.environ.get(nama)]
    if hilang:
        raise KonfigurasiTidakLengkap(f"Variabel lingkungan berikut belum diatur: {', '.join(hilang)}")
    return KonfigurasiLaporan(
        server_url=os.environ[_VAR_SERVER_URL],
        username=os.environ[_VAR_USERNAME],
        password=os.environ[_VAR_PASSWORD],
    )
