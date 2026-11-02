"""Tes lengkap mesin absensi: siswa dan guru, cutoff ALPA pagi, cutoff
pulang (konversi ALPA karena tidak scan pulang), hari libur, dan duplikat.
"""

from datetime import date, datetime, time

from app.config.pengaturan import Pengaturan
from app.core.engine import MesinAbsensi
from app.core.kalender_service import muat_kalender
from app.core.status import AlasanFinal, JenisHari, StatusGuru, StatusSiswa
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

TANGGAL = date(2026, 9, 8)  # Selasa (hari=2)
JAM_MASUK = time(7, 15)
BATAS_TERLAMBAT = time(7, 20, 59)  # inklusif ke bawah - lihat JadwalHarian
CUTOFF_ALPA = time(7, 30)
PULANG_MULAI = time(13, 20)
JAM_PULANG = time(15, 0)  # akhir jendela pulang, sekaligus cutoff ALPA pulang


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


def _buat_siswa(session) -> int:
    kelas = KelasRepository(session).tambah(tingkat=7, nama_kelas="7A", tahun_ajaran="2026/2027")
    siswa = StudentRepository(session).tambah(
        nisn="1000000001",
        nama_lengkap="Budi",
        id_kelas=kelas.id,
        jenis_kelamin=JenisKelamin.L,
        tempat_lahir="Pancarijang",
        tanggal_lahir=date(2012, 1, 1),
        alamat="Jl. Contoh No. 1",
    )
    return siswa.id


def _buat_guru(session) -> int:
    return TeacherRepository(session).tambah(nama_lengkap="Pak Anwar", nip="1122").id


def _seed_jadwal_siswa(session, *, hari: int = TANGGAL.isoweekday(), berlaku_mulai: date = date(2026, 1, 1)) -> None:
    ScheduleRepository(session).tambah(
        jenis_pemilik=JenisPemilik.SISWA,
        hari=hari,
        jam_masuk=JAM_MASUK,
        batas_terlambat=BATAS_TERLAMBAT,
        cutoff_alpa=CUTOFF_ALPA,
        pulang_mulai=PULANG_MULAI,
        jam_pulang=JAM_PULANG,
        berlaku_mulai=berlaku_mulai,
    )


# ---------------------------------------------------------------------------
# Siswa: HADIR / TERLAMBAT dari scan
# ---------------------------------------------------------------------------


def test_siswa_scan_sebelum_batas_terlambat_jadi_hadir(session):
    _seed_jadwal_siswa(session)
    id_siswa = _buat_siswa(session)
    mesin = _buat_mesin(session)

    hasil = mesin.proses_scan(
        uid_kartu="UID1",
        id_pemilik=id_siswa,
        jenis_pemilik=JenisPemilik.SISWA,
        waktu_scan=datetime.combine(TANGGAL, time(7, 10)),
    )

    assert hasil.event.jenis_event == JenisEventScan.MASUK
    assert hasil.absensi is not None
    assert hasil.absensi.status_masuk == StatusSiswa.HADIR.value
    assert hasil.absensi.status_final == StatusSiswa.HADIR.value
    assert hasil.absensi.jam_masuk == datetime.combine(TANGGAL, time(7, 10))


def test_siswa_scan_tepat_batas_terlambat_masih_hadir(session):
    _seed_jadwal_siswa(session)
    id_siswa = _buat_siswa(session)
    mesin = _buat_mesin(session)

    hasil = mesin.proses_scan(
        uid_kartu="UID1",
        id_pemilik=id_siswa,
        jenis_pemilik=JenisPemilik.SISWA,
        waktu_scan=datetime.combine(TANGGAL, BATAS_TERLAMBAT),
    )

    assert hasil.absensi.status_masuk == StatusSiswa.HADIR.value


def test_siswa_scan_setelah_batas_terlambat_jadi_terlambat(session):
    _seed_jadwal_siswa(session)
    id_siswa = _buat_siswa(session)
    mesin = _buat_mesin(session)

    hasil = mesin.proses_scan(
        uid_kartu="UID1",
        id_pemilik=id_siswa,
        jenis_pemilik=JenisPemilik.SISWA,
        waktu_scan=datetime.combine(TANGGAL, time(7, 21, 0)),
    )

    assert hasil.absensi.status_masuk == StatusSiswa.TERLAMBAT.value


