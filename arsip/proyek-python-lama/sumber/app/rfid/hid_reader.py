import queue

from app.rfid.base import JalurAlat, PembacaKartuBase


class HidReader(PembacaKartuBase):
    """Mode HID keyboard wedge - HANYA boleh dipasang di satu jalur (lihat
    TerlaluBanyakHid di app.rfid.base).

    Pembaca jenis ini "mengetikkan" UID ke bidang yang sedang fokus,
    diakhiri Enter - itu terjadi di level OS/antarmuka, bukan di sini.
    Lapisan antarmuka (belum dibangun di modul ini) memanggil
    terima_karakter() untuk setiap tombol yang masuk selagi fokus scan
    aktif. Thread di kelas ini yang menyusun karakter jadi UID dan
    memancarkan event - jadi perakitan tidak pernah terjadi di thread
    antarmuka.
    """

    jalur = JalurAlat.HID

    def __init__(self, *, station_id: str) -> None:
        super().__init__(station_id=station_id)
        self._antrean_karakter: queue.Queue[str] = queue.Queue()

    def terima_karakter(self, karakter: str) -> None:
        """Dipanggil dari lapisan antarmuka untuk setiap tombol yang masuk."""
        self._antrean_karakter.put(karakter)

    def _jalankan(self) -> None:
        self._pancarkan_terhubung()
        buffer: list[str] = []
        try:
            while not self._berhenti.is_set():
                try:
                    karakter = self._antrean_karakter.get(timeout=0.2)
                except queue.Empty:
                    continue
                if karakter in ("\n", "\r"):
                    uid_mentah = "".join(buffer).strip()
                    buffer.clear()
                    if uid_mentah:
                        self._pancarkan_uid(uid_mentah)
                else:
                    buffer.append(karakter)
        finally:
            self._pancarkan_terputus()
