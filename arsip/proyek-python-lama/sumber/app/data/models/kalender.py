from datetime import date, datetime

from sqlalchemy import Date, DateTime, ForeignKey, String, Text
from sqlalchemy.orm import Mapped, mapped_column

from app.core.timeutil import now_wita
from app.data.base import Base


class EntriKalender(Base):
    """Satu entri kalender akademik (SKILL.md "Struktur data inti": kalender
    - mulai, selesai, keterangan, jenis_siswa, jenis_guru, tahun_ajaran).

    jenis_siswa/jenis_guru kolom string biasa, bukan enum terikat DB -
    nilainya app.core.status.JenisHari, pola yang sama dengan
    Attendance.status_masuk (dua kosakata konseptual berbeda memakai satu
    kolom string, diterjemahkan di lapisan integrasi).

    Menggantikan tabel holidays lama (satu jalur, siswa=guru) sepenuhnya -
    lihat app.core.kalender_service dan SKILL.md "Kalender akademik".
    Diperlakukan setara koreksi massal: tercatat di log audit, ada
    pratinjau dampak (app.core.kalender.Kalender.dampak_perubahan), dan
    bisa dibatalkan (dibatalkan, bukan dihapus - riwayatnya tetap ada).
    """

    __tablename__ = "kalender"

    id: Mapped[int] = mapped_column(primary_key=True)
    mulai: Mapped[date] = mapped_column(Date)
    selesai: Mapped[date] = mapped_column(Date)
    keterangan: Mapped[str] = mapped_column(String(200))
    jenis_siswa: Mapped[str] = mapped_column(String(30))
    jenis_guru: Mapped[str] = mapped_column(String(30))
    tahun_ajaran: Mapped[str] = mapped_column(String(20))
    id_pengguna: Mapped[int] = mapped_column(ForeignKey("users.id"))
    waktu_dibuat: Mapped[datetime] = mapped_column(DateTime, default=now_wita)
    alasan: Mapped[str] = mapped_column(Text)
    dibatalkan: Mapped[bool] = mapped_column(default=False)
    waktu_dibatalkan: Mapped[datetime | None] = mapped_column(DateTime)
    dibatalkan_oleh: Mapped[int | None] = mapped_column(ForeignKey("users.id"))