def test_siswa_scan_kedua_tidak_mengubah_status_yang_sudah_ditetapkan(session):
    _seed_jadwal_siswa(session)
    id_siswa = _buat_siswa(session)
    mesin = _buat_mesin(session)

    pertama = mesin.proses_scan(
        uid_kartu="UID1",
        id_pemilik=id_siswa,
        jenis_pemilik=JenisPemilik.SISWA,
        waktu_scan=datetime.combine(TANGGAL, time(7, 10)),
    )
    kedua = mesin.proses_scan(
        uid_kartu="UID1",
        id_pemilik=id_siswa,
        jenis_pemilik=JenisPemilik.SISWA,
        waktu_scan=datetime.combine(TANGGAL, time(7, 25)),
    )

    assert kedua.absensi.id == pertama.absensi.id
    assert kedua.absensi.status_masuk == StatusSiswa.HADIR.value
    assert kedua.absensi.jam_masuk == datetime.combine(TANGGAL, time(7, 10))


# ---------------------------------------------------------------------------
# Siswa: absen pulang normal, dan kasus di luar periode
# ---------------------------------------------------------------------------


def test_siswa_scan_pulang_setelah_masuk_tercatat_normal(session):
    _seed_jadwal_siswa(session)
    id_siswa = _buat_siswa(session)
    mesin = _buat_mesin(session)

    mesin.proses_scan(
        uid_kartu="UID1",
        id_pemilik=id_siswa,
        jenis_pemilik=JenisPemilik.SISWA,
        waktu_scan=datetime.combine(TANGGAL, time(7, 10)),
    )
    hasil_pulang = mesin.proses_scan(
        uid_kartu="UID1",
        id_pemilik=id_siswa,
        jenis_pemilik=JenisPemilik.SISWA,
        waktu_scan=datetime.combine(TANGGAL, time(13, 34)),
    )

    assert hasil_pulang.event.jenis_event == JenisEventScan.PULANG
    assert hasil_pulang.absensi.jam_pulang == datetime.combine(TANGGAL, time(13, 34))
    assert hasil_pulang.absensi.status_final == StatusSiswa.HADIR.value


def test_siswa_scan_pulang_tanpa_absen_masuk_tidak_membentuk_baris(session):
    _seed_jadwal_siswa(session)
    id_siswa = _buat_siswa(session)
    mesin = _buat_mesin(session)

    hasil = mesin.proses_scan(
        uid_kartu="UID1",
        id_pemilik=id_siswa,
        jenis_pemilik=JenisPemilik.SISWA,
        waktu_scan=datetime.combine(TANGGAL, time(13, 34)),
    )

    assert hasil.event.jenis_event == JenisEventScan.DI_LUAR_PERIODE
    assert hasil.absensi is None


def test_siswa_scan_pada_hari_tanpa_jadwal_tercatat_di_luar_periode(session):
    id_siswa = _buat_siswa(session)
    mesin = _buat_mesin(session)  # tidak ada school_schedules sama sekali

    hasil = mesin.proses_scan(
        uid_kartu="UID1",
        id_pemilik=id_siswa,
        jenis_pemilik=JenisPemilik.SISWA,
        waktu_scan=datetime.combine(TANGGAL, time(7, 10)),
    )

    assert hasil.event.jenis_event == JenisEventScan.DI_LUAR_PERIODE
    assert hasil.absensi is None


# ---------------------------------------------------------------------------
# Kartu tidak dikenal dan duplikat
# ---------------------------------------------------------------------------


def test_kartu_tidak_dikenal_tidak_membentuk_absensi(session):
    mesin = _buat_mesin(session)

    hasil = mesin.proses_scan(
        uid_kartu="UID_ASING",
        id_pemilik=None,
        jenis_pemilik=None,
        waktu_scan=datetime.combine(TANGGAL, time(7, 10)),
    )

    assert hasil.event.jenis_event == JenisEventScan.UNKNOWN_CARD
    assert hasil.absensi is None


def test_scan_duplikat_dalam_jendela_tidak_mengubah_absensi(session):
    """Duplikat dideteksi lewat waktu scan TERAKHIR untuk UID ini (lihat
    app.core.mesin_absensi.scan_duplikat), bukan lewat parameter dari
    pemanggil - lapisan pembaca tidak lagi menyaring duplikat sendiri.
    """
    _seed_jadwal_siswa(session)
    id_siswa = _buat_siswa(session)
    mesin = _buat_mesin(session)

    pertama = mesin.proses_scan(
        uid_kartu="UID1",
        id_pemilik=id_siswa,
        jenis_pemilik=JenisPemilik.SISWA,
        waktu_scan=datetime.combine(TANGGAL, time(7, 10, 0)),
    )
    kedua = mesin.proses_scan(
        uid_kartu="UID1",
        id_pemilik=id_siswa,
        jenis_pemilik=JenisPemilik.SISWA,
        waktu_scan=datetime.combine(TANGGAL, time(7, 10, 5)),
    )

    assert kedua.event.jenis_event == JenisEventScan.DUPLICATE
    assert kedua.absensi.id == pertama.absensi.id


