from app.rfid.base import JalurAlat, PembacaKartu
from app.rfid.hid_reader import HidReader
from app.rfid.mock_reader import MockReader
from app.rfid.pcsc_reader import PcscReader
from app.rfid.serial_reader import SerialReader


def buat_pembaca(
    *,
    jalur: JalurAlat,
    station_id: str,
    port: str | None = None,
    baudrate: int = 9600,
    nama_reader: str | None = None,
) -> PembacaKartu:
    """Bangun instance pembaca sesuai jalur alat dari pengaturan (devices.jalur).

    Jalur tidak pernah ditentukan tetap di titik pemanggilan - selalu lewat
    parameter ini, yang nilainya berasal dari data perangkat.
    """
    if jalur == JalurAlat.SERIAL:
        if not port:
            raise ValueError("Jalur SERIAL memerlukan port")
        return SerialReader(station_id=station_id, port=port, baudrate=baudrate)
    if jalur == JalurAlat.HID:
        return HidReader(station_id=station_id)
    if jalur == JalurAlat.SIMULATOR:
        return MockReader(station_id=station_id)
    if jalur == JalurAlat.PCSC:
        return PcscReader(station_id=station_id, nama_reader=nama_reader)
    raise ValueError(f"Jalur alat tidak dikenal: {jalur}")
