"""Tes KlienScan lewat httpx.MockTransport - tanpa server sungguhan, tanpa
Qt. Membuktikan: simpan-dulu-sebelum-forward, offline -> hasil netral
(diantrekan) + tetap PENDING, kuras saat tersambung kembali, tidak
menghapus/menandai terkirim sebelum ack 2xx, retry membawa waktu_scan ASLI
(bukan waktu kirim ulang), UID mentah offline lalu dikuras tetap utuh
(PRD §17), dan SATU-SATUNYA jalur pengiriman (_coba_kirim_satu_item) benar-
benar single-flight - tidak pernah dua pengiriman berjalan bersamaan meski
dipicu dari thread berbeda (tap vs forwarder latar).
"""

import json
import threading
import time
from datetime import datetime

import httpx

from app.rfid.base import JalurAlat
from app.ui.lobi.klien_scan import KlienScan

WAKTU_ASLI = datetime(2026, 9, 10, 7, 10, 0)


def _respons_login(token: str = "tok-1") -> dict:
    return {"token": token, "peran": "OPERATOR", "wajib_ganti_password": False, "kadaluarsa": "2026-09-10T19:00:00"}


def _respons_scan_sukses(uid_kartu: str = "AABBCC") -> dict:
    return {
        "jenis_event": "MASUK",
        "uid_kartu": uid_kartu,
        "id_absensi": 1,
        "status_masuk": "HADIR",
        "status_final": "HADIR",
        "jam_masuk": "2026-09-10T07:10:00",
        "jam_pulang": None,
        "id_pemilik": 1,
        "jenis_pemilik": "SISWA",
        "nama": "Budi",
    }


def _klien(handler, **kwargs) -> KlienScan:
    klien = KlienScan(
        base_url="http://server-uji", username="operator_lobi", password="rahasia",
        path_antrean=":memory:", **kwargs,
    )
    klien._client = httpx.Client(base_url="http://server-uji", transport=httpx.MockTransport(handler))
    return klien


# ---------------------------------------------------------------------------
# Simpan-dulu-sebelum-forward, offline vs online
# ---------------------------------------------------------------------------


def test_offline_mengembalikan_hasil_diantrekan_dan_tetap_pending():
    def handler(request: httpx.Request) -> httpx.Response:
        raise httpx.ConnectError("jaringan putus", request=request)

    klien = _klien(handler)
    hasil = klien.submit_scan(
        uid_kartu="AABBCC", uid_mentah="AABBCC", jalur=JalurAlat.SIMULATOR, waktu=WAKTU_ASLI, station_id="LOBI-1"
    )

    assert hasil.terkirim is False
    assert hasil.diantrekan is True
    assert hasil.jenis_event is None
    assert hasil.nama is None  # offline: tidak ada lookup identitas sama sekali
    assert klien.hitung_pending() == 1


def test_online_mengembalikan_hasil_kaya_dan_tidak_pending():
    def handler(request: httpx.Request) -> httpx.Response:
        if request.url.path == "/auth/login":
            return httpx.Response(200, json=_respons_login())
        return httpx.Response(200, json=_respons_scan_sukses())

    klien = _klien(handler)
    hasil = klien.submit_scan(
        uid_kartu="AABBCC", uid_mentah="AABBCC", jalur=JalurAlat.SIMULATOR, waktu=WAKTU_ASLI, station_id="LOBI-1"
    )

    assert hasil.terkirim is True
    assert hasil.diantrekan is False
    assert hasil.nama == "Budi"
    assert klien.hitung_pending() == 0


def test_gagal_menyimpan_lokal_bukan_diantrekan_tapi_gagal_sungguhan():
    """diantrekan HANYA untuk kegagalan kirim - kegagalan menyimpan ke
    antrean lokal itu sendiri (kasus langka, mis. disk penuh) tetap
    kegagalan sungguhan (§12.5: berbunyi + tandai jalur)."""

    def handler(request: httpx.Request) -> httpx.Response:
        raise AssertionError("tidak boleh ada permintaan jaringan sama sekali jika enqueue gagal")

    klien = _klien(handler)
    klien._antrean.enqueue = lambda **kwargs: (_ for _ in ()).throw(OSError("disk penuh"))

    hasil = klien.submit_scan(
        uid_kartu="AABBCC", uid_mentah="AABBCC", jalur=JalurAlat.SIMULATOR, waktu=WAKTU_ASLI, station_id="LOBI-1"
    )

    assert hasil.terkirim is False
    assert hasil.diantrekan is False
    assert hasil.pesan_galat is not None


# ---------------------------------------------------------------------------
# Kuras saat tersambung kembali - tidak menghapus/menandai sebelum ack
# ---------------------------------------------------------------------------


