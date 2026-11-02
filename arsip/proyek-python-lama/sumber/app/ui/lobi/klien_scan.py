"""
KlienScan: SATU seam lewat mana seluruh scan dikirim ke server, dengan
antrean lokal store-and-forward di baliknya.

SIMPAN-DULU SELALU (SKILL.md §21: "Data absensi disimpan lebih dahulu,
sebelum hal lain terjadi"): submit_scan() SELALU menulis ke AntreanLokal
(SQLite lokal di PC lobi, terpisah dari basis data server) sebelum mencoba
apa pun lewat jaringan. Absensi tetap berjalan tanpa internet MAUPUN tanpa
LAN - kegagalan kirim tidak pernah berarti kehilangan data, hanya berarti
item tetap PENDING sampai forwarder (app.ui.lobi.forwarder.ForwarderAntrean)
berhasil mengirimnya.

SERIALISASI KETAT - SATU-SATUNYA titik pengiriman HTTP nyata adalah
_coba_kirim_satu_item(), dikunci self._lock_pengiriman. submit_scan()
(dipanggil dari thread GUI saat tap) dan ForwarderAntrean.kuras_sekali()
(dipanggil dari thread latar) SAMA-SAMA lewat method ini - tidak pernah dua
pengiriman berjalan bersamaan. Ini bukan optimisasi, ini SYARAT KEBENARAN:
verifikasi terhadap app.core.mesin_absensi.scan_duplikat membuktikan replay
item identik (uid_kartu, waktu_scan sama persis) aman dari duplikasi HANYA
selama tidak ada scan LAIN untuk UID yang sama menyisip di antara percobaan
pertama dan percobaan ulang - itu cuma benar kalau pengiriman selalu
satu-per-satu, FIFO by waktu_scan, tidak pernah tumpang tindih.

Signature submit_scan() TIDAK berubah dari slice sebelumnya - isinya saja
yang sekarang lewat antrean, bukan langsung ke jaringan.
"""

from __future__ import annotations

import logging
import threading
from dataclasses import dataclass
from datetime import datetime

import httpx

from app.data.models.attendance import JenisEventScan
from app.rfid.base import JalurAlat
from app.ui.lobi.antrean_lokal import AntreanLokal

_log = logging.getLogger(__name__)


@dataclass(frozen=True)
class HasilSubmit:
    """Hasil satu percobaan submit_scan - SELALU ada, apa pun hasilnya.
    Ini yang dikonsumsi app.ui.lobi.tampilan.petakan(), bukan respons HTTP
    mentah.

    diantrekan=True berarti tap ini SUDAH tersimpan durabel di antrean
    lokal, hanya belum sempat dikonfirmasi server - ini BUKAN kegagalan
    (SKILL.md §21), makanya dipisah dari terkirim=False biasa. Sengaja
    tanpa nama/status: offline berarti tak ada lookup identitas sama
    sekali (server tetap satu-satunya sumber sah, lihat
    app/server/routers/scan.py).

    terkirim=False DAN diantrekan=False berarti kegagalan yang lebih
    serius: tap ini GAGAL tersimpan bahkan di antrean lokal (mis. berkas
    antrean tidak bisa ditulis) - kasus langka, ditangani sebagai
    kegagalan sungguhan (§12.5: berbunyi + tandai jalur), bukan
    "menunggu koneksi".
    """

    uid_kartu: str
    jalur: JalurAlat
    terkirim: bool
    diantrekan: bool = False
    jenis_event: JenisEventScan | None = None
    id_absensi: int | None = None
    status_masuk: str | None = None
    status_final: str | None = None
    nama: str | None = None
    jenis_pemilik: str | None = None
    pesan_galat: str | None = None


@dataclass(frozen=True)
class _HasilKirimItem:
    """Internal - hasil satu percobaan _coba_kirim_satu_item()."""

    id_item: int
    hasil: HasilSubmit