def test_scan_setelah_jendela_duplikat_lewat_diproses_normal(session):
    _seed_jadwal_siswa(session)
    id_siswa = _buat_siswa(session)
    mesin = _buat_mesin(session)

    mesin.proses_scan(
        uid_kartu="UID1",
        id_pemilik=id_siswa,
        jenis_pemilik=JenisPemilik.SISWA,
        waktu_scan=datetime.combine(TANGGAL, time(7, 10, 0)),
    )
    kedua = mesin.proses_scan(
        uid_kartu="UID1",
        id_pemilik=id_siswa,
        jenis_pemilik=JenisPemilik.SISWA,
        waktu_scan=datetime.combine(TANGGAL, time(7, 10, 11)),
    )

    assert kedua.event.jenis_event != JenisEventScan.DUPLICATE


# ---------------------------------------------------------------------------
# Siswa: ALPA cutoff pagi
# ---------------------------------------------------------------------------


def test_tetapkan_alpa_untuk_siswa_tanpa_absen_masuk(session):
    _seed_jadwal_siswa(session)
    id_siswa = _buat_siswa(session)
    mesin = _buat_mesin(session)

    hasil = mesin.tetapkan_alpa(TANGGAL)

    assert len(hasil) == 1
    assert hasil[0].id_pemilik == id_siswa
    assert hasil[0].status_masuk == StatusSiswa.ALPA.value
    assert hasil[0].status_final == StatusSiswa.ALPA.value


def test_tetapkan_alpa_tidak_menimpa_siswa_yang_sudah_absen(session):
    _seed_jadwal_siswa(session)
    id_siswa = _buat_siswa(session)
    mesin = _buat_mesin(session)

    mesin.proses_scan(
        uid_kartu="UID1",
        id_pemilik=id_siswa,
        jenis_pemilik=JenisPemilik.SISWA,
        waktu_scan=datetime.combine(TANGGAL, time(7, 10)),
    )
    hasil = mesin.tetapkan_alpa(TANGGAL)

    assert hasil == []


def test_tetapkan_alpa_tanpa_jadwal_tidak_menghasilkan_apa_apa(session):
    _buat_siswa(session)
    mesin = _buat_mesin(session)

    assert mesin.tetapkan_alpa(TANGGAL) == []


# ---------------------------------------------------------------------------
# Guru: HADIR / TERLAMBAT
# ---------------------------------------------------------------------------


def test_guru_scan_sebelum_batas_jadi_hadir(session):
    id_guru = _buat_guru(session)
    mesin = _buat_mesin(session)

    hasil = mesin.proses_scan(
        uid_kartu="UIDG1",
        id_pemilik=id_guru,
        jenis_pemilik=JenisPemilik.GURU,
        waktu_scan=datetime.combine(TANGGAL, time(6, 0)),
    )

    assert hasil.absensi.status_masuk == StatusGuru.HADIR.value


def test_guru_scan_setelah_batas_jadi_terlambat(session):
    id_guru = _buat_guru(session)
    mesin = _buat_mesin(session)

    hasil = mesin.proses_scan(
        uid_kartu="UIDG1",
        id_pemilik=id_guru,
        jenis_pemilik=JenisPemilik.GURU,
        waktu_scan=datetime.combine(TANGGAL, time(9, 31)),
    )

    assert hasil.absensi.status_masuk == StatusGuru.TERLAMBAT.value


# ---------------------------------------------------------------------------
# Hari libur
# ---------------------------------------------------------------------------


def test_hari_libur_tidak_menghasilkan_alpa(session):
    id_admin = _buat_admin(session)
    _seed_jadwal_siswa(session)
    _buat_siswa(session)
    _buat_libur(session, id_admin=id_admin, tanggal=TANGGAL)
    mesin = _buat_mesin(session)

    assert mesin.tetapkan_alpa(TANGGAL) == []


