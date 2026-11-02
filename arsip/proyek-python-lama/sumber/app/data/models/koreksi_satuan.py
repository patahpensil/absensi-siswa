from datetime import datetime
from typing import Any

from sqlalchemy import JSON, Boolean, DateTime, ForeignKey, Text
from sqlalchemy.orm import Mapped, mapped_column

from app.core.timeutil import now_wita
from app.data.base import Base


class KoreksiSatuan(Base):
    """Satu koreksi status absensi satu siswa (PRD §9.1) - satu baris di
    sini per klik "Simpan". Nilai lama TIDAK PERNAH ditimpa: tersimpan
    permanen di ``nilai_sebelum``, terpisah dari baris ``attendance`` yang
    benar-benar dimutasi (AttendanceRepository.koreksi()).

    Setara "TindakanMassal" (app.data.models.koreksi_massal) tapi untuk
    SATU baris - tidak perlu tabel "baris" terpisah karena selalu persis
    satu baris attendance per koreksi.
    """

    __tablename__ = "koreksi_satuan"

    id: Mapped[int] = mapped_column(primary_key=True)
    id_absensi: Mapped[int] = mapped_column(ForeignKey("attendance.id"))
    id_pengguna: Mapped[int] = mapped_column(ForeignKey("users.id"))
    waktu: Mapped[datetime] = mapped_column(DateTime, default=now_wita)
    nilai_sebelum: Mapped[dict[str, Any]] = mapped_column(JSON)
    nilai_sesudah: Mapped[dict[str, Any]] = mapped_column(JSON)
    alasan: Mapped[str] = mapped_column(Text)
    dibatalkan: Mapped[bool] = mapped_column(Boolean, default=False)
    waktu_dibatalkan: Mapped[datetime | None] = mapped_column(DateTime)
    dibatalkan_oleh: Mapped[int | None] = mapped_column(ForeignKey("users.id"))
