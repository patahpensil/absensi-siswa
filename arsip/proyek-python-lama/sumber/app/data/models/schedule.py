from datetime import date, time

from sqlalchemy import Date, Time
from sqlalchemy.orm import Mapped, mapped_column

from app.data.base import Base


class SchoolSchedule(Base):
    """Jadwal masuk/pulang siswa per hari, dengan tanggal mulai berlaku.

    Perhitungan status selalu mencari baris yang berlaku pada tanggal
    absensi - jangan pernah memakai baris terbaru begitu saja. Nilai jam
    masih menunggu keputusan sekolah (lihat PRD Bagian 34 butir 1); tabel
    ini sengaja belum diisi.
    """

    __tablename__ = "school_schedules"

    id: Mapped[int] = mapped_column(primary_key=True)
    hari: Mapped[int]  # 1 = Senin ... 7 = Minggu
    jam_masuk: Mapped[time] = mapped_column(Time)
    batas_terlambat: Mapped[time] = mapped_column(Time)
    # Cutoff ALPA pagi - siswa yang belum scan sampai jam ini ditetapkan ALPA.
    cutoff_alpa: Mapped[time] = mapped_column(Time)
    # Awal jendela absen pulang. jam_pulang di bawah ini adalah akhir
    # jendela sekaligus cutoff ALPA pulang (lihat JadwalHarian.dalam_jendela_pulang).
    pulang_mulai: Mapped[time] = mapped_column(Time)
    jam_pulang: Mapped[time] = mapped_column(Time)
    berlaku_mulai: Mapped[date] = mapped_column(Date)
    berlaku_sampai: Mapped[date | None] = mapped_column(Date)


class TeacherSchedule(Base):
    """Jadwal masuk/pulang guru.

    TODO(PRD Bagian 34 butir 3): toleransi keterlambatan guru belum
    ditetapkan. Struktur disiapkan sejak awal, belum ada seed data.
    """

    __tablename__ = "teacher_schedules"

    id: Mapped[int] = mapped_column(primary_key=True)
    hari: Mapped[int]
    jam_masuk: Mapped[time] = mapped_column(Time)
    batas_terlambat: Mapped[time] = mapped_column(Time)
    jam_pulang: Mapped[time] = mapped_column(Time)
    berlaku_mulai: Mapped[date] = mapped_column(Date)
    berlaku_sampai: Mapped[date | None] = mapped_column(Date)
