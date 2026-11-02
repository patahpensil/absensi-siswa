"""Tes app/scheduler/: job terpicu di jam benar (termasuk cutoff pulang yang
jamnya berasal dari school_schedules per hari), hari libur dilewati, dan
pemulihan setelah restart. Tidak menyentuh mesin absensi - hanya memeriksa
scheduler memicunya dengan benar.
"""

from datetime import date, datetime, time

import pytest

from app.config.pengaturan import Pengaturan
from app.core.exceptions import KonfigurasiBelumLengkap
from app.core.status import JenisHari
from app.data.models.core import JenisKelamin
from app.data.models.enums import JenisPemilik
from app.data.models.system import PeranPengguna
from app.data.repositories.attendance import AttendanceRepository
from app.data.repositories.kalender import KalenderRepository
from app.data.repositories.kelas import KelasRepository
from app.data.repositories.schedule import ScheduleRepository
from app.data.repositories.setting import SettingRepository
from app.data.repositories.student import StudentRepository
from app.data.repositories.user import UserRepository
from app.scheduler.scheduler import (
    ID_JOB_CUTOFF_ALPA,
    ID_JOB_NOTIFIKASI_ALPA,
    WITA,
    SchedulerAbsensi,
    _id_cutoff_pulang,
    _id_notifikasi_alpa_pulang,
)

SELASA = date(2026, 9, 8)  # hari sekolah biasa (isoweekday=2)
MINGGU = date(2026, 9, 13)  # libur mingguan (isoweekday=7)

CUTOFF_ALPA = time(7, 30)
WAKTU_NOTIF = time(9, 15)
JAM_MASUK = time(7, 15)
BATAS_TERLAMBAT = time(7, 20, 59)
PULANG_MULAI = time(13, 20)
JAM_PULANG = time(15, 0)


def _buat_admin(session) -> int:
    return UserRepository(session).tambah(username="admin_uji", password_hash="x", peran=PeranPengguna.ADMIN).id


def _seed_pengaturan(session, *, id_admin: int) -> None:
    pengaturan = Pengaturan(SettingRepository(session))
    pengaturan.atur_cutoff_alpa(CUTOFF_ALPA, id_pengguna=id_admin, alasan="Seed uji")
    pengaturan.atur_waktu_kirim_notif_alpa(WAKTU_NOTIF, id_pengguna=id_admin, alasan="Seed uji")


def _buat_siswa(session) -> int:
    kelas = KelasRepository(session).tambah(tingkat=7, nama_kelas="7A", tahun_ajaran="2026/2027")
    return StudentRepository(session).tambah(
        nisn="1000000001",
        nama_lengkap="Budi",
        id_kelas=kelas.id,
        jenis_kelamin=JenisKelamin.L,
        tempat_lahir="Pancarijang",
        tanggal_lahir=date(2012, 1, 1),
        alamat="Jl. Contoh No. 1",
    ).id


def _buat_libur(session, *, id_admin: int, tanggal: date, keterangan: str = "Libur uji") -> None:
    KalenderRepository(session).tambah(
        mulai=tanggal,
        selesai=tanggal,
        keterangan=keterangan,
        jenis_siswa=JenisHari.LIBUR_NASIONAL.value,
        jenis_guru=JenisHari.LIBUR_NASIONAL.value,
        tahun_ajaran="2026/2027",
        id_pengguna=id_admin,
        alasan=keterangan,
    )


def _seed_jadwal_siswa(session, *, hari: int, jam_pulang: time = JAM_PULANG) -> None:
    ScheduleRepository(session).tambah(
        jenis_pemilik=JenisPemilik.SISWA,
        hari=hari,
        jam_masuk=JAM_MASUK,
        batas_terlambat=BATAS_TERLAMBAT,
        cutoff_alpa=CUTOFF_ALPA,
        pulang_mulai=PULANG_MULAI,
        jam_pulang=jam_pulang,
        berlaku_mulai=date(2026, 1, 1),
    )


@pytest.fixture
def scheduler(session_factory):
    sch = SchedulerAbsensi(session_factory=session_factory)
    yield sch
    if sch.is_running:
        sch.stop()


# ---------------------------------------------------------------------------
# Konfigurasi wajib dari config, bukan hardcode
# ---------------------------------------------------------------------------


def test_start_tanpa_konfigurasi_melempar_error(scheduler):
    with pytest.raises(KonfigurasiBelumLengkap):
        scheduler.start()


# ---------------------------------------------------------------------------
# Job terjadwal di jam yang benar, Minggu dilewati oleh pemicu cron
# ---------------------------------------------------------------------------


