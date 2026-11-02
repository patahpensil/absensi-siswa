from app.rfid.base import JalurAlat, PembacaKartuBase


class MockReader(PembacaKartuBase):
    """SimulatorReader - memancarkan UID palsu tanpa perangkat fisik apa pun.

    Dipakai untuk menguji seluruh modul rfid, dan nanti lapisan di atasnya,
    tanpa pembaca kartu sungguhan; juga untuk pengembangan dan pelatihan di
    PC yang belum dipasangi alat. uid_terjadwal dipancarkan berurutan saat
    mulai(); pancarkan_uid_manual() tersedia untuk memicu satu scan langsung
    dari kode tes, tanpa bergantung pada timing thread. Dikecualikan dari
    penguncian jalur alat (lihat periksa_kesesuaian_jalur di app.rfid.base).
    """

    jalur = JalurAlat.SIMULATOR

    def __init__(
        self,
        *,
        station_id: str = "MOCK",
        uid_terjadwal: list[str] | None = None,
        jeda_detik: float = 0.0,
    ) -> None:
        super().__init__(station_id=station_id)
        self._uid_terjadwal = list(uid_terjadwal or [])
        self._jeda_detik = jeda_detik

    def pancarkan_uid_manual(self, uid_mentah: str) -> None:
        self._pancarkan_uid(uid_mentah)

    def _jalankan(self) -> None:
        self._pancarkan_terhubung()
        try:
            for uid_mentah in self._uid_terjadwal:
                if self._berhenti.is_set():
                    break
                self._pancarkan_uid(uid_mentah)
                if self._jeda_detik:
                    self._berhenti.wait(self._jeda_detik)
        finally:
            self._pancarkan_terputus()
