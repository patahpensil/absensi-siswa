import threading

import pytest

import app.rfid.pcsc_reader as pcsc_reader_module
from app.rfid.base import JalurAlat
from app.rfid.pcsc_reader import PcscReader, ReaderPcscTidakDitemukan


class _KoneksiPalsu:
    """Pengganti CardConnection pyscard - tidak menyentuh reader sungguhan.

    PcscReader membuat SATU _KoneksiPalsu baru (createConnection()) setiap
    putaran polling - supaya urutan jawaban tetap maju lintas putaran,
    kursor jawabannya disimpan di _ReaderPalsu (dibagikan seluruh koneksi
    dari reader yang sama), bukan di _KoneksiPalsu sendiri."""

    def __init__(self, reader: "_ReaderPalsu"):
        self._reader = reader

    def connect(self) -> None:
        pass

    def transmit(self, apdu):
        return self._reader.jawaban_berikutnya()


class _ReaderPalsu:
    """``jawaban`` adalah antrean (data, sw1, sw2) yang maju satu per
    panggilan transmit(), lintas createConnection(). Kalau habis, jawaban
    terakhir diulang - meniru kartu yang tetap menempel."""

    def __init__(self, nama: str, jawaban: list[tuple[list[int], int, int]]):
        self._nama = nama
        self._jawaban = list(jawaban)
        self._terakhir = self._jawaban[-1] if self._jawaban else ([], 0x6F, 0x00)

    def __str__(self) -> str:
        return self._nama

    def jawaban_berikutnya(self):
        if self._jawaban:
            self._terakhir = self._jawaban.pop(0)
        return self._terakhir

    def createConnection(self) -> _KoneksiPalsu:
        return _KoneksiPalsu(self)


def _tunggu_n_event(events: list, n: int, selesai: threading.Event):
    def pendengar(event):
        events.append(event)
        if len(events) >= n:
            selesai.set()

    return pendengar


def test_pcsc_reader_memancarkan_uid_dari_apdu(monkeypatch):
    reader_palsu = _ReaderPalsu("ACS Reader 0", [([0x04, 0xA2, 0xB3, 0xC4], 0x90, 0x00)])
    monkeypatch.setattr(pcsc_reader_module.smartcard.System, "readers", lambda: [reader_palsu])

    events: list = []
    selesai = threading.Event()

    reader = PcscReader(station_id="PCSC-1", jeda_poll_detik=0.05)
    reader.saat_scan(_tunggu_n_event(events, 1, selesai))
    reader.mulai()
    try:
        assert selesai.wait(timeout=2.0)
        assert reader.terhubung() is True
    finally:
        reader.berhenti()

    assert events[0].uid == "04A2B3C4"
    assert events[0].uid_mentah == "04A2B3C4"
    assert events[0].jalur is JalurAlat.PCSC
    assert events[0].station_id == "PCSC-1"


def test_pcsc_reader_debounce_kartu_menempel_tidak_diulang(monkeypatch):
    """Kartu yang sama dibiarkan menempel (APDU sukses berulang dengan UID
    sama) hanya boleh memancarkan SATU ScanEvent - lihat docstring
    PcscReader soal debounce polling."""
    reader_palsu = _ReaderPalsu(
        "ACS Reader 0",
        [([0x04, 0xA2, 0xB3, 0xC4], 0x90, 0x00)] * 5,
    )
    monkeypatch.setattr(pcsc_reader_module.smartcard.System, "readers", lambda: [reader_palsu])

    events: list = []
    reader = PcscReader(station_id="PCSC-1", jeda_poll_detik=0.02)
    reader.saat_scan(lambda e: events.append(e))
    reader.mulai()
    try:
        threading.Event().wait(0.3)
    finally:
        reader.berhenti()

    assert len(events) == 1


def test_pcsc_reader_kartu_diangkat_lalu_ditap_lagi_memancarkan_ulang(monkeypatch):
    urutan = [
        ([0x04, 0xA2, 0xB3, 0xC4], 0x90, 0x00),  # tap 1
        ([], 0x6F, 0x00),  # kartu diangkat
        ([0x04, 0xA2, 0xB3, 0xC4], 0x90, 0x00),  # tap 2 (kartu sama)
    ]
    reader_palsu = _ReaderPalsu("ACS Reader 0", urutan)
    monkeypatch.setattr(pcsc_reader_module.smartcard.System, "readers", lambda: [reader_palsu])

    events: list = []
    selesai = threading.Event()
    reader = PcscReader(station_id="PCSC-1", jeda_poll_detik=0.03)
    reader.saat_scan(_tunggu_n_event(events, 2, selesai))
    reader.mulai()
    try:
        assert selesai.wait(timeout=2.0)
    finally:
        reader.berhenti()

    assert [e.uid for e in events] == ["04A2B3C4", "04A2B3C4"]


def test_pcsc_reader_pilih_reader_by_nama(monkeypatch):
    reader_a = _ReaderPalsu("Reader A", [([0xAA], 0x90, 0x00)])
    reader_b = _ReaderPalsu("Reader B", [([0xBB], 0x90, 0x00)])
    monkeypatch.setattr(pcsc_reader_module.smartcard.System, "readers", lambda: [reader_a, reader_b])

    events: list = []
    selesai = threading.Event()
    reader = PcscReader(station_id="PCSC-B", nama_reader="Reader B", jeda_poll_detik=0.05)
    reader.saat_scan(_tunggu_n_event(events, 1, selesai))
    reader.mulai()
    try:
        assert selesai.wait(timeout=2.0)
    finally:
        reader.berhenti()

    assert events[0].uid == "BB"


def test_pcsc_reader_nama_tidak_ditemukan_gagal_terhubung(monkeypatch):
    reader_a = _ReaderPalsu("Reader A", [])
    monkeypatch.setattr(pcsc_reader_module.smartcard.System, "readers", lambda: [reader_a])

    reader = PcscReader(station_id="PCSC-X", nama_reader="Reader Tak Ada")
    with pytest.raises(ReaderPcscTidakDitemukan):
        reader._cari_reader()


def test_pcsc_reader_tidak_ada_reader_sama_sekali(monkeypatch):
    monkeypatch.setattr(pcsc_reader_module.smartcard.System, "readers", lambda: [])

    reader = PcscReader(station_id="PCSC-X")
    with pytest.raises(ReaderPcscTidakDitemukan):
        reader._cari_reader()
