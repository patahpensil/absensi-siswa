import serial

from app.rfid.base import JalurAlat, PembacaKartuBase


class SerialReader(PembacaKartuBase):
    """Mode serial: membaca UID sebagai baris teks dari port serial."""

    jalur = JalurAlat.SERIAL

    def __init__(
        self,
        *,
        station_id: str,
        port: str,
        baudrate: int = 9600,
        timeout: float = 1.0,
    ) -> None:
        super().__init__(station_id=station_id)
        self._port = port
        self._baudrate = baudrate
        self._timeout = timeout

    def _jalankan(self) -> None:
        terhubung = False
        try:
            with serial.Serial(self._port, self._baudrate, timeout=self._timeout) as koneksi:
                terhubung = True
                self._pancarkan_terhubung()
                while not self._berhenti.is_set():
                    baris = koneksi.readline()
                    if not baris:
                        continue
                    uid_mentah = baris.decode("ascii", errors="ignore").strip()
                    if uid_mentah:
                        self._pancarkan_uid(uid_mentah)
        finally:
            if terhubung:
                self._pancarkan_terputus()
