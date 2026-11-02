import logging

import smartcard.System

from app.rfid.base import JalurAlat, PembacaKartuBase

_log = logging.getLogger(__name__)

#: APDU baku "GET DATA" untuk UID kartu (dipakai hampir seluruh reader
#: PC/SC dwi-frekuensi, termasuk ACR122U dan sejenisnya).
_APDU_BACA_UID = [0xFF, 0xCA, 0x00, 0x00, 0x00]


class ReaderPcscTidakDitemukan(Exception):
    """Tidak ada reader PC/SC yang cocok terdeteksi sistem operasi saat
    pengemudi dimulai - baik karena belum terpasang, driver belum
    terpasang, maupun layanan Smart Card Resource Manager tidak berjalan."""


class PcscReader(PembacaKartuBase):
    """Mode PC/SC (pyscard) - membaca UID lewat APDU GET DATA standar
    (FF CA 00 00 00), didukung hampir seluruh reader dwi-frekuensi PC/SC
    (ACR122U dan sejenisnya).

    Reader dipilih lewat nama persis yang dikembalikan sistem operasi
    (``nama_reader``); kosongkan untuk memilih reader pertama yang
    terdeteksi (dipakai kalau hanya satu unit PC/SC terpasang di PC itu).

    Debounce "kartu sama masih menempel" dilakukan DI SINI - beda dari
    anti-duplikat global lintas-jalur yang sengaja TIDAK dilakukan di
    app/rfid/ (lihat catatan app.rfid.base) - karena ini murni artefak
    polling perangkat keras: satu tap fisik yang dibiarkan menempel akan
    terbaca ulang setiap putaran polling APDU, padahal SerialReader/
    HidReader tidak pernah menghadapi ini (satu baris/satu ketikan = satu
    tap, sudah didebounce firmware alatnya sebelum sampai ke pengemudi
    ini). Begitu kartu diangkat dari antena (transmit gagal), debounce
    direset - tap berikutnya, kartu sama atau beda, tetap terkirim
    sebagai ScanEvent baru. Ini TIDAK menggantikan anti-duplikat global
    10 detik (app.core.mesin_absensi.scan_duplikat) - keduanya bekerja
    di lapisan yang berbeda untuk masalah yang berbeda.
    """

    jalur = JalurAlat.PCSC

    def __init__(
        self,
        *,
        station_id: str,
        nama_reader: str | None = None,
        jeda_poll_detik: float = 0.3,
    ) -> None:
        super().__init__(station_id=station_id)
        self._nama_reader = nama_reader
        self._jeda_poll_detik = jeda_poll_detik

    def _cari_reader(self):
        try:
            daftar = smartcard.System.readers()
        except Exception as exc:  # noqa: BLE001 - lihat docstring ReaderPcscTidakDitemukan
            raise ReaderPcscTidakDitemukan(
                f"Gagal mengakses subsistem PC/SC: {exc}"
            ) from exc
        if not daftar:
            raise ReaderPcscTidakDitemukan("Tidak ada reader PC/SC terdeteksi")
        if self._nama_reader is None:
            return daftar[0]
        for reader in daftar:
            if str(reader) == self._nama_reader:
                return reader
        raise ReaderPcscTidakDitemukan(f"Reader '{self._nama_reader}' tidak ditemukan")

    def _jalankan(self) -> None:
        reader = self._cari_reader()
        self._pancarkan_terhubung()
        uid_terakhir: str | None = None
        try:
            while not self._berhenti.is_set():
                try:
                    koneksi = reader.createConnection()
                    koneksi.connect()
                    data, sw1, sw2 = koneksi.transmit(_APDU_BACA_UID)
                    if sw1 == 0x90 and sw2 == 0x00 and data:
                        uid_mentah = "".join(f"{byte:02X}" for byte in data)
                        if uid_mentah != uid_terakhir:
                            self._pancarkan_uid(uid_mentah)
                            uid_terakhir = uid_mentah
                    else:
                        uid_terakhir = None
                except Exception:  # noqa: BLE001 - kartu tak ada/terangkat, pyscard melempar beragam tipe galat
                    uid_terakhir = None
                self._berhenti.wait(self._jeda_poll_detik)
        finally:
            self._pancarkan_terputus()
