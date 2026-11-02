from dataclasses import dataclass
from enum import Enum
from typing import Protocol


class StatusKirim(str, Enum):
    """Hasil satu percobaan pengiriman lewat provider."""

    TERKIRIM = "TERKIRIM"
    GAGAL = "GAGAL"
    TERLEWAT = "TERLEWAT"  # provider sengaja tidak mencoba mengirim (mis. NullProvider)


@dataclass(frozen=True)
class HasilKirim:
    status: StatusKirim
    pesan_galat: str | None = None
    id_pesan_provider: str | None = None


class WhatsAppProvider(Protocol):
    """Antarmuka standar penyedia WhatsApp - dibuat agar berpindah layanan
    nanti tidak menuntut penulisan ulang aplikasi.
    """

    def send(self, to: str, message: str, idempotency_key: str) -> HasilKirim: ...