def test_hari_libur_cutoff_pulang_tidak_mengonversi_apa_apa(session):
    id_admin = _buat_admin(session)
    _seed_jadwal_siswa(session)
    id_siswa = _buat_siswa(session)
    _buat_libur(session, id_admin=id_admin, tanggal=TANGGAL)
    mesin = _buat_mesin(session)

    AttendanceRepository(session).tambah(
        id_pemilik=id_siswa,
        jenis_pemilik=JenisPemilik.SISWA,
        tanggal=TANGGAL,
        status_masuk=StatusSiswa.HADIR.value,
        jam_masuk=datetime.combine(TANGGAL, time(7, 10)),
    )

    hasil = mesin.jalankan_cutoff_pulang(TANGGAL)

    assert hasil.jumlah_sasaran == 0
    absensi = AttendanceRepository(session).cari_by_pemilik_tanggal(
        id_pemilik=id_siswa, jenis_pemilik=JenisPemilik.SISWA, tanggal=TANGGAL
    )
    assert absensi.status_final == StatusSiswa.HADIR.value


def test_kalender_dua_jalur_libur_siswa_tidak_menghalangi_guru(session):
    """Contoh nyata dari SKILL.md "Kalender akademik": 14-19 Desember libur
    siswa (pengolahan nilai LHBPD) tapi tetap hari efektif guru. Cutoff
    ALPA siswa tidak jalan pada tanggal itu, tapi guru tetap bisa absen
    normal - _proses_scan_guru tidak pernah memeriksa kalender sama
    sekali (SKILL.md tidak memintanya, guru bisa bertugas kapan pun)."""
    id_admin = _buat_admin(session)
    _seed_jadwal_siswa(session)
    _buat_siswa(session)
    id_guru = _buat_guru(session)
    KalenderRepository(session).tambah(
        mulai=TANGGAL,
        selesai=TANGGAL,
        keterangan="Pengolahan nilai LHBPD",
        jenis_siswa=JenisHari.KEGIATAN_TANPA_KBM.value,
        jenis_guru=JenisHari.EFEKTIF.value,
        tahun_ajaran="2026/2027",
        id_pengguna=id_admin,
        alasan="Sesuai kalender dinas",
    )
    mesin = _buat_mesin(session)

    assert mesin.tetapkan_alpa(TANGGAL) == []  # siswa libur - tidak ada ALPA

    hasil_guru = mesin.proses_scan(
        uid_kartu="UIDG1",
        id_pemilik=id_guru,
        jenis_pemilik=JenisPemilik.GURU,
        waktu_scan=datetime.combine(TANGGAL, time(7, 0)),
    )
    assert hasil_guru.absensi.status_masuk == StatusGuru.HADIR.value  # guru tetap normal

    assert mesin._jenis_hari_siswa(TANGGAL) == JenisHari.KEGIATAN_TANPA_KBM
    assert muat_kalender(KalenderRepository(session)).jenis_hari_guru(TANGGAL) == JenisHari.EFEKTIF


# ---------------------------------------------------------------------------
# Cutoff pulang - konversi ALPA karena tidak scan pulang
# ---------------------------------------------------------------------------


def test_cutoff_pulang_mengonversi_siswa_yang_tidak_scan_pulang(session):
    _seed_jadwal_siswa(session)
    id_siswa = _buat_siswa(session)
    mesin = _buat_mesin(session)

    mesin.proses_scan(
        uid_kartu="UID1",
        id_pemilik=id_siswa,
        jenis_pemilik=JenisPemilik.SISWA,
        waktu_scan=datetime.combine(TANGGAL, time(7, 10)),
    )

    # Satu siswa saja berarti proporsi tidak-scan-pulang 100% - di atas
    # ambang 30% bawaan, jadi ditahan kecuali dipaksa. paksa=True di sini
    # murni untuk mengisolasi pengujian pemetaan DB <-> BarisAbsensi;
    # perilaku ambang itu sendiri sudah diuji tuntas di
    # tests/test_mesin_absensi.py.
    hasil = mesin.jalankan_cutoff_pulang(TANGGAL, paksa=True)

    assert hasil.jumlah_sasaran == 1
    absensi = AttendanceRepository(session).cari_by_pemilik_tanggal(
        id_pemilik=id_siswa, jenis_pemilik=JenisPemilik.SISWA, tanggal=TANGGAL
    )
    assert absensi.status_masuk == StatusSiswa.HADIR.value  # tidak berubah
    assert absensi.status_final == StatusSiswa.ALPA.value
    assert absensi.alasan_final == AlasanFinal.TIDAK_SCAN_PULANG.value


