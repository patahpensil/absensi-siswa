"""Konfigurasi layar Dashboard: alamat server dan kredensial akun yang
login. Sama seperti app.ui.pengaturan.konfigurasi/app.ui.perangkat.konfigurasi.
"""

from __future__ import annotations

import os
from dataclasses import dataclass

_VAR_SERVER_URL = "ABSENSI_SERVER_URL"
_VAR_USERNAME = "ABSENSI_DASHBOARD_USERNAME"
_VAR_PASSWORD = "ABSENSI_DASHBOARD_PASSWORD"


class KonfigurasiTidakLengkap(Exception):
    pass


@dataclass(frozen=True)
class KonfigurasiDashboard:
    server_url: str
    username: str
    password: str


def muat_dari_lingkungan() -> KonfigurasiDashboard:
    hilang = [nama for nama in (_VAR_SERVER_URL, _VAR_USERNAME, _VAR_PASSWORD) if not os.environ.get(nama)]
    if hilang:
        raise KonfigurasiTidakLengkap(f"Variabel lingkungan berikut belum diatur: {', '.join(hilang)}")
    return KonfigurasiDashboard(
        server_url=os.environ[_VAR_SERVER_URL],
        username=os.environ[_VAR_USERNAME],
        password=os.environ[_VAR_PASSWORD],
    )
