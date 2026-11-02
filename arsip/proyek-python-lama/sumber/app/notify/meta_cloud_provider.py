from app.notify.provider import HasilKirim


class MetaCloudProvider:
    """Kerangka untuk WhatsApp Cloud API resmi.

    Boleh belum lengkap sampai sekolah siap - jangan dipilih lewat
    app/config sampai kredensialnya benar-benar ada.
    """

    def __init__(self, *, access_token: str | None = None, phone_number_id: str | None = None) -> None:
        self._access_token = access_token
        self._phone_number_id = phone_number_id

    def send(self, to: str, message: str, idempotency_key: str) -> HasilKirim:
        raise NotImplementedError("MetaCloudProvider belum diimplementasikan - menunggu anggaran WhatsApp")
