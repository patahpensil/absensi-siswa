import pytest

from app.rfid.base import JalurAlat
from app.rfid.factory import buat_pembaca
from app.rfid.hid_reader import HidReader
from app.rfid.mock_reader import MockReader
from app.rfid.pcsc_reader import PcscReader
from app.rfid.serial_reader import SerialReader


def test_buat_pembaca_serial_dari_pengaturan():
    pembaca = buat_pembaca(jalur=JalurAlat.SERIAL, station_id="R1", port="COM3")
    assert isinstance(pembaca, SerialReader)


def test_buat_pembaca_hid_dari_pengaturan():
    pembaca = buat_pembaca(jalur=JalurAlat.HID, station_id="R1")
    assert isinstance(pembaca, HidReader)


def test_buat_pembaca_simulator_dari_pengaturan():
    pembaca = buat_pembaca(jalur=JalurAlat.SIMULATOR, station_id="R1")
    assert isinstance(pembaca, MockReader)


def test_buat_pembaca_serial_tanpa_port_gagal():
    with pytest.raises(ValueError):
        buat_pembaca(jalur=JalurAlat.SERIAL, station_id="R1")


def test_buat_pembaca_pcsc_dari_pengaturan():
    pembaca = buat_pembaca(jalur=JalurAlat.PCSC, station_id="R1")
    assert isinstance(pembaca, PcscReader)


def test_buat_pembaca_pcsc_meneruskan_nama_reader():
    pembaca = buat_pembaca(jalur=JalurAlat.PCSC, station_id="R1", nama_reader="ACS Reader 0")
    assert isinstance(pembaca, PcscReader)
    assert pembaca._nama_reader == "ACS Reader 0"