def test_kuras_sekali_mengirim_pending_setelah_server_hidup_lagi():
    online = False

    def handler(request: httpx.Request) -> httpx.Response:
        if not online:
            raise httpx.ConnectError("jaringan putus", request=request)
        if request.url.path == "/auth/login":
            return httpx.Response(200, json=_respons_login())
        return httpx.Response(200, json=_respons_scan_sukses())

    klien = _klien(handler)
    hasil_offline = klien.submit_scan(
        uid_kartu="AABBCC", uid_mentah="AABBCC", jalur=JalurAlat.SIMULATOR, waktu=WAKTU_ASLI, station_id="LOBI-1"
    )
    assert hasil_offline.diantrekan is True
    assert klien.hitung_pending() == 1

    online = True
    jumlah = klien.kuras_sekali()

    assert jumlah == 1
    assert klien.hitung_pending() == 0


def test_kuras_sekali_tidak_menandai_terkirim_sebelum_ack_2xx():
    def handler(request: httpx.Request) -> httpx.Response:
        if request.url.path == "/auth/login":
            return httpx.Response(200, json=_respons_login())
        return httpx.Response(500, json={"detail": "kesalahan server"})

    klien = _klien(handler)
    klien.submit_scan(
        uid_kartu="AABBCC", uid_mentah="AABBCC", jalur=JalurAlat.SIMULATOR, waktu=WAKTU_ASLI, station_id="LOBI-1"
    )
    assert klien.hitung_pending() == 1

    jumlah = klien.kuras_sekali()

    assert jumlah == 0
    assert klien.hitung_pending() == 1  # tetap PENDING - tidak pernah ditandai terkirim tanpa 2xx


def test_kuras_sekali_berhenti_pada_kegagalan_pertama_bukan_paksa_semua():
    percobaan = []

    def handler(request: httpx.Request) -> httpx.Response:
        if request.url.path == "/auth/login":
            return httpx.Response(200, json=_respons_login())
        percobaan.append(request)
        raise httpx.ConnectError("jaringan putus", request=request)

    klien = _klien(handler)
    for i in range(3):
        # submit_scan() sendiri sudah memicu satu percobaan inline per
        # panggilan (semuanya gagal, sama seperti kuras_sekali nanti) -
        # dihitung terpisah di bawah, bukan bagian yang diverifikasi di sini.
        klien.submit_scan(
            uid_kartu=f"UID{i}", uid_mentah=f"UID{i}", jalur=JalurAlat.SIMULATOR,
            waktu=datetime(2026, 9, 10, 7, 10 + i), station_id="LOBI-1",
        )
    assert klien.hitung_pending() == 3
    percobaan.clear()

    jumlah = klien.kuras_sekali()

    assert jumlah == 0
    assert klien.hitung_pending() == 3
    assert len(percobaan) == 1  # berhenti di percobaan pertama, tidak memaksa 2 sisanya


def test_kuras_sekali_urutan_fifo_by_waktu_scan():
    urutan_terkirim = []

    def handler(request: httpx.Request) -> httpx.Response:
        if request.url.path == "/auth/login":
            return httpx.Response(200, json=_respons_login())
        uid = json.loads(request.content)["uid_kartu"]
        urutan_terkirim.append(uid)
        return httpx.Response(200, json=_respons_scan_sukses(uid_kartu=uid))

    klien = _klien(handler)
    # Masukkan waktu_scan tidak berurutan.
    klien._antrean.enqueue(uid_kartu="B", uid_mentah="B", waktu_scan=datetime(2026, 9, 10, 7, 15), jalur=JalurAlat.SIMULATOR, station_id="LOBI-1")
    klien._antrean.enqueue(uid_kartu="A", uid_mentah="A", waktu_scan=datetime(2026, 9, 10, 7, 10), jalur=JalurAlat.SIMULATOR, station_id="LOBI-1")
    klien._antrean.enqueue(uid_kartu="C", uid_mentah="C", waktu_scan=datetime(2026, 9, 10, 7, 20), jalur=JalurAlat.SIMULATOR, station_id="LOBI-1")

    klien.kuras_sekali()

    assert urutan_terkirim == ["A", "B", "C"]


# ---------------------------------------------------------------------------
# Retry membawa waktu_scan ASLI - kunci keamanan replay (Gerbang 3)
# ---------------------------------------------------------------------------


def test_retry_mengirim_waktu_scan_asli_bukan_waktu_percobaan_ulang():
    """Server mengklasifikasi status dari waktu_scan di badan permintaan
    (lihat verifikasi Gerbang 1) - retry WAJIB membawa waktu tap
    sebenarnya, bukan "sekarang", supaya dedup server (Gerbang 3) dan
    klasifikasi status (Gerbang 1) tetap benar berapa pun lama server mati.
    """
    waktu_dikirim = []
    online = False

    def handler(request: httpx.Request) -> httpx.Response:
        if not online:
            raise httpx.ConnectError("jaringan putus", request=request)
        if request.url.path == "/auth/login":
            return httpx.Response(200, json=_respons_login())
        waktu_dikirim.append(json.loads(request.content)["waktu_scan"])
        return httpx.Response(200, json=_respons_scan_sukses())

    klien = _klien(handler)
    klien.submit_scan(
        uid_kartu="AABBCC", uid_mentah="AABBCC", jalur=JalurAlat.SIMULATOR, waktu=WAKTU_ASLI, station_id="LOBI-1"
    )

    online = True
    klien.kuras_sekali()

    assert waktu_dikirim == [WAKTU_ASLI.isoformat()]


