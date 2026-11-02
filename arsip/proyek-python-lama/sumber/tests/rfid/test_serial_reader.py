import threading

import app.rfid.serial_reader as serial_reader_module
from app.rfid.base import JalurAlat
from app.rfid.serial_reader import SerialReader


class _SerialPalsu:
    """Pengganti serial.Serial untuk tes - tidak menyentuh port sungguhan."""

    def __init__(self, port, baudrate, timeout):
        self.port = port
        self._baris = [b"UIDA\n", b"UIDB\n"]

    def __enter__(self):
        return self

    def __exit__(self, exc_type, exc, tb):
        return False

    def readline(self):
        if self._baris:
            return self._baris.pop(0)
        return b""


def test_serial_reader_memancarkan_uid_dari_baris_serial(monkeypatch):
    monkeypatch.setattr(serial_reader_module.serial, "Serial", _SerialPalsu)

    events = []
    dua_uid = threading.Event()

    def pendengar(event):
        events.append(event)
        if len(events) == 2:
            dua_uid.set()

    reader = SerialReader(station_id="SERIAL-1", port="COM9")
    reader.saat_scan(pendengar)
    reader.mulai()
    try:
        assert dua_uid.wait(timeout=2.0)
        assert reader.terhubung() is True
    finally:
        reader.berhenti()

    assert [e.uid for e in events] == ["UIDA", "UIDB"]
    assert all(e.jalur is JalurAlat.SERIAL for e in events)
