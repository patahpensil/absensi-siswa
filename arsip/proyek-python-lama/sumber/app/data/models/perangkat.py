from datetime import datetime

from sqlalchemy import Boolean, DateTime, ForeignKey, Integer, String, Text
from sqlalchemy.orm import Mapped, mapped_column

from app.core.timeutil import now_wita
from app.data.base import Base


class IzinDaruratAlat(Base):
    """Izin darurat alat RFID (PRD §12.4): mengizinkan sementara alat jalur
    lain berjalan di satu ``station_id`` yang sedang dikunci ke jalur
    berbeda (lihat app.rfid.base.periksa_kesesuaian_jalur, parameter
    ``izin_darurat_station``).

    Satu baris per pemberian izin - TIDAK ditimpa. ``waktu_berakhir``
    dimutasi hanya lewat perpanjangan (kepsek/admin saja, lihat
    app.core.status.PERAN_PERPANJANG_IZIN_DARURAT_ALAT); ``dicabut``
    dimutasi hanya lewat pencabutan manual sebelum kedaluwarsa alami.
    Baik perpanjangan maupun pencabutan TIDAK membuat baris baru - jejak
    siapa/kapan sudah tercatat lewat audit_logs terpisah (PRD §15.2:
    "pengaktifan izin darurat perangkat" wajib teraudit), tabel ini hanya
    menyimpan status yang sedang berlaku untuk dicek
    periksa_kesesuaian_jalur()/banner MODE DARURAT lobi.
    """

    __tablename__ = "izin_darurat_alat"

    id: Mapped[int] = mapped_column(primary_key=True)
    #: Cocok dengan app.rfid.base.PembacaKartuBase.station_id (string
    #: bebas) - bukan ID tabel perangkat, karena tidak ada registry
    #: perangkat terpusat (lihat README, catatan Device/ModePerangkat).
    station_id: Mapped[str] = mapped_column(String(100), index=True)
    id_pengguna_pemberi: Mapped[int] = mapped_column(ForeignKey("users.id"))
    waktu_mulai: Mapped[datetime] = mapped_column(DateTime, default=now_wita)
    waktu_berakhir: Mapped[datetime] = mapped_column(DateTime)
    alasan: Mapped[str] = mapped_column(Text)
    dicabut: Mapped[bool] = mapped_column(Boolean, default=False)
    id_pengguna_pencabut: Mapped[int | None] = mapped_column(ForeignKey("users.id"))
    waktu_dicabut: Mapped[datetime | None] = mapped_column(DateTime)


class TelemetriLobi(Base):
    """Telemetri operasional terakhir SATU PC lobi (PRD §14.1: "jumlah
    scan yang masih mengantre di PC lobi"), dilaporkan berkala oleh
    ``app.ui.lobi.klien_scan.KlienScan.laporkan_telemetri`` lewat
    ``POST /perangkat/telemetri``.

    SATU baris per ``station_id``, DITIMPA (upsert) setiap laporan baru -
    bukan log historis. Dashboard cuma butuh keadaan TERKINI; kalau
    ``waktu_lapor`` sudah lama, itu sendiri jadi sinyal ("klien berhenti
    lapor"), tidak perlu riwayat lengkap untuk keperluan ini.
    """

    __tablename__ = "telemetri_lobi"

    station_id: Mapped[str] = mapped_column(String(100), primary_key=True)
    jumlah_pending: Mapped[int] = mapped_column(Integer)
    waktu_lapor: Mapped[datetime] = mapped_column(DateTime, default=now_wita)
    id_pengguna: Mapped[int] = mapped_column(ForeignKey("users.id"))
