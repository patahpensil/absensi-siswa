"""Tes "fidelitas UID mentah" (PRD §17): membuktikan
MesinAbsensi.proses_scan meneruskan uid_mentah apa adanya ke KETUJUH titik
attendance_event_repo.buat() di app/core/engine.py, tanpa menyentuh
klasifikasi (HasilScan/klasifikasi_scan/cutoff/ambang/anti-duplikat) sama
sekali - satu slice, satu tesis.

Tes klasifikasi murni tetap sepenuhnya di tests/core/test_engine.py, TIDAK
DIUBAH oleh slice ini - uid_mentah opsional (bawaan None) di setiap titik,
lihat test_uid_mentah_opsional_klasifikasi_tak_berubah_tanpa_itu di bawah.
"""

from datetime import date, datetime, time

from app.config.pengaturan import Pengaturan
from app.core.engine import MesinAbsensi
from app.core.status import JenisHari
from app.data.models.attendance import JenisEventScan
from app.data.models.core import JenisKelamin
from app.data.models.enums import JenisPemilik
from app.data.models.system import PeranPengguna
from app.data.repositories.attendance import AttendanceRepository
from app.data.repositories.attendance_event import AttendanceEventRepository
from app.data.repositories.audit_log import AuditLogRepository
from app.data.repositories.kalender import KalenderRepository
from app.data.repositories.kelas import KelasRepository
from app.data.repositories.schedule import ScheduleRepository
from app.data.repositories.setting import SettingRepository
from app.data.repositories.student import StudentRepository
from app.data.repositories.teacher import TeacherRepository
from app.data.repositories.user import UserRepository

TANGGAL = date(2026, 9, 8)  # Selasa
JAM_MASUK = time(7, 15)
BATAS_TERLAMBAT = time(7, 20, 59)
CUTOFF_ALPA = time(7, 30)
PULANG_MULAI = time(13, 20)
JAM_PULANG = time(15, 0)


def _buat_mesin(session) -> MesinAbsensi:
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


def _buat_admin(session) -> int:
    return UserRepository(session).tambah(username="admin_uji", password_hash="x", peran=PeranPengguna.ADMIN).id


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


def _buat_guru(session) -> int:
    return TeacherRepository(session).tambah(nama_lengkap="Pak Anwar", nip="1122").id


def _seed_jadwal_siswa(session, *, hari: int = TANGGAL.isoweekday()) -> None:
    ScheduleRepository(session).tambah(
        jenis_pemilik=JenisPemilik.SISWA,
        hari=hari,
        jam_masuk=JAM_MASUK,
        batas_terlambat=BATAS_TERLAMBAT,
        cutoff_alpa=CUTOFF_ALPA,
        pulang_mulai=PULANG_MULAI,
        jam_pulang=JAM_PULANG,
        berlaku_mulai=date(2026, 1, 1),
    )


def _buat_libur(session, *, id_admin: int, tanggal: date) -> None:
    KalenderRepository(session).tambah(
        mulai=tanggal, selesai=tanggal, keterangan="Libur uji",
        jenis_siswa=JenisHari.LIBUR_NASIONAL.value, jenis_guru=JenisHari.LIBUR_NASIONAL.value,
        tahun_ajaran="2026/2027", id_pengguna=id_admin, alasan="Libur uji",
    )


def _uid_mentah_terbaru(session, uid_kartu: str) -> str | None:
    """daftar_by_uid terurut waktu_scan DESC - [0] adalah yang terbaru.
    Hanya dipakai kasus satu peristiwa per UID; kasus dua peristiwa
    (duplikat, pulang) mencari eksplisit lewat jenis_event di bawah."""
    return AttendanceEventRepository(session).daftar_by_uid(uid_kartu)[0].uid_mentah


# ---------------------------------------------------------------------------
# Titik 1: proses_scan - UNKNOWN_CARD
# ---------------------------------------------------------------------------