def test_job_cutoff_alpa_dan_notifikasi_terjadwal_dari_config(session, scheduler):
    id_admin = _buat_admin(session)
    _seed_pengaturan(session, id_admin=id_admin)

    scheduler.start(jalankan_susulan=False)
    try:
        pekerjaan = {p.id: p for p in scheduler.pekerjaan()}
        # Tidak ada school_schedules yang di-seed - jadi tidak ada job
        # cutoff pulang/notifikasi pulang yang terdaftar.
        assert set(pekerjaan) == {ID_JOB_CUTOFF_ALPA, ID_JOB_NOTIFIKASI_ALPA}

        acuan = datetime(2026, 9, 8, 0, 0, tzinfo=WITA)  # Selasa dini hari

        pemicu_cutoff = pekerjaan[ID_JOB_CUTOFF_ALPA].trigger.get_next_fire_time(None, acuan)
        assert pemicu_cutoff == datetime(2026, 9, 8, 7, 30, tzinfo=WITA)

        pemicu_notif = pekerjaan[ID_JOB_NOTIFIKASI_ALPA].trigger.get_next_fire_time(None, acuan)
        assert pemicu_notif == datetime(2026, 9, 8, 9, 15, tzinfo=WITA)

        # Dari Sabtu malam, pemicu berikutnya harus lompat ke Senin - Minggu dilewati.
        acuan_sabtu_malam = datetime(2026, 9, 12, 23, 0, tzinfo=WITA)
        pemicu_setelah_sabtu = pekerjaan[ID_JOB_CUTOFF_ALPA].trigger.get_next_fire_time(None, acuan_sabtu_malam)
        assert pemicu_setelah_sabtu == datetime(2026, 9, 14, 7, 30, tzinfo=WITA)
    finally:
        scheduler.stop()


def test_job_cutoff_pulang_terjadwal_untuk_hari_yang_punya_jadwal(session, scheduler):
    id_admin = _buat_admin(session)
    _seed_pengaturan(session, id_admin=id_admin)
    _seed_jadwal_siswa(session, hari=SELASA.isoweekday(), jam_pulang=JAM_PULANG)

    scheduler.start(jalankan_susulan=False)
    try:
        pekerjaan = {p.id: p for p in scheduler.pekerjaan()}
        id_cutoff_pulang = _id_cutoff_pulang(SELASA.isoweekday())
        id_notif_pulang = _id_notifikasi_alpa_pulang(SELASA.isoweekday())
        assert id_cutoff_pulang in pekerjaan
        assert id_notif_pulang in pekerjaan

        acuan = datetime(2026, 9, 8, 0, 0, tzinfo=WITA)  # Selasa dini hari
        pemicu_cutoff_pulang = pekerjaan[id_cutoff_pulang].trigger.get_next_fire_time(None, acuan)
        assert pemicu_cutoff_pulang == datetime(2026, 9, 8, 15, 0, tzinfo=WITA)

        # Notifikasi ALPA pulang - jeda bawaan 60 menit setelah cutoff.
        pemicu_notif_pulang = pekerjaan[id_notif_pulang].trigger.get_next_fire_time(None, acuan)
        assert pemicu_notif_pulang == datetime(2026, 9, 8, 16, 0, tzinfo=WITA)

        # Hari lain (mis. Jumat) tidak punya jadwal - tidak terdaftar.
        assert _id_cutoff_pulang(5) not in pekerjaan
    finally:
        scheduler.stop()


# ---------------------------------------------------------------------------
# Hari libur dilewati (dinamis, dari kalender akademik)
# ---------------------------------------------------------------------------


def test_cutoff_alpa_dilewati_pada_hari_libur(session, scheduler):
    id_admin = _buat_admin(session)
    _seed_pengaturan(session, id_admin=id_admin)
    _buat_siswa(session)
    _buat_libur(session, id_admin=id_admin, tanggal=SELASA)

    scheduler._jalankan_cutoff_alpa(tanggal=SELASA)

    # Tidak ada baris attendance yang terbentuk - cutoff tidak pernah
    # memanggil tetapkan_alpa() karena hari libur.
    kosong = AttendanceRepository(session).daftar_by_tanggal(tanggal=SELASA, jenis_pemilik=JenisPemilik.SISWA)
    assert kosong == []


def test_semua_job_dilewati_pada_hari_minggu(session, scheduler):
    id_admin = _buat_admin(session)
    _seed_pengaturan(session, id_admin=id_admin)
    _buat_siswa(session)

    scheduler._jalankan_cutoff_alpa(tanggal=MINGGU)
    scheduler._jalankan_notifikasi_alpa(tanggal=MINGGU)
    scheduler._jalankan_cutoff_pulang(tanggal=MINGGU)
    scheduler._jalankan_notifikasi_alpa_pulang(tanggal=MINGGU)

    assert AttendanceRepository(session).daftar_by_tanggal(tanggal=MINGGU, jenis_pemilik=JenisPemilik.SISWA) == []


# ---------------------------------------------------------------------------
# Pemulihan setelah restart - proses yang terlewat dijalankan susulan
# ---------------------------------------------------------------------------


