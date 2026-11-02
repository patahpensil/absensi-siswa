"""Konfigurasi layar Riwayat Scan: alamat server dan kredensial akun
yang login. Sama seperti klien layar lainnya - siapa pun dari keempat
peran bisa login di sini, lingkupnya dibatasi server.
"""

from __future__ import annotations

import os
from dataclasses import dataclass

_VAR_SERVER_URL = "ABSENSI_SERVER_URL"
_VAR_USERNAME = "ABSENSI_RIWAYAT_SCAN_USERNAME"
_VAR_PASSWORD = "ABSENSI_RIWAYAT_SCAN_PASSWORD"


class KonfigurasiTidakLengkap(Exception):
    pass


@dataclass(frozen=True)
class KonfigurasiRiwayatScan:
    server_url: str
    username: str
    password: str


def muat_dari_lingkungan() -> KonfigurasiRiwayatScan:
    hilang = [nama for nama in (_VAR_SERVER_URL, _VAR_USERNAME, _VAR_PASSWORD) if not os.environ.get(nama)]
    if hilang:
        raise KonfigurasiTidakLengkap(f"Variabel lingkungan berikut belum diatur: {', '.join(hilang)}")
    return KonfigurasiRiwayatScan(
        server_url=os.environ[_VAR_SERVER_URL],
        username=os.environ[_VAR_USERNAME],
        password=os.environ[_VAR_PASSWORD],
    )