def test_titik_1_kartu_tidak_dikenal_membawa_uid_mentah(session):
    mesin = _buat_mesin(session)

    mesin.proses_scan(
        uid_kartu="UID_ASING", id_pemilik=None, jenis_pemilik=None,
        waktu_scan=datetime.combine(TANGGAL, time(7, 10)), uid_mentah="uid_asing:mentah",
    )

    assert _uid_mentah_terbaru(session, "UID_ASING") == "uid_asing:mentah"


# ---------------------------------------------------------------------------
# Titik 2: proses_scan - DUPLIKAT guru
# ---------------------------------------------------------------------------


def test_titik_2_duplikat_guru_membawa_uid_mentah(session):
    id_guru = _buat_guru(session)
    mesin = _buat_mesin(session)

    mesin.proses_scan(
        uid_kartu="UIDG1", id_pemilik=id_guru, jenis_pemilik=JenisPemilik.GURU,
        waktu_scan=datetime.combine(TANGGAL, time(7, 0, 0)), uid_mentah="pertama",
    )
    mesin.proses_scan(
        uid_kartu="UIDG1", id_pemilik=id_guru, jenis_pemilik=JenisPemilik.GURU,
        waktu_scan=datetime.combine(TANGGAL, time(7, 0, 5)), uid_mentah="duplikat-mentah",
    )

    peristiwa = AttendanceEventRepository(session).daftar_by_uid("UIDG1")
    duplikat = next(p for p in peristiwa if p.jenis_event == JenisEventScan.DUPLICATE)
    assert duplikat.uid_mentah == "duplikat-mentah"


# ---------------------------------------------------------------------------
# Titik 3: _proses_scan_siswa - DUPLIKAT
# ---------------------------------------------------------------------------


def test_titik_3_duplikat_siswa_membawa_uid_mentah(session):
    _seed_jadwal_siswa(session)
    id_siswa = _buat_siswa(session)
    mesin = _buat_mesin(session)

    mesin.proses_scan(
        uid_kartu="UID1", id_pemilik=id_siswa, jenis_pemilik=JenisPemilik.SISWA,
        waktu_scan=datetime.combine(TANGGAL, time(7, 10, 0)), uid_mentah="pertama",
    )
    mesin.proses_scan(
        uid_kartu="UID1", id_pemilik=id_siswa, jenis_pemilik=JenisPemilik.SISWA,
        waktu_scan=datetime.combine(TANGGAL, time(7, 10, 5)), uid_mentah="duplikat-mentah",
    )

    peristiwa = AttendanceEventRepository(session).daftar_by_uid("UID1")
    duplikat = next(p for p in peristiwa if p.jenis_event == JenisEventScan.DUPLICATE)
    assert duplikat.uid_mentah == "duplikat-mentah"


# ---------------------------------------------------------------------------
# Titik 4: _proses_scan_siswa - DI_LUAR_PERIODE (hari libur / di luar jendela)
# ---------------------------------------------------------------------------


def test_titik_4_hari_libur_membawa_uid_mentah(session):
    id_admin = _buat_admin(session)
    _seed_jadwal_siswa(session)
    id_siswa = _buat_siswa(session)
    _buat_libur(session, id_admin=id_admin, tanggal=TANGGAL)
    mesin = _buat_mesin(session)

    mesin.proses_scan(
        uid_kartu="UID1", id_pemilik=id_siswa, jenis_pemilik=JenisPemilik.SISWA,
        waktu_scan=datetime.combine(TANGGAL, time(7, 10)), uid_mentah="libur-mentah",
    )

    assert _uid_mentah_terbaru(session, "UID1") == "libur-mentah"


def test_titik_4_di_luar_jendela_membawa_uid_mentah(session):
    _seed_jadwal_siswa(session)
    id_siswa = _buat_siswa(session)
    mesin = _buat_mesin(session)

    mesin.proses_scan(
        uid_kartu="UID1", id_pemilik=id_siswa, jenis_pemilik=JenisPemilik.SISWA,
        waktu_scan=datetime.combine(TANGGAL, time(10, 0)), uid_mentah="luar-jendela-mentah",
    )

    assert _uid_mentah_terbaru(session, "UID1") == "luar-jendela-mentah"


