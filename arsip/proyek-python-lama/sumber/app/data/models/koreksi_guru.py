from datetime import datetime
from typing import Any

from sqlalchemy import JSON, Boolean, DateTime, ForeignKey, String, Text
from sqlalchemy.orm import Mapped, mapped_column

from app.core.timeutil import now_wita
from app.data.base import Base


class KoreksiGuru(Base):
    """Satu koreksi status absensi satu guru (PRD §6) - satu baris di
    sini per klik "Simpan". Nilai lama TIDAK PERNAH ditimpa: tersimpan
    permanen di ``nilai_sebelum``, terpisah dari baris ``attendance`` yang
    benar-benar dimutasi (AttendanceRepository.koreksi()).

    Setara "KoreksiSatuan" (app.data.models.koreksi_satuan, siswa) tapi
    untuk guru - sengaja tabel terpisah, bukan menambah kolom
    jenis_pemilik ke koreksi_satuan, supaya kedua alur (siswa vs guru,
    kewenangan dan target status berbeda) tidak bercampur di satu tabel.

    ``lampiran``: path RELATIF (bukan isi berkas) ke foto surat tugas/
    keterangan (PRD §6: "sebaiknya disertai lampiran") - disimpan lewat
    app.server.lampiran_storage di direktori server, None kalau admin
    tidak melampirkan apa pun (rekomendasi PRD, bukan kewajiban tegas).
    """

    __tablename__ = "koreksi_guru"

    id: Mapped[int] = mapped_column(primary_key=True)
    id_absensi: Mapped[int] = mapped_column(ForeignKey("attendance.id"))
    id_pengguna: Mapped[int] = mapped_column(ForeignKey("users.id"))
    waktu: Mapped[datetime] = mapped_column(DateTime, default=now_wita)
    nilai_sebelum: Mapped[dict[str, Any]] = mapped_column(JSON)
    nilai_sesudah: Mapped[dict[str, Any]] = mapped_column(JSON)
    alasan: Mapped[str] = mapped_column(Text)
    lampiran: Mapped[str | None] = mapped_column(String(500))
    dibatalkan: Mapped[bool] = mapped_column(Boolean, default=False)
    waktu_dibatalkan: Mapped[datetime | None] = mapped_column(DateTime)
    dibatalkan_oleh: Mapped[int | None] = mapped_column(ForeignKey("users.id"))
