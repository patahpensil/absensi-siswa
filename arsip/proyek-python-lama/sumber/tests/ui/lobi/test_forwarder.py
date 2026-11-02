"""Tes ForwarderAntrean: siklus hidup thread (mulai/berhenti bersih) dan
logika backoff (_satu_putaran, dipanggil langsung supaya deterministik -
tidak bergantung timing thread nyata). Memakai klien palsu (duck-typed),
bukan KlienScan/httpx sungguhan - forwarder tidak peduli DARI MANA
kuras_sekali()/hitung_pending() datang.
"""

import time

from app.ui.lobi.forwarder import ForwarderAntrean


class _KlienPalsu:
    def __init__(self, urutan_kuras: list[int]) -> None:
        self._urutan = list(urutan_kuras)
        self._indeks = 0
        self.jumlah_pending = 0
        self.panggilan_kuras = 0

    def kuras_sekali(self) -> int:
        self.panggilan_kuras += 1
        hasil = self._urutan[min(self._indeks, len(self._urutan) - 1)]
        self._indeks += 1
        return hasil

    def hitung_pending(self) -> int:
        return self.jumlah_pending


def test_backoff_menggandakan_interval_saat_gagal_berturut_turut():
    klien = _KlienPalsu(urutan_kuras=[0, 0, 0])
    klien.jumlah_pending = 5  # masih ada yang tertunda, tapi tak ada yang berhasil dikirim
    forwarder = ForwarderAntrean(klien, interval_detik=1.0, interval_maks_detik=10.0)

    forwarder._satu_putaran()
    assert forwarder._interval_sekarang == 2.0
    forwarder._satu_putaran()
    assert forwarder._interval_sekarang == 4.0
    forwarder._satu_putaran()
    assert forwarder._interval_sekarang == 8.0


def test_backoff_berhenti_di_batas_atas():
    klien = _KlienPalsu(urutan_kuras=[0])
    klien.jumlah_pending = 5
    forwarder = ForwarderAntrean(klien, interval_detik=8.0, interval_maks_detik=10.0)

    forwarder._satu_putaran()

    assert forwarder._interval_sekarang == 10.0  # 16.0 dibatasi ke 10.0


def test_interval_direset_ke_bawaan_setelah_berhasil_mengirim():
    klien = _KlienPalsu(urutan_kuras=[0, 0, 3])
    klien.jumlah_pending = 5
    forwarder = ForwarderAntrean(klien, interval_detik=1.0, interval_maks_detik=10.0)

    forwarder._satu_putaran()  # gagal -> 2.0
    forwarder._satu_putaran()  # gagal -> 4.0
    forwarder._satu_putaran()  # berhasil kirim 3 item -> reset ke bawaan

    assert forwarder._interval_sekarang == 1.0


def test_tidak_membesarkan_interval_kalau_antrean_memang_kosong():
    klien = _KlienPalsu(urutan_kuras=[0])
    klien.jumlah_pending = 0  # tidak ada yang tertunda sama sekali
    forwarder = ForwarderAntrean(klien, interval_detik=1.0, interval_maks_detik=10.0)

    forwarder._satu_putaran()

    assert forwarder._interval_sekarang == 1.0


def test_putaran_gagal_tak_terduga_tidak_menghentikan_forwarder():
    class _KlienMeledak:
        def kuras_sekali(self):
            raise RuntimeError("sengaja gagal")

        def hitung_pending(self):
            return 1

    forwarder = ForwarderAntrean(_KlienMeledak(), interval_detik=1.0, interval_maks_detik=10.0)

    forwarder._satu_putaran()  # tidak boleh melempar keluar

    assert forwarder._interval_sekarang == 2.0


def test_mulai_dan_berhenti_bersih():
    klien = _KlienPalsu(urutan_kuras=[0, 0, 0, 0, 0])
    forwarder = ForwarderAntrean(klien, interval_detik=0.02, interval_maks_detik=0.1)

    forwarder.mulai()
    time.sleep(0.15)
    assert forwarder.is_running is True
    assert klien.panggilan_kuras >= 1

    forwarder.berhenti()

    assert forwarder.is_running is False


def test_mulai_dua_kali_tidak_membuat_thread_kedua():
    klien = _KlienPalsu(urutan_kuras=[0])
    forwarder = ForwarderAntrean(klien, interval_detik=1.0)

    forwarder.mulai()
    thread_pertama = forwarder._thread
    forwarder.mulai()

    assert forwarder._thread is thread_pertama
    forwarder.berhenti()