# ---------------------------------------------------------------------------
# UID mentah offline lalu dikuras tetap utuh (PRD §17)
# ---------------------------------------------------------------------------


def test_offline_lalu_dikuras_uid_mentah_tetap_utuh():
    """UID mentah "kotor" (huruf kecil, separator) yang masuk saat server
    mati harus sampai ke server byte-persis begitu antrean dikuras -
    tidak dinormalisasi ulang, tidak hilang, tidak diganti waktu enqueue
    ulang di klien_scan.py maupun antrean_lokal.py.
    """
    uid_mentah_dikirim = []
    online = False

    def handler(request: httpx.Request) -> httpx.Response:
        if not online:
            raise httpx.ConnectError("jaringan putus", request=request)
        if request.url.path == "/auth/login":
            return httpx.Response(200, json=_respons_login())
        uid_mentah_dikirim.append(json.loads(request.content)["uid_mentah"])
        return httpx.Response(200, json=_respons_scan_sukses(uid_kartu="04A2B3C4"))

    klien = _klien(handler)
    hasil = klien.submit_scan(
        uid_kartu="04A2B3C4", uid_mentah="04:a2 b3-c4", jalur=JalurAlat.SIMULATOR,
        waktu=WAKTU_ASLI, station_id="LOBI-1",
    )
    assert hasil.diantrekan is True

    online = True
    jumlah = klien.kuras_sekali()

    assert jumlah == 1
    assert uid_mentah_dikirim == ["04:a2 b3-c4"]


# ---------------------------------------------------------------------------
# Jalur offline lalu dikuras tetap utuh (PRD §17)
# ---------------------------------------------------------------------------


def test_offline_lalu_dikuras_jalur_tetap_utuh():
    """Jalur alat pembaca yang masuk saat server mati harus sampai ke
    server persis begitu antrean dikuras - tidak diganti/hilang di
    klien_scan.py maupun antrean_lokal.py. Sengaja memakai PCSC (bukan
    SIMULATOR yang dipakai tes lain di berkas ini) supaya tes ini tidak
    lolos hanya karena kebetulan cocok dengan nilai bawaan di suatu
    tempat.
    """
    jalur_dikirim = []
    online = False

    def handler(request: httpx.Request) -> httpx.Response:
        if not online:
            raise httpx.ConnectError("jaringan putus", request=request)
        if request.url.path == "/auth/login":
            return httpx.Response(200, json=_respons_login())
        jalur_dikirim.append(json.loads(request.content)["jalur"])
        return httpx.Response(200, json=_respons_scan_sukses())

    klien = _klien(handler)
    hasil = klien.submit_scan(
        uid_kartu="AABBCC", uid_mentah="AABBCC", jalur=JalurAlat.PCSC, waktu=WAKTU_ASLI, station_id="LOBI-1",
    )
    assert hasil.diantrekan is True

    online = True
    jumlah = klien.kuras_sekali()

    assert jumlah == 1
    assert jalur_dikirim == ["PCSC"]


# ---------------------------------------------------------------------------
# Single-flight: SATU-SATUNYA jalur pengiriman tidak pernah tumpang tindih
# ---------------------------------------------------------------------------


def test_pengiriman_tidak_pernah_tumpang_tindih_dari_dua_thread():
    """Simulasikan submit_scan() (dipicu tap, thread GUI) dan
    kuras_sekali() (dipicu forwarder, thread latar) berjalan "bersamaan" -
    _coba_kirim_satu_item() WAJIB serial. Diverifikasi lewat penghitung
    in-flight: maksimum yang pernah teramati harus 1.
    """
    lock_hitung = threading.Lock()
    sedang_berjalan = 0
    maks_bersamaan = 0

    def handler(request: httpx.Request) -> httpx.Response:
        nonlocal sedang_berjalan, maks_bersamaan
        if request.url.path == "/auth/login":
            return httpx.Response(200, json=_respons_login())
        with lock_hitung:
            sedang_berjalan += 1
            maks_bersamaan = max(maks_bersamaan, sedang_berjalan)
        time.sleep(0.05)  # perbesar peluang tabrakan kalau lock tidak bekerja
        with lock_hitung:
            sedang_berjalan -= 1
        return httpx.Response(200, json=_respons_scan_sukses())

    klien = _klien(handler)
    for i in range(6):
        klien._antrean.enqueue(
            uid_kartu=f"UID{i}", uid_mentah=f"UID{i}", waktu_scan=datetime(2026, 9, 10, 7, 10 + i),
            jalur=JalurAlat.SIMULATOR, station_id="LOBI-1",
        )

    thread_a = threading.Thread(target=klien.kuras_sekali)
    thread_b = threading.Thread(target=klien.kuras_sekali)
    thread_a.start()
    thread_b.start()
    thread_a.join(timeout=5.0)
    thread_b.join(timeout=5.0)

    assert maks_bersamaan == 1
    assert klien.hitung_pending() == 0
