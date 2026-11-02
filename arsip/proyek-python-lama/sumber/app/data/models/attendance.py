from datetime import date, datetime
from enum import Enum
from typing import Any

from sqlalchemy import JSON, Date, DateTime, ForeignKey, Index, String, UniqueConstraint
from sqlalchemy import Enum as SAEnum
from sqlalchemy.orm import Mapped, mapped_column

from app.core.timeutil import now_wita
from app.data.base import Base
from app.data.models.enums import JenisPemilik
from app.rfid.base import JalurAlat


class JenisEventScan(str, Enum):
    MASUK = "MASUK"
    PULANG = "PULANG"
    DUPLICATE = "DUPLICATE"
    UNKNOWN_CARD = "UNKNOWN_CARD"
    DI_LUAR_PERIODE = "DI_LUAR_PERIODE"


class AttendanceEvent(Base):
    """Peristiwa scan mentah.

    Baris di sini tidak pernah diubah dan tidak pernah dihapus, bahkan
    ketika hasil pengolahannya di tabel attendance berubah atau dikoreksi.
    """

    __tablename__ = "attendance_events"
    __table_args__ = (Index("ix_attendance_events_waktu_scan", "waktu_scan"),)

    id: Mapped[int] = mapped_column(primary_key=True)
    uid_kartu: Mapped[str] = mapped_column(String(64))
    # UID apa adanya dari alat SEBELUM dinormalisasi (PRD §17: "UID mentah,
    # UID baku"). Nullable karena baris lama (sebelum kolom ini ada) tidak
    # punya nilai jujur untuk diisi - dibiarkan None, tidak dikarang.
    # Server tidak pernah menormalisasi ulang nilai ini (app.rfid.base
    # tetap satu-satunya tempat normalisasi terjadi, PRD §12.3).
    uid_mentah: Mapped[str | None] = mapped_column(String(64))
    # Boleh kosong bila kartu tidak dikenal.
    id_pemilik: Mapped[int | None]
    jenis_pemilik: Mapped[JenisPemilik | None] = mapped_column(SAEnum(JenisPemilik, native_enum=False))
    waktu_scan: Mapped[datetime] = mapped_column(DateTime, default=now_wita)
    id_perangkat: Mapped[int | None] = mapped_column(ForeignKey("devices.id"))
    jenis_event: Mapped[JenisEventScan] = mapped_column(SAEnum(JenisEventScan, native_enum=False))
    metadata_json: Mapped[dict[str, Any] | None] = mapped_column("metadata", JSON)
    # Jalur alat pembaca (PCSC/SERIAL/HID/SIMULATOR) - PRD §17: "peristiwa_scan
    # UID mentah, UID baku, jalur, waktu, hasil". Nullable karena baris lama
    # (sebelum kolom ini ada) tidak punya nilai jujur untuk diisi - dibiarkan
    # None, tidak dikarang (pola sama uid_mentah di atas). Beda tujuan dari
    # id_perangkat (identitas SATU unit alat, belum pernah diisi - lihat
    # README) - jalur soal diagnostik/kesehatan alat per JENIS jalur
    # (PRD §12.5), bukan identitas unit.
    jalur: Mapped[JalurAlat | None] = mapped_column(SAEnum(JalurAlat, native_enum=False))


class Attendance(Base):
    """Hasil olahan absensi: satu baris per orang per tanggal.

    Kalau aturan status berubah, seluruh isi tabel ini bisa dihitung ulang
    dari attendance_events tanpa kehilangan apa pun.
    """

    __tablename__ = "attendance"
    __table_args__ = (
        UniqueConstraint("id_pemilik", "jenis_pemilik", "tanggal", name="uq_attendance_pemilik_tanggal"),
    )

    id: Mapped[int] = mapped_column(primary_key=True)
    id_pemilik: Mapped[int]
    jenis_pemilik: Mapped[JenisPemilik] = mapped_column(SAEnum(JenisPemilik, native_enum=False))
    tanggal: Mapped[date] = mapped_column(Date)
    jam_masuk: Mapped[datetime | None] = mapped_column(DateTime)
    # Hasil scan pagi apa adanya. Nilai dikenal - siswa: HADIR, TERLAMBAT,
    # ALPA, IZIN, SAKIT, DISPENSASI. Guru: HADIR, TERLAMBAT, DINAS_LUAR,
    # IZIN, SAKIT, CUTI (lihat app.core.status). Kolom ini sengaja bukan
    # enum terikat DB - dua kosakata berbeda berbagi satu kolom tergantung
    # jenis_pemilik. Tidak pernah berubah otomatis setelah diisi - lihat
    # status_final untuk status resmi laporan.
    status_masuk: Mapped[str | None] = mapped_column(String(20))
    jam_pulang: Mapped[datetime | None] = mapped_column(DateTime)
    # Status resmi untuk laporan - sama dengan status_masuk kecuali
    # dikoreksi atau dikonversi ALPA karena tidak scan pulang (lihat
    # app.core.mesin_absensi.konversi_alpa_pulang dan AlasanFinal). Kolom
    # terpisah dari status_masuk supaya "tidak pernah datang" tetap bisa
    # dibedakan dari "datang tapi tidak absen pulang" - lihat SKILL.md,
    # "Absensi pulang dan ALPA pulang".
    status_final: Mapped[str | None] = mapped_column(String(20))
    # Kenapa status_final berbeda dari status_masuk - nilai AlasanFinal
    # (app.core.status), kosong bila keduanya sama.
    alasan_final: Mapped[str | None] = mapped_column(String(30))
    # Siswa ditandai "izin pulang cepat" - dikecualikan dari konversi ALPA
    # pulang meski tidak sempat scan pulang.
    izin_pulang_cepat: Mapped[bool] = mapped_column(default=False)
    # Polimorfik: merujuk ke school_schedules.id atau teacher_schedules.id
    # tergantung jenis_pemilik. Tidak ada FK tunggal.
    id_jadwal: Mapped[int | None]
    dikoreksi: Mapped[bool] = mapped_column(default=False)