# ---------------------------------------------------------------------------
# Titik 5: _proses_scan_siswa - PULANG
# ---------------------------------------------------------------------------


def test_titik_5_absen_pulang_membawa_uid_mentah(session):
    _seed_jadwal_siswa(session)
    id_siswa = _buat_siswa(session)
    mesin = _buat_mesin(session)

    mesin.proses_scan(
        uid_kartu="UID1", id_pemilik=id_siswa, jenis_pemilik=JenisPemilik.SISWA,
        waktu_scan=datetime.combine(TANGGAL, time(7, 10)), uid_mentah="masuk-mentah",
    )
    mesin.proses_scan(
        uid_kartu="UID1", id_pemilik=id_siswa, jenis_pemilik=JenisPemilik.SISWA,
        waktu_scan=datetime.combine(TANGGAL, time(13, 34)), uid_mentah="pulang-mentah",
    )

    peristiwa = AttendanceEventRepository(session).daftar_by_uid("UID1")
    pulang = next(p for p in peristiwa if p.jenis_event == JenisEventScan.PULANG)
    assert pulang.uid_mentah == "pulang-mentah"


# ---------------------------------------------------------------------------
# Titik 6: _proses_scan_siswa - MASUK
# ---------------------------------------------------------------------------


def test_titik_6_masuk_siswa_membawa_uid_mentah_kotor_apa_adanya(session):
    """UID mentah "kotor" (huruf kecil + separator) - membuktikan tidak
    dinormalisasi ulang sebelum disimpan, konsisten dengan verifikasi di
    tests/server/test_app.py untuk lapisan HTTP."""
    _seed_jadwal_siswa(session)
    id_siswa = _buat_siswa(session)
    mesin = _buat_mesin(session)

    mesin.proses_scan(
        uid_kartu="04A2B3C4", id_pemilik=id_siswa, jenis_pemilik=JenisPemilik.SISWA,
        waktu_scan=datetime.combine(TANGGAL, time(7, 10)), uid_mentah="04:a2 b3-c4",
    )

    assert _uid_mentah_terbaru(session, "04A2B3C4") == "04:a2 b3-c4"


# ---------------------------------------------------------------------------
# Titik 7: _proses_scan_guru - MASUK
# ---------------------------------------------------------------------------


def test_titik_7_masuk_guru_membawa_uid_mentah(session):
    id_guru = _buat_guru(session)
    mesin = _buat_mesin(session)

    mesin.proses_scan(
        uid_kartu="UIDG1", id_pemilik=id_guru, jenis_pemilik=JenisPemilik.GURU,
        waktu_scan=datetime.combine(TANGGAL, time(6, 0)), uid_mentah="guru-masuk-mentah",
    )

    assert _uid_mentah_terbaru(session, "UIDG1") == "guru-masuk-mentah"


# ---------------------------------------------------------------------------
# uid_mentah opsional - tidak menggeser klasifikasi
# ---------------------------------------------------------------------------


def test_uid_mentah_opsional_klasifikasi_tak_berubah_tanpa_itu(session):
    """Memanggil proses_scan TANPA uid_mentah sama sekali - persis seperti
    seluruh tests/core/test_engine.py yang tidak diubah oleh slice ini -
    tetap menghasilkan status yang sama. Ini bukti bahwa penambahan
    parameter ini tidak menggeser satu pun keputusan klasifikasi."""
    _seed_jadwal_siswa(session)
    id_siswa = _buat_siswa(session)
    mesin = _buat_mesin(session)

    hasil = mesin.proses_scan(
        uid_kartu="UID1", id_pemilik=id_siswa, jenis_pemilik=JenisPemilik.SISWA,
        waktu_scan=datetime.combine(TANGGAL, time(7, 10)),
    )

    assert hasil.absensi.status_masuk == "HADIR"
    assert _uid_mentah_terbaru(session, "UID1") is None
