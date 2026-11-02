from app.notify.provider import HasilKirim, StatusKirim


class NullProvider:
    """Bawaan. Tidak mengirim apa pun - menandai pesan sebagai terlewat.

    Dipakai sampai anggaran WhatsApp tersedia. Aman dipanggil kapan saja:
    tidak pernah melempar galat, tidak pernah menghubungi layanan luar.
    """

    def send(self, to: str, message: str, idempotency_key: str) -> HasilKirim:
        return HasilKirim(status=StatusKirim.TERLEWAT)