def test_cutoff_pulang_tidak_mengonversi_siswa_yang_sudah_pulang(session):
    _seed_jadwal_siswa(session)
    id_siswa = _buat_siswa(session)
    mesin = _buat_mesin(session)

    mesin.proses_scan(
        uid_kartu="UID1",
        id_pemilik=id_siswa,
        jenis_pemilik=JenisPemilik.SISWA,
        waktu_scan=datetime.combine(TANGGAL, time(7, 10)),
    )
    mesin.proses_scan(
        uid_kartu="UID1",
        id_pemilik=id_siswa,
        jenis_pemilik=JenisPemilik.SISWA,
        waktu_scan=datetime.combine(TANGGAL, time(13, 34)),
    )

    hasil = mesin.jalankan_cutoff_pulang(TANGGAL)

    assert hasil.jumlah_sasaran == 0
    absensi = AttendanceRepository(session).cari_by_pemilik_tanggal(
        id_pemilik=id_siswa, jenis_pemilik=JenisPemilik.SISWA, tanggal=TANGGAL
    )
    assert absensi.status_final == StatusSiswa.HADIR.value
    assert absensi.alasan_final is None


def test_cutoff_pulang_tidak_menimpa_siswa_yang_sudah_alpa_pagi(session):
    """Satu ketidakhadiran tidak boleh kena dua penalti - siswa yang sudah
    ALPA sejak pagi tidak ikut dihitung sebagai sasaran cutoff pulang."""
    _seed_jadwal_siswa(session)
    id_siswa = _buat_siswa(session)
    mesin = _buat_mesin(session)

    hasil_alpa = mesin.tetapkan_alpa(TANGGAL)
    assert hasil_alpa[0].id_pemilik == id_siswa

    hasil_pulang = mesin.jalankan_cutoff_pulang(TANGGAL)

    assert hasil_pulang.jumlah_sasaran == 0
    absensi = AttendanceRepository(session).cari_by_pemilik_tanggal(
        id_pemilik=id_siswa, jenis_pemilik=JenisPemilik.SISWA, tanggal=TANGGAL
    )
    assert absensi.status_masuk == StatusSiswa.ALPA.value
    assert absensi.status_final == StatusSiswa.ALPA.value
    assert absensi.alasan_final is None


def test_cutoff_pulang_aman_dipanggil_ulang_untuk_tanggal_yang_sama(session):
    """status_masuk tidak pernah berubah oleh konversi ALPA pulang, jadi
    baris yang sudah dikonversi tetap terhitung "sasaran" di
    HasilCutoffPulang pada panggilan berikutnya (dihitung ulang tiap kali
    dari status_masuk/jam_pulang, bukan disimpan). Yang harus tetap stabil
    adalah PENYIMPANANNYA - dipanggil ulang tidak boleh menggandakan
    baris atau mengubah alasan_final yang sudah tercatat.
    """
    _seed_jadwal_siswa(session)
    id_siswa = _buat_siswa(session)
    mesin = _buat_mesin(session)

    mesin.proses_scan(
        uid_kartu="UID1",
        id_pemilik=id_siswa,
        jenis_pemilik=JenisPemilik.SISWA,
        waktu_scan=datetime.combine(TANGGAL, time(7, 10)),
    )

    mesin.jalankan_cutoff_pulang(TANGGAL, paksa=True)
    hasil_kedua = mesin.jalankan_cutoff_pulang(TANGGAL, paksa=True)

    assert hasil_kedua.jumlah_sasaran == 1
    absensi = AttendanceRepository(session).cari_by_pemilik_tanggal(
        id_pemilik=id_siswa, jenis_pemilik=JenisPemilik.SISWA, tanggal=TANGGAL
    )
    assert absensi.status_final == StatusSiswa.ALPA.value
    assert absensi.alasan_final == AlasanFinal.TIDAK_SCAN_PULANG.value


# ---------------------------------------------------------------------------
# Immutability: attendance_events tidak pernah berubah akibat pemrosesan lanjutan
# ---------------------------------------------------------------------------


def test_attendance_event_tidak_berubah_setelah_cutoff_pulang(session):
    _seed_jadwal_siswa(session)
    id_siswa = _buat_siswa(session)
    mesin = _buat_mesin(session)

    hasil_scan = mesin.proses_scan(
        uid_kartu="UID1",
        id_pemilik=id_siswa,
        jenis_pemilik=JenisPemilik.SISWA,
        waktu_scan=datetime.combine(TANGGAL, time(7, 10)),
    )
    id_event = hasil_scan.event.id
    waktu_asli = hasil_scan.event.waktu_scan
    jenis_asli = hasil_scan.event.jenis_event

    mesin.jalankan_cutoff_pulang(TANGGAL, paksa=True)

    event_repo = AttendanceEventRepository(session)
    event_setelah = event_repo.cari_by_id(id_event)
    assert event_setelah.waktu_scan == waktu_asli
    assert event_setelah.jenis_event == jenis_asli
