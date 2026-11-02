import logging

from app.notify.provider import HasilKirim, StatusKirim

_log = logging.getLogger("app.notify.log_provider")


class LogProvider:
    """Menulis pesan ke log, bukan benar-benar mengirim.

    Untuk pengujian jalur notifikasi tanpa biaya dan tanpa risiko
    mengirim pesan sungguhan ke nomor orang tua/wali.
    """

    def send(self, to: str, message: str, idempotency_key: str) -> HasilKirim:
        _log.info("WHATSAPP [%s] -> %s: %s", idempotency_key, to, message)
        return HasilKirim(status=StatusKirim.TERKIRIM)
