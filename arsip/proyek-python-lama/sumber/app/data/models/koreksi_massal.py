from datetime import datetime
from typing import Any

from sqlalchemy import JSON, Boolean, DateTime, ForeignKey, Integer, String, Text
from sqlalchemy.orm import Mapped, mapped_column

from app.core.timeutil import now_wita
from app.data.base import Base


class TindakanMassal(Base):
    """Satu tindakan koreksi massal - satu baris di sini per klik "Jalankan",
    berapa pun baris attendance yang terdampak (SKILL.md "Koreksi massal":
    "Satu entri log audit induk berisi penyaring dan jumlah baris - bukan
    ribuan entri").

    jenis_tindakan dan status_baru sengaja kolom string biasa, bukan enum
    terikat DB - nilainya berasal dari app.core.koreksi_massal.JenisTindakanMassal
    dan StatusSiswa, tapi model data ini tidak mengimpor app.core (pola yang
    sama dengan Attendance.status_masuk).

    Detail baris yang terdampak (untuk keperluan "batalkan tindakan")
    tersimpan di TindakanMassalBaris, bukan di sini dan bukan di audit_logs.
    """

    __tablename__ = "tindakan_massal"

    id: Mapped[int] = mapped_column(primary_key=True)
    id_pengguna: Mapped[int] = mapped_column(ForeignKey("users.id"))
    waktu: Mapped[datetime] = mapped_column(DateTime, default=now_wita)
    jenis_tindakan: Mapped[str] = mapped_column(String(40))
    # Hanya terisi untuk jenis_tindakan UBAH_STATUS.
    status_baru: Mapped[str | None] = mapped_column(String(20))
    penyaring: Mapped[dict[str, Any]] = mapped_column(JSON)
    jumlah_baris: Mapped[int] = mapped_column(Integer)
    alasan: Mapped[str] = mapped_column(Text)
    dibatalkan: Mapped[bool] = mapped_column(Boolean, default=False)
    waktu_dibatalkan: Mapped[datetime | None] = mapped_column(DateTime)
    dibatalkan_oleh: Mapped[int | None] = mapped_column(ForeignKey("users.id"))


class TindakanMassalBaris(Base):
    """Nilai sebelum/sesudah SATU baris attendance yang terdampak satu
    TindakanMassal.

    Ini "detail" yang sengaja tidak ditulis ke audit_logs (lihat docstring
    TindakanMassal) - dipakai untuk membatalkan tindakan (mengembalikan
    nilai_sebelum), bukan untuk ditampilkan satu per satu di layar riwayat.
    """

    __tablename__ = "tindakan_massal_baris"

    id: Mapped[int] = mapped_column(primary_key=True)
    id_tindakan_massal: Mapped[int] = mapped_column(ForeignKey("tindakan_massal.id"))
    id_absensi: Mapped[int] = mapped_column(ForeignKey("attendance.id"))
    nilai_sebelum: Mapped[dict[str, Any]] = mapped_column(JSON)
    nilai_sesudah: Mapped[dict[str, Any]] = mapped_column(JSON)
