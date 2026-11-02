"""
AntreanLokal: antrean store-and-forward SQLite lokal di PC lobi.

TERPISAH dari basis data server (SKILL.md, prinsip arsitektur: klien tidak
pernah membuka berkas basis data server langsung) - ini file SQLite lain,
hidup di PC klien saja, tidak pernah disentuh proses server. Memakai
`sqlite3` stdlib langsung, BUKAN SQLAlchemy - antrean ini bukan bagian
app/data (model server), cukup satu tabel kecil yang tidak butuh ORM.

Menyimpan HANYA uid_kartu (baku), uid_mentah (apa adanya dari alat, PRD
§17), waktu_scan, jalur, station_id, dan status kirim. TIDAK PERNAH nama,
NIK, atau data pribadi apa pun - UID bukan data pribadi (tidak
mengidentifikasi orang, hanya kartu fisik), klasifikasi dan identitas
tetap sepenuhnya wewenang server (lihat app.ui.lobi.klien_scan).

Item baru selalu masuk sebagai PENDING lebih dulu (SKILL.md §21: "Data
absensi disimpan lebih dahulu, sebelum hal lain terjadi") - forwarding ke
server adalah langkah SETELAHNYA lewat KlienScan, boleh gagal tanpa
kehilangan data. Baris HANYA berpindah ke TERKIRIM setelah ack 2xx dari
server - tidak pernah dihapus atau diubah saat sekadar mencoba kirim.
"""

from __future__ import annotations

import sqlite3
import threading
from dataclasses import dataclass
from datetime import datetime

from app.rfid.base import JalurAlat

_SKEMA = """
CREATE TABLE IF NOT EXISTS antrean_scan (
    id INTEGER PRIMARY KEY AUTOINCREMENT,
    uid_kartu TEXT NOT NULL,
    uid_mentah TEXT NOT NULL,
    waktu_scan TEXT NOT NULL,
    jalur TEXT NOT NULL,
    station_id TEXT NOT NULL,
    status TEXT NOT NULL DEFAULT 'PENDING',
    dibuat_pada TEXT NOT NULL
)
"""

_STATUS_PENDING = "PENDING"
_STATUS_TERKIRIM = "TERKIRIM"


@dataclass(frozen=True)
class ItemAntrean:
    """Satu baris antrean - bentuk yang dipakai app.ui.lobi.klien_scan,
    bukan baris sqlite3 mentah."""

    id: int
    uid_kartu: str
    uid_mentah: str
    waktu_scan: datetime
    jalur: JalurAlat
    station_id: str


class AntreanLokal:
    """Thread-safe lewat satu Lock bersama - volume dan frekuensi akses
    kecil (satu PC lobi, satu jalur pada slice ini), jadi satu koneksi
    dipegang bersama + lock lebih sederhana untuk dinalar dan diuji
    ketimbang koneksi per-thread.
    """

    def __init__(self, path: str) -> None:
        self._lock = threading.Lock()
        self._koneksi = sqlite3.connect(path, check_same_thread=False)
        with self._lock:
            self._koneksi.execute("PRAGMA journal_mode=WAL")
            self._koneksi.execute(_SKEMA)
            self._koneksi.commit()

    def enqueue(
        self, *, uid_kartu: str, uid_mentah: str, waktu_scan: datetime, jalur: JalurAlat, station_id: str
    ) -> int:
        """Simpan satu tap sebagai PENDING. Mengembalikan id baris -
        dipakai pemanggil (KlienScan) untuk mengenali apakah item yang
        BARU disimpan ini yang benar-benar berhasil dikirim, atau masih
        menunggu di belakang antrean yang sudah ada sebelumnya.

        uid_mentah disimpan APA ADANYA (PRD §17) - tidak pernah
        dinormalisasi ulang di sini maupun di server; normalisasi tetap
        wewenang klien saat ScanEvent dibentuk (app.rfid.base).
        """
        with self._lock:
            kursor = self._koneksi.execute(
                "INSERT INTO antrean_scan (uid_kartu, uid_mentah, waktu_scan, jalur, station_id, status, dibuat_pada) "
                "VALUES (?, ?, ?, ?, ?, ?, ?)",
                (
                    uid_kartu, uid_mentah, waktu_scan.isoformat(), jalur.value, station_id,
                    _STATUS_PENDING, datetime.now().isoformat(),
                ),
            )
            self._koneksi.commit()
            return kursor.lastrowid

    def daftar_pending(self, *, limit: int = 100) -> list[ItemAntrean]:
        """PENDING, terurut FIFO by waktu_scan (waktu tap sebenarnya) -
        bukan urutan dimasukkan ke antrean. Konsisten dengan
        app.core.mesin_absensi.scan_duplikat/AttendanceEventRepository.
        waktu_scan_terakhir, yang juga mengurut lewat nilai waktu_scan,
        bukan urutan baris."""
        with self._lock:
            baris = self._koneksi.execute(
                "SELECT id, uid_kartu, uid_mentah, waktu_scan, jalur, station_id FROM antrean_scan "
                "WHERE status = ? ORDER BY waktu_scan ASC LIMIT ?",
                (_STATUS_PENDING, limit),
            ).fetchall()
        return [
            ItemAntrean(
                id=id_,
                uid_kartu=uid,
                uid_mentah=mentah,
                waktu_scan=datetime.fromisoformat(waktu),
                jalur=JalurAlat(jalur),
                station_id=station,
            )
            for id_, uid, mentah, waktu, jalur, station in baris
        ]

    def tandai_terkirim(self, id_item: int) -> None:
        with self._lock:
            self._koneksi.execute("UPDATE antrean_scan SET status = ? WHERE id = ?", (_STATUS_TERKIRIM, id_item))
            self._koneksi.commit()

    def hitung_pending(self) -> int:
        with self._lock:
            (jumlah,) = self._koneksi.execute(
                "SELECT COUNT(*) FROM antrean_scan WHERE status = ?", (_STATUS_PENDING,)
            ).fetchone()
        return jumlah

    def tutup(self) -> None:
        with self._lock:
            self._koneksi.close()
