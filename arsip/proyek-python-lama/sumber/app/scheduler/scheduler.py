import logging
from dataclasses import dataclass, field
from datetime import date, datetime, time

from apscheduler.schedulers.background import BackgroundScheduler
from apscheduler.triggers.cron import CronTrigger
from sqlalchemy.orm import Session, sessionmaker

from app.config.pengaturan import Pengaturan
from app.core.engine import MesinAbsensi, jadwal_dari_baris
from app.core.exceptions import KonfigurasiBelumLengkap
from app.core.kalender_service import muat_kalender
from app.core.mesin_absensi import waktu_kirim_alpa_pulang
from app.core.status import HARI_TANPA_ABSENSI
from app.core.timeutil import WITA, now_wita
from app.data.models.enums import JenisPemilik
from app.data.repositories.attendance import AttendanceRepository
from app.data.repositories.attendance_event import AttendanceEventRepository
from app.data.repositories.audit_log import AuditLogRepository
from app.data.repositories.kalender import KalenderRepository
from app.data.repositories.notification import NotificationRepository
from app.data.repositories.schedule import ScheduleRepository
from app.data.repositories.setting import SettingRepository
from app.data.repositories.student import StudentRepository
from app.data.repositories.teacher import TeacherRepository
from app.notify.factory import buat_provider
from app.notify.processor import QueueProcessor

_log = logging.getLogger(__name__)

_HARI_SEKOLAH = range(1, 7)  # Senin(1) .. Sabtu(6)
_CRON_HARI = {1: "mon", 2: "tue", 3: "wed", 4: "thu", 5: "fri", 6: "sat", 7: "sun"}

ID_JOB_CUTOFF_ALPA = "cutoff_alpa"
ID_JOB_NOTIFIKASI_ALPA = "notifikasi_alpa"
_PREFIX_CUTOFF_PULANG = "cutoff_pulang_"
_PREFIX_NOTIFIKASI_ALPA_PULANG = "notifikasi_alpa_pulang_"


def _id_cutoff_pulang(hari: int) -> str:
    return f"{_PREFIX_CUTOFF_PULANG}{hari}"


def _id_notifikasi_alpa_pulang(hari: int) -> str:
    return f"{_PREFIX_NOTIFIKASI_ALPA_PULANG}{hari}"


@dataclass(frozen=True)
class _JamJadwal:
    cutoff_alpa: time
    waktu_notif_alpa_pagi: time
    #: Jam cutoff pulang per hari (isoweekday) - hanya berisi hari yang
    #: sudah punya baris school_schedules aktif. Bisa berbeda tiap hari
    #: (mis. Jumat lebih awal), lihat SKILL.md "Perubahan jadwal".
    cutoff_pulang_per_hari: dict[int, time] = field(default_factory=dict)
    #: Waktu kirim notifikasi ALPA pulang per hari - dihitung lewat
    #: app.core.mesin_absensi.waktu_kirim_alpa_pulang (cutoff + jeda dari
    #: Pengaturan.jeda_kirim_alpa_pulang_menit), bukan dihitung ulang di sini.
    notif_pulang_per_hari: dict[int, time] = field(default_factory=dict)


