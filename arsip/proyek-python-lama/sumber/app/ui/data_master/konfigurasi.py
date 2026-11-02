"""Konfigurasi layar Data Master: alamat server dan kredensial akun
admin/kepala sekolah yang login. Dibaca dari variabel lingkungan, sama
seperti app.ui.lobi.konfigurasi - kredensial tidak pernah masuk kode
maupun sistem kontrol versi.
"""

from __future__ import annotations

import os
from dataclasses import dataclass

_VAR_SERVER_URL = "ABSENSI_SERVER_URL"
_VAR_USERNAME = "ABSENSI_DATAMASTER_USERNAME"
_VAR_PASSWORD = "ABSENSI_DATAMASTER_PASSWORD"


class KonfigurasiTidakLengkap(Exception):
    pass


@dataclass(frozen=True)
class KonfigurasiDataMaster:
    server_url: str
    username: str
    password: str


def muat_dari_lingkungan() -> KonfigurasiDataMaster:
    hilang = [nama for nama in (_VAR_SERVER_URL, _VAR_USERNAME, _VAR_PASSWORD) if not os.environ.get(nama)]
    if hilang:
        raise KonfigurasiTidakLengkap(f"Variabel lingkungan berikut belum diatur: {', '.join(hilang)}")
    return KonfigurasiDataMaster(
        server_url=os.environ[_VAR_SERVER_URL],
        username=os.environ[_VAR_USERNAME],
        password=os.environ[_VAR_PASSWORD],
    )
