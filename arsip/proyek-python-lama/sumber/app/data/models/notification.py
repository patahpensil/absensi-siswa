from datetime import datetime
from enum import Enum

from sqlalchemy import DateTime, ForeignKey, Index, String, Text, UniqueConstraint
from sqlalchemy import Enum as SAEnum
from sqlalchemy.orm import Mapped, mapped_column

from app.data.base import Base


class JenisNotifikasi(str, Enum):
    """Empat template terpisah (PRD §13.1) - menggantikan MASUK/PULANG/ALPA
    generik yang lama (utang tercatat di README, ditutup slice ini).

    Waktu kirim masing-masing (bukan diatur di sini, murni dokumentasi):
    TERLAMBAT segera setelah absensi tercatat, ALPA_PAGI 09:15, ALPA_PULANG
    60 menit setelah cutoff pulang, REKAP sesuai pengaturan. Slice ini HANYA
    membangun struktur (jenis, template, antrean, mode uji) - pemicu nyata
    yang benar-benar memanggil NotificationRepository.antrekan() dari
    app/core/engine.py/scheduler belum disambungkan (lihat README, "Utang
    aktif").
    """

    TERLAMBAT = "TERLAMBAT"
    ALPA_PAGI = "ALPA_PAGI"
    ALPA_PULANG = "ALPA_PULANG"
    REKAP = "REKAP"


class StatusNotifikasi(str, Enum):
    PENDING = "PENDING"
    SENDING = "SENDING"
    SENT = "SENT"
    FAILED = "FAILED"
    CANCELLED = "CANCELLED"


class NotificationQueue(Base):
    """Antrean notifikasi WhatsApp.

    Dibuat dalam transaksi terpisah dari penyimpanan absensi - kegagalan di
    sini tidak boleh menggagalkan atau membatalkan absensi yang sudah
    tersimpan.
    """

    __tablename__ = "notification_queue"
    __table_args__ = (Index("ix_notification_queue_status_waktu_dijadwalkan", "status", "waktu_dijadwalkan"),)

    id: Mapped[int] = mapped_column(primary_key=True)
    id_absensi: Mapped[int] = mapped_column(ForeignKey("attendance.id"))
    id_penerima: Mapped[int | None] = mapped_column(ForeignKey("guardians.id"))
    nomor_tujuan: Mapped[str] = mapped_column(String(20))
    isi_pesan: Mapped[str] = mapped_column(Text)
    # Template yang dipakai merakit isi_pesan - boleh kosong untuk pesan
    # yang belum/tidak lewat template. isi_pesan sendiri tetap teks jadi
    # yang tersimpan permanen; kolom ini hanya menjawab "template mana
    # yang dipakai", termasuk versinya lewat message_templates.versi.
    id_template: Mapped[int | None] = mapped_column(ForeignKey("message_templates.id"))
    jenis: Mapped[JenisNotifikasi] = mapped_column(SAEnum(JenisNotifikasi, native_enum=False))
    status: Mapped[StatusNotifikasi] = mapped_column(
        SAEnum(StatusNotifikasi, native_enum=False), default=StatusNotifikasi.PENDING
    )
    kunci_idempotensi: Mapped[str] = mapped_column(String(100), unique=True)
    # waktu_kejadian dan waktu_kirim wajib terpisah - laporan harus tetap
    # menunjukkan kapan siswa scan meski pesannya baru terkirim belakangan.
    waktu_kejadian: Mapped[datetime] = mapped_column(DateTime)
    waktu_dijadwalkan: Mapped[datetime | None] = mapped_column(DateTime)
    waktu_kirim: Mapped[datetime | None] = mapped_column(DateTime)
    jumlah_percobaan: Mapped[int] = mapped_column(default=0)
    pesan_galat: Mapped[str | None] = mapped_column(Text)


class MessageTemplate(Base):
    """Template pesan notifikasi.

    Versi disimpan agar terlihat jelas template mana yang dipakai pada
    pesan lama.
    """

    __tablename__ = "message_templates"
    __table_args__ = (UniqueConstraint("jenis", "versi", name="uq_message_templates_jenis_versi"),)

    id: Mapped[int] = mapped_column(primary_key=True)
    jenis: Mapped[JenisNotifikasi] = mapped_column(SAEnum(JenisNotifikasi, native_enum=False))
    versi: Mapped[int] = mapped_column(default=1)
    konten: Mapped[str] = mapped_column(Text)
    aktif: Mapped[bool] = mapped_column(default=True)