class SchedulerAbsensi:
    """Pemicu waktu untuk tugas absensi harian: cutoff ALPA pagi, pemrosesan
    notifikasi ALPA pagi, cutoff pulang (konversi ALPA karena tidak scan
    pulang), dan notifikasi ALPA pulang.

    Hanya memicu fungsi yang sudah ada di app/core/ dan app/notify/ - tidak
    ada logika status, cutoff, maupun pengiriman di sini. Seluruh jam
    diambil dari app/config/ dan app/data/repositories/schedule.py, tidak
    ada satu pun ditulis tetap di kode.

    Setiap job membuat Session-nya sendiri lewat session_factory dan
    menutupnya setelah selesai - job berjalan di thread milik APScheduler,
    jadi tidak boleh berbagi Session dengan thread lain.

    Tidak ada lagi job Tutup Hari otomatis - konsep itu digantikan cutoff
    pulang + modul koreksi massal (lihat SKILL.md, "Absensi pulang dan
    ALPA pulang" dan "Koreksi massal"). Cutoff pulang aman dipicu ulang
    untuk tanggal yang sama; baris yang sudah dikonversi dikecualikan
    otomatis oleh app.core.mesin_absensi.
    """

    def __init__(self, *, session_factory: sessionmaker[Session]) -> None:
        self._session_factory = session_factory
        self._scheduler = BackgroundScheduler(timezone=WITA)

    @property
    def is_running(self) -> bool:
        return self._scheduler.running

    def pekerjaan(self):
        """Daftar Job APScheduler yang terdaftar - untuk pemeriksaan/tes."""
        return self._scheduler.get_jobs()

    # ------------------------------------------------------------------
    # Siklus hidup
    # ------------------------------------------------------------------

    def start(self, *, jalankan_susulan: bool = True, sekarang: datetime | None = None) -> None:
        """Jadwalkan seluruh job dari config lalu nyalakan scheduler.

        Sebelum menyalakan, periksa apakah proses hari ini yang seharusnya
        sudah lewat jamnya ternyata belum pernah jalan (mis. aplikasi baru
        dibuka pukul 10:00 padahal cutoff ALPA jam 07:30), lalu jalankan
        susulan secara sinkron sebelum job terjadwal berikutnya menyusul.
        """
        if self._scheduler.running:
            return

        jam = self._baca_jam_dari_config()

        if jalankan_susulan:
            self._jalankan_susulan(jam, sekarang=sekarang or now_wita())

        self._daftarkan_job(jam)
        self._scheduler.start()

    def stop(self, *, wait: bool = True) -> None:
        """Matikan scheduler bersih - tidak ada job yang menggantung.

        wait=True (bawaan) menunggu job yang sedang berjalan selesai lebih
        dulu sebelum method ini kembali.
        """
        if self._scheduler.running:
            self._scheduler.shutdown(wait=wait)

    def reload_jadwal(self) -> None:
        """Baca ulang jam dari config dan jadwalkan ulang seluruh job.

        Dipanggil setelah admin mengubah pengaturan jam cutoff ALPA, waktu
        kirim notifikasi ALPA, atau jadwal pulang, supaya scheduler yang
        sudah berjalan memakai jam yang baru tanpa perlu me-restart aplikasi.
        """
        jam = self._baca_jam_dari_config()
        self._daftarkan_job(jam)

    # ------------------------------------------------------------------
    # Konfigurasi
    # ------------------------------------------------------------------

    def _baca_jam_dari_config(self) -> _JamJadwal:
        session = self._session_factory()
        try:
            pengaturan = Pengaturan(SettingRepository(session))
            cutoff_alpa = pengaturan.cutoff_alpa()
            waktu_notif = pengaturan.waktu_kirim_notif_alpa()

            hari_ini = now_wita().date()
            cutoff_pulang_per_hari: dict[int, time] = {}
            notif_pulang_per_hari: dict[int, time] = {}
            schedule_repo = ScheduleRepository(session)
            for hari in _HARI_SEKOLAH:
                baris = schedule_repo.jadwal_berlaku(jenis_pemilik=JenisPemilik.SISWA, hari=hari, tanggal=hari_ini)
                if baris is None:
                    continue
                jadwal = jadwal_dari_baris(baris)
                cutoff_pulang_per_hari[hari] = jadwal.pulang_selesai
                # Tanggal acuan tidak memengaruhi jam-nya sendiri (hanya
                # dipakai fungsi murni untuk membentuk datetime) - hari_ini
                # dipakai apa adanya, bukan tanggal fire sesungguhnya.
                notif_pulang_per_hari[hari] = waktu_kirim_alpa_pulang(hari_ini, jadwal, pengaturan).time()
        finally:
            session.close()

        hilang = [
            nama
            for nama, nilai in (
                ("ATTENDANCE_CUTOFF_TIME", cutoff_alpa),
                ("WAKTU_KIRIM_NOTIF_ALPA", waktu_notif),
            )
            if nilai is None
        ]
        if hilang:
            raise KonfigurasiBelumLengkap(
                f"Pengaturan berikut belum diatur, scheduler tidak bisa dijadwalkan: {', '.join(hilang)}"
            )

        return _JamJadwal(
            cutoff_alpa=cutoff_alpa,
            waktu_notif_alpa_pagi=waktu_notif,
            cutoff_pulang_per_hari=cutoff_pulang_per_hari,
            notif_pulang_per_hari=notif_pulang_per_hari,
        )

    def _daftarkan_job(self, jam: _JamJadwal) -> None:
        self._scheduler.add_job(
            self._jalankan_cutoff_alpa,
            trigger=_pemicu_harian(jam.cutoff_alpa),
            id=ID_JOB_CUTOFF_ALPA,
            replace_existing=True,
        )
        self._scheduler.add_job(
            self._jalankan_notifikasi_alpa,
            trigger=_pemicu_harian(jam.waktu_notif_alpa_pagi),
            id=ID_JOB_NOTIFIKASI_ALPA,
            replace_existing=True,
        )

        # Hapus job cutoff-pulang/notifikasi-pulang lama - jumlah dan jamnya
        # per hari bisa berubah mengikuti school_schedules yang berlaku.
        for job in self._scheduler.get_jobs():
            if job.id.startswith(_PREFIX_CUTOFF_PULANG) or job.id.startswith(_PREFIX_NOTIFIKASI_ALPA_PULANG):
                self._scheduler.remove_job(job.id)

        for hari, jam_pulang in jam.cutoff_pulang_per_hari.items():
            self._scheduler.add_job(
                self._jalankan_cutoff_pulang,
                trigger=_pemicu_satu_hari(hari, jam_pulang),
                id=_id_cutoff_pulang(hari),
                replace_existing=True,
            )
            self._scheduler.add_job(
                self._jalankan_notifikasi_alpa_pulang,
                trigger=_pemicu_satu_hari(hari, jam.notif_pulang_per_hari[hari]),
                id=_id_notifikasi_alpa_pulang(hari),
                replace_existing=True,
            )

    def _jalankan_susulan(self, jam: _JamJadwal, *, sekarang: datetime) -> None:
        waktu_sekarang = sekarang.time()
        tanggal = sekarang.date()
        hari = tanggal.isoweekday()

        if waktu_sekarang >= jam.cutoff_alpa:
            self._jalankan_cutoff_alpa(tanggal=tanggal)
        if waktu_sekarang >= jam.waktu_notif_alpa_pagi:
            self._jalankan_notifikasi_alpa(tanggal=tanggal)

        jam_pulang_hari_ini = jam.cutoff_pulang_per_hari.get(hari)
        if jam_pulang_hari_ini is not None and waktu_sekarang >= jam_pulang_hari_ini:
            self._jalankan_cutoff_pulang(tanggal=tanggal)
            if waktu_sekarang >= jam.notif_pulang_per_hari[hari]:
                self._jalankan_notifikasi_alpa_pulang(tanggal=tanggal)

    # ------------------------------------------------------------------
    # Job - masing-masing membuat Session sendiri, aman dipanggil dari
    # thread manapun (cron APScheduler maupun langsung untuk susulan/tes).
    # ------------------------------------------------------------------

    def _apakah_hari_sekolah(self, tanggal: date, session: Session) -> bool:
        """Jalur siswa - seluruh job di kelas ini berpusat pada absensi
        siswa (cutoff ALPA, cutoff pulang, notifikasi terkait)."""
        jenis = muat_kalender(KalenderRepository(session)).jenis_hari_siswa(tanggal)
        return jenis not in HARI_TANPA_ABSENSI

    def _buat_mesin(self, session: Session) -> MesinAbsensi:
        return MesinAbsensi(
            attendance_repo=AttendanceRepository(session),
            attendance_event_repo=AttendanceEventRepository(session),
            schedule_repo=ScheduleRepository(session),
            kalender_repo=KalenderRepository(session),
            student_repo=StudentRepository(session),
            teacher_repo=TeacherRepository(session),
            audit_repo=AuditLogRepository(session),
            pengaturan=Pengaturan(SettingRepository(session)),
        )

    def _jalankan_cutoff_alpa(self, *, tanggal: date | None = None) -> None:
        tanggal = tanggal or now_wita().date()
        session = self._session_factory()
        try:
            if not self._apakah_hari_sekolah(tanggal, session):
                _log.info("Cutoff ALPA %s dilewati - bukan hari sekolah", tanggal)
                return
            hasil = self._buat_mesin(session).tetapkan_alpa(tanggal)
            _log.info("Cutoff ALPA %s selesai: %d siswa ditetapkan ALPA", tanggal, len(hasil))
        finally:
            session.close()

    def _jalankan_notifikasi_alpa(self, *, tanggal: date | None = None) -> None:
        tanggal = tanggal or now_wita().date()
        session = self._session_factory()
        try:
            if not self._apakah_hari_sekolah(tanggal, session):
                _log.info("Pemrosesan notifikasi ALPA %s dilewati - bukan hari sekolah", tanggal)
                return
            diproses = self._proses_antrean_notifikasi(session)
            _log.info("Pemrosesan notifikasi %s selesai: %d pesan diproses", tanggal, len(diproses))
        finally:
            session.close()

    def _jalankan_cutoff_pulang(self, *, tanggal: date | None = None) -> None:
        tanggal = tanggal or now_wita().date()
        session = self._session_factory()
        try:
            if not self._apakah_hari_sekolah(tanggal, session):
                _log.info("Cutoff pulang %s dilewati - bukan hari sekolah", tanggal)
                return
            hasil = self._buat_mesin(session).jalankan_cutoff_pulang(tanggal)
            _log.info(
                "Cutoff pulang %s selesai: %d/%d tidak scan pulang, ditahan=%s",
                tanggal,
                hasil.jumlah_sasaran,
                hasil.jumlah_hadir,
                hasil.ditahan,
            )
        finally:
            session.close()

    def _jalankan_notifikasi_alpa_pulang(self, *, tanggal: date | None = None) -> None:
        tanggal = tanggal or now_wita().date()
        session = self._session_factory()
        try:
            if not self._apakah_hari_sekolah(tanggal, session):
                _log.info("Notifikasi ALPA pulang %s dilewati - bukan hari sekolah", tanggal)
                return
            diproses = self._proses_antrean_notifikasi(session)
            _log.info("Notifikasi ALPA pulang %s selesai: %d pesan diproses", tanggal, len(diproses))
        finally:
            session.close()

    def _proses_antrean_notifikasi(self, session: Session) -> list:
        pengaturan = Pengaturan(SettingRepository(session))
        provider = buat_provider(pengaturan)
        processor = QueueProcessor(
            notification_repo=NotificationRepository(session),
            attendance_repo=AttendanceRepository(session),
            provider=provider,
            pengaturan=pengaturan,
        )
        return processor.proses_satu_putaran()


def _pemicu_harian(jam: time) -> CronTrigger:
    """Pemicu cron Senin-Sabtu pada jam tertentu, zona waktu WITA.

    Minggu dilewati di sini (lewat CronTrigger); tanggal libur di kalender
    akademik dilewati di dalam job masing-masing (_apakah_hari_sekolah),
    karena itu data dinamis yang tidak bisa diketahui CronTrigger di muka.
    """
    return CronTrigger(
        day_of_week="mon-sat",
        hour=jam.hour,
        minute=jam.minute,
        second=jam.second,
        timezone=WITA,
    )


def _pemicu_satu_hari(hari: int, jam: time) -> CronTrigger:
    """Pemicu cron untuk satu hari dalam seminggu (isoweekday 1=Senin..6=Sabtu)."""
    return CronTrigger(
        day_of_week=_CRON_HARI[hari],
        hour=jam.hour,
        minute=jam.minute,
        second=jam.second,
        timezone=WITA,
    )
