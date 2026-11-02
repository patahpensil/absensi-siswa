import threading

from app.rfid.base import JalurAlat
from app.rfid.hid_reader import HidReader


def test_hid_reader_menyusun_uid_dari_karakter_dan_enter():
    events = []
    diterima = threading.Event()

    def pendengar(event):
        events.append(event)
        diterima.set()

    reader = HidReader(station_id="HID-1")
    reader.saat_scan(pendengar)
    reader.mulai()
    try:
        for karakter in "UID123\n":
            reader.terima_karakter(karakter)

        assert diterima.wait(timeout=2.0)
        assert reader.terhubung() is True
    finally:
        reader.berhenti()

    assert len(events) == 1
    assert events[0].uid == "UID123"
    assert events[0].uid_mentah == "UID123"
    assert events[0].jalur is JalurAlat.HID
    assert events[0].station_id == "HID-1"


def test_hid_reader_menangani_beberapa_scan_berurutan():
    events = []
    dua_scan = threading.Event()

    def pendengar(event):
        events.append(event)
        if len(events) == 2:
            dua_scan.set()

    reader = HidReader(station_id="HID-1")
    reader.saat_scan(pendengar)
    reader.mulai()
    try:
        for karakter in "AAA\r\nBBB\r\n":
            reader.terima_karakter(karakter)

        assert dua_scan.wait(timeout=2.0)
    finally:
        reader.berhenti()

    # "AAA"/"BBB" - panjang ganjil, normalisasi_uid menambah "0" di depan.
    assert [e.uid for e in events] == ["0AAA", "0BBB"]
    assert [e.uid_mentah for e in events] == ["AAA", "BBB"]