def test_pemulihan_menjalankan_cutoff_dan_notifikasi_yang_terlewat(session, scheduler):
    id_admin = _buat_admin(session)
    _seed_pengaturan(session, id_admin=id_admin)
    _seed_jadwal_siswa(session, hari=SELASA.isoweekday())
    id_siswa = _buat_siswa(session)

    # Aplikasi baru dibuka pukul 10:00 - cutoff (07:30) dan notifikasi
    # (09:15) sudah lewat, tapi cutoff pulang (15:00) belum.
    scheduler.start(sekarang=datetime.combine(SELASA, time(10, 0)))
    try:
        absensi = AttendanceRepository(session).cari_by_pemilik_tanggal(
            id_pemilik=id_siswa, jenis_pemilik=JenisPemilik.SISWA, tanggal=SELASA
        )
        assert absensi is not None
        assert absensi.status_masuk == "ALPA"
        assert absensi.status_final == "ALPA"
    finally:
        scheduler.stop()


def test_pemulihan_menjalankan_cutoff_pulang_jika_sudah_lewat_jamnya(session, scheduler):
    id_admin = _buat_admin(session)
    _seed_pengaturan(session, id_admin=id_admin)
    _seed_jadwal_siswa(session, hari=SELASA.isoweekday())
    id_siswa = _buat_siswa(session)
    AttendanceRepository(session).tambah(
        id_pemilik=id_siswa,
        jenis_pemilik=JenisPemilik.SISWA,
        tanggal=SELASA,
        status_masuk="HADIR",
        jam_masuk=datetime.combine(SELASA, time(7, 10)),
    )

    # Aplikasi baru dibuka pukul 16:00 - keempat jam sudah lewat. Satu
    # siswa saja berarti proporsi tidak-scan-pulang 100%, di atas ambang
    # bawaan - jadi konversi tetap DITAHAN (bukan berarti susulan gagal
    # jalan, hanya tidak memaksa konversi begitu saja).
    scheduler.start(sekarang=datetime.combine(SELASA, time(16, 0)))
    try:
        absensi = AttendanceRepository(session).cari_by_pemilik_tanggal(
            id_pemilik=id_siswa, jenis_pemilik=JenisPemilik.SISWA, tanggal=SELASA
        )
        assert absensi.status_final == "HADIR"  # ditahan, bukan otomatis ALPA
    finally:
        scheduler.stop()


def test_pemulihan_tidak_menjalankan_apa_pun_bila_belum_waktunya(session, scheduler):
    id_admin = _buat_admin(session)
    _seed_pengaturan(session, id_admin=id_admin)
    id_siswa = _buat_siswa(session)

    # Aplikasi dibuka pukul 06:00 - belum ada satu pun jam yang lewat.
    scheduler.start(sekarang=datetime.combine(SELASA, time(6, 0)))
    try:
        absensi = AttendanceRepository(session).cari_by_pemilik_tanggal(
            id_pemilik=id_siswa, jenis_pemilik=JenisPemilik.SISWA, tanggal=SELASA
        )
        assert absensi is None
    finally:
        scheduler.stop()


# ---------------------------------------------------------------------------
# Siklus hidup: stop bersih, reload jadwal
# ---------------------------------------------------------------------------


def test_stop_scheduler_bersih(session, scheduler):
    id_admin = _buat_admin(session)
    _seed_pengaturan(session, id_admin=id_admin)

    scheduler.start(jalankan_susulan=False)
    assert scheduler.is_running is True

    scheduler.stop()
    assert scheduler.is_running is False


def test_reload_jadwal_memakai_jam_cutoff_alpa_baru(session, scheduler):
    id_admin = _buat_admin(session)
    _seed_pengaturan(session, id_admin=id_admin)

    scheduler.start(jalankan_susulan=False)
    try:
        pengaturan = Pengaturan(SettingRepository(session))
        pengaturan.atur_cutoff_alpa(time(8, 0), id_pengguna=id_admin, alasan="Ubah jam uji")
        scheduler.reload_jadwal()

        pekerjaan = {p.id: p for p in scheduler.pekerjaan()}
        acuan = datetime(2026, 9, 8, 0, 0, tzinfo=WITA)
        pemicu_baru = pekerjaan[ID_JOB_CUTOFF_ALPA].trigger.get_next_fire_time(None, acuan)
        assert pemicu_baru == datetime(2026, 9, 8, 8, 0, tzinfo=WITA)
    finally:
        scheduler.stop()


def test_reload_jadwal_menambahkan_job_cutoff_pulang_setelah_jadwal_diisi(session, scheduler):
    id_admin = _buat_admin(session)
    _seed_pengaturan(session, id_admin=id_admin)

    scheduler.start(jalankan_susulan=False)
    try:
        pekerjaan_awal = {p.id for p in scheduler.pekerjaan()}
        assert _id_cutoff_pulang(SELASA.isoweekday()) not in pekerjaan_awal

        _seed_jadwal_siswa(session, hari=SELASA.isoweekday())
        scheduler.reload_jadwal()

        pekerjaan_setelah = {p.id for p in scheduler.pekerjaan()}
        assert _id_cutoff_pulang(SELASA.isoweekday()) in pekerjaan_setelah
    finally:
        scheduler.stop()
