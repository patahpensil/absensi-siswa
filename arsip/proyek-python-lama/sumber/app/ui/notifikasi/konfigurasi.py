"""Konfigurasi layar Notifikasi: alamat server dan kredensial akun yang
login. Endpoint /notifikasi/* dibatasi kepala sekolah/admin - akun lain
tetap bisa login di sini, layar menampilkan pesan yang jelas begitu
server menolak (sama seperti app.ui.pengaturan)."""

from __future__ import annotations

import os
from dataclasses import dataclass

_VAR_SERVER_URL = "ABSENSI_SERVER_URL"
_VAR_USERNAME = "ABSENSI_NOTIFIKASI_USERNAME"
_VAR_PASSWORD = "ABSENSI_NOTIFIKASI_PASSWORD"


class KonfigurasiTidakLengkap(Exception):
    pass


@dataclass(frozen=True)
class KonfigurasiNotifikasi:
    server_url: str
    username: str
    password: str


def muat_dari_lingkungan() -> KonfigurasiNotifikasi:
    hilang = [nama for nama in (_VAR_SERVER_URL, _VAR_USERNAME, _VAR_PASSWORD) if not os.environ.get(nama)]
    if hilang:
        raise KonfigurasiTidakLengkap(f"Variabel lingkungan berikut belum diatur: {', '.join(hilang)}")
    return KonfigurasiNotifikasi(
        server_url=os.environ[_VAR_SERVER_URL],
        username=os.environ[_VAR_USERNAME],
        password=os.environ[_VAR_PASSWORD],
    )
