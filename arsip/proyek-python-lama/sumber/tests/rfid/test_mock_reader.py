import threading

from app.rfid.base import JalurAlat
from app.rfid.mock_reader import MockReader


def test_mock_reader_memancarkan_uid_terjadwal_lalu_terputus():
    events = []
    dua_uid = threading.Event()

    def pendengar(event):
        events.append(event)
        if len(events) == 2:
            dua_uid.set()

    # jeda_detik bawaan 0.0 - thread bisa memancarkan seluruh uid_terjadwal
    # dan langsung berhenti sebelum baris ini sempat mengintip status
    # terhubung() di tengah jalan, jadi tesnya hanya memeriksa status
    # akhir setelah berhenti() (yang menunggu thread selesai).
    reader = MockReader(uid_terjadwal=["UID1", "UID2"])
    reader.saat_scan(pendengar)
    reader.mulai()
    try:
        assert dua_uid.wait(timeout=2.0)
    finally:
        reader.berhenti()

    assert reader.terhubung() is False
    assert [e.uid for e in events] == ["UID1", "UID2"]
    assert all(e.waktu is not None for e in events)
    assert all(e.station_id == "MOCK" for e in events)
    assert all(e.jalur is JalurAlat.SIMULATOR for e in events)


def test_pancarkan_uid_manual_tidak_perlu_thread_berjalan():
    events = []
    reader = MockReader(uid_terjadwal=[])
    reader.saat_scan(events.append)

    reader.pancarkan_uid_manual("UID1")
    reader.pancarkan_uid_manual("UID2")

    assert [e.uid for e in events] == ["UID1", "UID2"]


def test_pendengar_yang_gagal_tidak_menghentikan_pembaca():
    def pendengar_rusak(event):
        raise RuntimeError("sengaja gagal")

    events = []
    satu_uid = threading.Event()

    def pendengar_baik(event):
        events.append(event)
        satu_uid.set()

    reader = MockReader(uid_terjadwal=["UID1"])
    reader.saat_scan(pendengar_rusak)
    reader.saat_scan(pendengar_baik)
    reader.mulai()
    try:
        assert satu_uid.wait(timeout=2.0)
    finally:
        reader.berhenti()

    assert [e.uid for e in events] == ["UID1"]
