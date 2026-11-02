from datetime import datetime
from zoneinfo import ZoneInfo

WITA = ZoneInfo("Asia/Makassar")


def now_wita() -> datetime:
    """Waktu lokal WITA saat ini, sebagai datetime naive.

    Satu-satunya sumber "sekarang" yang boleh dipakai untuk kolom waktu di
    basis data. Jangan pakai datetime.utcnow() - perbandingan jam sekolah
    harus selalu di zona waktu lokal, bukan UTC.
    """
    return datetime.now(WITA).replace(tzinfo=None)