class KlienScan:
    """Pembungkus HTTP ke POST /scan untuk satu stasiun kiosk, dengan
    antrean lokal di baliknya.

    Memegang token akun Operator kiosk di memori (tidak pernah ditulis ke
    disk). Login sekali saat token belum ada; pada respons 401 (token
    kadaluarsa/dicabut), login ulang SEKALI lalu ulangi permintaannya -
    bukan retry tak berhingga, supaya kegagalan autentikasi yang menetap
    (mis. akun dinonaktifkan admin) tetap terlihat sebagai kegagalan
    submit, bukan diam-diam dicoba berulang-ulang.
    """

    def __init__(
        self,
        *,
        base_url: str,
        username: str,
        password: str,
        path_antrean: str,
        timeout: float = 1.5,
    ) -> None:
        self._client = httpx.Client(base_url=base_url.rstrip("/"), timeout=timeout)
        self._username = username
        self._password = password
        self._token: str | None = None
        self._antrean = AntreanLokal(path_antrean)
        #: SATU-SATUNYA lock yang menjaga _coba_kirim_satu_item() serial -
        #: lihat docstring modul.
        self._lock_pengiriman = threading.Lock()

    # ------------------------------------------------------------------
    # Seam publik - dipanggil app.ui.lobi.layar per tap.
    # ------------------------------------------------------------------

    def submit_scan(
        self, *, uid_kartu: str, uid_mentah: str, jalur: JalurAlat, waktu: datetime, station_id: str
    ) -> HasilSubmit:
        """SELALU simpan ke antrean lokal dulu, baru coba kirim SATU
        putaran (FIFO by waktu_scan - lihat _coba_kirim_satu_item). Hasil
        "sukses kaya" hanya dikembalikan bila item yang BARU disimpan ini
        yang benar-benar berhasil dikirim; kalau ada antrean lain di
        depannya, atau pengiriman gagal, tap ini tetap tercatat sebagai
        "diantrekan" - netral, bukan alarm - dan akan ikut terkuras oleh
        ForwarderAntrean di latar belakang.

        uid_kartu: UID ternormalisasi (baku), dipakai server untuk mencari
        kartu. uid_mentah: UID apa adanya dari alat (PRD §17) - disimpan
        durabel di antrean lokal supaya tidak hilang meski scan ini
        dibuffer offline berjam-jam, lalu diteruskan utuh ke server.
        """
        try:
            id_item = self._antrean.enqueue(
                uid_kartu=uid_kartu, uid_mentah=uid_mentah, waktu_scan=waktu, jalur=jalur, station_id=station_id
            )
        except Exception as exc:  # noqa: BLE001 - kegagalan lokal genuinely tak terduga, harus terlihat
            _log.exception("Gagal menyimpan scan ke antrean lokal - data BENAR-BENAR belum tersimpan")
            return HasilSubmit(
                uid_kartu=uid_kartu, jalur=jalur, terkirim=False,
                pesan_galat=f"Gagal menyimpan lokal: {exc}",
            )

        hasil_item = self._coba_kirim_satu_item()
        if hasil_item is not None and hasil_item.id_item == id_item and hasil_item.hasil.terkirim:
            return hasil_item.hasil

        return HasilSubmit(uid_kartu=uid_kartu, jalur=jalur, terkirim=False, diantrekan=True)

    # ------------------------------------------------------------------
    # Dipanggil ForwarderAntrean (thread latar) - lihat app.ui.lobi.forwarder.
    # ------------------------------------------------------------------

    def kuras_sekali(self, *, maks_item: int = 100) -> int:
        """Kirim PENDING FIFO by waktu_scan selama masih berhasil, berhenti
        pada kegagalan PERTAMA - kalau satu gagal karena jaringan/server
        tak terjangkau, sisanya kemungkinan besar akan gagal juga; tak ada
        gunanya memaksa (dan tetap menjaga single-flight: satu percobaan
        gagal, jangan lompat ke item berikutnya seolah tak terjadi apa-apa).
        Mengembalikan jumlah yang benar-benar berhasil dikirim.
        """
        jumlah = 0
        for _ in range(maks_item):
            hasil_item = self._coba_kirim_satu_item()
            if hasil_item is None:
                break  # antrean kosong
            if not hasil_item.hasil.terkirim:
                break  # gagal - server kemungkinan masih tak terjangkau
            jumlah += 1
        return jumlah

    def hitung_pending(self) -> int:
        return self._antrean.hitung_pending()

    def cek_mode_darurat(self, station_id: str) -> tuple[bool, str | None]:
        """Polling ringan untuk banner MODE DARURAT (§12.4, lihat
        app.ui.lobi.layar.LayarAbsensiLobi.set_mode_darurat) - GAGAL DIAM-
        DIAM (kembalikan aktif=False) kalau server tak terjangkau, supaya
        kegagalan jaringan sesaat tidak pernah memunculkan/menghilangkan
        banner secara keliru. Ini murni informasional, terpisah dari
        pencatatan absensi (§21) - TIDAK PERNAH lewat antrean lokal/single-
        flight seperti submit_scan(), tidak menyentuhnya sama sekali."""
        if self._token is None:
            galat = self._login()
            if galat is not None:
                return False, None
        try:
            respons = self._client.get(
                "/perangkat/izin-darurat/aktif",
                params={"station_id": station_id},
                headers={"Authorization": f"Bearer {self._token}"},
            )
            if respons.status_code == 401:
                galat = self._login()
                if galat is not None:
                    return False, None
                respons = self._client.get(
                    "/perangkat/izin-darurat/aktif",
                    params={"station_id": station_id},
                    headers={"Authorization": f"Bearer {self._token}"},
                )
            if respons.status_code != 200:
                return False, None
            badan = respons.json()
        except httpx.HTTPError:
            return False, None

        if not badan["aktif"]:
            return False, None
        pesan = (
            f"MODE DARURAT — alat jalur {station_id} tidak sesuai standar "
            f"(berlaku sampai {badan['waktu_berakhir']})"
        )
        return True, pesan

    def laporkan_telemetri(self, *, station_id: str, jumlah_pending: int) -> bool:
        """Lapor jumlah antrean lokal ke server untuk status operasional
        Dashboard (PRD §14.1: "jumlah scan yang masih mengantre di PC
        lobi") - GAGAL DIAM-DIAM (kembalikan False), pola sama persis
        cek_mode_darurat(). Murni informasional, terpisah dari pencatatan
        absensi - TIDAK PERNAH lewat antrean lokal/single-flight seperti
        submit_scan()."""
        if self._token is None:
            galat = self._login()
            if galat is not None:
                return False
        try:
            respons = self._client.post(
                "/perangkat/telemetri",
                json={"station_id": station_id, "jumlah_pending": jumlah_pending},
                headers={"Authorization": f"Bearer {self._token}"},
            )
            if respons.status_code == 401:
                galat = self._login()
                if galat is not None:
                    return False
                respons = self._client.post(
                    "/perangkat/telemetri",
                    json={"station_id": station_id, "jumlah_pending": jumlah_pending},
                    headers={"Authorization": f"Bearer {self._token}"},
                )
            return respons.status_code == 201
        except httpx.HTTPError:
            return False

    def tutup(self) -> None:
        self._client.close()
        self._antrean.tutup()

    # ------------------------------------------------------------------
    # SATU-SATUNYA titik pengiriman HTTP nyata - lihat docstring modul.
    # ------------------------------------------------------------------

    def _coba_kirim_satu_item(self) -> _HasilKirimItem | None:
        with self._lock_pengiriman:
            daftar = self._antrean.daftar_pending(limit=1)
            if not daftar:
                return None
            item = daftar[0]
            hasil = self._kirim_item(
                uid_kartu=item.uid_kartu, uid_mentah=item.uid_mentah, jalur=item.jalur,
                waktu=item.waktu_scan, station_id=item.station_id,
            )
            if hasil.terkirim:
                self._antrean.tandai_terkirim(item.id)
            return _HasilKirimItem(id_item=item.id, hasil=hasil)

    def _kirim_item(
        self, *, uid_kartu: str, uid_mentah: str, jalur: JalurAlat, waktu: datetime, station_id: str
    ) -> HasilSubmit:
        """Satu percobaan round-trip POST /scan (dengan login/re-login
        seperlunya). TIDAK menyentuh antrean lokal sama sekali - murni
        "coba kirim, laporkan hasilnya" - supaya dipakai ulang oleh
        submit_scan() (lewat _coba_kirim_satu_item) tanpa duplikasi logika.
        """
        if self._token is None:
            galat = self._login()
            if galat is not None:
                return HasilSubmit(uid_kartu=uid_kartu, jalur=jalur, terkirim=False, pesan_galat=galat)

        try:
            respons = self._kirim(
                uid_kartu=uid_kartu, uid_mentah=uid_mentah, jalur=jalur, waktu=waktu, station_id=station_id
            )
        except httpx.HTTPError as exc:
            return HasilSubmit(
                uid_kartu=uid_kartu, jalur=jalur, terkirim=False,
                pesan_galat=f"Tidak terhubung ke server: {exc}",
            )

        if respons.status_code == 401:
            galat = self._login()
            if galat is not None:
                return HasilSubmit(uid_kartu=uid_kartu, jalur=jalur, terkirim=False, pesan_galat=galat)
            try:
                respons = self._kirim(
                    uid_kartu=uid_kartu, uid_mentah=uid_mentah, jalur=jalur, waktu=waktu, station_id=station_id
                )
            except httpx.HTTPError as exc:
                return HasilSubmit(
                    uid_kartu=uid_kartu, jalur=jalur, terkirim=False,
                    pesan_galat=f"Tidak terhubung ke server: {exc}",
                )

        if respons.status_code != 200:
            return HasilSubmit(
                uid_kartu=uid_kartu, jalur=jalur, terkirim=False,
                pesan_galat=f"Server menolak scan (kode {respons.status_code})",
            )

        badan = respons.json()
        return HasilSubmit(
            uid_kartu=badan["uid_kartu"],
            jalur=jalur,
            terkirim=True,
            jenis_event=JenisEventScan(badan["jenis_event"]),
            id_absensi=badan["id_absensi"],
            status_masuk=badan["status_masuk"],
            status_final=badan["status_final"],
            nama=badan.get("nama"),
            jenis_pemilik=badan.get("jenis_pemilik"),
        )

    def _login(self) -> str | None:
        """Login akun Operator kiosk. Mengembalikan None bila berhasil,
        atau pesan galat (str) bila gagal - dipilih begini, bukan
        melempar exception, supaya _kirim_item() punya satu pola
        penanganan galat yang seragam di setiap titik pemanggilan."""
        try:
            respons = self._client.post(
                "/auth/login", json={"username": self._username, "password": self._password}
            )
        except httpx.HTTPError as exc:
            return f"Tidak bisa login ke server: {exc}"

        if respons.status_code != 200:
            return f"Login akun operator kiosk gagal (kode {respons.status_code}) - periksa konfigurasi kredensial"

        self._token = respons.json()["token"]
        return None

    def _kirim(
        self, *, uid_kartu: str, uid_mentah: str, jalur: JalurAlat, waktu: datetime, station_id: str
    ) -> httpx.Response:
        return self._client.post(
            "/scan",
            json={
                "uid_kartu": uid_kartu,
                "uid_mentah": uid_mentah,
                "jalur": jalur.value,
                "waktu_scan": waktu.isoformat(),
                "station_id": station_id,
            },
            headers={"Authorization": f"Bearer {self._token}"},
        )
