from datetime import datetime
from typing import Any

from sqlalchemy import func, select

from app.core.timeutil import now_wita
from app.data.models.attendance import AttendanceEvent, JenisEventScan
from app.data.models.enums import JenisPemilik
from app.data.repositories.base import BaseRepository
from app.rfid.base import JalurAlat


class AttendanceEventRepository(BaseRepository):
    """Akses data untuk attendance_events.

    Hanya create dan read. Peristiwa scan mentah tidak pernah diubah dan
    tidak pernah dihapus, bahkan ketika hasil pengolahannya di tabel
    attendance berubah atau dikoreksi - lihat skill proyek. Repository ini
    sengaja tidak menyediakan method update maupun delete apa pun.
    """

    def buat(
        self,
        *,
        uid_kartu: str,
        jenis_event: JenisEventScan,
        uid_mentah: str | None = None,
        id_pemilik: int | None = None,
        jenis_pemilik: JenisPemilik | None = None,
        id_perangkat: int | None = None,
        waktu_scan: datetime | None = None,
        metadata_json: dict[str, Any] | None = None,
        jalur: JalurAlat | None = None,
    ) -> AttendanceEvent:
        """uid_mentah/jalur opsional (bawaan None) - pemanggil yang tidak
        punya nilai itu (mis. tes murni yang tak peduli soal ini) tidak
        wajib menyertakannya. Pemanggil produksi (app.core.engine.MesinAbsensi)
        selalu menyertakan keduanya sejak PRD §17 mewajibkan penyimpanannya.
        """
        peristiwa = AttendanceEvent(
            uid_kartu=uid_kartu,
            uid_mentah=uid_mentah,
            id_pemilik=id_pemilik,
            jenis_pemilik=jenis_pemilik,
            waktu_scan=waktu_scan or now_wita(),
            id_perangkat=id_perangkat,
            jenis_event=jenis_event,
            metadata_json=metadata_json,
            jalur=jalur,
        )
        self._session.add(peristiwa)
        self._commit()
        return peristiwa

    def cari_by_id(self, id_peristiwa: int) -> AttendanceEvent | None:
        return self._session.get(AttendanceEvent, id_peristiwa)

    def daftar_by_uid(self, uid_kartu: str, *, sejak: datetime | None = None) -> list[AttendanceEvent]:
        stmt = select(AttendanceEvent).where(AttendanceEvent.uid_kartu == uid_kartu)
        if sejak is not None:
            stmt = stmt.where(AttendanceEvent.waktu_scan >= sejak)
        stmt = stmt.order_by(AttendanceEvent.waktu_scan.desc())
        return list(self._session.execute(stmt).scalars())

    def waktu_scan_terakhir(self, uid_kartu: str) -> datetime | None:
        """Waktu scan TERAKHIR untuk UID ini, di seluruh jalur/perangkat.

        Dipakai app.core.engine untuk mendeteksi duplikat lewat
        app.core.mesin_absensi.scan_duplikat - global per UID, bukan per
        perangkat, sesuai SKILL.md ("Scan berulang").
        """
        stmt = (
            select(AttendanceEvent.waktu_scan)
            .where(AttendanceEvent.uid_kartu == uid_kartu)
            .order_by(AttendanceEvent.waktu_scan.desc())
            .limit(1)
        )
        return self._session.execute(stmt).scalar_one_or_none()

    def waktu_terakhir_per_jalur(self) -> dict[JalurAlat, datetime]:
        """Waktu scan TERAKHIR per jalur, DI SELURUH RIWAYAT (bukan
        hanya hari ini) - untuk status operasional Dashboard (PRD §14.1:
        "kondisi tiap jalur beserta waktu terakhir membaca"). Mengambil
        SELURUH peristiwa (termasuk kartu tidak dikenal/duplikat/di luar
        jendela - lihat app.core.status.HasilScan), bukan hanya yang
        berhasil membentuk absensi, karena "terakhir membaca" adalah
        pertanyaan soal alatnya, bukan soal kehadirannya. Baris lama
        dengan jalur=None (sebelum kolom ini ada, lihat migrasi
        0df2d01ad591) tidak ikut - tidak ada jalur jujur untuk
        dikelompokkan.
        """
        stmt = (
            select(AttendanceEvent.jalur, func.max(AttendanceEvent.waktu_scan))
            .where(AttendanceEvent.jalur.is_not(None))
            .group_by(AttendanceEvent.jalur)
        )
        return dict(self._session.execute(stmt).all())

    def daftar_terbaru(self, *, limit: int = 50) -> list[AttendanceEvent]:
        stmt = select(AttendanceEvent).order_by(AttendanceEvent.waktu_scan.desc()).limit(limit)
        return list(self._session.execute(stmt).scalars())

    def cari(
        self,
        *,
        sejak: datetime | None = None,
        sampai: datetime | None = None,
        id_pemilik: int | None = None,
        jenis_pemilik: JenisPemilik | None = None,
        ids_pemilik_siswa: list[int] | None = None,
        limit: int = 200,
    ) -> list[AttendanceEvent]:
        """Untuk layar Riwayat Scan (PRD §7.4, §10) - saring rentang
        waktu, orang tertentu, atau sekumpulan siswa (dipakai untuk
        lingkup kelas/wali kelas - lihat app.server.riwayat_scan_service).
        Murni baca, tidak pernah mengubah peristiwa mentah.

        ``id_pemilik``/``jenis_pemilik`` dan ``ids_pemilik_siswa`` adalah
        dua cara saring orang yang TERPISAH - pemanggil memilih salah
        satu, bukan menggabungkan keduanya secara implisit di sini.
        """
        stmt = select(AttendanceEvent)
        if sejak is not None:
            stmt = stmt.where(AttendanceEvent.waktu_scan >= sejak)
        if sampai is not None:
            stmt = stmt.where(AttendanceEvent.waktu_scan <= sampai)
        if id_pemilik is not None:
            stmt = stmt.where(AttendanceEvent.id_pemilik == id_pemilik)
        if jenis_pemilik is not None:
            stmt = stmt.where(AttendanceEvent.jenis_pemilik == jenis_pemilik)
        if ids_pemilik_siswa is not None:
            stmt = stmt.where(
                AttendanceEvent.jenis_pemilik == JenisPemilik.SISWA,
                AttendanceEvent.id_pemilik.in_(ids_pemilik_siswa),
            )
        stmt = stmt.order_by(AttendanceEvent.waktu_scan.desc()).limit(limit)
        return list(self._session.execute(stmt).scalars())
