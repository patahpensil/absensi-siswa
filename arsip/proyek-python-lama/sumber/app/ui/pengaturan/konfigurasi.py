"""Konfigurasi layar Pengaturan: alamat server dan kredensial akun yang
login. Sama seperti app.ui.kalender.konfigurasi/app.ui.data_master.konfigurasi.
"""

from __future__ import annotations

import os
from dataclasses import dataclass

_VAR_SERVER_URL = "ABSENSI_SERVER_URL"
_VAR_USERNAME = "ABSENSI_PENGATURAN_USERNAME"
_VAR_PASSWORD = "ABSENSI_PENGATURAN_PASSWORD"


class KonfigurasiTidakLengkap(Exception):
    pass


@dataclass(frozen=True)
class KonfigurasiPengaturan:
    server_url: str
    username: str
    password: str


def muat_dari_lingkungan() -> KonfigurasiPengaturan:
    hilang = [nama for nama in (_VAR_SERVER_URL, _VAR_USERNAME, _VAR_PASSWORD) if not os.environ.get(nama)]
    if hilang:
        raise KonfigurasiTidakLengkap(f"Variabel lingkungan berikut belum diatur: {', '.join(hilang)}")
    return KonfigurasiPengaturan(
        server_url=os.environ[_VAR_SERVER_URL],
        username=os.environ[_VAR_USERNAME],
        password=os.environ[_VAR_PASSWORD],
    )
