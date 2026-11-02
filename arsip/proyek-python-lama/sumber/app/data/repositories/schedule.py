from datetime import date, time

from sqlalchemy import select

from app.data.models.enums import JenisPemilik
from app.data.models.schedule import SchoolSchedule, TeacherSchedule
from app.data.repositories.base import BaseRepository

_MODEL_PER_JENIS: dict[JenisPemilik, type[SchoolSchedule] | type[TeacherSchedule]] = {
    JenisPemilik.SISWA: SchoolSchedule,
    JenisPemilik.GURU: TeacherSchedule,
}


class ScheduleRepository(BaseRepository):
    """Satu repository untuk school_schedules dan teacher_schedules.

    Kedua tabel berstruktur sama; jenis_pemilik menentukan tabel mana yang
    dipakai, mengikuti pola polimorfik yang sama dengan rfid_cards dan
    attendance.
    """

    def tambah(
        self,
        *,
        jenis_pemilik: JenisPemilik,
        hari: int,
        jam_masuk: time,
        batas_terlambat: time,
        jam_pulang: time,
        berlaku_mulai: date,
        cutoff_alpa: time | None = None,
        pulang_mulai: time | None = None,
        berlaku_sampai: date | None = None,
    ) -> SchoolSchedule | TeacherSchedule:
        """cutoff_alpa dan pulang_mulai hanya berlaku untuk jenis SISWA -
        TeacherSchedule tidak punya kolom ini (guru tidak mengenal ALPA
        maupun jendela pulang terpisah, lihat SKILL.md "Absensi guru").
        """
        model = _MODEL_PER_JENIS[jenis_pemilik]
        kolom_tambahan = {}
        if model is SchoolSchedule:
            kolom_tambahan = {
                "cutoff_alpa": cutoff_alpa if cutoff_alpa is not None else batas_terlambat,
                "pulang_mulai": pulang_mulai if pulang_mulai is not None else jam_pulang,
            }
        jadwal = model(
            hari=hari,
            jam_masuk=jam_masuk,
            batas_terlambat=batas_terlambat,
            jam_pulang=jam_pulang,
            berlaku_mulai=berlaku_mulai,
            berlaku_sampai=berlaku_sampai,
            **kolom_tambahan,
        )
        self._session.add(jadwal)
        self._commit()
        return jadwal

    def jadwal_berlaku(
        self, *, jenis_pemilik: JenisPemilik, hari: int, tanggal: date
    ) -> SchoolSchedule | TeacherSchedule | None:
        """Cari baris jadwal yang berlaku pada tanggal tertentu - bukan
        baris terbaru. Baris dengan berlaku_mulai <= tanggal dan
        (berlaku_sampai kosong atau >= tanggal), diambil yang paling baru
        mulai berlaku di antara kandidat tersebut.

        Ini murni resolusi baris data sesuai versi tanggal; tidak
        menghitung status kehadiran apa pun.
        """
        model = _MODEL_PER_JENIS[jenis_pemilik]
        stmt = (
            select(model)
            .where(
                model.hari == hari,
                model.berlaku_mulai <= tanggal,
                (model.berlaku_sampai.is_(None)) | (model.berlaku_sampai >= tanggal),
            )
            .order_by(model.berlaku_mulai.desc())
            .limit(1)
        )
        return self._session.execute(stmt).scalar_one_or_none()

    def daftar(self, *, jenis_pemilik: JenisPemilik) -> list[SchoolSchedule | TeacherSchedule]:
        model = _MODEL_PER_JENIS[jenis_pemilik]
        return list(self._session.execute(select(model)).scalars())
