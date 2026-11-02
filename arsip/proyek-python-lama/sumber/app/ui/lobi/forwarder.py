"""
ForwarderAntrean: thread latar yang mengosongkan antrean lokal (PENDING ->
TERKIRIM) begitu server terjangkau lagi.

Pola threading yang sama dengan app.rfid.base.PembacaKartuBase (mulai()/
berhenti(), daemon thread) - bukan APScheduler (itu untuk proses server
berumur panjang; ini kiosk client, satu thread ringan sudah cukup). Tidak
pernah membekukan thread GUI - lihat ui-guidelines.md: "Jalankan pembacaan
RFID dan pengiriman notifikasi di thread terpisah."

Seluruh pengiriman nyata tetap lewat KlienScan._coba_kirim_satu_item() yang
dikunci - forwarder ini HANYA memanggil KlienScan.kuras_sekali() berulang
dengan jeda; tidak pernah menyentuh jaringan atau antrean secara langsung.
"""

from __future__ import annotations

import logging
import threading

from app.ui.lobi.klien_scan import KlienScan

_log = logging.getLogger(__name__)


class ForwarderAntrean:
    """Backoff sederhana: jeda direset ke nilai bawaan begitu satu putaran
    berhasil mengirim sesuatu, dan digandakan (sampai batas atas) tiap
    putaran yang tidak berhasil mengirim apa pun padahal masih ada PENDING -
    server kemungkinan masih tak terjangkau, tak perlu mencoba secepat itu
    lagi.
    """

    def __init__(
        self,
        klien: KlienScan,
        *,
        interval_detik: float = 5.0,
        interval_maks_detik: float = 60.0,
    ) -> None:
        self._klien = klien
        self._interval_bawaan = interval_detik
        self._interval_maks = interval_maks_detik
        self._interval_sekarang = interval_detik
        self._berhenti = threading.Event()
        self._thread: threading.Thread | None = None

    @property
    def is_running(self) -> bool:
        return self._thread is not None and self._thread.is_alive()

    def mulai(self) -> None:
        if self.is_running:
            return
        self._berhenti.clear()
        self._thread = threading.Thread(target=self._jalankan, name="forwarder-antrean", daemon=True)
        self._thread.start()

    def berhenti(self) -> None:
        self._berhenti.set()
        if self._thread is not None:
            self._thread.join(timeout=2.0)
        self._thread = None

    def _jalankan(self) -> None:
        while not self._berhenti.is_set():
            self._satu_putaran()
            self._berhenti.wait(self._interval_sekarang)

    def _satu_putaran(self) -> None:
        try:
            terkirim = self._klien.kuras_sekali()
        except Exception:
            _log.exception("ForwarderAntrean: putaran kuras gagal tak terduga")
            terkirim = 0

        if terkirim > 0:
            self._interval_sekarang = self._interval_bawaan
            return

        if self._klien.hitung_pending() > 0:
            self._interval_sekarang = min(self._interval_sekarang * 2, self._interval_maks)
