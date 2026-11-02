"""
Konfigurasi PC lobi: kredensial akun Operator kiosk, alamat server, dan
parameter antrean lokal/forwarder. Dibaca dari variabel lingkungan -
kredensial TIDAK PERNAH masuk kode maupun sistem kontrol versi (lihat
.gitignore: ".env"/".env.*" sudah dikecualikan sebagai "Rahasia dan
konfigurasi lokal").

Provisioning (di luar cakupan slice ini): admin membuat akun Operator
khusus stasiun lobi ini (peran OPERATOR) lewat jalur yang sudah ada
(app.server.auth_service/UserRepository) dan sudah mengganti sandi awalnya
(wajib_ganti_password=False) SEBELUM kredensial ditaruh di konfigurasi PC
lobi.
"""

from __future__ import annotations

import os
from dataclasses import dataclass

_VAR_SERVER_URL = "ABSENSI_SERVER_URL"
_VAR_USERNAME = "ABSENSI_LOBI_USERNAME"
_VAR_PASSWORD = "ABSENSI_LOBI_PASSWORD"
_VAR_STATION_ID = "ABSENSI_LOBI_STATION_ID"
_VAR_PATH_ANTREAN = "ABSENSI_LOBI_ANTREAN_PATH"
_VAR_TIMEOUT_DETIK = "ABSENSI_LOBI_TIMEOUT_DETIK"
_VAR_FORWARDER_INTERVAL_DETIK = "ABSENSI_LOBI_FORWARDER_INTERVAL_DETIK"
_VAR_FORWARDER_INTERVAL_MAKS_DETIK = "ABSENSI_LOBI_FORWARDER_INTERVAL_MAKS_DETIK"

_STATION_ID_BAWAAN = "LOBI-1"
_PATH_ANTREAN_BAWAAN = "antrean_lobi.db"
#: ~1,5 detik - selaras durasi tampil kartu sukses (§12.5), supaya satu tap
#: tidak pernah membekukan layar lebih lama dari itu sebelum jatuh ke
#: "diantrekan".
_TIMEOUT_DETIK_BAWAAN = 1.5
_FORWARDER_INTERVAL_DETIK_BAWAAN = 5.0
_FORWARDER_INTERVAL_MAKS_DETIK_BAWAAN = 60.0


class KonfigurasiTidakLengkap(Exception):
    """Variabel lingkungan wajib untuk kiosk lobi belum diatur."""


@dataclass(frozen=True)
class KonfigurasiLobi:
    server_url: str
    username: str
    password: str
    station_id: str
    path_antrean: str
    timeout_detik: float
    forwarder_interval_detik: float
    forwarder_interval_maks_detik: float


def muat_dari_lingkungan() -> KonfigurasiLobi:
    """Baca konfigurasi dari variabel lingkungan - diisi lewat berkas .env
    lokal yang dimuat sebelum aplikasi berjalan (mis. skrip peluncur di PC
    lobi), atau langsung lewat Environment Variables Windows di PC itu."""
    hilang = [nama for nama in (_VAR_SERVER_URL, _VAR_USERNAME, _VAR_PASSWORD) if not os.environ.get(nama)]
    if hilang:
        raise KonfigurasiTidakLengkap(f"Variabel lingkungan berikut belum diatur: {', '.join(hilang)}")

    return KonfigurasiLobi(
        server_url=os.environ[_VAR_SERVER_URL],
        username=os.environ[_VAR_USERNAME],
        password=os.environ[_VAR_PASSWORD],
        station_id=os.environ.get(_VAR_STATION_ID, _STATION_ID_BAWAAN),
        path_antrean=os.environ.get(_VAR_PATH_ANTREAN, _PATH_ANTREAN_BAWAAN),
        timeout_detik=float(os.environ.get(_VAR_TIMEOUT_DETIK, _TIMEOUT_DETIK_BAWAAN)),
        forwarder_interval_detik=float(
            os.environ.get(_VAR_FORWARDER_INTERVAL_DETIK, _FORWARDER_INTERVAL_DETIK_BAWAAN)
        ),
        forwarder_interval_maks_detik=float(
            os.environ.get(_VAR_FORWARDER_INTERVAL_MAKS_DETIK, _FORWARDER_INTERVAL_MAKS_DETIK_BAWAAN)
        ),
    )
